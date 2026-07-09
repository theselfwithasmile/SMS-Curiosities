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
// Baseline only: doesn't check for/consume an exit-lane Container on arrival, and generation
// (placing cars, solver-verifying the puzzle is actually solvable) isn't wired up yet.
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

    public void OnPointerClick(PointerEventData eventData)
    {
        Vector2Int anchor = Grid.Instance.WorldToCell(transform.position);
        FreeFootprint(anchor);

        Vector2Int next = anchor + Grid.Instance.GetFlowDirection(anchor, defaultDirection);
        while (FootprintFree(next))
        {
            anchor = next;
            next = anchor + Grid.Instance.GetFlowDirection(anchor, defaultDirection);
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
}
