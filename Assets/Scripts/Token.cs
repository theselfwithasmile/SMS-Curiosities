using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// Token absorbs what used to be a separate Draggable component - every token that exists is
// draggable, and the only remaining per-variant differences (CellOffsets, FixedDirection) are
// plain data rather than a pluggable strategy object, so there was never a real reason to keep
// the drag verb as a component of its own.
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

    // Arrow/Parking Jam: a fixed slide direction set at spawn. Non-null means drag-end resolves
    // via ResolveFixedDirectionDrag (fully escape or fully revert) instead of ordinary container
    // placement - the car either clears all the way to the grid edge or never actually moves.
    public Vector2Int? FixedDirection;

    Vector3 pointerOffset;
    Vector3 originalPosition;
    Container originalContainer;
    float zDistance;
    bool dragAllowed;

    void Awake()
    {
        GetComponent<SpriteRenderer>().color = GameState.Instance.SecondaryGroupColor(Group);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        originalPosition = transform.position;
        originalContainer = CurrentContainer;

        // Leaving a container is its own legality check (e.g. Screw's blocked-by-neighbors
        // check) - separate from whether the drop target will accept the token.
        dragAllowed = originalContainer == null || originalContainer.TryRemove(this);
        if (!dragAllowed) return;

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

        if (FixedDirection.HasValue)
        {
            ResolveFixedDirectionDrag();
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
            transform.position = Grid.Instance.CellToWorld(targetCell);
        }
        else
        {
            transform.position = originalPosition;
            originalContainer?.ForceAccept(this);
        }
    }

    // Every car either clears all the way to the grid boundary or doesn't move at all - no
    // intermediate resting position is ever kept, since a car that doesn't fully escape never
    // actually changes the board. Compares the maximal slide against other cars with the maximal
    // slide against the boundary alone: if they land on the same cell, nothing but the edge
    // stopped it, so it escapes; if another car stopped it earlier, it reverts.
    void ResolveFixedDirectionDrag()
    {
        Vector2Int direction = FixedDirection.Value;
        Vector2Int anchor = Grid.Instance.WorldToCell(originalPosition);

        SetFootprintOccupied(anchor, false);
        Vector2Int withObstacles = Grid.SlideUntilBlocked(anchor, _ => direction, c => FootprintFree(c));
        Vector2Int boundsOnly = Grid.SlideUntilBlocked(anchor, _ => direction, c => FootprintFitsBounds(c));

        if (withObstacles == boundsOnly)
        {
            Destroy(gameObject);
            return;
        }

        SetFootprintOccupied(anchor, true);
        transform.position = originalPosition;
        originalContainer?.ForceAccept(this);
    }

    bool FootprintFree(Vector2Int anchor)
    {
        foreach (Vector2Int offset in CellOffsets)
        {
            Vector2Int cell = anchor + offset;
            if (!Grid.Instance.IsInBounds(cell) || Grid.Instance.IsOccupied(cell)) return false;
        }
        return true;
    }

    bool FootprintFitsBounds(Vector2Int anchor)
    {
        foreach (Vector2Int offset in CellOffsets)
        {
            if (!Grid.Instance.IsInBounds(anchor + offset)) return false;
        }
        return true;
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
            transform.position = Grid.Instance.CellToWorld(anchor);
        }
        else
        {
            transform.position = originalPosition;
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
