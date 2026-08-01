using System.Collections.Generic;
using UnityEngine;
using Variants;

// Merge (2048-direct): single-cell containers, no entry constraint (empty cells accept anything).
// Dragging a token onto a same-group/same-tier occupant combines them via MergeInteraction
// instead of being rejected. Win condition is "some cell's occupant reached targetTier" rather
// than "everything cleared", since merging can never empty the board on its own.
public class MergeBaseZone : BaseZone
{
    [SerializeField] int initialTokenCount = 6;
    [SerializeField] int targetTier = 4;

    int effectiveTargetTier;

    protected override List<Container> GenerateContainers()
    {
        List<Container> cells = new PerTileLayout().Build(new RectInt(0, 0, boardSize, boardSize), Color.gray);

        // Reaching a tier requires 2^tier same-group tokens merged in sequence, so the target has
        // to fit within how many cells could ever hold that group's tokens at once - otherwise the
        // win condition is silently unreachable (tier 4 needs 16 tokens, easily more than a small
        // board can even hold simultaneously).
        int maxAchievableTier = Mathf.Max(1, Mathf.FloorToInt(Mathf.Log(Mathf.Max(1, cells.Count), 2f)));
        effectiveTargetTier = Mathf.Clamp(targetTier, 1, maxAchievableTier);

        new ContainerRuleSet
        {
            OccupantInteraction = new MergeInteraction(),
            CompletionPredicate = new TierReachedPredicate(effectiveTargetTier),
            Resolution = new LogResolution(),
        }.ApplyToAll(cells);
        ContainerManager.Shuffle(cells);

        return cells;
    }

    // initialTokenCount and targetTier used to be independent, so the win condition was often
    // mathematically unreachable (tier 4 needs 16 same-group tokens merged together, but 6 tokens
    // spread randomly across several groups essentially never share that many). One randomly
    // chosen "winning" group is now guaranteed at least 2^effectiveTargetTier tokens;
    // initialTokenCount only controls how many additional distractor tokens (other groups) pad out
    // the rest, and clamps up if it was set below what the target actually requires.
    protected override void GenerateTokens()
    {
        int requiredForWin = 1 << effectiveTargetTier;
        int spawnCount = Mathf.Min(Mathf.Max(initialTokenCount, requiredForWin), containers.Count);

        int winningGroup = Random.Range(0, groupCount);
        var tokenGroups = new List<int>(spawnCount);
        for (int i = 0; i < requiredForWin; i++) tokenGroups.Add(winningGroup);
        for (int i = tokenGroups.Count; i < spawnCount; i++) tokenGroups.Add(Random.Range(0, groupCount));
        ContainerManager.Shuffle(tokenGroups);

        for (int i = 0; i < spawnCount; i++)
        {
            Container cell = containers[i];
            Token token = TokenSpawner.Instance.SpawnColoredToken(tokenGroups[i], grid.CellToWorld(cell.OrderedCells[0]));
            cell.TryAccept(token);
        }
    }
}
