using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

//the board lives in UI space: cells are laid out inside `board`'s rect, in its local units.
//CellToWorld/WorldToCell still speak world positions, so anything that moves a transform keeps working
//regardless of the canvas render mode
public class Grid : MonoBehaviour
{
    public static Grid Instance;

    [SerializeField] int columns = 6;
    [SerializeField] int rows = 6;
    [SerializeField, Range(0f, 0.4f)] float viewportPadding = 0.05f;
    [SerializeField] RectTransform board;

    public int Columns => columns;
    public int Rows => rows;
    public float CellSize => cellSize; //board-local units, i.e. what a sizeDelta on any layer child wants

    public RectTransform Board => board;
    //draw order is sibling order, so layers are created back to front
    public RectTransform ContainerLayer { get; private set; }
    public RectTransform TokenLayer { get; private set; }
    public RectTransform EffectLayer { get; private set; }

    public int BenchOrigin => rows;
    public int TotalRows => rows + benchRows;
    int benchRows;

    public void SetBoardSize(int size)
    {
        if (columns == size && rows == size) return;
        columns = size;
        rows = size;
        RecomputeLayout();
    }

    readonly HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();

    Camera eventCamera; //null for a Screen Space - Overlay canvas, which is what RectTransformUtility expects
    float cellSize;
    Vector2 origin;     //board-local position of cell (0,0)'s bottom-left corner

    void Awake()
    {
        Instance = this;
        if (board == null)
        {
            Debug.LogError("Grid: no board RectTransform assigned; tokens and containers would spawn outside any canvas.", this);
            return;
        }

        Canvas canvas = board.GetComponentInParent<Canvas>().rootCanvas;
        eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        ContainerLayer = CreateLayer("Containers");
        TokenLayer = CreateLayer("Tokens");
        EffectLayer = CreateLayer("Effects");

        RecomputeLayout();
    }

    void Update()
    {
        if (board != null) RecomputeLayout();
    }

    public void ReserveBenchRows(int count)
    {
        if (count <= benchRows) return;
        benchRows = count;
        RecomputeLayout();
    }

    //full-stretch, unscaled children of the board, so their local units are the board's local units
    RectTransform CreateLayer(string layerName)
    {
        var layer = new GameObject(layerName, typeof(RectTransform)).GetComponent<RectTransform>();
        layer.SetParent(board, false);
        layer.anchorMin = Vector2.zero;
        layer.anchorMax = Vector2.one;
        layer.offsetMin = Vector2.zero;
        layer.offsetMax = Vector2.zero;
        //if the board sits under/with a layout group, keep it from re-anchoring and stacking the layers
        //layer.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        return layer;
    }

    //cell size is derived from the board rect's size
    void RecomputeLayout()
    {
        Rect rect = board.rect;
        float usableWidth = rect.width * (1f - viewportPadding * 2f);
        float usableHeight = rect.height * (1f - viewportPadding * 2f);
        cellSize = Mathf.Min(usableWidth / columns, usableHeight / TotalRows);

        Vector2 gridSize = new Vector2(cellSize * columns, cellSize * TotalRows);
        origin = rect.center - gridSize * 0.5f;
    }

    public Vector2Int WorldToCell(Vector3 worldPosition)
    {
        Vector2 local = (Vector2)board.InverseTransformPoint(worldPosition) - origin;
        int cellX = Mathf.Clamp(Mathf.FloorToInt(local.x / cellSize), 0, columns - 1);
        int cellY = Mathf.Clamp(Mathf.FloorToInt(local.y / cellSize), 0, TotalRows - 1);
        return new Vector2Int(cellX, cellY);
    }

    //is a world position is actually within the grid+bench footprint at all
    public bool IsWorldPositionOnBoard(Vector3 worldPosition)
    {
        Vector2 local = (Vector2)board.InverseTransformPoint(worldPosition) - origin;
        int cellX = Mathf.FloorToInt(local.x / cellSize);
        int cellY = Mathf.FloorToInt(local.y / cellSize);
        return cellX >= 0 && cellX < columns && cellY >= 0 && cellY < TotalRows;
    }

    public Vector3 CellToWorld(Vector2Int cell)
    {
        return board.TransformPoint(CellToLocal(cell));
    }

    public Vector2 CellToLocal(Vector2Int cell)
    {
        return new Vector2(origin.x + (cell.x + 0.5f) * cellSize, origin.y + (cell.y + 0.5f) * cellSize);
    }

    //for offsets measured in cells/CellSize (e.g. a buried token's peek) that get added to a world position
    public Vector3 BoardVectorToWorld(Vector2 boardVector)
    {
        return board.TransformVector(boardVector);
    }

    //screen pointer -> world point on the board's plane
    public Vector3 ScreenToWorld(Vector2 screenPosition)
    {
        RectTransformUtility.ScreenPointToWorldPointInRectangle(board, screenPosition, eventCamera, out Vector3 world);
        return world;
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
}
