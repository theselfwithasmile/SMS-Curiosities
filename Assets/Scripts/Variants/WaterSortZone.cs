using System.Collections.Generic;
using UnityEngine;
using Variants;

public class WaterSortZone : Zone
{
    [SerializeField] int tubeCount = 3;
    protected override int QuotaFactor => 3;  //tube capacity
    protected override bool NeedsBench => true;

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
