using System.Collections.Generic;
using UnityEngine;
using Variants;

//the whole board is single-cell containers, fully packed with tokens
//dragging one onto a different-group occupant swaps them via SwapInteraction,
//which flood-fills same-group neighbors and clears runs of minMatchSize+
public class ToonBlastBaseZone : BaseZone
{
    protected override int QuotaFactor => 3;  //min match capacity

    protected override List<Container> GenerateContainers()
    {
        List<Container> cells = new PerTileLayout().Build(new RectInt(0, 0, boardSize, boardSize), GameState.Instance.BubbleColor);
        new ContainerRuleSet
        {
            OccupantInteraction = new SwapInteraction(bench, QuotaFactor),
        }.ApplyToAll(cells);

        return cells;
    }

    protected override void GenerateTokens()
    {
        for (int i = 0; i < containerCount; i++)
        {
            Container cell = containers[i];
            Token top = TokenSpawner.Instance.SpawnColoredToken(groups[i], grid.CellToWorld(cell.OrderedCells[0]));
            cell.TryAccept(top);
        }
    }
}
