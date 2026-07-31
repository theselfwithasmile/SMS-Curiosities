using System.Collections.Generic;
using UnityEngine;

namespace Variants
{
    public abstract class Zone : MonoBehaviour
    {
        [SerializeField] int boardSize = 5;
        [SerializeField] int pieceCount = 3;
        [SerializeField] Color boardColor = Color.gray;
        
        void Start()
        {
            Grid grid = Grid.Instance;
            int size = Mathf.Min(boardSize, grid.Columns, grid.Rows - 1);

            Container bench = ContainerManager.Instance.CreateFixedContainer(new RectInt(0, size, grid.Columns, 1), Color.gray);
            
            List<Container> containers = GenerateContainers();
            List<int> groups = GenerateGroups();

            for (int i = 0; i < containers.Count; i++)
            {
                Token token = GenerateTokens(TokenSpawner.Instance, groups[i], grid.CellToWorld(containers[i].OrderedCells[0]));
                containers[i].TryAccept(token);
            }
        }

        protected abstract List<Container>  GenerateContainers();
        protected abstract List<int> GenerateGroups();
        protected abstract Token GenerateTokens(TokenSpawner spawner, int group, Vector3 pos);
    }
}