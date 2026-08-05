using System.Collections.Generic;
using UnityEngine;

namespace Variants
{
    public abstract class BaseZone : MonoBehaviour
    {
        [SerializeField] protected int boardLength = 5;
        [SerializeField] protected int pieceCount = 3;
        [SerializeField] protected Color boardColor = Color.gray;

        // How much harder each accumulated difficulty level makes this zone - boardGrowthPerLevel
        // widens the board (Grid itself grows to match via Grid.SetBoardSize, rather than
        // boardSize being clamped down to whatever the scene's Grid inspector values happen to
        // be), spawnGrowthPerLevel is a general-purpose "one more level, one more X" knob
        // individual zones opt into for their own extra counts (tube count, initial tokens, staged
        // piece batch size, clear quota...) via the shared Scaled() helper below.
        [SerializeField] protected int boardGrowthPerLevel = 1;
        [SerializeField] protected int spawnGrowthPerLevel = 1;

        protected virtual int QuotaFactor => 3;
        protected virtual int maxGenerationAttempts => 1;

        protected Grid grid;
        protected int boardSize = 5;
        protected int containerCount = 0;
        protected int groupCount = 0;

        protected Container bench;
        protected List<Container> containers;
        protected List<int> groups;

        // Set true only once a layout actually committed (so Update()'s polling can't fire on a
        // zone that failed to generate at all), then zoneEnded latches true the moment a win/lose
        // fires so it can only ever report an outcome once per zone instance.
        protected bool zoneReady;
        protected bool zoneEnded;

        // How many times the player has advanced (GameFlowManager.NextLevel) - read defensively
        // since GameFlowManager is a scene addition the editor wiring may not have placed yet;
        // absent, every zone just plays at its unscaled baseline.
        protected int Difficulty => GameFlowManager.Instance != null ? GameFlowManager.Instance.DifficultyLevel : 0;

        protected int Scaled(int baseValue, int perLevel) => baseValue + Difficulty * perLevel;

        // True while there's no GameFlowManager to say otherwise (keeps zones usable without one
        // wired in yet) or it explicitly says Playing - false for Menu/Paused/Won/Lost. Gates both
        // this class's own Update() polling and Block Puzzle's extra per-frame batch-refill logic.
        protected bool IsPlaying => GameFlowManager.Instance == null || GameFlowManager.Instance.State == FlowState.Playing;

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
            boardSize = Scaled(boardLength, boardGrowthPerLevel);
            grid.SetBoardSize(boardSize);
            groupCount = GameState.Instance.GroupCount;

            int benchRows = BenchCapacity > 0 ? Mathf.Max(1, Mathf.CeilToInt(BenchCapacity / (float)grid.Columns)) : 0;
            int reservedRows = benchRows + ExtraReservedRows;
            if (reservedRows > 0)
            {
                grid.ReserveBenchRows(reservedRows);
            }
            if (benchRows > 0)
            {
                bench = ContainerManager.Instance.CreateFixedContainer(new RectInt(0, grid.BenchOrigin, grid.Columns, benchRows), GameState.Instance.BubbleColor);
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
                    zoneReady = true;
                    return;
                }
            }
            Debug.LogWarning($"{GetType().Name}: no solvable layout found after {maxGenerationAttempts} attempt(s).");
        }

        // Polls once per frame rather than hooking every mutation site (container resolution,
        // token destruction, escape...) individually - those live across half a dozen classes
        // (Container, TweenRunner, Token, per-zone interactions), and a puzzle board is small
        // enough that a plain scan is free. Subclasses with their own per-frame work (Block
        // Puzzle's batch refill) override and call base.Update() first so an outcome detected this
        // frame short-circuits whatever they'd otherwise do next.
        protected virtual void Update()
        {
            if (!zoneReady || zoneEnded || !IsPlaying) return;

            if (CheckWinCondition())
            {
                zoneEnded = true;
                GameFlowManager.Instance?.ReportWin();
            }
            else if (CheckLoseCondition())
            {
                zoneEnded = true;
                GameFlowManager.Instance?.ReportLose();
            }
        }

        // Universal default: the board is fully cleared (every container empty, nothing left
        // buried). Holds as-is for Toon Blast, Tile Connect and Water Sort - each empties its
        // containers as the only way to make progress, just via different mechanics. Merge
        // (tier-reached) and Parking Jam (no containers at all) override outright; Block Puzzle
        // overrides because clearing rows/columns never reduces the board - it needs its own quota.
        protected virtual bool CheckWinCondition()
        {
            if (containers == null || containers.Count == 0) return false;

            foreach (Container container in containers)
            {
                if (container.Members.Count > 0 || container.BuriedCount > 0) return false;
            }
            return true;
        }

        // No zone can get permanently stuck by default - the ones that can (Block Puzzle) override.
        protected virtual bool CheckLoseCondition() => false;

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