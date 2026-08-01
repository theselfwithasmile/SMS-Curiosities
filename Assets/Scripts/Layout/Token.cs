using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// Token absorbs what used to be a separate Draggable component - every token that exists is
// draggable, and the only remaining per-variant differences (CellOffsets, EscapeLane) are plain
// data rather than a pluggable strategy object, so there was never a real reason to keep the drag
// verb as a component of its own.
[RequireComponent(typeof(Collider2D))]
public class Token : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
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

    Vector3 pointerOffset;
    Vector3 originalPosition;
    Container originalContainer;
    float zDistance;
    bool dragAllowed;
    Coroutine activeMoveTween;
    Transform escapeArrow;

    static Mesh arrowMesh;

    void Awake()
    {
        GetComponent<SpriteRenderer>().color = GameState.Instance.SecondaryGroupColor(Group);
    }

    // Parking Jam: a small triangle child pointing along EscapeDirection, sitting at the body's
    // head cell (CellOffsets' last entry - ParkingJamBaseZone builds offsets in the same
    // tail-to-head order as the car's body). Built procedurally, same as Grid/ContainerManager's
    // own quads - there's no art asset for this yet, and one triangle doesn't need one.
    public void ShowEscapeArrow()
    {
        if (arrowMesh == null) arrowMesh = BuildArrowMesh();

        var arrowObject = new GameObject("EscapeArrow");
        arrowObject.transform.SetParent(transform, false);

        Vector2Int headOffset = CellOffsets[CellOffsets.Count - 1];
        arrowObject.transform.localPosition = (Vector3)((Vector2)headOffset * Grid.Instance.CellSize);
        arrowObject.transform.localRotation = Quaternion.FromToRotation(Vector3.up, new Vector3(EscapeDirection.x, EscapeDirection.y, 0f));
        arrowObject.transform.localScale = Vector3.one * (Grid.Instance.CellSize * 0.5f);

        var meshFilter = arrowObject.AddComponent<MeshFilter>();
        meshFilter.mesh = arrowMesh;

        var meshRenderer = arrowObject.AddComponent<MeshRenderer>();
        meshRenderer.material = Grid.BuildMaterial(Color.white);
        meshRenderer.sortingOrder = GetComponent<SpriteRenderer>().sortingOrder + 1;

        escapeArrow = arrowObject.transform;
    }

    static Mesh BuildArrowMesh()
    {
        var vertices = new[]
        {
            new Vector3(-0.3f, -0.35f, 0f),
            new Vector3(0.3f, -0.35f, 0f),
            new Vector3(0f, 0.35f, 0f),
        };
        var triangles = new[] { 0, 1, 2 };
        return Grid.BuildMesh("EscapeArrow", vertices, triangles);
    }

    // Layering: a buried token starts hidden and non-interactive (SetRevealed(false) right after
    // spawn) until whatever's above it in the same cell clears, at which point Container.RevealNext
    // calls this again with true.
    public void SetRevealed(bool revealed)
    {
        GetComponent<SpriteRenderer>().enabled = revealed;
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

        if (candidates.Count > 0 && TryEnterAll(candidates, out Vector2Int targetCell))
        {
            MoveToSlot(Grid.Instance.CellToWorld(targetCell));
        }
        else
        {
            MoveToSlot(originalPosition);
            originalContainer?.ForceAccept(this);
        }
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

    // Snaps into the container's next open slot (claim order) rather than the exact cell
    // dropped on, so two tokens accepted into the same multi-cell container never overlap.
    bool TryEnterAll(IReadOnlyList<Container> containers, out Vector2Int targetCell)
    {
        targetCell = default;
        for (int i = 0; i < containers.Count; i++)
        {
            if (!containers[i].CanAccept(this)) return false;
        }

        targetCell = containers[0].NextAvailableCell();
        for (int i = 0; i < containers.Count; i++)
        {
            containers[i].TryAccept(this);
        }
        return true;
    }

    // Block Puzzle-style placement: every offset cell (anchored at the drop cell) must belong to
    // at least one container, and all of them must have room, checked as one atomic footprint
    // rather than cell by cell.
    void TryPlaceFootprint()
    {
        Vector2Int anchor = Grid.Instance.WorldToCell(transform.position);
        if (TokenSpawner.Instance.TryClaimFootprint(this, anchor, CellOffsets))
        {
            MoveToSlot(Grid.Instance.CellToWorld(anchor));
        }
        else
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
