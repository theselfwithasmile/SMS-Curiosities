using System.Collections.Generic;
using UnityEngine;
using Variants;

public class WaterSortBaseZone : BaseZone
{
    [SerializeField] int tubeCount = 3;
    protected override int QuotaFactor => 3;  //tube capacity

    // Water Sort's containers come from tubeCount directly rather than boardSize, so difficulty
    // scaling needs to touch this rather than relying on the board-growth default to do it for free.
    int EffectiveTubeCount => Scaled(tubeCount, spawnGrowthPerLevel);

    protected override int BenchCapacity => EffectiveTubeCount * QuotaFactor;

    protected override List<Container> GenerateContainers()
    {
        var tubeLayout = new RegionGrowthLayout(QuotaFactor, Vector2Int.up, 1f);
        var tubes = new List<Container>();
        for (int group = 0; group < EffectiveTubeCount; group++)
        {
            tubes.AddRange(tubeLayout.Build(default, GameState.Instance.GroupColor(group)));
        }

        new ContainerRuleSet
        {
            EntryConstraint = new GroupMatchConstraint(),
            CompletionPredicate = new FullPredicate(),
        }.ApplyToAll(tubes);

        return tubes;
    }

    // The shared default GenerateGroups() quota-matches one entry per CONTAINER (right for a
    // zone that spawns one token per cell) - Water Sort instead needs one entry per eventual
    // BENCH TOKEN (tubeCount * capacity), matched against exactly tubeCount groups so every tube
    // can be filled with a single color. Using the container-count default here was generating
    // far too few tokens (one per tube, not per tube-slot) and spreading them across the wrong
    // number of groups.
    protected override List<int> GenerateGroups()
    {
        int effectiveTubeCount = EffectiveTubeCount;
        List<int> tokenGroups = ContainerManager.BuildQuotaMatchedGroups(effectiveTubeCount * QuotaFactor, effectiveTubeCount, QuotaFactor);
        ContainerManager.Shuffle(tokenGroups);
        return tokenGroups;
    }

    protected override void GenerateTokens()
    {
        foreach (int group in groups)
        {
            Vector2Int cell = bench.NextAvailableCell();
            Token token = TokenSpawner.Instance.SpawnColoredToken(group, grid.CellToWorld(cell));
            bench.TryAccept(token);  //places tokens into bench
        }
    }
}
