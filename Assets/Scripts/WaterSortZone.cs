using System.Collections.Generic;
using UnityEngine;

public class WaterSortZone : MonoBehaviour
{
    [SerializeField] int tubeCount = 3;
    [SerializeField] int tubeCapacity = 3;
    [SerializeField] Color[] groupColors;

    void Start()
    {
        Grid grid = Grid.Instance;

        int totalTokens = tubeCount * tubeCapacity;
        int benchRows = Mathf.Max(1, Mathf.CeilToInt(totalTokens / (float)grid.Columns));
        Container bench = grid.CreateFixedContainer(new RectInt(0, 0, grid.Columns, benchRows), Color.gray);

        //generate containers
        var tubes = new List<Container>();
        for (int group = 0; group < tubeCount; group++)
        {
            Container tube = grid.GenerateContainer(tubeCapacity, GroupColor(group), Vector2Int.up, 1f);
            if (tube == null) continue;

            //enforce containers rules
            tube.EntryConstraints.Add(new GroupMatchConstraint());
            tube.CompletionPredicate = new FullPredicate();
            tube.Resolution = new SpawnTokenResolution(bench);
            
            tubes.Add(tube);
        }

        //generate token groups
        var groups = new List<int>();
        for (int group = 0; group < tubes.Count; group++)
        {
            for (int i = 0; i < tubeCapacity; i++)
            {
                groups.Add(group);
            }
        }
        Shuffle(groups);

        //generate each group's tokens to be placed on the bench
        foreach (int group in groups)
        {
            Vector2Int cell = bench.NextAvailableCell();
            Token token = grid.SpawnToken(group, grid.CellToWorld(cell));
            token.GetComponent<SpriteRenderer>().color = GroupColor(group);
            bench.TryAccept(token);
        }
    }

    Color GroupColor(int group)
    {
        return groupColors.Length > 0 ? groupColors[group % groupColors.Length] : Color.white;
    }

    static void Shuffle(List<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
