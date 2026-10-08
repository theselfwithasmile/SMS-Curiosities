using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.EventSystems;
using UnityEngine.Playables;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class Token : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    Container currentContainer;
    //a token lives under its container's visual so it scrolls, scales and draws with it
    public Container CurrentContainer
    {
        get => currentContainer;
        set
        {
            currentContainer = value;
            AttachTo(value);
        }
    }

    public int Group;
    public int Tier;
    public List<Vector2Int> CellOffsets = new List<Vector2Int> { Vector2Int.zero };
    
    public bool IsEscapePiece;
    public List<Vector2Int> EscapeLane;
    public Vector2Int EscapeDirection;
    public System.Action OnEscaped; // Fired right as this piece clears its lane and leaves the board
    
    public bool CanExitBoard; //true once a zone considers this token "finished"

    
    public Vector2Int AnchorCell;  //absolute grid cell CellOffsets is anchored to
    public List<Transform> CellParts = new List<Transform>(); //the extra per-cell sprites SpawnMultiCellToken creates for a multi-cell token

    Vector3 pointerOffset;
    Vector3 originalPosition;
    Container originalContainer;
    bool dragAllowed;
    Coroutine activeMoveTween;
    
    Animator animator;
    PlayableGraph playableGraph;
    AnimationClipPlayable clipPlayable;
    AnimationClip currentClip;
    bool isHovered;
    bool isDragging;
    bool isAnimating;
    public bool HasAnimation => currentClip != null;

    void Awake()
    {
        GetComponent<Image>().color = GameState.Instance.SecondaryGroupColor(Group);
    }
    
    void Update()
    {
        if (!isAnimating || currentClip == null || currentClip.length <= 0f) return;

        //the graph is driven entirely by hand 
        playableGraph.Evaluate(Time.deltaTime);
        if (clipPlayable.GetTime() >= currentClip.length)
        {
            clipPlayable.SetTime(clipPlayable.GetTime() % currentClip.length);
        }
    }

    void OnDestroy()
    {
        if (playableGraph.IsValid()) playableGraph.Destroy();
    }
    
    public void SetAnimationClip(AnimationClip clip)
    {
        if (clip == null) return;

        if (playableGraph.IsValid()) playableGraph.Destroy();
        currentClip = clip;

        //prefer an Animator already on the prefab
        if (animator == null) animator = GetComponent<Animator>();
        if (animator == null) animator = gameObject.AddComponent<Animator>();
        playableGraph = PlayableGraph.Create($"{name}Animation");
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(playableGraph, "Output", animator);
        clipPlayable = AnimationClipPlayable.Create(playableGraph, clip);
        output.SetSourcePlayable(clipPlayable);
        
        GetComponent<Image>().color = Color.white;

        StopAnimationAndReset();
    }

    void PlayAnimation()
    {
        if (currentClip == null || !playableGraph.IsValid()) return;
        isAnimating = true;
    }

    // Stops and rewinds to frame 0 rather than just pausing, so "not hovered, not dragging"
    // always reads as the same static resting pose regardless of where playback left off.
    void StopAnimationAndReset()
    {
        if (currentClip == null || !playableGraph.IsValid()) return;
        isAnimating = false;
        clipPlayable.SetTime(0);
        playableGraph.Evaluate(0);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        PlayAnimation();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        if (!isDragging) StopAnimationAndReset();
    }
    
    
    const float BuriedColorFactor = 0.45f;
    const float BuriedPeekFraction = 0.08f;
    static readonly Vector2 BuriedPeekDirection = new Vector2(1f, -1f).normalized;

    Color revealedColor;
    bool isBuried;

    public void SetRevealed(bool revealed)
    {
        if (revealed != isBuried) return;

        Image image = GetComponent<Image>();
        if (revealed)
        {
            image.color = revealedColor;
            transform.SetAsLastSibling();
            transform.position -= BuriedPeekOffset();
            isBuried = false;
        }
        else
        {
            revealedColor = image.color;
            image.color = new Color(
                revealedColor.r * BuriedColorFactor,
                revealedColor.g * BuriedColorFactor,
                revealedColor.b * BuriedColorFactor,
                revealedColor.a);
            transform.SetAsFirstSibling(); // UI draws in sibling order, so first = always behind its occupant
            transform.position += BuriedPeekOffset();
            isBuried = true;
        }
        SetInteractable(revealed);
    }

    static Vector3 BuriedPeekOffset()
    {
        return Grid.Instance.BoardVectorToWorld(BuriedPeekDirection * Grid.Instance.CellSize * BuriedPeekFraction);
    }

    //container-less tokens (being dragged, orphaned) go on the token layer, above every container
    public void AttachTo(Container container)
    {
        Transform visual = container != null ? ContainerManager.Instance.GetContainerObject(container) : null;
        Transform parent = visual != null ? visual : Grid.Instance.TokenLayer;
        if (transform.parent == parent) return;

        //carried in board units instead of world space: a container still growing in has a near-zero scale,
        //and preserving world position/scale under it would blow the token's own scale up
        Vector3 boardPosition = Grid.Instance.Board.InverseTransformPoint(transform.position);
        Vector3 parentOrigin = parent == Grid.Instance.TokenLayer ? Vector3.zero : parent.localPosition;
        transform.SetParent(parent, false);
        transform.localPosition = boardPosition - parentOrigin;
    }

    //pointer events come from the canvas GraphicRaycaster, so "can be grabbed" == "some image of mine is a raycast target"
    public void SetInteractable(bool interactable)
    {
        foreach (Graphic graphic in GetComponentsInChildren<Graphic>())
        {
            graphic.raycastTarget = interactable;
        }
    }

    //cancels any in-flight drop/revert tween before repositioning 
    void MoveToSlot(Vector3 destination)
    {
        if (activeMoveTween != null) TweenRunner.Instance.StopCoroutine(activeMoveTween);
        activeMoveTween = TweenRunner.Instance.MoveTo(transform, destination);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (GameFlowManager.Instance != null && GameFlowManager.Instance.State != FlowState.Playing)
        {
            dragAllowed = false;
            return;
        }

        if (activeMoveTween != null)
        {
            TweenRunner.Instance.StopCoroutine(activeMoveTween);
            activeMoveTween = null;
        }

        originalPosition = transform.position;
        originalContainer = CurrentContainer;

        //leaving a container is its own legality check
        dragAllowed = originalContainer == null || originalContainer.TryRemove(this);
        if (!dragAllowed) return;

        TweenRunner.Instance.PickupPop(transform);
        transform.SetAsLastSibling(); //carried token draws over everything on its layer
        isDragging = true;
        PlayAnimation();

        pointerOffset = transform.position - Grid.Instance.ScreenToWorld(eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragAllowed) return;
        transform.position = Grid.Instance.ScreenToWorld(eventData.position) + pointerOffset;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;
        if (!isHovered) StopAnimationAndReset();

        if (!dragAllowed || Grid.Instance == null) return;

        if (IsEscapePiece)
        {
            ResolveEscapeDrag();
            return;
        }

        //a multi-cell footprint places at exactly the drop anchor rather than
        if (CellOffsets.Count > 1)
        {
            TryPlaceFootprint();
            return;
        }

        //actually off the grid+bench footprint
        if (CanExitBoard && !Grid.Instance.IsWorldPositionOnBoard(transform.position))
        {
            NotifyOriginVacated();
            OnEscaped?.Invoke();
            TweenRunner.Instance.ShrinkAndDestroy(this);
            return;
        }

        Vector2Int cell = Grid.Instance.WorldToCell(transform.position);
        IReadOnlyList<Container> candidates = ContainerManager.Instance.GetContainersAt(cell);

        //a single occupied slot gets a shot at an occupant interaction before falling back to "rejected"
        if (candidates.Count == 1 && !candidates[0].CanAccept(this) && candidates[0].TryInteractWithOccupant(this, originalContainer))
        {
            return;
        }

        if (candidates.Count > 0 && TryEnterAll(candidates, cell))
        {
            MoveToSlot(Grid.Instance.CellToWorld(cell));
            NotifyOriginVacated();
        }
        else
        {
            MoveToSlot(originalPosition);
            originalContainer?.ForceAccept(this);
        }
    }

    //confirms the origin slot is permanently vacated
    void NotifyOriginVacated()
    {
        if (originalContainer == null) return;
        originalContainer.RevealAt(Grid.Instance.WorldToCell(originalPosition));
    }

    //the body either escapes whole or reverts whole
    void ResolveEscapeDrag()
    {
        Vector2 worldDelta = (Vector2)transform.position - (Vector2)originalPosition;
        Vector2 escapeDirWorld = new Vector2(EscapeDirection.x, EscapeDirection.y);

        //only actually attempt the exit if the drag aimed this piece toward its own escape direction
        bool draggedTowardExit = Vector2.Dot(worldDelta, escapeDirWorld) > 0f;
        if (!draggedTowardExit)
        {
            MoveToSlot(originalPosition);
            originalContainer?.ForceAccept(this);
            return;
        }

        foreach (Vector2Int cell in EscapeLane)
        {
            if (Grid.Instance.IsOccupied(cell))
            {
                MoveToSlot(originalPosition);
                originalContainer?.ForceAccept(this);
                return;
            }
        }

        SetFootprintOccupied(AnchorCell, false);
        OnEscaped?.Invoke();
        TweenRunner.Instance.CascadeShrinkAndDestroy(this);
    }

    void SetFootprintOccupied(Vector2Int anchor, bool occupied)
    {
        foreach (Vector2Int offset in CellOffsets)
        {
            Grid.Instance.SetOccupied(anchor + offset, occupied);
        }
    }
    
    bool TryEnterAll(IReadOnlyList<Container> containers, Vector2Int dropCell)
    {
        for (int i = 0; i < containers.Count; i++)
        {
            if (!containers[i].CanAcceptAt(this, dropCell)) return false;
        }

        for (int i = 0; i < containers.Count; i++)
        {
            containers[i].TryAcceptAt(this, dropCell);
        }
        return true;
    }
    
    void TryPlaceFootprint()
    {
        Vector2Int anchor = Grid.Instance.WorldToCell(transform.position);
        if (!TokenSpawner.Instance.TryClaimFootprint(this, anchor, CellOffsets))
        {
            MoveToSlot(originalPosition);
            originalContainer?.ForceAccept(this);
        }
    }
}
