using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridRenderer : MonoBehaviour
{
    const int MaxInstancesPerBatch = 1023;

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
