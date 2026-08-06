using System.Collections.Generic;
using UnityEngine;
using Variants;

//woodoku-style: one container per row + one per column, clearing on full.
//Pieces are drawn from a small canonical shape set and staged in the row just below the board
public class BlockPuzzleBaseZone : BaseZone
{
    static readonly List<Vector2Int>[] Shapes =
    {
        new List<Vector2Int> { Vector2Int.zero },
        new List<Vector2Int> { Vector2Int.zero, Vector2Int.right },
        new List<Vector2Int> { Vector2Int.zero, Vector2Int.right, Vector2Int.right * 2 },
        new List<Vector2Int> { Vector2Int.zero, Vector2Int.up, Vector2Int.right },
        new List<Vector2Int> { Vector2Int.zero, Vector2Int.right, Vector2Int.up, Vector2Int.up + Vector2Int.right },
    };

    //raw grid space for the staging row
    protected override int ExtraReservedRows => 2;
    readonly List<Token> stagedPieces = new List<Token>();

    //the win objective instead is a target number of row/column clears
    [SerializeField] int targetClears = 10;
    int clearsCompleted = 0;
    
    protected override void Update()
    {
        base.Update(); // may report the win/lose outcome this frame
        if (zoneEnded || !IsPlaying) return;

        if (stagedPieces.Count == 0) return;

        foreach (Token piece in stagedPieces)
        {
            if (piece != null) return; //at least one piece still unplaced
        }
        SpawnBatch();
    }

    protected override List<Container> GenerateContainers()
    {
        List<Container> board = new OrthogonalLayout().Build(new RectInt(0, 0, boardSize, boardSize), boardColor);
        new ContainerRuleSet
        {
            CompletionPredicate = new FullPredicate(),
            Resolution = new CompositeResolution(new ClearResolution(), () => clearsCompleted++),
        }.ApplyToAll(board);

        return board;
    }

    protected override bool CheckWinCondition() => clearsCompleted >= Scaled(targetClears, spawnGrowthPerLevel);

    //a loss here is "the current batch has a piece left, and none of the still-unplaced pieces
    //fit anywhere on the board" 
    protected override bool CheckLoseCondition()
    {
        bool anyUnplaced = false;
        foreach (Token piece in stagedPieces)
        {
            if (piece == null) continue; // already placed
            anyUnplaced = true;
            if (CanPlaceAnywhere(piece.CellOffsets)) return false;
        }
        return anyUnplaced;
    }

    bool CanPlaceAnywhere(List<Vector2Int> offsets)
    {
        for (int y = 0; y < boardSize; y++)
        {
            for (int x = 0; x < boardSize; x++)
            {
                if (CanPlaceAt(new Vector2Int(x, y), offsets)) return true;
            }
        }
        return false;
    }

    //same atomic-footprint capacity check TokenSpawner.TryClaimFootprint
    bool CanPlaceAt(Vector2Int anchor, List<Vector2Int> offsets)
    {
        var pendingCounts = new Dictionary<Container, int>();
        foreach (Vector2Int offset in offsets)
        {
            Vector2Int cell = anchor + offset;
            IReadOnlyList<Container> owners = ContainerManager.Instance.GetContainersAt(cell);
            if (owners.Count == 0) return false;

            foreach (Container container in owners)
            {
                pendingCounts.TryGetValue(container, out int pending);
                if (container.Members.Count + pending >= container.Capacity) return false;
                pendingCounts[container] = pending + 1;
            }
        }
        return true;
    }

    protected override void GenerateTokens() => SpawnBatch();

    void SpawnBatch()
    {
        stagedPieces.Clear();

        int stagingRow = boardSize;
        int stageX = 0;
        int effectivePieceCount = Scaled(pieceCount, spawnGrowthPerLevel);
        for (int i = 0; i < effectivePieceCount; i++)
        {
            List<Vector2Int> shape = Shapes[Random.Range(0, Shapes.Length)];
            int width = ShapeWidth(shape);

            if (stageX + width > grid.Columns) break;

            Vector3 anchorWorld = grid.CellToWorld(new Vector2Int(stageX, stagingRow));
            //the shared `groups` list is quota-matched against board CELLS for an unrelated purpose
            //and is sized to containerCount, not pieceCount, so indexing it by staged-piece index was fragile.
            Token piece = TokenSpawner.Instance.SpawnMultiCellToken(Random.Range(0, groupCount), anchorWorld, shape);
            stagedPieces.Add(piece);

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
