using System.Collections.Generic;
using UnityEngine;

// Tile Connect (Onet-style, simplified): every tile-field cell is a single-cell container that
// only ever loses its token via PathConnectInteraction, never gains one via ordinary drag
// (NoEntryConstraint) - tokens are seeded once at generation, bypassing TryAccept directly.
//
// Generated backward ("reverse search", per the puzzle-generation research this project has been
// chasing): start empty, repeatedly pick an unpaired cell and a same-region partner reachable
// from it (given everything already placed), assign them a matching group, mark both reserved,
// repeat. This guarantees solvability - clearing pairs in the exact reverse of generation order
// recreates, at each step, precisely the occupancy that pair was originally verified against.
//
// The tile field is inset from the grid's own bounds, leaving an outer margin with no containers
// at all - that margin is the shared "outside" corridor connecting all four edges, without which
// edge tiles would have nowhere to route through at all.
public class TileConnectZone : MonoBehaviour
{
    [SerializeField] int margin = 1;
    [SerializeField] Color boardColor = Color.gray;

    void Start()
    {
        Grid grid = Grid.Instance;
        ContainerManager containers = ContainerManager.Instance;

		//leave the outermost grid layers to be empty to create room for dragging(?)
        int fieldWidth = Mathf.Max(2, grid.Columns - margin * 2);
        int fieldHeight = Mathf.Max(2, grid.Rows - margin * 2);
        if ((fieldWidth * fieldHeight) % 2 != 0) fieldWidth -= 1; // needs an even cell count to pair fully

        var bounds = new RectInt(margin, margin, fieldWidth, fieldHeight);

        List<Container> cells = new PerTileLayout().Build(bounds, boardColor);
        new ContainerRuleSet { EntryConstraint = new NoEntryConstraint(), OccupantInteraction = new PathConnectInteraction() }.ApplyToAll(cells);

        var cellLookup = new Dictionary<Vector2Int, Container>();
        foreach (Container cell in cells) cellLookup[cell.OrderedCells[0]] = cell;

        var pairs = new List<(Vector2Int a, Vector2Int b, int group)>();
        var reserved = new HashSet<Vector2Int>();
        var order = new List<Vector2Int>(cellLookup.Keys);
        ContainerManager.Shuffle(order);

        int nextGroup = 0;
        foreach (Vector2Int a in order)
        {
            if (reserved.Contains(a)) continue;

            List<Vector2Int> candidates = ReachableTileCells(a, cellLookup, reserved);
            if (candidates.Count == 0) continue; // straggler - left unpaired, a rare acceptable leftover

            Vector2Int b = candidates[Random.Range(0, candidates.Count)];
            reserved.Add(a);
            reserved.Add(b);
            pairs.Add((a, b, nextGroup));
            nextGroup++;
        }

        foreach ((Vector2Int a, Vector2Int b, int group) in pairs)
        {
            SeedToken(cellLookup[a], group, grid.CellToWorld(a));
            SeedToken(cellLookup[b], group, grid.CellToWorld(b));
        }
    }

    // Places a token directly into Members rather than through TryAccept, since the cell's own
    // NoEntryConstraint would otherwise reject it the same way it rejects a player's drag.
    static void SeedToken(Container cell, int group, Vector3 worldPosition)
    {
        Token token = ContainerManager.Instance.SpawnColoredToken(group, worldPosition);
        cell.Members.Add(token);
        token.CurrentContainer = cell;
    }

    // Cells reachable from seed by walking through unreserved space (open margin cells have no
    // container at all and are always passable), restricted to other unpaired tile-field cells -
    // structurally the same flood-fill as the runtime PathConnectInteraction uses, but checked
    // against the generation-time reserved set instead of live Container membership.
    static List<Vector2Int> ReachableTileCells(Vector2Int seed, Dictionary<Vector2Int, Container> cellLookup, HashSet<Vector2Int> reserved)
    {
        var visited = new HashSet<Vector2Int> { seed };
        var frontier = new Queue<Vector2Int>();
        frontier.Enqueue(seed);
        var result = new List<Vector2Int>();

        while (frontier.Count > 0)
        {
            Vector2Int cell = frontier.Dequeue();
            foreach (Vector2Int neighbor in ContainerManager.Instance.Neighbors(cell))
            {
                if (visited.Contains(neighbor) || reserved.Contains(neighbor)) continue;
                visited.Add(neighbor);
                frontier.Enqueue(neighbor);
                if (cellLookup.ContainsKey(neighbor) && neighbor != seed) result.Add(neighbor);
            }
        }
        return result;
    }
}
