using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//briefly draws a line along the route
//PathConnectInteraction already verified, so a match reads as "connected, then cleared"
//instead of "two tiles vanished for reasons only the rules know."
public static class ConnectPathEffect
{
    public static void ShowThenDestroy(List<Vector2Int> pathCells, Color color, Token a, Token b, float holdDuration = 0.18f)
    {
        TweenRunner.Instance.StartCoroutine(Routine(pathCells, color, a, b, holdDuration));
    }

    static IEnumerator Routine(List<Vector2Int> pathCells, Color color, Token a, Token b, float holdDuration)
    {
        GameObject line = BuildLine(pathCells, color);
        yield return new WaitForSeconds(holdDuration);
        Object.Destroy(line);

        TweenRunner.Instance.ShrinkAndDestroy(a);
        TweenRunner.Instance.ShrinkAndDestroy(b);
    }

    static GameObject BuildLine(List<Vector2Int> pathCells, Color color)
    {
        var line = new GameObject("ConnectPath");
        var renderer = line.AddComponent<LineRenderer>();
        renderer.useWorldSpace = true;
        renderer.positionCount = pathCells.Count;
        for (int i = 0; i < pathCells.Count; i++)
        {
            renderer.SetPosition(i, Grid.Instance.CellToWorld(pathCells[i]));
        }

        float width = Grid.Instance.CellSize * 0.15f;
        renderer.startWidth = width;
        renderer.endWidth = width;
        renderer.numCapVertices = 4;
        renderer.material = BuildMaterial(color);
        renderer.sortingOrder = 100;

        return line;
    }
    
    public static Material BuildMaterial(Color color)
    {
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
