using System.Collections.Generic;
using UnityEngine;
using Variants;

//tokens spawn directly on the containers and can be dragged into any open slot
//of a container that already matches their group (or is empty)
//A container resolves (clears) once it's both full AND all-same-group
public class WaterSortBaseZone : BaseZone
{
    [SerializeField] int tubeCount = 3;
    [SerializeField] int emptyTubeCount = 1;    // buffer tubes; >= 1 required for solvability
    [SerializeField] int maxBuriedLayers = 1;  

    protected override int QuotaFactor => 3;  
    int EffectiveTubeCount => Scaled(tubeCount, spawnGrowthPerLevel);
    List<int> buriedCounts;

    protected override List<Container> GenerateContainers()
    {
        var tubeLayout = new RegionGrowthLayout(QuotaFactor, Vector2Int.up);
        var tubes = new List<Container>();

        //one coloured container per group
        for (int group = 0; group < EffectiveTubeCount; group++)
        {
            tubes.AddRange(tubeLayout.Build(default, GameState.Instance.GroupColor(group)));
        }

        //empty buffer containers 
        for (int i = 0; i < emptyTubeCount; i++)
        {
            tubes.AddRange(tubeLayout.Build(default, GameState.Instance.BubbleColor));
        }
        
        new ContainerRuleSet
        {
            EntryConstraint = new GroupMatchConstraint(),
            Resolution = new ClearResolution(),
            CompletionPredicate = new FullAndSameGroupPredicate(),
        }.ApplyToAll(tubes);

        return tubes;
    }

    // Generate token-group assignments for every visible slot PLUS every buried token beneath them
    // Buried counts are decided here, up front, so the pool can be sized correctly
    protected override List<int> GenerateGroups()
    {
        int visibleCount = EffectiveTubeCount * QuotaFactor;

        buriedCounts = new List<int>(visibleCount);
        int totalBuried = 0;
        for (int i = 0; i < visibleCount; i++)
        {
            int count = maxBuriedLayers > 0 ? Random.Range(0, maxBuriedLayers + 1) : 0;
            buriedCounts.Add(count);
            totalBuried += count;
        }

        // BuildQuotaMatchedGroups only guarantees every group's total is a QuotaFactor multiple
        // when the pool size itself is one
        int overflow = totalBuried % QuotaFactor;
        while (overflow > 0)
        {
            int slot = Random.Range(0, visibleCount);
            if (buriedCounts[slot] <= 0) continue;
            
            //trim total down to the nearest multiple to ensure no strays left
            buriedCounts[slot]--;
            totalBuried--;
            overflow--;
        }

        List<int> tokenGroups = ContainerManager.BuildQuotaMatchedGroups(
            visibleCount + totalBuried, EffectiveTubeCount, QuotaFactor);
        ContainerManager.Shuffle(tokenGroups);
        return tokenGroups;
    }

    //Spawn tokens directly onto containers, one per visible slot, until every coloured tube's
    //slots are filled
    protected override void GenerateTokens()
    {
        int visibleCount = EffectiveTubeCount * QuotaFactor;
        int groupIndex = 0;
        int visibleSlot = 0;

        foreach (Container tube in containers)
        {
            for (int slot = 0; slot < tube.Capacity; slot++)
            {
                if (visibleSlot >= visibleCount) return;  // remaining containers are empty buffers

                Vector2Int cell = tube.OrderedCells[slot];
                Vector3 worldPos = grid.CellToWorld(cell);

                //token placed using ForceAccept to bypass entry constraints during generation 
                Token visible = TokenSpawner.Instance.SpawnColoredToken(groups[groupIndex++], worldPos);
                tube.ForceAcceptAt(visible, cell);

                //optional buried layers beneath the visible one
                int buriedCount = buriedCounts[visibleSlot++];
                for (int layer = 0; layer < buriedCount; layer++)
                {
                    Token buried = TokenSpawner.Instance.SpawnColoredToken(groups[groupIndex++], worldPos);
                    buried.SetRevealed(false);
                    tube.BuryAt(cell, buried);
                }
            }
        }
    }
}
