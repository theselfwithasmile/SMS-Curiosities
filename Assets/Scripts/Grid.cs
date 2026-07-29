using System;
using System.Collections.Generic;
using UnityEngine;

// Pure geometry + the grid's own dotted-cell visual. Knows nothing about Container/Token -
// ContainerManager owns that, and queries/updates occupancy here so dots don't draw under it.
public class Grid : MonoBehaviour
{
    public static Grid Instance;

    [SerializeField] int columns = 6;
    [SerializeField] int rows = 6;
    [SerializeField] float dotScale = 0.15f;
    [SerializeField] Color dotColor = Color.white;
    [SerializeField, Range(0f, 0.4f)] float viewportPadding = 0.05f;

    public int Columns => columns;
    public int Rows => rows;
    public float CellSize => cellSize;

    const int MaxInstancesPerBatch = 1023;

    readonly HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();

    Camera cam;
    float cellSize;
    Vector2 origin;

    Mesh dotMesh;
    Material dotMaterial;

    void Awake()
    {
        Instance = this;
        cam = Camera.main;
        dotMesh = BuildDotMesh();
        dotMaterial = BuildMaterial(dotColor);
        RecomputeLayout();
    }

    void Update()
    {
        RecomputeLayout();
        DrawEmptyCells();
    }

    // Cell size is derived from the camera's visible world size (not a fixed value) so the
    // whole grid keeps fitting the screen across aspect ratios/orientations, with square cells.
    void RecomputeLayout()
    {
        float viewportHeight = cam.orthographicSize * 2f;
        float viewportWidth = viewportHeight * cam.aspect;
        float usableWidth = viewportWidth * (1f - viewportPadding * 2f);
        float usableHeight = viewportHeight * (1f - viewportPadding * 2f);

        cellSize = Mathf.Min(usableWidth / columns, usableHeight / rows);

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

    public bool IsInBounds(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < columns && cell.y >= 0 && cell.y < rows;
    }

    public bool IsOccupied(Vector2Int cell)
    {
        return occupiedCells.Contains(cell);
    }

    public void SetOccupied(Vector2Int cell, bool occupied)
    {
        if (occupied) occupiedCells.Add(cell);
        else occupiedCells.Remove(cell);
    }

    // Steps in whatever direction directionAt reports at each cell, until anchorFree says the
    // next step isn't. Shared by real-time movement (a car's own path) and the Parking Jam
    // solver's simulation, which walks the same stepping against a hypothetical board state.
    public static Vector2Int SlideUntilBlocked(Vector2Int start, Func<Vector2Int, Vector2Int> directionAt, Func<Vector2Int, bool> anchorFree)
    {
        Vector2Int anchor = start;
        Vector2Int next = anchor + directionAt(anchor);
        while (anchorFree(next))
        {
            anchor = next;
            next = anchor + directionAt(anchor);
        }
        return anchor;
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
                if (occupiedCells.Contains(cell)) continue;

                Vector3 center = CellCenter(x, y);
                matrices.Add(Matrix4x4.TRS(center, Quaternion.identity, Vector3.one * (cellSize * dotScale)));
            }
        }
        DrawBatched(dotMesh, dotMaterial, matrices);
    }

    public static void DrawBatched(Mesh mesh, Material material, List<Matrix4x4> matrices)
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

    public static Mesh BuildMesh(string name, Vector3[] vertices, int[] triangles)
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

    public static Material BuildMaterial(Color color)
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
