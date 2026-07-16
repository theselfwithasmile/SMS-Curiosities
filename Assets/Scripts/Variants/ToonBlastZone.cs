using System.Collections.Generic;
using UnityEngine;

// Toon Blast-style: the whole board is single-cell containers, fully packed with tokens (no
// gaps, no refill). Dragging one onto a different-group occupant swaps them via SwapInteraction,
// which flood-fills same-group neighbors and clears runs of minMatchSize+ into a grouped token
// in the bench. Group counts are quota-matched (each a multiple of minMatchSize) so the board is
// guaranteed fully clearable - free rearrangement means any existing run is always reachable,
// no solver needed (same reasoning as Water Sort's quota-matched supply).
public class ToonBlastZone : MonoBehaviour
{
    [SerializeField] int boardSize = 6;
    [SerializeField] int groupCount = 5;
    [SerializeField] int minMatchSize = 3;

    void Start()
    {
        Grid grid = Grid.Instance;
        ContainerManager containers = ContainerManager.Instance;
        int size = Mathf.Min(boardSize, grid.Columns, grid.Rows - 1);

        Container bench = containers.CreateFixedContainer(new RectInt(0, size, grid.Columns, 1), Color.gray);

        List<Container> cells = new PerTileLayout().Build(new RectInt(0, 0, size, size), Color.gray);
        new ContainerRuleSet
        {
            OccupantInteraction = new SwapInteraction(bench, minMatchSize),
        }.ApplyToAll(cells);

        List<int> groups = ContainerManager.BuildQuotaMatchedGroups(cells.Count, groupCount, minMatchSize);
        ContainerManager.Shuffle(groups);

        for (int i = 0; i < cells.Count; i++)
        {
            Token token = containers.SpawnColoredToken(groups[i], grid.CellToWorld(cells[i].OrderedCells[0]));
            cells[i].TryAccept(token);
        }
    }
}
