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

    const int MaxInstancesPerBatch = 1023;

    private Camera cam;
    private float cellSize;
    private Vector2 origin;

    private Mesh dotMesh;
    private Material dotMaterial;
    private Matrix4x4[][] dotBatches;

    void Awake()
    {
        Instance = this;
        cam = Camera.main;
        dotMesh = BuildDotMesh();
        dotMaterial = BuildDotMaterial(dotColor);
        RecomputeLayout();
        dotBatches = BuildDotBatches();
    }

    void Update()
    {
        RecomputeLayout();
        dotBatches = BuildDotBatches();

        foreach (Matrix4x4[] batch in dotBatches)
        {

            Graphics.DrawMeshInstanced(dotMesh, 0, dotMaterial, batch);
            //Debug.LogError("hellooooo");
        }
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

    public Vector3 SnapToWorld(Vector3 worldPosition)
    {
        Vector2 local = (Vector2)worldPosition - origin;
        int cellX = Mathf.Clamp(Mathf.FloorToInt(local.x / cellSize), 0, columns - 1);
        int cellY = Mathf.Clamp(Mathf.FloorToInt(local.y / cellSize), 0, rows - 1);
        Vector3 snapped = CellCenter(cellX, cellY);
        snapped.z = worldPosition.z;
        return snapped;
    }

    Vector3 CellCenter(int x, int y)
    {
        return new Vector3(origin.x + (x + 0.5f) * cellSize, origin.y + (y + 0.5f) * cellSize, 0f);
    }

    Matrix4x4[][] BuildDotBatches()
    {
        var matrices = new List<Matrix4x4>(columns * rows);
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                Vector3 center = CellCenter(x, y);
                matrices.Add(Matrix4x4.TRS(center, Quaternion.identity, Vector3.one * (cellSize * dotScale)));
            }
        }

        int batchCount = Mathf.CeilToInt(matrices.Count / (float)MaxInstancesPerBatch);
        var batches = new Matrix4x4[batchCount][];
        for (int i = 0; i < batchCount; i++)
        {
            int start = i * MaxInstancesPerBatch;
            int count = Mathf.Min(MaxInstancesPerBatch, matrices.Count - start);
            batches[i] = matrices.GetRange(start, count).ToArray();
        }
        return batches;
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

        var colors = new Color[vertices.Length];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = Color.white;
        }

        var mesh = new Mesh { name = "Dot" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        return mesh;
    }

    static Material BuildDotMaterial(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            Debug.LogError("Grid: could not find shader 'Sprites/Default'.");
        }
        var material = new Material(shader);
        material.color = color;
        material.enableInstancing = true;
        material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        return material;
    }
}
