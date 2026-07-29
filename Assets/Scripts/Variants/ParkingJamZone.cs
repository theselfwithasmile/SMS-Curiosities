using System.Collections.Generic;
using UnityEngine;

// Parking Jam (arrow-maze variant): every car has its own fixed slide direction and its own exit
// - the grid boundary in that direction - rather than one target car chasing a shared exit lane.
// A drag either clears a car all the way to the boundary or reverts it completely (Token's
// FixedDirection path handles this at drag-end); no intermediate resting position ever persists,
// so the whole layout is monotone: a car that currently has a clear run to the edge will always
// still have one later (nothing ever repositions to block it), which is exactly why solvability
// only needs a simple greedy check instead of a BFS over joint car positions.
public class ParkingJamZone : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] float fillRatio = 0.55f;
    [SerializeField] int maxGenerationAttempts = 60;

    struct CarSpec
    {
        public Vector2Int anchor;
        public Vector2Int direction;
        public List<Vector2Int> offsets;
        public int group;
    }

    void Start()
    {
        Grid grid = Grid.Instance;
        var bounds = new RectInt(0, 0, grid.Columns, grid.Rows);

        List<CarSpec> layout = null;
        for (int attempt = 0; attempt < maxGenerationAttempts; attempt++)
        {
            List<CarSpec> candidate = GenerateLayout(bounds);
            if (candidate != null && IsSolvable(candidate, bounds))
            {
                layout = candidate;
                break;
            }
        }

        if (layout == null)
        {
            Debug.LogWarning("ParkingJamZone: no solvable layout found within the attempt budget; falling back to an empty board.");
            layout = new List<CarSpec>();
        }

        foreach (CarSpec car in layout)
        {
            SpawnCar(car);
        }
    }

    void SpawnCar(CarSpec car)
    {
        Grid grid = Grid.Instance;
        Vector3 worldPosition = grid.CellToWorld(car.anchor);
        Token token = TokenSpawner.Instance.SpawnMultiCellToken(car.group, worldPosition, car.offsets);
        token.FixedDirection = car.direction;

        foreach (Vector2Int offset in car.offsets)
        {
            grid.SetOccupied(car.anchor + offset, true);
        }
    }

    // Fills free cells (in random order) with random cars until roughly fillRatio of the board is
    // occupied - walking actual remaining free cells rather than blind-guessing coordinates, so
    // density scales reliably instead of degrading as the board fills up.
    List<CarSpec> GenerateLayout(RectInt bounds)
    {
        var cars = new List<CarSpec>();
        var occupied = new HashSet<Vector2Int>();
        int targetFilled = Mathf.RoundToInt(fillRatio * bounds.width * bounds.height);

        var freeCells = new List<Vector2Int>();
        for (int y = bounds.yMin; y < bounds.yMax; y++)
        {
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                freeCells.Add(new Vector2Int(x, y));
            }
        }
        ContainerManager.Shuffle(freeCells);

        foreach (Vector2Int cell in freeCells)
        {
            if (occupied.Count >= targetFilled) break;
            if (occupied.Contains(cell)) continue;

            CarSpec? car = TryBuildCarAt(cell, bounds, occupied);
            if (car.HasValue) cars.Add(car.Value);
        }

        return cars.Count > 0 ? cars : null;
    }

    // Tries a shuffled set of (orientation, length, direction) combos anchored at this specific
    // free cell, keeping the first that fits - maximizes the chance of successfully packing a
    // car at any given spot rather than failing the whole cell on one fixed shape.
    static CarSpec? TryBuildCarAt(Vector2Int cell, RectInt bounds, HashSet<Vector2Int> occupied)
    {
        var combos = new List<(bool horizontal, int length, Vector2Int direction)>();
        for (int length = 1; length <= 3; length++)
        {
            combos.Add((true, length, Vector2Int.right));
            combos.Add((true, length, Vector2Int.left));
            combos.Add((false, length, Vector2Int.up));
            combos.Add((false, length, Vector2Int.down));
        }
        ContainerManager.Shuffle(combos);

        int groupRange = Mathf.Max(1, GameState.Instance.GroupCount);
        foreach ((bool horizontal, int length, Vector2Int direction) in combos)
        {
            List<Vector2Int> offsets = horizontal ? StraightOffsets(length) : StraightOffsetsVertical(length);
            var car = new CarSpec { anchor = cell, direction = direction, offsets = offsets, group = Random.Range(0, groupRange) };
            if (TryClaim(car, bounds, occupied)) return car;
        }
        return null;
    }

    static bool TryClaim(CarSpec car, RectInt bounds, HashSet<Vector2Int> occupied)
    {
        var cells = new List<Vector2Int>();
        foreach (Vector2Int offset in car.offsets)
        {
            Vector2Int cell = car.anchor + offset;
            if (!bounds.Contains(cell) || occupied.Contains(cell)) return false;
            cells.Add(cell);
        }
        foreach (Vector2Int cell in cells) occupied.Add(cell);
        return true;
    }

    static List<Vector2Int> StraightOffsets(int length)
    {
        var offsets = new List<Vector2Int>(length);
        for (int i = 0; i < length; i++) offsets.Add(new Vector2Int(i, 0));
        return offsets;
    }

    static List<Vector2Int> StraightOffsetsVertical(int length)
    {
        var offsets = new List<Vector2Int>(length);
        for (int i = 0; i < length; i++) offsets.Add(new Vector2Int(0, i));
        return offsets;
    }

    // Monotone solvability check: repeatedly find any remaining car with a currently clear run to
    // the boundary, remove it, repeat. Correct because nothing ever repositions to block a car -
    // a car that's clear now stays clear (or becomes clear) regardless of removal order, so a
    // single greedy pass (no backtracking, no joint-state search) is sufficient.
    static bool IsSolvable(List<CarSpec> cars, RectInt bounds)
    {
        var remaining = new List<CarSpec>(cars);
        bool progress = true;
        while (progress && remaining.Count > 0)
        {
            progress = false;
            for (int i = remaining.Count - 1; i >= 0; i--)
            {
                if (ReachesEdge(remaining, i, bounds))
                {
                    remaining.RemoveAt(i);
                    progress = true;
                }
            }
        }
        return remaining.Count == 0;
    }

    // A car "reaches the edge" if nothing but the boundary itself stops its maximal slide -
    // compares the maximal slide against the other remaining cars with the maximal slide against
    // the boundary alone; identical results mean no other car was actually in the way.
    static bool ReachesEdge(List<CarSpec> cars, int index, RectInt bounds)
    {
        CarSpec car = cars[index];
        Vector2Int withObstacles = Grid.SlideUntilBlocked(car.anchor, _ => car.direction, c => FootprintFree(cars, index, c, bounds));
        Vector2Int boundsOnly = Grid.SlideUntilBlocked(car.anchor, _ => car.direction, c => FootprintFitsBounds(car, c, bounds));
        return withObstacles == boundsOnly;
    }

    static bool FootprintFree(List<CarSpec> cars, int movingIndex, Vector2Int anchor, RectInt bounds)
    {
        CarSpec car = cars[movingIndex];
        foreach (Vector2Int offset in car.offsets)
        {
            Vector2Int cell = anchor + offset;
            if (!bounds.Contains(cell)) return false;

            for (int j = 0; j < cars.Count; j++)
            {
                if (j == movingIndex) continue;
                foreach (Vector2Int otherOffset in cars[j].offsets)
                {
                    if (cars[j].anchor + otherOffset == cell) return false;
                }
            }
        }
        return true;
    }

    static bool FootprintFitsBounds(CarSpec car, Vector2Int anchor, RectInt bounds)
    {
        foreach (Vector2Int offset in car.offsets)
        {
            if (!bounds.Contains(anchor + offset)) return false;
        }
        return true;
    }
}
