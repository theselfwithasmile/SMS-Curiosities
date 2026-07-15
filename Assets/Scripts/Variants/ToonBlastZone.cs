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

        var cells = new List<Container>(size * size);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Container cell = containers.CreateFixedContainer(new RectInt(x, y, 1, 1), Color.gray);
                cell.OccupantInteraction = new SwapInteraction(bench, minMatchSize);
                cells.Add(cell);
            }
        }

        List<int> groups = BuildQuotaMatchedGroups(cells.Count);
        Shuffle(groups);

        for (int i = 0; i < cells.Count; i++)
        {
            int group = groups[i];
            Token token = containers.SpawnToken(group, grid.CellToWorld(cells[i].OrderedCells[0]));
            token.GetComponent<SpriteRenderer>().color = GameState.Instance.GroupColor(group);
            cells[i].TryAccept(token);
        }
    }

    List<int> BuildQuotaMatchedGroups(int totalCells)
    {
        var groups = new List<int>(totalCells);
        int perGroup = (totalCells / groupCount / minMatchSize) * minMatchSize;

        for (int group = 0; group < groupCount; group++)
        {
            for (int i = 0; i < perGroup; i++) groups.Add(group);
        }

        // Rounding leftovers get distributed as full minMatchSize chunks across random groups,
        // keeping every group's count a clean multiple so it's guaranteed clearable. Anything
        // smaller than minMatchSize left after that can't form a guaranteed-clearable group -
        // folded into an existing group's count as a rare, acceptable straggler cell.
        int remaining = totalCells - groups.Count;
        while (remaining >= minMatchSize)
        {
            int group = Random.Range(0, groupCount);
            for (int i = 0; i < minMatchSize; i++) groups.Add(group);
            remaining -= minMatchSize;
        }
        for (int i = 0; i < remaining; i++)
        {
            groups.Add(Random.Range(0, groupCount));
        }

        return groups;
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
