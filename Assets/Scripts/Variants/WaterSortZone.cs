using System.Collections.Generic;
using UnityEngine;

public class WaterSortZone : MonoBehaviour
{
    [SerializeField] int tubeCount = 3;
    [SerializeField] int tubeCapacity = 3;

    void Start()
    {
        Grid grid = Grid.Instance;
        ContainerManager containers = ContainerManager.Instance;

        int totalTokens = tubeCount * tubeCapacity;
        int benchRows = Mathf.Max(1, Mathf.CeilToInt(totalTokens / (float)grid.Columns));
        Container bench = containers.CreateFixedContainer(new RectInt(0, 0, grid.Columns, benchRows), Color.gray);

        var tubeLayout = new RegionGrowthLayout(tubeCapacity, Vector2Int.up, 1f);
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

        List<int> groups = ContainerManager.BuildQuotaMatchedGroups(tubes.Count * tubeCapacity, tubes.Count, tubeCapacity);
        ContainerManager.Shuffle(groups);

        foreach (int group in groups)
        {
            Vector2Int cell = bench.NextAvailableCell();
            Token token = containers.SpawnColoredToken(group, grid.CellToWorld(cell));
            bench.TryAccept(token);
        }
    }
}
