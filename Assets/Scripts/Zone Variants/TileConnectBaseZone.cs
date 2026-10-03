using System.Collections.Generic;
using UnityEngine;
using Variants;

//every tile-field cell is a single-cell container that only ever loses its token
//via PathConnectInteraction, never gains one via ordinary drag
public class TileConnectBaseZone : BaseZone
{
    [SerializeField] int margin = 1;
    protected override int maxGenerationAttempts => 300;
    
    Dictionary<Vector2Int, Container> cellLookup;
    List<(Vector2Int a, Vector2Int b, int group)> pendingPairs;
    
    protected override List<Container> GenerateContainers()
    {
        var fieldBounds = new RectInt(margin, margin, boardSize - margin * 2, boardSize - margin * 2);
        List<Container> cells = new PerTileLayout().Build(fieldBounds, GameState.Instance.BubbleColor);

        //dropping one cell up front to keep the count even and pairing possible
        if (cells.Count % 2 != 0)
        {
            Container extra = cells[cells.Count - 1];
            cells.RemoveAt(cells.Count - 1);
            ContainerManager.Instance.RemoveContainer(extra);
        }

        new ContainerRuleSet { EntryConstraint = new NoEntryConstraint(), OccupantInteraction = new PathConnectInteraction() }.ApplyToAll(cells);
        cellLookup = new Dictionary<Vector2Int, Container>();
        foreach (Container cell in cells) cellLookup[cell.OrderedCells[0]] = cell; //maps each (per-grid) container to the grid coords

        return cells;
    }

    protected override bool TryGenerateLayout()
    {
        //pair generation
        pendingPairs = new List<(Vector2Int a, Vector2Int b, int group)>();
        var reserved = new HashSet<Vector2Int>();  //tracks cells already paired
        var order = new List<Vector2Int>(cellLookup.Keys);
        ContainerManager.Shuffle(order);
        
        //match any two reachable same-colored tiles
        int pairCount = cellLookup.Count / 2;
        List<int> colors = ContainerManager.BuildQuotaMatchedGroups(pairCount, groupCount, 1);
        ContainerManager.Shuffle(colors);
        int nextColorIndex = 0;

        foreach (Vector2Int a in order)
        {
            if (reserved.Contains(a)) continue;

            //runs a BFS through all reachable unreserved space
            List<Vector2Int> candidates = ReachableTileCells(a, cellLookup, reserved);
            if (candidates.Count == 0) continue; //left unpaired this attempt

            Vector2Int b = candidates[Random.Range(0, candidates.Count)];  //picks random compatible partner
            reserved.Add(a);
            reserved.Add(b);
            pendingPairs.Add((a, b, colors[nextColorIndex]));
            nextColorIndex++;
        }

        return true;
    }
    
    protected override bool IsSolvable() => pendingPairs.Count * 2 == cellLookup.Count;

    protected override void CommitLayout()
    {
        foreach ((Vector2Int a, Vector2Int b, int group) in pendingPairs)
        {
            SeedToken(cellLookup[a], group, grid.CellToWorld(a));
            SeedToken(cellLookup[b], group, grid.CellToWorld(b));
        }
    }

    //places a token directly into Members rather than through TryAccept, since the cell's own
    //NoEntryConstraint would otherwise reject it the same way it rejects a player's drag
    static void SeedToken(Container cell, int group, Vector3 worldPosition)
    {
        Token token = TokenSpawner.Instance.SpawnColoredToken(group, worldPosition);
        cell.Members.Add(token);
        token.CurrentContainer = cell;
    }

    //BFS flood fills
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
                frontier.Enqueue(neighbor);  //flood fills even on container-less cells
                if (cellLookup.ContainsKey(neighbor) && neighbor != seed) result.Add(neighbor);
            }
        }
        return result;
    }
}
