using System.Collections.Generic;
using UnityEngine;
using Variants;

// Woodoku-style: one container per row + one per column (overlapping, via the multi-membership
// lookup), tier-agnostic (geometry only, no entry constraint), clearing on full. Pieces are drawn
// from a small canonical shape set and staged in the row just below the board, ready to drag.
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

    // No real bench Container here, just raw grid space for the staging row - reserved as its own
    // rows rather than borrowed from boardSize, so it never competes with the board for space.
    // 2 rows (not 1) since the tallest canonical shapes above are 2 cells tall.
    protected override int ExtraReservedRows => 2;

    // Board-cleared (BaseZone's default) doesn't apply here - rows/columns clear their members but
    // stay on the board forever, ready to accept more pieces, so the board is never actually
    // "done". The win objective instead is a target number of row/column clears, scaling with
    // difficulty the same way other zones scale their spawn counts.
    [SerializeField] int targetClears = 10;
    int clearsCompleted = 0;

    // Tracks the current batch so Update() can tell when every staged piece has been placed
    // (TryClaimFootprint destroys the original token on a successful placement - see
    // TokenSpawner - so Unity's overloaded null-check on a placed entry reads as null here) and
    // spawn a fresh batch. Without this the zone only ever had the first 3 pieces to work with.
    readonly List<Token> stagedPieces = new List<Token>();

    protected override void Update()
    {
        base.Update(); // may report the win/lose outcome this frame
        if (zoneEnded || !IsPlaying) return;

        if (stagedPieces.Count == 0) return;

        foreach (Token piece in stagedPieces)
        {
            if (piece != null) return; // at least one piece still unplaced - wait
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

    // A loss here is "the current batch has a piece left, and none of the still-unplaced pieces
    // fit anywhere on the board" - checked against every remaining piece rather than stopping at
    // the first, since one boxed-in piece next to a perfectly placeable one isn't game over.
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

    // Same atomic-footprint capacity check TokenSpawner.TryClaimFootprint uses to actually commit a
    // placement, just without committing - board containers have no EntryConstraint, so capacity is
    // the whole story.
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
            // Each staged piece picks its own group directly - the shared `groups` list is quota-
            // matched against board CELLS for an unrelated purpose (and doesn't even apply here,
            // since these board containers have no group entry constraint), and is sized to
            // containerCount, not pieceCount, so indexing it by staged-piece index was fragile.
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
