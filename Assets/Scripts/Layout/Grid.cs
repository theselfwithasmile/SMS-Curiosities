using System.Collections.Generic;
using UnityEngine;

// Pure geometry + the grid's own dotted-cell visual. Knows nothing about Container/Token -
// ContainerManager owns that, and queries/updates occupancy here so dots don't draw under it.
public class Grid : MonoBehaviour
{
    public static Grid Instance;

    [SerializeField] int columns = 6;
    [SerializeField] int rows = 6;
    [SerializeField, Range(0f, 0.4f)] float viewportPadding = 0.05f;

    public int Columns => columns;
    public int Rows => rows;
    public float CellSize => cellSize;

    // The board size Token.prefab's baked scale (3,3,1) was tuned to fill one cell of - the
    // inspector's own pre-zone default (see the columns/rows fields above). TokenSpawner divides
    // CellSize by this to rescale tokens for whatever board size a zone actually picked, so a
    // denser board (more, smaller cells) doesn't leave tokens overflowing their container.
    const int ReferenceBoardSize = 6;
    public float ReferenceCellSize => ComputeCellSize(ReferenceBoardSize, ReferenceBoardSize);

    // Rows appended past the puzzle rows for a zone's bench/staging area - claimed dynamically
    // via ReserveBenchRows by whichever zone actually needs one, rather than manually budgeted
    // into `rows` per scene (that's what silently overflowed before: a bench trying to fit inside
    // whatever was left of a fixed row count, rather than the grid growing to fit the bench).
    public int BenchOrigin => rows;
    public int TotalRows => rows + benchRows;
    int benchRows;

    // Puzzle board dimensions are driven by the active zone's own difficulty-scaled board size
    // (SetBoardSize, called once early in BaseZone.Start()) - the inspector columns/rows above are
    // only the pre-zone default a fresh scene load starts from. Square only (columns == rows),
    // same as every layout that builds against boardSize already assumes.
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

    // Grows the viewport-fit math to include a zone's bench rows - called once, early in a
    // zone's Start(), before it positions anything there. Recomputes layout immediately (rather
    // than waiting for the next Update()) so this same frame's CellToWorld calls already reflect
    // the corrected sizing, avoiding a one-frame visual jump.
    public void ReserveBenchRows(int count)
    {
        if (count <= benchRows) return;
        benchRows = count;
        RecomputeLayout();
    }

    // Cell size is derived from the camera's visible world size (not a fixed value) so the
    // whole grid keeps fitting the screen across aspect ratios/orientations, with square cells.
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

    // Unlike WorldToCell (clamped, so every drop resolves to *some* valid edge cell - the right
    // behaviour for ordinary placement), this tells a caller whether a world position is actually
    // within the grid+bench footprint at all. Needed for Token's CanExitBoard drag-off-board check
    // - without it, dragging a token arbitrarily far past the board's edge still clamps to a real,
    // in-bounds cell and never reads as "off the board".
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

    // Deliberately scoped to the puzzle rows only, not TotalRows - this backs geometry/adjacency
    // logic (e.g. ContainerManager.Neighbors, used by flood-fill matching), and bench cells
    // shouldn't ever be treated as puzzle-board neighbors just because they happen to sit
    // adjacent to the board's edge row. WorldToCell intentionally uses TotalRows
    // instead, since a bench does need to be reachable by drops and visible on screen.
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
