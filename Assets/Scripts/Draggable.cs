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

        Vector2Int cell = Grid.Instance.WorldToCell(transform.position);
        IReadOnlyList<Container> candidates = Grid.Instance.GetContainersAt(cell);

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