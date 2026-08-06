using System;
using System.Collections.Generic;
using UnityEngine;

namespace Variants
{
    public abstract class BaseZone : MonoBehaviour
    {
        [SerializeField] protected int boardLength = 5;
        [SerializeField] protected int pieceCount = 3;
        [SerializeField] protected Color boardColor = Color.gray;
        [SerializeField] protected int boardGrowthPerLevel = 1;
        [SerializeField] protected int spawnGrowthPerLevel = 1;

        protected virtual int QuotaFactor => 3;
        protected virtual int maxGenerationAttempts => 1;
        protected virtual int BenchCapacity => 0;
        protected virtual int ExtraReservedRows => 0; //extra rows reserved with no Container attached 

        protected Grid grid;
        protected int boardSize = 5;
        protected int containerCount = 0;
        protected int groupCount = 0;

        protected Container bench;
        protected List<Container> containers;
        protected List<int> groups;
        
        protected bool zoneReady;
        protected bool zoneEnded;
        protected int Difficulty => GameFlowManager.Instance != null ? GameFlowManager.Instance.DifficultyLevel : 0;

        protected int Scaled(int baseValue, int perLevel) => baseValue + Difficulty * perLevel;

        //true while there's no GameFlowManager to say otherwise or it explicitly says Playing
        protected bool IsPlaying => GameFlowManager.Instance == null || GameFlowManager.Instance.State == FlowState.Playing;
        

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

            //solvability check
            groups = GenerateGroups();
            for (int attempt = 0; attempt < maxGenerationAttempts; attempt++)
            {
                if (TryGenerateLayout() && IsSolvable())
                {
                    CommitLayout();
                    zoneReady = true;
                    return;
                }
            }
        }
        
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

        private void FixedUpdate()
        {
            GameState.Instance.CurrScore = Difficulty;
        }

        //the board is fully cleared as default win condition
        protected virtual bool CheckWinCondition()
        {
            if (containers == null || containers.Count == 0) return false;

            foreach (Container container in containers)
            {
                if (container.Members.Count > 0 || container.BuriedCount > 0) return false;
            }
            return true;
        }
        
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