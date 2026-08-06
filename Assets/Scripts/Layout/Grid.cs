using System.Collections.Generic;
using UnityEngine;

public class Grid : MonoBehaviour
{
    public static Grid Instance;

    [SerializeField] int columns = 6;
    [SerializeField] int rows = 6;
    [SerializeField, Range(0f, 0.4f)] float viewportPadding = 0.05f;

    public int Columns => columns;
    public int Rows => rows;
    public float CellSize => cellSize;
    
    const int ReferenceBoardSize = 6;
    public float ReferenceCellSize => ComputeCellSize(ReferenceBoardSize, ReferenceBoardSize);
    
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

    Camera cam;
    float cellSize;
    Vector2 origin;



    void Awake()
    {
        Instance = this;
        cam = Camera.main;

        RecomputeLayout();
    }

    void Update()
    {
        RecomputeLayout();
    }
    
    public void ReserveBenchRows(int count)
    {
        if (count <= benchRows) return;
        benchRows = count;
        RecomputeLayout();
    }

    //cell size is derived from the camera's visible world size
    void RecomputeLayout()
    {
        cellSize = ComputeCellSize(columns, TotalRows);

        Vector2 gridSize = new Vector2(cellSize * columns, cellSize * TotalRows);
        Vector2 center = (Vector2)cam.transform.position + (Vector2)transform.position;
        origin = center - gridSize * 0.5f;
    }

    float ComputeCellSize(int cols, int totalRows)
    {
        float viewportHeight = cam.orthographicSize * 2f;
        float viewportWidth = viewportHeight * cam.aspect;
        float usableWidth = viewportWidth * (1f - viewportPadding * 2f);
        float usableHeight = viewportHeight * (1f - viewportPadding * 2f);

        return Mathf.Min(usableWidth / cols, usableHeight / totalRows);
    }

    public Vector2Int WorldToCell(Vector3 worldPosition)
    {
        Vector2 local = (Vector2)worldPosition - origin;
        int cellX = Mathf.Clamp(Mathf.FloorToInt(local.x / cellSize), 0, columns - 1);
        int cellY = Mathf.Clamp(Mathf.FloorToInt(local.y / cellSize), 0, TotalRows - 1);
        return new Vector2Int(cellX, cellY);
    }

    //is a world position is actually within the grid+bench footprint at all
    public bool IsWorldPositionOnBoard(Vector3 worldPosition)
    {
        Vector2 local = (Vector2)worldPosition - origin;
        int cellX = Mathf.FloorToInt(local.x / cellSize);
        int cellY = Mathf.FloorToInt(local.y / cellSize);
        return cellX >= 0 && cellX < columns && cellY >= 0 && cellY < TotalRows;
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

    Vector3 CellCenter(int x, int y)
    {
        return new Vector3(origin.x + (x + 0.5f) * cellSize, origin.y + (y + 0.5f) * cellSize, 0f);
    }
}
