using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Collider2D))]
public class Draggable :
    MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    Vector3 pointerOffset;
    float zDistance;

    public void OnBeginDrag(PointerEventData eventData)
    {
        Camera camera = EventCamera(eventData);
        zDistance = camera.WorldToScreenPoint(transform.position).z;
        pointerOffset = transform.position - PointerToWorld(eventData, camera);
    }

    public void OnDrag(PointerEventData eventData)
    {
        transform.position = PointerToWorld(eventData, EventCamera(eventData)) + pointerOffset;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (Grid.Instance != null)
        {
            transform.position = Grid.Instance.SnapToWorld(transform.position);
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