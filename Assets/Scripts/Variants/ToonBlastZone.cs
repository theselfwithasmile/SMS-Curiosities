using System.Collections.Generic;
using UnityEngine;
using Variants;

// Toon Blast-style: the whole board is single-cell containers, fully packed with tokens (no
// gaps, no refill). Dragging one onto a different-group occupant swaps them via SwapInteraction,
// which flood-fills same-group neighbors and clears runs of minMatchSize+ into a grouped token
// in the bench. Group counts are quota-matched (each a multiple of minMatchSize) so the board is
// guaranteed fully clearable - free rearrangement means any existing run is always reachable,
// no solver needed (same reasoning as Water Sort's quota-matched supply).
public class ToonBlastZone : Zone
{
    protected override int QuotaFactor => 3;  //min match capacity
    protected override bool NeedsBench => true;

    protected override List<Container> GenerateContainers()
    {
        List<Container> cells = new PerTileLayout().Build(new RectInt(0, 0, boardSize, boardSize), Color.gray);
        new ContainerRuleSet
        {
            OccupantInteraction = new SwapInteraction(bench, QuotaFactor),
        }.ApplyToAll(cells);

        return cells;
    }
}
