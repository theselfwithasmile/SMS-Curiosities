using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridRenderer : MonoBehaviour
{
    public static GridRenderer Instance;
    
    [SerializeField] float dotScale = 0.15f;
    [SerializeField] Color dotColor = Color.white;

    const int MaxInstancesPerBatch = 1023;

    Mesh dotMesh;
    Material dotMaterial;

    void Awake()
    {
        Instance = this;
        dotMesh = BuildDotMesh();
        dotMaterial = BuildMaterial(dotColor);
    }


    // Update is called once per frame
    void Update()
    {
        DrawEmptyCells();
    }

    void DrawEmptyCells()
    {
        int columns = Grid.Instance.Columns;
        int totalRows = Grid.Instance.TotalRows;
        float cellSize = Grid.Instance.CellSize;

        var matrices = new List<Matrix4x4>(columns * totalRows);
        for (int y = 0; y < totalRows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                var cell = new Vector2Int(x, y);
                if (Grid.Instance.IsOccupied(cell)) continue;

                Vector3 center = Grid.Instance.CellToWorld(cell);
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
