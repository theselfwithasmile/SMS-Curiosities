using System.Collections.Generic;
using UnityEngine;
using Variants;

// Shelf-sort style zone (formerly Water Sort): tokens spawn directly on the containers and can be
// dragged into any open slot of a container that already matches their group (or is empty) —
// GroupMatchConstraint still gates entry by colour, but unlike classic water sort there's no
// "pour from the top"/stack-order rule: Container.TryAcceptAt targets the exact cell dropped on,
// not whichever slot happens to be next in claim order. Empty buffer tubes accept any colour (see
// below), giving mismatched tokens somewhere to wait. A container resolves (clears) once it's
// both full AND all-same-group, so the puzzle is to sort the jumbled tokens back into monochrome
// containers.
//
// Solvability: exactly (tubeCount * QuotaFactor) visible tokens are generated across (tubeCount +
// emptyTubeCount) containers.  The emptyTubeCount containers start completely empty and serve as
// buffer space.  With free arbitrary placement, any permutation of tokens across containers is
// reachable as long as there's at least one empty slot — emptyTubeCount >= 1 guarantees this.
//
// Buried tokens are supported: some container slots start with a hidden token beneath the visible
// one, just like ToonBlast, revealed (in that same cell) once the visible token above it is
// permanently moved away or the container clears. Buried tokens are folded into the same
// quota-matched pool as the visible ones (see GenerateGroups) rather than assigned an independent
// random group — otherwise a colour's total count (visible + buried) could land on a number that
// isn't a multiple of QuotaFactor, leaving a leftover that can never fill a whole container and
// permanently soft-locking the puzzle once it surfaces.
public class WaterSortBaseZone : BaseZone
{
    [SerializeField] int tubeCount = 3;
    [SerializeField] int emptyTubeCount = 1;    // buffer tubes; >= 1 required for solvability
    [SerializeField] int maxBuriedLayers = 1;   // 0 = no buried tokens; 1+ adds layering depth

    protected override int QuotaFactor => 3;  // container slot capacity

    // No bench needed — tokens live on the containers from the start.
    protected override int BenchCapacity => 0;

    // Colour-group tubes only (not counting the empty buffers).
    int EffectiveTubeCount => Scaled(tubeCount, spawnGrowthPerLevel);

    // How many tokens are buried under each visible slot (index-aligned with visible-slot order),
    // decided up front in GenerateGroups so the total token count is known before the
    // quota-matched pool is built. Consumed in the same order by GenerateTokens.
    List<int> buriedCounts;

    protected override List<Container> GenerateContainers()
    {
        var tubeLayout = new RegionGrowthLayout(QuotaFactor, Vector2Int.up, 1f);
        var tubes = new List<Container>();

        // One coloured container per group — the colour hints at what the player should sort in.
        for (int group = 0; group < EffectiveTubeCount; group++)
        {
            tubes.AddRange(tubeLayout.Build(default, GameState.Instance.GroupColor(group)));
        }

        // Empty buffer containers — neutral colour, no pre-filled tokens.
        // These are the "free slots" that make arbitrary rearrangement possible.
        for (int i = 0; i < emptyTubeCount; i++)
        {
            tubes.AddRange(tubeLayout.Build(default, Color.gray));
        }

        // GroupMatchConstraint still gates which colour may enter a given tube (or an empty one,
        // which accepts anything) - but within that, any of the tube's open cells is a valid
        // target (see Token.TryEnterAll), not just the next one in claim order. That guarantee
        // only holds for player-initiated entry though: a buried token's reveal deliberately
        // bypasses it (Container.RevealAt), so a full tube isn't necessarily solved - hence
        // FullAndSameGroupPredicate rather than plain FullPredicate.
        new ContainerRuleSet
        {
            EntryConstraint = new GroupMatchConstraint(),
            Resolution = new ClearResolution(),
            CompletionPredicate = new FullAndSameGroupPredicate(),
        }.ApplyToAll(tubes);

        return tubes;
    }

    // Generate token-group assignments for every visible slot PLUS every buried token beneath
    // them, all drawn from one quota-matched pool (each group's grand total — visible + buried —
    // a multiple of QuotaFactor). Buried counts are decided here, up front, so the pool can be
    // sized correctly; GenerateTokens consumes visible and buried entries in the same order.
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

        List<int> tokenGroups = ContainerManager.BuildQuotaMatchedGroups(
            visibleCount + totalBuried, EffectiveTubeCount, QuotaFactor);
        ContainerManager.Shuffle(tokenGroups);
        return tokenGroups;
    }

    // Spawn tokens directly onto containers, one per visible slot, until every coloured tube's
    // slots are filled — the empty buffer tubes are never reached (visibleSlot caps at
    // tubeCount * QuotaFactor) and stay empty as working space.
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

                // Visible (top) token — placed using ForceAccept to bypass entry constraints
                // during generation (the constraint only applies to player moves).
                Token visible = TokenSpawner.Instance.SpawnColoredToken(groups[groupIndex++], worldPos);
                tube.ForceAcceptAt(visible, cell);

                // Optional buried layers beneath the visible one, revealed (in this same cell)
                // once it's vacated — see Container.RevealAt.
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
