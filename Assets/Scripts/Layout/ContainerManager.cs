using System.Collections.Generic;
using UnityEngine;

// Container/token generation, lookup, and rendering. Depends on Grid for coordinate conversion
// and bounds, and keeps Grid's occupancy set in sync so its dotted-cell rendering stays correct.
public class ContainerManager : MonoBehaviour
{
    public static ContainerManager Instance;

    [SerializeField] Token tokenPrefab;
    [SerializeField] SpriteRenderer containerPrefab;

    public readonly List<Container> Containers = new List<Container>();
    readonly Dictionary<Vector2Int, List<Container>> cellMemberships = new Dictionary<Vector2Int, List<Container>>();
    static readonly List<Container> NoContainers = new List<Container>();
    readonly Dictionary<Container, SpriteRenderer> containerVisuals = new Dictionary<Container, SpriteRenderer>();

    void Awake()
    {
        Instance = this;
    }

    public IReadOnlyList<Container> GetContainersAt(Vector2Int cell)
    {
        return cellMemberships.TryGetValue(cell, out List<Container> list) ? list : NoContainers;
    }

    //partitions totalCount into groupCount buckets, each a multiple of chunkSize
    public static List<int> BuildQuotaMatchedGroups(int totalCount, int groupCount, int chunkSize)
    {
        var groups = new List<int>(totalCount);
        int perGroup = (totalCount / groupCount / chunkSize) * chunkSize;

        for (int group = 0; group < groupCount; group++)
        {
            for (int i = 0; i < perGroup; i++) groups.Add(group);
        }

        //rounding leftovers get distributed as full chunks across random groups
        int remaining = totalCount - groups.Count;
        while (remaining >= chunkSize)
        {
            int group = Random.Range(0, groupCount);
            for (int i = 0; i < chunkSize; i++) groups.Add(group);
            remaining -= chunkSize;
        }
        
        //anything smaller than chunkSize left is folded in as a rare, acceptable straggler.
        for (int i = 0; i < remaining; i++)
        {
            groups.Add(Random.Range(0, groupCount));
        }

        return groups;
    }

    public static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
    
    public Container CreateFixedContainer(RectInt bounds, Color color)
    {
        var cells = new List<Vector2Int>();
        for (int y = bounds.yMin; y < bounds.yMax; y++)
        {
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                var cell = new Vector2Int(x, y);
                if (!cellMemberships.ContainsKey(cell)) cells.Add(cell);
            }
        }
        return RegisterContainer(cells, color, bounds);
    }

    public void RemoveContainer(Container container)
    {
        Containers.Remove(container);
        if (containerVisuals.TryGetValue(container, out SpriteRenderer visual))
        {
            Destroy(visual.gameObject);
            containerVisuals.Remove(container);
        }

        //removes container's cells
        foreach (Vector2Int cell in container.Cells)
        {
            if (cellMemberships.TryGetValue(cell, out List<Container> owners))
            {
                owners.Remove(container);
                if (owners.Count == 0)
                {
                    cellMemberships.Remove(cell);
                    Grid.Instance.SetOccupied(cell, false);
                }
            }
        }
    }

    // Picks a rect of the given capacity (a straight line along axisBias if one's given, otherwise
    // the closest-to-square factor pair) and places it at a random free position on the board -
    // the generated-container counterpart to CreateFixedContainer's caller-supplied rect.
    public Container GenerateContainer(int capacity, Color color, Vector2Int axisBias = default)
    {
        Vector2Int size = RectSizeFor(capacity, axisBias);
        if (size.x > Grid.Instance.Columns || size.y > Grid.Instance.Rows) return null;

        var origins = new List<Vector2Int>();
        for (int y = 0; y <= Grid.Instance.Rows - size.y; y++)
        {
            for (int x = 0; x <= Grid.Instance.Columns - size.x; x++)
            {
                origins.Add(new Vector2Int(x, y));
            }
        }
        Shuffle(origins);

        foreach (Vector2Int origin in origins)
        {
            var bounds = new RectInt(origin.x, origin.y, size.x, size.y);
            if (!IsRectFree(bounds)) continue;

            var cells = new List<Vector2Int>(capacity);
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                for (int x = bounds.xMin; x < bounds.xMax; x++)
                {
                    cells.Add(new Vector2Int(x, y));
                }
            }
            return RegisterContainer(cells, color, bounds);
        }
        return null;
    }

    // No bias: closest-to-square factor pair of capacity. Biased: a 1-wide line running along the
    // bias axis (capacity cells long) - e.g. Water Sort's vertical tubes.
    static Vector2Int RectSizeFor(int capacity, Vector2Int axisBias)
    {
        if (axisBias.y != 0) return new Vector2Int(1, capacity);
        if (axisBias.x != 0) return new Vector2Int(capacity, 1);

        int width = Mathf.CeilToInt(Mathf.Sqrt(capacity));
        while (width > 1 && capacity % width != 0) width--;
        return new Vector2Int(width, capacity / width);
    }

    bool IsRectFree(RectInt bounds)
    {
        for (int y = bounds.yMin; y < bounds.yMax; y++)
        {
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                if (cellMemberships.ContainsKey(new Vector2Int(x, y))) return false;
            }
        }
        return true;
    }

    Container RegisterContainer(List<Vector2Int> cells, Color color, RectInt bounds)
    {
        var container = new Container(cells, color, bounds);
        Containers.Add(container);
        foreach (Vector2Int cell in cells)
        {
            if (!cellMemberships.TryGetValue(cell, out List<Container> owners))
            {
                owners = new List<Container>();
                cellMemberships[cell] = owners;
            }
            owners.Add(container);
            Grid.Instance.SetOccupied(cell, true);
        }
        containerVisuals[container] = CreateVisual(container);
        return container;
    }

    // Sizes/positions the prefab's 9-sliced sprite to exactly cover the container's rect, and
    // tints it to the container's colour the same way Token tints its own sprite per-group.
    SpriteRenderer CreateVisual(Container container)
    {
        SpriteRenderer visual = Instantiate(containerPrefab, transform);
        visual.color = container.Color;
        visual.drawMode = SpriteDrawMode.Sliced;
        // Tokens sit at the prefab's default sortingOrder (0) - draw containers behind them
        // deterministically rather than relying on transparent-queue instantiation order, which
        // isn't guaranteed for sprites sharing the same Z.
        visual.sortingOrder = -1;
        // `size` below is a local-space measurement that gets multiplied by whatever scale the
        // prefab happens to carry (the same authored-scale convention Token.prefab uses to blow
        // its own small native sprite up to world size) - neutralize it so `size` alone controls
        // world-space dimensions, otherwise the rect comes out scaled by whatever the prefab's
        // transform was left at in the editor.
        visual.transform.localScale = Vector3.one;

        Vector3 min = Grid.Instance.CellToWorld(new Vector2Int(container.Bounds.xMin, container.Bounds.yMin));
        Vector3 max = Grid.Instance.CellToWorld(new Vector2Int(container.Bounds.xMax - 1, container.Bounds.yMax - 1));
        visual.transform.position = (min + max) * 0.5f;
        visual.size = new Vector2(container.Bounds.width, container.Bounds.height) * Grid.Instance.CellSize;

        return visual;
    }

    static readonly Vector2Int[] NeighborOffsets = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    //adjacent cells
    public List<Vector2Int> Neighbors(Vector2Int cell)
    {
        var neighbors = new List<Vector2Int>(NeighborOffsets.Length);
        foreach (Vector2Int offset in NeighborOffsets)
        {
            Vector2Int neighbor = cell + offset;
            if (Grid.Instance.IsInBounds(neighbor)) neighbors.Add(neighbor);
        }
        return neighbors;
    }
}
