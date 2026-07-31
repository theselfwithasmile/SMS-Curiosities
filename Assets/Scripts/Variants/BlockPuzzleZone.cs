using System.Collections.Generic;
using UnityEngine;
using Variants;

// Woodoku-style: one container per row + one per column (overlapping, via the multi-membership
// lookup), tier-agnostic (geometry only, no entry constraint), clearing on full. Pieces are drawn
// from a small canonical shape set and staged in the row just below the board, ready to drag.
public class BlockPuzzleZone : Zone
{
    static readonly List<Vector2Int>[] Shapes =
    {
        new List<Vector2Int> { Vector2Int.zero },
        new List<Vector2Int> { Vector2Int.zero, Vector2Int.right },
        new List<Vector2Int> { Vector2Int.zero, Vector2Int.right, Vector2Int.right * 2 },
        new List<Vector2Int> { Vector2Int.zero, Vector2Int.up, Vector2Int.right },
        new List<Vector2Int> { Vector2Int.zero, Vector2Int.right, Vector2Int.up, Vector2Int.up + Vector2Int.right },
    };

    // No real bench Container here, just raw grid space for the staging row - reserved as its own
    // rows rather than borrowed from boardSize, so it never competes with the board for space.
    // 2 rows (not 1) since the tallest canonical shapes above are 2 cells tall.
    protected override int ExtraReservedRows => 2;

    protected override List<Container> GenerateContainers()
    {
        List<Container> board = new OrthogonalLayout().Build(new RectInt(0, 0, boardSize, boardSize), boardColor);
        new ContainerRuleSet
        {
            CompletionPredicate = new FullPredicate(),
            Resolution = new ClearResolution(),
        }.ApplyToAll(board);

        return board;
    }

    protected override void GenerateTokens()
    {
        int stagingRow = boardSize;
        int stageX = 0;
        for (int i = 0; i < pieceCount; i++)
        {
            List<Vector2Int> shape = Shapes[Random.Range(0, Shapes.Length)];
            int width = ShapeWidth(shape);

            if (stageX + width > grid.Columns) break;

            Vector3 anchorWorld = grid.CellToWorld(new Vector2Int(stageX, stagingRow));
            // Each staged piece picks its own group directly - the shared `groups` list is quota-
            // matched against board CELLS for an unrelated purpose (and doesn't even apply here,
            // since these board containers have no group entry constraint), and is sized to
            // containerCount, not pieceCount, so indexing it by staged-piece index was fragile.
            TokenSpawner.Instance.SpawnMultiCellToken(Random.Range(0, groupCount), anchorWorld, shape);

            stageX += width + 1;
        }
    }

    static int ShapeWidth(List<Vector2Int> shape)
    {
        int maxX = 0;
        foreach (Vector2Int cell in shape) maxX = Mathf.Max(maxX, cell.x);
        return maxX + 1;
    }
}
