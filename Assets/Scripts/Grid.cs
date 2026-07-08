using System.Collections.Generic;
using UnityEngine;

public class Grid : MonoBehaviour
{
    public static Grid Instance;

    [SerializeField] int columns = 6;
    [SerializeField] int rows = 6;
    [SerializeField] float dotScale = 0.15f;
    [SerializeField] Color dotColor = Color.white;
    [SerializeField, Range(0f, 0.4f)] float viewportPadding = 0.05f;
    [SerializeField] Token tokenPrefab;

    public int Columns => columns;
    public int Rows => rows;

    const int MaxInstancesPerBatch = 1023;

    public readonly List<Container> Containers = new List<Container>();
    readonly Dictionary<Vector2Int, List<Container>> cellMemberships = new Dictionary<Vector2Int, List<Container>>();
    static readonly List<Container> NoContainers = new List<Container>();

    private Camera cam;
    private float cellSize;
    private Vector2 origin;

    private Mesh dotMesh;
    private Mesh quadMesh;
    private Material dotMaterial;
    private readonly Dictionary<Container, Material> containerMaterials = new Dictionary<Container, Material>();

    void Awake()
    {
        Instance = this;
        cam = Camera.main;
        dotMesh = BuildDotMesh();
        quadMesh = BuildQuadMesh();
        dotMaterial = BuildMaterial(dotColor);
        RecomputeLayout();
    }

    void Update()
    {
        RecomputeLayout();
        DrawEmptyCells();
        DrawContainers();
    }
    
    void RecomputeLayout()
    {
        //derives cell size from camera viewport and grid dimensions, with padding
        float viewportHeight = cam.orthographicSize * 2f;
        float viewportWidth = viewportHeight * cam.aspect;
        float usableWidth = viewportWidth * (1f - viewportPadding * 2f);
        float usableHeight = viewportHeight * (1f - viewportPadding * 2f);

        cellSize = Mathf.Min(usableWidth / columns, usableHeight / rows);

        //adjusts origin to cell size
        Vector2 gridSize = new Vector2(cellSize * columns, cellSize * rows);
        Vector2 center = (Vector2)cam.transform.position + (Vector2)transform.position;
        origin = center - gridSize * 0.5f;
    }

    public Vector2Int WorldToCell(Vector3 worldPosition)
    {
        Vector2 local = (Vector2)worldPosition - origin;
        int cellX = Mathf.Clamp(Mathf.FloorToInt(local.x / cellSize), 0, columns - 1);
        int cellY = Mathf.Clamp(Mathf.FloorToInt(local.y / cellSize), 0, rows - 1);
        return new Vector2Int(cellX, cellY);
    }

    public Vector3 CellToWorld(Vector2Int cell)
    {
        return CellCenter(cell.x, cell.y);
    }

    public IReadOnlyList<Container> GetContainersAt(Vector2Int cell)
    {
        return cellMemberships.TryGetValue(cell, out List<Container> list) ? list : NoContainers;
    }

    public Token SpawnToken(int group, Vector3 worldPosition)
    {
        Token token = Instantiate(tokenPrefab, worldPosition, Quaternion.identity);
        token.Group = group;
        return token;
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
                if (owners.Count == 0) cellMemberships.Remove(cell);
            }
        }
    }

    // Randomized region growth: claims a random unclaimed seed cell, then repeatedly claims a
    // random neighboring cell of the claimed region until it reaches the target capacity.
    // axisBias (e.g. Vector2Int.up) with biasStrength > 0 skews growth along that axis, which is
    // how the same algorithm produces both blobs (no bias) and tubes/lanes (strong axis bias).
    public Container GenerateContainer(int capacity, Color color, Vector2Int axisBias = default, float biasStrength = 0f)
    {
        //builds list of available cells
        var unclaimed = new List<Vector2Int>();
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
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

        //propagates outward from the seed cell until the target capacity is reached or no more frontier cells are available
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
        }
        containerMaterials[container] = BuildMaterial(color);
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

    List<Vector2Int> Neighbors(Vector2Int cell)
    {
        var neighbors = new List<Vector2Int>(NeighborOffsets.Length);
        foreach (Vector2Int offset in NeighborOffsets)
        {
            Vector2Int neighbor = cell + offset;

            //excludes out of bound cells
            if (neighbor.x >= 0 && neighbor.x < columns && neighbor.y >= 0 && neighbor.y < rows)
            {
                neighbors.Add(neighbor);
            }
        }
        return neighbors;
    }

    int PickFrontierIndex(List<Vector2Int> frontier, Vector2Int seed, Vector2Int axisBias, float biasStrength)
    {
        if (axisBias == Vector2Int.zero || Random.value > biasStrength)
        {
            return Random.Range(0, frontier.Count);
        }
        
        //finds the best frontier cell along the axis bias direction
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

    Vector3 CellCenter(int x, int y)
    {
        return new Vector3(origin.x + (x + 0.5f) * cellSize, origin.y + (y + 0.5f) * cellSize, 0f);
    }

    void DrawEmptyCells()
    {
        var matrices = new List<Matrix4x4>(columns * rows);
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                var cell = new Vector2Int(x, y);
                if (cellMemberships.ContainsKey(cell)) continue;

                Vector3 center = CellCenter(x, y);
                matrices.Add(Matrix4x4.TRS(center, Quaternion.identity, Vector3.one * (cellSize * dotScale)));
            }
        }
        DrawBatched(dotMesh, dotMaterial, matrices);
    }

    void DrawContainers()
    {
        foreach (Container container in Containers)
        {
            var matrices = new List<Matrix4x4>(container.Cells.Count);
            foreach (Vector2Int cell in container.Cells)
            {
                Vector3 center = CellCenter(cell.x, cell.y);
                matrices.Add(Matrix4x4.TRS(center, Quaternion.identity, Vector3.one * cellSize));
            }
            DrawBatched(quadMesh, containerMaterials[container], matrices);
        }
    }

    static void DrawBatched(Mesh mesh, Material material, List<Matrix4x4> matrices)
    {
        int batchCount = Mathf.CeilToInt(matrices.Count / (float)MaxInstancesPerBatch);
        for (int i = 0; i < batchCount; i++)
        {
            int start = i * MaxInstancesPerBatch;
            int count = Mathf.Min(MaxInstancesPerBatch, matrices.Count - start);
            Graphics.DrawMeshInstanced(mesh, 0, material, matrices.GetRange(start, count));
        }
    }

    static Mesh BuildDotMesh(int segments = 16)
    {
        var vertices = new Vector3[segments + 1];
        var triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;
        for (int i = 0; i < segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 0.5f;

            int triIndex = i * 3;
            triangles[triIndex] = 0;
            triangles[triIndex + 1] = i + 1;
            triangles[triIndex + 2] = i + 2 > segments ? 1 : i + 2;
        }

        return BuildMesh("Dot", vertices, triangles);
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

        return BuildMesh("Quad", vertices, triangles);
    }

    static Mesh BuildMesh(string name, Vector3[] vertices, int[] triangles)
    {
        var colors = new Color[vertices.Length];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = Color.white;
        }

        var mesh = new Mesh { name = name };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        // Sprites/Default-derived shaders multiply by vertex color; without this the missing
        // COLOR stream defaults to (0,0,0,0) and the mesh renders fully transparent.
        mesh.colors = colors;
        mesh.RecalculateBounds();
        // DrawMeshInstanced culls the whole batch using this mesh's local bounds only - it never
        // expands them per-instance across the matrices array. Since instances are scattered
        // across the whole grid, inflate the bounds so the batch never gets wrongly culled.
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
        return mesh;
    }

    static Material BuildMaterial(Color color)
    {
        // URP's 2D Renderer only draws passes it recognizes (e.g. Sprites/Default's);
        // a plain "Universal Render Pipeline/Unlit" material gets silently skipped.
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            Debug.LogError("Grid: could not find shader 'Universal Render Pipeline/Unlit'.");
        }
        var material = new Material(shader);
        material.color = color;
        material.enableInstancing = true;
        material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        return material;
    }
}
