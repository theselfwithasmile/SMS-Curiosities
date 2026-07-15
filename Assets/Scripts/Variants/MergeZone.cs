using System.Collections.Generic;
using UnityEngine;

// Merge (2048-direct): single-cell containers, no entry constraint (empty cells accept anything).
// Dragging a token onto a same-group/same-tier occupant combines them via MergeInteraction
// instead of being rejected. Win condition is "some cell's occupant reached targetTier" rather
// than "everything cleared", since merging can never empty the board on its own.
public class MergeZone : MonoBehaviour
{
    [SerializeField] int boardSize = 5;
    [SerializeField] int initialTokenCount = 6;
    [SerializeField] int groupCount = 2;
    [SerializeField] int targetTier = 4;

    void Start()
    {
        Grid grid = Grid.Instance;
        ContainerManager containers = ContainerManager.Instance;
        int size = Mathf.Min(boardSize, grid.Columns, grid.Rows);

        var cells = new List<Container>(size * size);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Container cell = containers.CreateFixedContainer(new RectInt(x, y, 1, 1), Color.gray);
                cell.OccupantInteraction = new MergeInteraction();
                cell.CompletionPredicate = new TierReachedPredicate(targetTier);
                cell.Resolution = new LogResolution();
                cells.Add(cell);
            }
        }

        Shuffle(cells);
        int spawnCount = Mathf.Min(initialTokenCount, cells.Count);
        for (int i = 0; i < spawnCount; i++)
        {
            Container cell = cells[i];
            int group = Random.Range(0, groupCount);
            Token token = containers.SpawnToken(group, grid.CellToWorld(cell.OrderedCells[0]));
            token.GetComponent<SpriteRenderer>().color = GameState.Instance.GroupColor(group);
            cell.TryAccept(token);
        }
    }

    static void Shuffle(List<Container> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
