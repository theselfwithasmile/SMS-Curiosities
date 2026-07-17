using System.Collections.Generic;
using UnityEngine;

// Woodoku-style: one container per row + one per column (overlapping, via the multi-membership
// lookup), tier-agnostic (geometry only, no entry constraint), clearing on full. Pieces are drawn
// from a small canonical shape set and staged in the row just below the board, ready to drag.
public class BlockPuzzleZone : MonoBehaviour
{
    [SerializeField] int boardSize = 5;
    [SerializeField] int pieceCount = 3;
    [SerializeField] Color boardColor = Color.gray;

    static readonly List<Vector2Int>[] Shapes =
    {
        new List<Vector2Int> { Vector2Int.zero },
        new List<Vector2Int> { Vector2Int.zero, Vector2Int.right },
        new List<Vector2Int> { Vector2Int.zero, Vector2Int.right, Vector2Int.right * 2 },
        new List<Vector2Int> { Vector2Int.zero, Vector2Int.up, Vector2Int.right },
        new List<Vector2Int> { Vector2Int.zero, Vector2Int.right, Vector2Int.up, Vector2Int.up + Vector2Int.right },
    };

    void Start()
    {
        Grid grid = Grid.Instance;
        ContainerManager containers = ContainerManager.Instance;
        int size = Mathf.Min(boardSize, grid.Columns, grid.Rows - 1);

        List<Container> board = new OrthogonalLayout().Build(new RectInt(0, 0, size, size), boardColor);
        new ContainerRuleSet
        {
            CompletionPredicate = new FullPredicate(),
            Resolution = new ClearResolution(),
        }.ApplyToAll(board);

        
        int stagingRow = size;
        int stageX = 0;
        for (int i = 0; i < pieceCount; i++)
        {
            List<Vector2Int> shape = Shapes[Random.Range(0, Shapes.Length)];
            int group = Random.Range(0, 6);
            int width = ShapeWidth(shape);

            if (stageX + width > grid.Columns) break;

            Vector3 anchorWorld = grid.CellToWorld(new Vector2Int(stageX, stagingRow));
            containers.SpawnMultiCellToken(group, anchorWorld, shape);

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
