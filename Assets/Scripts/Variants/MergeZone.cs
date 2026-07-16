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

        List<Container> cells = new PerTileLayout().Build(new RectInt(0, 0, size, size), Color.gray);
        new ContainerRuleSet
        {
            OccupantInteraction = new MergeInteraction(),
            CompletionPredicate = new TierReachedPredicate(targetTier),
            Resolution = new LogResolution(),
        }.ApplyToAll(cells);

        ContainerManager.Shuffle(cells);
        int spawnCount = Mathf.Min(initialTokenCount, cells.Count);
        for (int i = 0; i < spawnCount; i++)
        {
            Container cell = cells[i];
            int group = Random.Range(0, groupCount);
            Token token = containers.SpawnColoredToken(group, grid.CellToWorld(cell.OrderedCells[0]));
            cell.TryAccept(token);
        }
    }
}
