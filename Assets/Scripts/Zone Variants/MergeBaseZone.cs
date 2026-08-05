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

    // How many distinct groups actually get a chunk on this board - decided in GenerateContainers
    // (see comment there) and reused by GenerateTokens as the group count to quota-match against.
    int playGroups;

    protected override List<Container> GenerateContainers()
    {
        List<Container> cells = new PerTileLayout().Build(new RectInt(0, 0, boardSize, boardSize), GameState.Instance.BubbleColor);

        // Reaching a tier requires 2^tier same-group tokens merged in sequence. Sizing that
        // against the WHOLE board (as if only one group would ever be in play) left
        // BuildQuotaMatchedGroups below with no room for a second group's full chunk once
        // totalTokens landed at exactly one chunk's worth - it silently dumped the entire quota
        // into a single random group instead of spreading across groupCount. playGroups is how
        // many distinct groups actually get a chunk this board; each needs its own equal share of
        // the cells, so the target tier has to fit an EQUAL SHARE, not the entire board.
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

    // Packs (most of) the board so every one of the playGroups groups gets its own multiple of
    // 2^effectiveTargetTier - the same quota-matching approach Toon Blast/Water Sort rely on, just
    // scoped to playGroups (not every group in the palette) and with the merge chunk size instead
    // of minMatchSize. A leftover smaller than the chunk size can still land as a straggler (same
    // accepted imperfection BuildQuotaMatchedGroups already has for those zones) - a token stuck
    // in a group too small to ever reach the target tier.
    protected override void GenerateTokens()
    {
        int requiredForWin = 1 << effectiveTargetTier;
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
