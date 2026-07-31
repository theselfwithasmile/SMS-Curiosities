using System.Collections.Generic;
using UnityEngine;

namespace Variants
{
    public abstract class Zone : MonoBehaviour
    {
        [SerializeField] protected int boardLength = 5;
        [SerializeField] protected int pieceCount = 3;
        [SerializeField] protected Color boardColor = Color.gray;

        protected virtual int QuotaFactor => 3;
        protected virtual int maxGenerationAttempts => 1;

        protected Grid grid;
        protected int boardSize = 5;
        protected int containerCount = 0;
        protected int groupCount = 0;

        protected Container bench;
        protected List<Container> containers;
        protected List<int> groups;

        // How many token slots the bench needs to hold - 0 means no bench at all (most zones).
        // Converted to rows and reserved on the grid itself (Grid.ReserveBenchRows), which grows
        // the grid to fit rather than squeezing the bench into whatever's left of a fixed row
        // count - that's what silently overflowed before (Water Sort needing tubeCount*capacity
        // slots, easily more than one row's worth of columns).
        protected virtual int BenchCapacity => 0;

        // Extra rows reserved with no Container attached - for a zone that just needs raw grid
        // space outside the board (Block Puzzle's piece-staging row), as opposed to BenchCapacity
        // which also creates a real accept/reject Container there.
        protected virtual int ExtraReservedRows => 0;

        void Start()
        {
            grid = Grid.Instance;
            boardSize = Mathf.Min(boardLength, grid.Columns, grid.Rows);
            groupCount = GameState.Instance.GroupCount;

            int benchRows = BenchCapacity > 0 ? Mathf.Max(1, Mathf.CeilToInt(BenchCapacity / (float)grid.Columns)) : 0;
            int reservedRows = benchRows + ExtraReservedRows;
            if (reservedRows > 0)
            {
                grid.ReserveBenchRows(reservedRows);
            }
            if (benchRows > 0)
            {
                bench = ContainerManager.Instance.CreateFixedContainer(new RectInt(0, grid.BenchOrigin, grid.Columns, benchRows), Color.gray);
            }

            containers = GenerateContainers();
            containerCount = containers.Count;

            groups = GenerateGroups();

            // Most zones' layouts are trivially valid (TryGenerateLayout/IsSolvable default to
            // true, CommitLayout defaults to the simple GenerateTokens below) - a zone that needs
            // to retry random layouts against a solvability check (Parking Jam) just overrides the
            // three hooks instead of this loop itself.
            for (int attempt = 0; attempt < maxGenerationAttempts; attempt++)
            {
                if (TryGenerateLayout() && IsSolvable())
                {
                    CommitLayout();
                    return;
                }
            }
            Debug.LogWarning($"{GetType().Name}: no solvable layout found after {maxGenerationAttempts} attempt(s).");
        }

        protected abstract List<Container> GenerateContainers();

        protected virtual List<int> GenerateGroups()
        {
            List<int> groups = ContainerManager.BuildQuotaMatchedGroups(containerCount, groupCount, QuotaFactor);
            ContainerManager.Shuffle(groups);

            return groups;
        }

        protected virtual bool TryGenerateLayout() => true;
        protected virtual bool IsSolvable() => true;
        protected virtual void CommitLayout() => GenerateTokens();

        protected virtual void GenerateTokens()
        {
            for (int i = 0; i < containerCount; i++)
            {
                Token token = TokenSpawner.Instance.SpawnColoredToken(groups[i], grid.CellToWorld(containers[i].OrderedCells[0]));
                containers[i].TryAccept(token);
            }
        }
    }
}