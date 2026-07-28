using System.Collections.Generic;
using UnityEngine;

// Container/token generation, lookup, and rendering. Depends on Grid for coordinate conversion
// and bounds, and keeps Grid's occupancy set in sync so its dotted-cell rendering stays correct.
public class ContainerManager : MonoBehaviour
{
    public static ContainerManager Instance;

    [SerializeField] Token tokenPrefab;

    public readonly List<Container> Containers = new List<Container>();
    readonly Dictionary<Vector2Int, List<Container>> cellMemberships = new Dictionary<Vector2Int, List<Container>>();
    static readonly List<Container> NoContainers = new List<Container>();

    Mesh quadMesh;
    readonly Dictionary<Container, Material> containerMaterials = new Dictionary<Container, Material>();

    void Awake()
    {
        Instance = this;
        quadMesh = BuildQuadMesh();
    }

    void Update()
    {
        DrawContainers();
    }

    public IReadOnlyList<Container> GetContainersAt(Vector2Int cell)
    {
        return cellMemberships.TryGetValue(cell, out List<Container> list) ? list : NoContainers;
    }
    

    // Partitions totalCount into groupCount buckets, each a multiple of chunkSize, so a zone
    // built from the result is guaranteed fully clearable/fillable by construction (Water Sort's
    // per-tube supply, Toon Blast's per-group cell counts - same operation, different chunk size).
    public static List<int> BuildQuotaMatchedGroups(int totalCount, int groupCount, int chunkSize)
    {
        var groups = new List<int>(totalCount);
        int perGroup = (totalCount / groupCount / chunkSize) * chunkSize;

        for (int group = 0; group < groupCount; group++)
        {
            for (int i = 0; i < perGroup; i++) groups.Add(group);
        }

        // Rounding leftovers get distributed as full chunks across random groups, keeping every
        // group's count a clean multiple. Anything smaller than chunkSize left after that can't
        // form a guaranteed-complete group - folded in as a rare, acceptable straggler.
        int remaining = totalCount - groups.Count;
        while (remaining >= chunkSize)
        {
            int group = Random.Range(0, groupCount);
            for (int i = 0; i < chunkSize; i++) groups.Add(group);
            remaining -= chunkSize;
        }
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

    // Claims an exact rectangular region rather than growing randomly - for deliberately placed
    // regions (e.g. the bench) rather than procedurally shaped puzzle containers.
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
        return RegisterContainer(cells, color);
    }

    public void RemoveContainer(Container container)
    {
        Containers.Remove(container);
        containerMaterials.Remove(container);
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

    // Randomized region growth: claims a random unclaimed seed cell, then repeatedly claims a
    // random neighboring cell of the claimed region until it reaches the target capacity.
    // axisBias (e.g. Vector2Int.up) with biasStrength > 0 skews growth along that axis, which is
    // how the same algorithm produces both blobs (no bias) and tubes/lanes (strong axis bias).
    public Container GenerateContainer(int capacity, Color color, Vector2Int axisBias = default, float biasStrength = 0f)
    {
        var unclaimed = new List<Vector2Int>();
        for (int y = 0; y < Grid.Instance.Rows; y++)
        {
            for (int x = 0; x < Grid.Instance.Columns; x++)
            {
                var cell = new Vector2Int(x, y);
                if (!cellMemberships.ContainsKey(cell)) unclaimed.Add(cell);
            }
        }
        if (unclaimed.Count == 0) return null;

        Vector2Int seed = unclaimed[Random.Range(0, unclaimed.Count)];
        var claimed = new HashSet<Vector2Int> { seed };
        var orderedClaimed = new List<Vector2Int> { seed };
        var frontier = new List<Vector2Int>();
        AddFrontier(seed, claimed, frontier);

        while (claimed.Count < capacity && frontier.Count > 0)
        {
            int index = PickFrontierIndex(frontier, seed, axisBias, biasStrength);
            Vector2Int next = frontier[index];
            frontier.RemoveAt(index);
            if (claimed.Contains(next)) continue;

            claimed.Add(next);
            orderedClaimed.Add(next);
            AddFrontier(next, claimed, frontier);
        }

        return RegisterContainer(orderedClaimed, color);
    }

    Container RegisterContainer(List<Vector2Int> cells, Color color)
    {
        var container = new Container(cells, color);
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
        containerMaterials[container] = Grid.BuildMaterial(color);
        return container;
    }

    void AddFrontier(Vector2Int cell, HashSet<Vector2Int> claimed, List<Vector2Int> frontier)
    {
        foreach (Vector2Int neighbor in Neighbors(cell))
        {
            if (!claimed.Contains(neighbor) && !cellMemberships.ContainsKey(neighbor) && !frontier.Contains(neighbor))
            {
                frontier.Add(neighbor);
            }
        }
    }

    static readonly Vector2Int[] NeighborOffsets = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

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

    int PickFrontierIndex(List<Vector2Int> frontier, Vector2Int seed, Vector2Int axisBias, float biasStrength)
    {
        if (axisBias == Vector2Int.zero || Random.value > biasStrength)
        {
            return Random.Range(0, frontier.Count);
        }

        int bestIndex = 0;
        int bestScore = int.MinValue;
        for (int i = 0; i < frontier.Count; i++)
        {
            Vector2Int offset = frontier[i] - seed;
            int score = offset.x * axisBias.x + offset.y * axisBias.y;
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }
        return bestIndex;
    }

    void DrawContainers()
    {
        foreach (Container container in Containers)
        {
            var matrices = new List<Matrix4x4>(container.Cells.Count);
            foreach (Vector2Int cell in container.Cells)
            {
                Vector3 center = Grid.Instance.CellToWorld(cell);
                matrices.Add(Matrix4x4.TRS(center, Quaternion.identity, Vector3.one * Grid.Instance.CellSize));
            }
            Grid.DrawBatched(quadMesh, containerMaterials[container], matrices);
        }
    }

    static Mesh BuildQuadMesh()
    {
        var vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
        };
        var triangles = new[] { 0, 1, 2, 0, 2, 3 };
        return Grid.BuildMesh("Quad", vertices, triangles);
    }
}
