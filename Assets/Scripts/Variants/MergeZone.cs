using System.Collections.Generic;
using UnityEngine;
using Variants;

// Merge (2048-direct): single-cell containers, no entry constraint (empty cells accept anything).
// Dragging a token onto a same-group/same-tier occupant combines them via MergeInteraction
// instead of being rejected. Win condition is "some cell's occupant reached targetTier" rather
// than "everything cleared", since merging can never empty the board on its own.
public class MergeZone : Zone
{
    [SerializeField] int initialTokenCount = 6;
    [SerializeField] int targetTier = 4;

    protected override List<Container> GenerateContainers()
    {
        List<Container> cells = new PerTileLayout().Build(new RectInt(0, 0, boardSize, boardSize), Color.gray);
        new ContainerRuleSet
        {
            OccupantInteraction = new MergeInteraction(),
            CompletionPredicate = new TierReachedPredicate(targetTier),
            Resolution = new LogResolution(),
        }.ApplyToAll(cells);
        ContainerManager.Shuffle(cells);

        return cells;
    }

    // Merge needs a mostly-empty board (room to drag tokens into), not one token per cell - the
    // shared default GenerateTokens() fully packs the board, which is wrong here.
    protected override void GenerateTokens()
    {
        int spawnCount = Mathf.Min(initialTokenCount, containers.Count);
        for (int i = 0; i < spawnCount; i++)
        {
            Container cell = containers[i];
            int group = Random.Range(0, groupCount);
            Token token = TokenSpawner.Instance.SpawnColoredToken(group, grid.CellToWorld(cell.OrderedCells[0]));
            cell.TryAccept(token);
        }
    }
}
