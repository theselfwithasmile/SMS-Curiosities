using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

//briefly draws a line along the route
//PathConnectInteraction already verified, so a match reads as "connected, then cleared"
//instead of "two tiles vanished for reasons only the rules know."
public static class ConnectPathEffect
{
    const float WidthFraction = 0.15f; //line thickness as a fraction of a cell

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

    //a UI line is one stretched Image per segment, plus a square joint at each cell so corners don't notch
    static GameObject BuildLine(List<Vector2Int> pathCells, Color color)
    {
        var line = new GameObject("ConnectPath", typeof(RectTransform)).GetComponent<RectTransform>();
        line.SetParent(Grid.Instance.EffectLayer, false);

        float width = Grid.Instance.CellSize * WidthFraction;
        for (int i = 0; i < pathCells.Count; i++)
        {
            Vector2 point = Grid.Instance.CellToLocal(pathCells[i]);
            AddPiece(line, point, new Vector2(width, width), 0f, color);
            if (i == 0) continue;

            Vector2 previous = Grid.Instance.CellToLocal(pathCells[i - 1]);
            Vector2 delta = point - previous;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            AddPiece(line, (point + previous) * 0.5f, new Vector2(delta.magnitude, width), angle, color);
        }

        return line.gameObject;
    }

    static void AddPiece(RectTransform parent, Vector2 localPosition, Vector2 size, float angle, Color color)
    {
        var piece = new GameObject("Segment", typeof(RectTransform)).GetComponent<RectTransform>();
        piece.SetParent(parent, false);
        piece.localPosition = localPosition;
        piece.localRotation = Quaternion.Euler(0f, 0f, angle);
        piece.sizeDelta = size;

        var image = piece.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }
}
