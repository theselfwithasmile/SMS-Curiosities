using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.EventSystems;
using UnityEngine.Playables;

// Token absorbs what used to be a separate Draggable component - every token that exists is
// draggable, and the only remaining per-variant differences (CellOffsets, EscapeLane) are plain
// data rather than a pluggable strategy object, so there was never a real reason to keep the drag
// verb as a component of its own.
[RequireComponent(typeof(Collider2D))]
public class Token : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    public Container CurrentContainer;
    
    public int Group;
    public int Tier; // merge level only (2048-style) - never used for group/type matching

    // Relative footprint in cells. Size 1 (the default) behaves exactly like a normal single-cell
    // token; a multi-cell piece (Block Puzzle, Parking Jam) just has more entries here - any
    // extra visuals for those cells are child sprites baked into the prefab, which move for free
    // since they're parented under this Transform.
    public List<Vector2Int> CellOffsets = new List<Vector2Int> { Vector2Int.zero };

    // Arrow/Parking Jam: true means drag-end resolves via ResolveEscapeDrag (fully escape or
    // fully revert) instead of ordinary container placement. Deliberately a plain bool rather than
    // gating on "EscapeLane != null" - Unity's serializer can't represent null for a List<T> field
    // on a prefab-instantiated object, so an untouched EscapeLane silently comes back as an empty
    // list rather than null, making a null-check unusable as a marker here.
    public bool IsEscapePiece;

    // The fixed corridor (absolute cells, beyond the body) leading from this token's head to the
    // grid boundary, baked in at spawn - only meaningful when IsEscapePiece is true. The piece's
    // own body never partially moves, so all that matters is whether every lane cell is currently
    // free of every other still-present piece.
    public List<Vector2Int> EscapeLane;

    // The direction EscapeLane runs in - needed to check that a drag actually aimed the piece
    // toward its own exit before consulting the lane at all (a lane that happens to be clear
    // shouldn't let ANY drag, in any direction or distance, trigger an escape).
    public Vector2Int EscapeDirection;

    // Fired right as this piece clears its lane and leaves the board - ParkingJamBaseZone uses it
    // to track how many cars are still in play for its win check, without Token needing to know
    // anything about zones or win conditions itself.
    public System.Action OnEscaped;

    // Generic (lane-free) counterpart to IsEscapePiece: true once a zone considers this token
    // "finished" (e.g. Merge's target-tier result) - dropping it anywhere off the board (outside
    // every container, not just a specific baked corridor) destroys it instead of reverting. Lets
    // a zone's win condition just be "every container empty" (BaseZone's generic default) rather
    // than needing its own bespoke completion check.
    public bool CanExitBoard;

    Vector3 pointerOffset;
    Vector3 originalPosition;
    Container originalContainer;
    float zDistance;
    bool dragAllowed;
    Coroutine activeMoveTween;
    Transform escapeArrow;

    // The AnimationClips TokenSpawner assigns are ordinary (non-legacy) clips authored in the
    // Animation window, not legacy clips - the old Animation component can't play those at all,
    // so playback goes through the Playables API instead: an Animator purely as a bind target
    // (no Controller needed) plus a single AnimationClipPlayable we drive by hand.
    Animator animator;
    PlayableGraph playableGraph;
    AnimationClipPlayable clipPlayable;
    AnimationClip currentClip;
    bool isHovered;
    bool isDragging;
    bool isAnimating;

    // True once an emoji AnimationClip has been assigned - lets callers (TokenSpawner's flat
    // color tint) skip work that would otherwise fight the emoji art's own colors.
    public bool HasAnimation => currentClip != null;

    static Mesh arrowMesh;

    void Awake()
    {
        GetComponent<SpriteRenderer>().color = GameState.Instance.SecondaryGroupColor(Group);
    }

    // Parking Jam: a small triangle child pointing along EscapeDirection, sitting at the body's
    // head cell (CellOffsets' last entry - ParkingJamBaseZone builds offsets in the same
    // tail-to-head order as the car's body). Built procedurally, same as GridRenderer's own
    // quads/dots - there's no art asset for this yet, and one triangle doesn't need one.
    public void ShowEscapeArrow()
    {
        if (arrowMesh == null) arrowMesh = GridRenderer.BuildArrowMesh();

        var arrowObject = new GameObject("EscapeArrow");

        // World-space position/rotation/scale first, then reparent with worldPositionStays -
        // same trick TokenSpawner uses for the extra cell sprites. The token root's own prefab
        // scale isn't 1 (see Token.prefab), so setting localPosition/localScale directly under
        // it (as this used to) put the arrow 3x too far from the head cell and 3x oversized.
        Vector2Int headOffset = CellOffsets[CellOffsets.Count - 1];
        arrowObject.transform.position = transform.position + (Vector3)((Vector2)headOffset * Grid.Instance.CellSize);
        arrowObject.transform.rotation = Quaternion.FromToRotation(Vector3.up, new Vector3(EscapeDirection.x, EscapeDirection.y, 0f));
        arrowObject.transform.localScale = Vector3.one * (Grid.Instance.CellSize * 0.5f);
        arrowObject.transform.SetParent(transform, true);

        var meshFilter = arrowObject.AddComponent<MeshFilter>();
        meshFilter.mesh = arrowMesh;

        var meshRenderer = arrowObject.AddComponent<MeshRenderer>();
        meshRenderer.material = GridRenderer.BuildMaterial(Color.white);
        meshRenderer.sortingOrder = GetComponent<SpriteRenderer>().sortingOrder + 1;

        escapeArrow = arrowObject.transform;
    }

    // The graph is driven entirely by hand (never PlayableGraph.Play()) - deliberately, so this
    // Evaluate call is the only thing advancing it and there's no risk of Unity's own per-frame
    // auto-evaluation double-advancing time on top of it. Playables don't loop clips on their
    // own either, so wrapping past clip length is manual too. Guarded by isAnimating so idle
    // (the common case: not hovered, not dragged) tokens pay nothing here.
    void Update()
    {
        if (!isAnimating || currentClip == null || currentClip.length <= 0f) return;

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

    // Assigns the sprite flipbook for this token's current Group/Tier (TokenSpawner resolves
    // which clip that is). Tokens sit on a static resting frame otherwise - the clip only plays
    // while hovered or dragged, via OnPointerEnter/Exit and the drag handlers below.
    public void SetAnimationClip(AnimationClip clip)
    {
        if (clip == null) return;

        if (playableGraph.IsValid()) playableGraph.Destroy();
        currentClip = clip;

        // Prefer an Animator already on the prefab - one added here via AddComponent isn't
        // reliably ready to accept a Playables binding in the same frame it's created, so the
        // very first Evaluate (the one that shows the resting frame) can silently no-op.
        if (animator == null) animator = GetComponent<Animator>();
        if (animator == null) animator = gameObject.AddComponent<Animator>();
        playableGraph = PlayableGraph.Create($"{name}Animation");
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(playableGraph, "Output", animator);
        clipPlayable = AnimationClipPlayable.Create(playableGraph, clip);
        output.SetSourcePlayable(clipPlayable);

        // Emoji art carries its own color - stop tinting it with the group's flat placeholder.
        GetComponent<SpriteRenderer>().color = Color.white;

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
    

    // How much a buried token's color is darkened by, relative to its normal (revealed) color -
    // a visible-but-dim cue that something is stacked underneath, rather than hiding it outright.
    const float BuriedColorFactor = 0.45f;

    // Buried tokens spawn dead-center under their occupant (same cell, same anchor position), so
    // without an offset they'd sit perfectly behind an identically-sized sprite and the dimming
    // above would never actually be visible. Nudging toward one corner by a fraction of a cell
    // lets that corner peek out from behind the occupant instead.
    const float BuriedPeekFraction = 0.22f;
    static readonly Vector2 BuriedPeekDirection = new Vector2(1f, -1f).normalized;

    Color revealedColor;
    int revealedSortingOrder;
    bool isBuried;

    // Layering: a buried token starts dimmed, peeking from one corner, and non-interactive
    // (SetRevealed(false) right after spawn, once its real color/sortingOrder are already
    // assigned) until whatever's above it in the same cell is vacated, at which point
    // Container.RevealAt calls this again with true to restore it - the SpriteRenderer itself
    // stays enabled either way, since staying visible (if dim) is the whole point.
    public void SetRevealed(bool revealed)
    {
        if (revealed != isBuried) return; // already in the requested state - avoid re-applying the offset

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (revealed)
        {
            renderer.color = revealedColor;
            renderer.sortingOrder = revealedSortingOrder;
            transform.position -= (Vector3)(BuriedPeekDirection * Grid.Instance.CellSize * BuriedPeekFraction);
            isBuried = false;
        }
        else
        {
            revealedColor = renderer.color;
            revealedSortingOrder = renderer.sortingOrder;
            renderer.color = new Color(
                revealedColor.r * BuriedColorFactor,
                revealedColor.g * BuriedColorFactor,
                revealedColor.b * BuriedColorFactor,
                revealedColor.a);
            renderer.sortingOrder = revealedSortingOrder - 1; // always draws behind its occupant
            transform.position += (Vector3)(BuriedPeekDirection * Grid.Instance.CellSize * BuriedPeekFraction);
            isBuried = true;
        }
        GetComponent<Collider2D>().enabled = revealed;
    }

    // Cancels any in-flight drop/revert tween before repositioning - otherwise a fast re-grab
    // mid-tween leaves both it and the drag itself writing transform.position the same frame.
    void MoveToSlot(Vector3 destination)
    {
        if (activeMoveTween != null) TweenRunner.Instance.StopCoroutine(activeMoveTween);
        activeMoveTween = TweenRunner.Instance.MoveTo(transform, destination);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Once a zone has reported an outcome (or before Playing even starts, e.g. still on the
        // menu) the board underneath a Won/Lost/Menu panel must stop responding on its own -
        // relying on the panel to visually cover it isn't enough, since UI raycast blocking is a
        // scene/graphic-raycaster setup detail, not something this script controls.
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

        // Leaving a container is its own legality check (e.g. Screw's blocked-by-neighbors
        // check) - separate from whether the drop target will accept the token.
        dragAllowed = originalContainer == null || originalContainer.TryRemove(this);
        if (!dragAllowed) return;

        TweenRunner.Instance.PickupPop(transform);
        isDragging = true;
        PlayAnimation();

        Camera camera = EventCamera(eventData);
        zDistance = camera.WorldToScreenPoint(transform.position).z;
        pointerOffset = transform.position - PointerToWorld(eventData, camera);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragAllowed) return;
        transform.position = PointerToWorld(eventData, EventCamera(eventData)) + pointerOffset;
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

        // A multi-cell footprint (Block Puzzle) places at exactly the drop anchor rather than
        // the stacking behaviour single-cell tokens normally use.
        if (CellOffsets.Count > 1)
        {
            TryPlaceFootprint();
            return;
        }

        Vector2Int cell = Grid.Instance.WorldToCell(transform.position);
        IReadOnlyList<Container> candidates = ContainerManager.Instance.GetContainersAt(cell);

        // A single occupied slot gets a shot at an occupant interaction (Merge's combine,
        // Toon Blast's swap) before falling back to "rejected" - fully handled by the
        // interaction itself (which may destroy/reposition tokens on its own), so just return.
        if (candidates.Count == 1 && !candidates[0].CanAccept(this) && candidates[0].TryInteractWithOccupant(this, originalContainer))
        {
            return;
        }

        // Dropped outside every container (off the board entirely) and this token is flagged as
        // done - self-destruct instead of falling through to the revert branch below.
        if (candidates.Count == 0 && CanExitBoard)
        {
            NotifyOriginVacated();
            OnEscaped?.Invoke();
            TweenRunner.Instance.ShrinkAndDestroy(this);
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

    // Confirms the origin slot is permanently vacated (as opposed to the drag reverting back into
    // it) - only now is it safe to reveal whatever was buried underneath, since a revert would
    // otherwise collide the returning token with a freshly revealed one in the same cell.
    void NotifyOriginVacated()
    {
        if (originalContainer == null) return;
        originalContainer.RevealAt(Grid.Instance.WorldToCell(originalPosition));
    }

    // The body never partially moves - it either escapes whole or reverts whole. Since the lane is
    // baked in at spawn as exactly the cells beyond the head that need to be clear, checking
    // escape is just "is every one of those currently free of other still-present pieces" - no
    // slide simulation needed at all.
    void ResolveEscapeDrag()
    {
        Vector2Int anchor = Grid.Instance.WorldToCell(originalPosition);

        // Continuous world-space movement, not grid-cell movement - a cell can easily span a
        // large chunk of the screen, so requiring the drop to have crossed into a whole different
        // cell before even registering direction would make ordinary drags never trigger at all.
        Vector2 worldDelta = (Vector2)transform.position - (Vector2)originalPosition;
        Vector2 escapeDirWorld = new Vector2(EscapeDirection.x, EscapeDirection.y);

        // Only actually attempt the exit if the drag aimed this piece toward its own escape
        // direction - dropping it anywhere else (or barely moving it at all) should just snap
        // back, not silently trigger an escape check the drag itself never aimed at.
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

        SetFootprintOccupied(anchor, false);
        OnEscaped?.Invoke();
        if (escapeArrow != null)
        {
            TweenRunner.Instance.PulseThenShrinkAndDestroy(this, escapeArrow);
        }
        else
        {
            TweenRunner.Instance.ShrinkAndDestroy(this);
        }
    }

    void SetFootprintOccupied(Vector2Int anchor, bool occupied)
    {
        foreach (Vector2Int offset in CellOffsets)
        {
            Grid.Instance.SetOccupied(anchor + offset, occupied);
        }
    }

    // Places the token into the exact cell dropped on, across every container that owns that
    // cell (a shared cell like Block Puzzle's row/column pair) - arbitrary-slot placement rather
    // than always compacting into a multi-cell container's next open slot in claim order.
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

    // Block Puzzle-style placement: every offset cell (anchored at the drop cell) must belong to
    // at least one container, and all of them must have room, checked as one atomic footprint
    // rather than cell by cell. On success, TryClaimFootprint decomposes this piece into
    // independent single-cell tokens (already positioned) and destroys this GameObject as part of
    // that - nothing left to do here, and touching `this` afterward would be a destroyed reference.
    void TryPlaceFootprint()
    {
        Vector2Int anchor = Grid.Instance.WorldToCell(transform.position);
        if (!TokenSpawner.Instance.TryClaimFootprint(this, anchor, CellOffsets))
        {
            MoveToSlot(originalPosition);
            originalContainer?.ForceAccept(this);
        }
    }

    Vector3 PointerToWorld(PointerEventData eventData, Camera camera)
    {
        Vector3 screenPoint = new Vector3(eventData.position.x, eventData.position.y, zDistance);
        return camera.ScreenToWorldPoint(screenPoint);
    }

    Camera EventCamera(PointerEventData eventData)
    {
        return eventData.pressEventCamera != null ? eventData.pressEventCamera : Camera.main;
    }
}
