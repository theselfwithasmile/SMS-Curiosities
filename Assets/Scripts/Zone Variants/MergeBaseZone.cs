using System.Collections.Generic;
using UnityEngine;
using Variants;

// Merge (2048-direct): single-cell containers, no entry constraint (empty cells accept anything).
// Dragging a token onto a same-group/same-tier occupant combines them via MergeInteraction
// instead of being rejected. Every group's token count is quota-matched to a multiple of
// 2^targetTier (BuildQuotaMatchedGroups, same helper Toon Blast/Water Sort use for their own
// solvability guarantee) so every group can in principle be merged all the way up. A token that
// reaches the target tier is flagged CanExitBoard and can be dragged off the grid to destroy
// itself (Token.OnEndDrag), so the board can actually go to zero occupants - letting this zone
// fall back to BaseZone's plain "every container empty" win condition instead of a bespoke one.
public class MergeBaseZone : BaseZone
{
    [SerializeField] int initialTokenCount = 6;
    [SerializeField] int targetTier = 4;

    int effectiveTargetTier;

    protected override List<Container> GenerateContainers()
    {
        List<Container> cells = new PerTileLayout().Build(new RectInt(0, 0, boardSize, boardSize), GameState.Instance.BubbleColor);

        // Reaching a tier requires 2^tier same-group tokens merged in sequence, so the target has
        // to fit within how many cells could ever hold that group's tokens at once - otherwise the
        // win condition is silently unreachable (tier 4 needs 16 tokens, easily more than a small
        // board can even hold simultaneously).
        int maxAchievableTier = Mathf.Max(1, Mathf.FloorToInt(Mathf.Log(Mathf.Max(1, cells.Count), 2f)));
        effectiveTargetTier = Mathf.Clamp(Scaled(targetTier, spawnGrowthPerLevel), 1, maxAchievableTier);

        new ContainerRuleSet
        {
            OccupantInteraction = new MergeInteraction(effectiveTargetTier),
        }.ApplyToAll(cells);
        ContainerManager.Shuffle(cells);

        return cells;
    }

    // Packs (most of) the board with tokens whose per-group totals are each a multiple of
    // 2^effectiveTargetTier - the same quota-matching approach Toon Blast/Water Sort rely on, just
    // with the merge chunk size instead of minMatchSize. A leftover smaller than the chunk size
    // can still land as a straggler (same accepted imperfection BuildQuotaMatchedGroups already
    // has for those zones) - a token stuck in a group too small to ever reach the target tier.
    protected override void GenerateTokens()
    {
        int requiredForWin = 1 << effectiveTargetTier;
        int totalTokens = Mathf.Clamp(Scaled(initialTokenCount, spawnGrowthPerLevel), requiredForWin, containers.Count);

        List<int> tokenGroups = ContainerManager.BuildQuotaMatchedGroups(totalTokens, groupCount, requiredForWin);
        ContainerManager.Shuffle(tokenGroups);

        for (int i = 0; i < tokenGroups.Count; i++)
        {
            Container cell = containers[i];
            Token token = TokenSpawner.Instance.SpawnColoredToken(tokenGroups[i], grid.CellToWorld(cell.OrderedCells[0]), byTier: true);
            cell.TryAccept(token);
        }
    }
}
