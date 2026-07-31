using System.Collections.Generic;
using UnityEngine;
using Variants;

public class WaterSortZone : Zone
{
    [SerializeField] int tubeCount = 3;
    protected override int QuotaFactor => 3;  //tube capacity
    protected override int BenchCapacity => tubeCount * QuotaFactor;

    protected override List<Container> GenerateContainers()
    {
        var tubeLayout = new RegionGrowthLayout(QuotaFactor, Vector2Int.up, 1f);
        var tubes = new List<Container>();
        for (int group = 0; group < tubeCount; group++)
        {
            tubes.AddRange(tubeLayout.Build(default, GameState.Instance.GroupColor(group)));
        }

        new ContainerRuleSet
        {
            EntryConstraint = new GroupMatchConstraint(),
            CompletionPredicate = new FullPredicate(),
            Resolution = new SpawnTokenResolution(bench),
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
        List<int> tokenGroups = ContainerManager.BuildQuotaMatchedGroups(tubeCount * QuotaFactor, tubeCount, QuotaFactor);
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
