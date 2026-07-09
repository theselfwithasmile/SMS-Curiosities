using System;
using UnityEngine;
using UnityEngine.EventSystems;

// Parking Jam (arrow variant): tapping slides the whole rigid group until the next step is
// occupied or out of bounds - no drag, no player-chosen axis, the only decision is which car to
// tap and when. Footprint comes from Token.CellOffsets - any extra visuals are child sprites
// baked into the prefab, moving for free via Transform parenting.
//
// Direction is per-step, read from the anchor cell's track (Grid.GetFlowDirection), falling back
// to defaultDirection where no track override is set - a plain straight lane just never needs an
// override and behaves exactly like a fixed direction. Bends are only safe for shapes that don't
// need to reorient through a turn (single cells, or symmetric footprints) - a multi-cell car
// should only ever be placed somewhere its whole lane shares one direction; this component
// doesn't enforce that itself, it's a generation-time placement rule.
//
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Token))]
public class TapToSlide : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] Vector2Int defaultDirection = Vector2Int.right;

    Token token;

    void Awake()
    {
        token = GetComponent<Token>();
    }

    // AddComponent can't take constructor args, and the field is serialized/private for the
    // Inspector case - this is how code-spawned cars (ParkingJamZone) set their fixed direction.
    public void Initialize(Vector2Int direction)
    {
        defaultDirection = direction;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Vector2Int anchor = Grid.Instance.WorldToCell(transform.position);
        FreeFootprint(anchor);

        anchor = SlideUntilBlocked(anchor, a => Grid.Instance.GetFlowDirection(a, defaultDirection), FootprintFree);

        // If the resting cells belong to a real Container (e.g. an exit lane), let it consume
        // the car via its own rules instead of just parking there - ordinary lane cells have no
        // Container at all, so this simply falls through to the plain occupancy claim below.
        if (ContainerManager.Instance.TryClaimFootprint(token, anchor, token.CellOffsets))
        {
            return;
        }

        ClaimFootprint(anchor);
        transform.position = Grid.Instance.CellToWorld(anchor);
    }

    bool FootprintFree(Vector2Int anchor)
    {
        foreach (Vector2Int offset in token.CellOffsets)
        {
            Vector2Int cell = anchor + offset;
            if (!Grid.Instance.IsInBounds(cell) || Grid.Instance.IsOccupied(cell)) return false;
        }
        return true;
    }

    void FreeFootprint(Vector2Int anchor)
    {
        foreach (Vector2Int offset in token.CellOffsets)
        {
            Grid.Instance.SetOccupied(anchor + offset, false);
        }
    }

    void ClaimFootprint(Vector2Int anchor)
    {
        foreach (Vector2Int offset in token.CellOffsets)
        {
            Grid.Instance.SetOccupied(anchor + offset, true);
        }
    }

    // Steps in whatever direction directionAt reports at each cell, until anchorFree says the
    // next step isn't. Shared with ParkingJamZone's solver, which simulates the same stepping
    // against a hypothetical state instead of real Grid occupancy.
    public static Vector2Int SlideUntilBlocked(Vector2Int start, Func<Vector2Int, Vector2Int> directionAt, Func<Vector2Int, bool> anchorFree)
    {
        Vector2Int anchor = start;
        Vector2Int next = anchor + directionAt(anchor);
        while (anchorFree(next))
        {
            anchor = next;
            next = anchor + directionAt(anchor);
        }
        return anchor;
    }
}
