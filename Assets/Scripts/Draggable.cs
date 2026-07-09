using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Token))]
public class Draggable :
    MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    Token token;
    Vector3 pointerOffset;
    Vector3 originalPosition;
    Container originalContainer;
    float zDistance;
    bool dragAllowed;

    void Awake()
    {
        token = GetComponent<Token>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        originalPosition = transform.position;
        originalContainer = token.CurrentContainer;

        // Leaving a container is its own legality check (e.g. Screw's blocked-by-neighbors
        // check) - separate from whether the drop target will accept the token.
        dragAllowed = originalContainer == null || originalContainer.TryRemove(token);
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

        if (token.CellOffsets.Count > 1)
        {
            TryPlaceFootprint();
            return;
        }

        Vector2Int cell = Grid.Instance.WorldToCell(transform.position);
        IReadOnlyList<Container> candidates = ContainerManager.Instance.GetContainersAt(cell);

        // A single occupied slot gets a shot at an occupant interaction (Merge's combine,
        // Toon Blast's swap) before falling back to "rejected" - fully handled by the
        // interaction itself (which may destroy/reposition tokens on its own), so just return.
        if (candidates.Count == 1 && !candidates[0].CanAccept(token) && candidates[0].TryInteractWithOccupant(token, originalContainer))
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
            originalContainer?.TryAccept(token);
        }
    }

    // Snaps into the container's next open slot (claim order) rather than the exact cell
    // dropped on, so two tokens accepted into the same multi-cell container never overlap.
    bool TryEnterAll(IReadOnlyList<Container> containers, out Vector2Int targetCell)
    {
        targetCell = default;
        for (int i = 0; i < containers.Count; i++)
        {
            if (!containers[i].CanAccept(token)) return false;
        }

        targetCell = containers[0].NextAvailableCell();
        for (int i = 0; i < containers.Count; i++)
        {
            containers[i].TryAccept(token);
        }
        return true;
    }

    // Block Puzzle/Parking Jam-style placement: every offset cell (anchored at the drop cell)
    // must belong to at least one container, and all of them must have room, checked as one
    // atomic footprint rather than cell by cell. Tracks pending claims per container so two
    // offset cells landing in the same container (e.g. two cells of one piece in the same
    // Woodoku row) can't both pass the capacity check against its stale, pre-placement count.
    void TryPlaceFootprint()
    {
        Vector2Int anchor = Grid.Instance.WorldToCell(transform.position);
        var claims = new List<Container>();
        var pendingCounts = new Dictionary<Container, int>();

        foreach (Vector2Int offset in token.CellOffsets)
        {
            Vector2Int cell = anchor + offset;
            IReadOnlyList<Container> owners = ContainerManager.Instance.GetContainersAt(cell);
            if (owners.Count == 0)
            {
                RejectFootprint();
                return;
            }

            foreach (Container container in owners)
            {
                pendingCounts.TryGetValue(container, out int pending);
                if (container.Members.Count + pending >= container.Capacity || !container.CanAccept(token))
                {
                    RejectFootprint();
                    return;
                }
                pendingCounts[container] = pending + 1;
                claims.Add(container);
            }
        }

        foreach (Container container in claims)
        {
            container.TryAccept(token);
        }
        transform.position = Grid.Instance.CellToWorld(anchor);
    }

    void RejectFootprint()
    {
        transform.position = originalPosition;
        originalContainer?.TryAccept(token);
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