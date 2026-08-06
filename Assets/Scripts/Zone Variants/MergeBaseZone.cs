using System.Collections.Generic;
using UnityEngine;
using Variants;

//single-cell containers, no entry constraint (empty cells accept anything)
//dragging a token onto a same-group/same-tier occupant combines them via MergeInteraction instead of being rejected
public class MergeBaseZone : BaseZone
{
    [SerializeField] int initialTokenCount = 6;
    [SerializeField] int targetTier = 4;

    int effectiveTargetTier;
    int playGroups;

    protected override List<Container> GenerateContainers()
    {
        List<Container> cells = new PerTileLayout().Build(new RectInt(0, 0, boardSize, boardSize), GameState.Instance.BubbleColor);

        //reaching a tier requires 2^tier same-group tokens merged in sequence
        playGroups = Mathf.Clamp(groupCount, 1, Mathf.Max(1, cells.Count / 2));
        int maxAchievableTier = Mathf.Max(1, Mathf.FloorToInt(Mathf.Log(Mathf.Max(2, cells.Count / playGroups), 2f)));
        effectiveTargetTier = Mathf.Clamp(Scaled(targetTier, spawnGrowthPerLevel), 1, maxAchievableTier);

        new ContainerRuleSet
        {
            OccupantInteraction = new MergeInteraction(effectiveTargetTier),
        }.ApplyToAll(cells);
        ContainerManager.Shuffle(cells);

        return cells;
    }

    //packs most of the board so every one of the playGroups groups gets its own multiple of 2^effectiveTargetTier
    protected override void GenerateTokens()
    {
        int requiredForWin = 1 << effectiveTargetTier;   //multiply by 2^effectiveTargetTier
        int minTokens = playGroups * requiredForWin;
        int totalTokens = Mathf.Clamp(Scaled(initialTokenCount, spawnGrowthPerLevel), minTokens, containers.Count);

        List<int> tokenGroups = ContainerManager.BuildQuotaMatchedGroups(totalTokens, playGroups, requiredForWin);
        ContainerManager.Shuffle(tokenGroups);

        for (int i = 0; i < tokenGroups.Count; i++)
        {
            Container cell = containers[i];
            Token token = TokenSpawner.Instance.SpawnColoredToken(tokenGroups[i], grid.CellToWorld(cell.OrderedCells[0]), byTier: true);
            cell.TryAccept(token);
        }
    }
}
