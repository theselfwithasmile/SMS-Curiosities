using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Parking Jam (arrow variant): a target car must reach a matching exit lane by tapping cars in
// the right order - each car's direction is fixed at generation time, never player-chosen.
// Generation retries random layouts until a BFS solver over car positions confirms the target
// can actually escape, so every generated puzzle is guaranteed solvable.
public class ParkingJamZone : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] float fillRatio = 0.55f;
    [SerializeField] int maxGenerationAttempts = 60;
    [SerializeField] int targetGroup = 0;

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
        ContainerManager containers = ContainerManager.Instance;
        var bounds = new RectInt(0, 0, grid.Columns, grid.Rows);

        List<CarSpec> layout = null;
        
        Vector2Int exitAnchor = default; //exit anchor is the anchor of the target car, which is also the exit lane's anchor

        for (int attempt = 0; attempt < maxGenerationAttempts; attempt++)
        {
            List<CarSpec> candidate = GenerateLayout(bounds, out Vector2Int candidateExit);
            if (candidate != null && IsSolvable(candidate, bounds, candidateExit))
            {
                layout = candidate;
                exitAnchor = candidateExit;
                break;
            }
        }

        if (layout == null)
        {
            Debug.LogWarning("ParkingJamZone: no solvable layout found within the attempt budget; falling back to just the target car.");
            CarSpec target = CreateRandomTarget(bounds, out exitAnchor);
            layout = new List<CarSpec> { target };
        }

        RectInt exitBounds = FootprintBounds(exitAnchor, layout[0].offsets);
        Container exitLane = containers.CreateFixedContainer(exitBounds, GameState.Instance.GroupColor(targetGroup));
        exitLane.EntryConstraints.Add(new TargetGroupConstraint(targetGroup));
        exitLane.CompletionPredicate = new FullPredicate();
        exitLane.Resolution = new ClearResolution();

        foreach (CarSpec car in layout)
        {
            SpawnCar(car);
        }
    }

    void SpawnCar(CarSpec car)
    {
        Grid grid = Grid.Instance;
        ContainerManager containers = ContainerManager.Instance;

        Vector3 worldPosition = grid.CellToWorld(car.anchor);
        Token token = containers.SpawnMultiCellToken(car.group, worldPosition, car.offsets);
        token.GetComponent<SpriteRenderer>().color = GameState.Instance.GroupColor(car.group);

        // The car keeps its ordinary Draggable - a fresh reachable-path container gets computed
        // per drag gesture instead of a bespoke tap-and-slide component.
        token.GetComponent<Draggable>().SetEphemeralContainerProvider(new CarPathProvider(car.direction));

        foreach (Vector2Int offset in car.offsets)
        {
            grid.SetOccupied(car.anchor + offset, true);
        }
    }

    // Places a randomly shaped/positioned target first, then fills free cells (in random order)
    // with random cars until roughly fillRatio of the board is occupied - walking actual
    // remaining free cells rather than blind-guessing coordinates, so density scales reliably
    // instead of degrading as the board fills up.
    List<CarSpec> GenerateLayout(RectInt bounds, out Vector2Int exitAnchor)
    {
        var cars = new List<CarSpec>();
        var occupied = new HashSet<Vector2Int>();
        int targetFilled = Mathf.RoundToInt(fillRatio * bounds.width * bounds.height);

        //find target
        CarSpec target = CreateRandomTarget(bounds, out exitAnchor);
        if (!TryClaim(target, bounds, occupied))
        {
            exitAnchor = default;
            return null;
        }
        cars.Add(target);
        
        
        //find free cells and shuffle them
        var freeCells = new List<Vector2Int>();
        for (int y = bounds.yMin; y < bounds.yMax; y++)
        {
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                var cell = new Vector2Int(x, y);
                if (!occupied.Contains(cell)) freeCells.Add(cell);
            }
        }
        Shuffle(freeCells);

        
        //generates blockers on free cells
        foreach (Vector2Int cell in freeCells)
        {
            if (occupied.Count >= targetFilled) break;
            if (occupied.Contains(cell)) continue;

            CarSpec? blocker = TryBuildBlockerAt(cell, bounds, occupied);
            if (blocker.HasValue) cars.Add(blocker.Value);
        }

        return cars;
    }

    CarSpec CreateRandomTarget(RectInt bounds, out Vector2Int exitAnchor)
    {
        int length = Random.Range(2, 4);
        bool horizontal = Random.value < 0.5f;

        if (horizontal)
        {
            int row = Random.Range(bounds.yMin, bounds.yMax);
            bool exitsRight = Random.value < 0.5f;
            Vector2Int direction = exitsRight ? Vector2Int.right : Vector2Int.left;
            Vector2Int start = exitsRight ? new Vector2Int(bounds.xMin, row) : new Vector2Int(bounds.xMax - length, row);
            exitAnchor = exitsRight ? new Vector2Int(bounds.xMax - length, row) : new Vector2Int(bounds.xMin, row);
            return new CarSpec { anchor = start, direction = direction, offsets = StraightOffsets(length), group = targetGroup };
        }

        int column = Random.Range(bounds.xMin, bounds.xMax);
        bool exitsUp = Random.value < 0.5f;
        Vector2Int verticalDirection = exitsUp ? Vector2Int.up : Vector2Int.down;
        Vector2Int verticalStart = exitsUp ? new Vector2Int(column, bounds.yMin) : new Vector2Int(column, bounds.yMax - length);
        exitAnchor = exitsUp ? new Vector2Int(column, bounds.yMax - length) : new Vector2Int(column, bounds.yMin);
        return new CarSpec { anchor = verticalStart, direction = verticalDirection, offsets = StraightOffsetsVertical(length), group = targetGroup };
    }

    // Tries a shuffled set of (orientation, length, direction) combos anchored at this specific
    // free cell, keeping the first that fits - maximizes the chance of successfully packing a
    // car at any given spot rather than failing the whole cell on one fixed shape.
    static CarSpec? TryBuildBlockerAt(Vector2Int cell, RectInt bounds, HashSet<Vector2Int> occupied)
    {
        var combos = new List<(bool horizontal, int length, Vector2Int direction)>();
        for (int length = 1; length <= 3; length++)
        {
            combos.Add((true, length, Vector2Int.right));
            combos.Add((true, length, Vector2Int.left));
            combos.Add((false, length, Vector2Int.up));
            combos.Add((false, length, Vector2Int.down));
        }
        Shuffle(combos);

        int groupRange = Mathf.Max(2, GameState.Instance.GroupCount);
        foreach ((bool horizontal, int length, Vector2Int direction) in combos)
        {
            List<Vector2Int> offsets = horizontal ? StraightOffsets(length) : StraightOffsetsVertical(length);
            var car = new CarSpec { anchor = cell, direction = direction, offsets = offsets, group = Random.Range(1, groupRange) };
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

    static RectInt FootprintBounds(Vector2Int anchor, List<Vector2Int> offsets)
    {
        Vector2Int min = anchor + offsets[0];
        Vector2Int max = min;
        foreach (Vector2Int offset in offsets)
        {
            Vector2Int cell = anchor + offset;
            min = Vector2Int.Min(min, cell);
            max = Vector2Int.Max(max, cell);
        }
        return new RectInt(min.x, min.y, max.x - min.x + 1, max.y - min.y + 1);
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

    static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // The joint state is every relevant car's position, so cost is combinatorial in car count -
    // fine for a handful of cars, but a dense fillRatio board can easily have 15+ cars, which
    // blows up even though each car's own position range is small. Most of those cars are
    // parked somewhere the target's path never touches, so they're pruned to static obstacles
    // before the BFS ever runs, via ComputeRelevantCars below.
    static bool IsSolvable(List<CarSpec> allCars, RectInt bounds, Vector2Int exitAnchor)
    {
        List<int> relevantIndices = ComputeRelevantCars(allCars, bounds);
        var cars = new List<CarSpec>(relevantIndices.Count);
        foreach (int index in relevantIndices) cars.Add(allCars[index]);

        var staticObstacles = new HashSet<Vector2Int>();
        for (int i = 0; i < allCars.Count; i++)
        {
            if (relevantIndices.Contains(i)) continue;
            foreach (Vector2Int offset in allCars[i].offsets)
            {
                staticObstacles.Add(allCars[i].anchor + offset);
            }
        }

        var start = new Vector2Int[cars.Count];
        for (int i = 0; i < cars.Count; i++) start[i] = cars[i].anchor;

        var visited = new HashSet<string> { Key(start) };
        var queue = new Queue<Vector2Int[]>();
        queue.Enqueue(start);

        // Defensive cap - relevance pruning should already keep this small, but a hard ceiling
        // means a pathological layout aborts this attempt and retries a fresh one instead of
        // ever stalling the editor again.
        const int maxStatesExplored = 200000;
        int statesExplored = 0;

        while (queue.Count > 0)
        {
            if (++statesExplored > maxStatesExplored) return false;

            Vector2Int[] state = queue.Dequeue();
            if (state[0] == exitAnchor) return true; //if car 1 reaches the exit, the puzzle is solvable

            for (int i = 0; i < cars.Count; i++)
            {
                Vector2Int moved = SimulateSlide(cars, state, i, bounds, staticObstacles);
                if (moved == state[i]) continue; //ignore if blocked (no movement)

                // clone the state (to ensure original state remains untouched for other cars' moves)
                // and update the moved car's position
                var next = (Vector2Int[])state.Clone();
                next[i] = moved;

                //if the next state hasn't been visited yet, add it to the queue for further exploration
                if (visited.Add(Key(next))) queue.Enqueue(next);
            }
        }
        return false;
    }

    // A car can only ever occupy the straight corridor from its starting position to the board
    // edge in its own fixed direction - that's a static property of its start+direction, not
    // something that changes as other cars move. So closing over "does car X's current position
    // sit inside any relevant car's full corridor" once, up front, correctly captures every car
    // that could ever matter, without needing to re-derive relevance as the search progresses.
    static List<int> ComputeRelevantCars(List<CarSpec> cars, RectInt bounds)
    {
        var relevant = new HashSet<int> { 0 }; // target is always index 0
        var frontier = new Queue<int>();
        frontier.Enqueue(0);

        while (frontier.Count > 0)
        {
            int index = frontier.Dequeue();
            HashSet<Vector2Int> lane = LaneCells(cars[index], bounds);

            for (int j = 0; j < cars.Count; j++)
            {
                if (relevant.Contains(j)) continue;

                foreach (Vector2Int offset in cars[j].offsets)
                {
                    if (lane.Contains(cars[j].anchor + offset))
                    {
                        relevant.Add(j);
                        frontier.Enqueue(j);
                        break;
                    }
                }
            }
        }

        return new List<int>(relevant);
    }

    static HashSet<Vector2Int> LaneCells(CarSpec car, RectInt bounds)
    {
        var cells = new HashSet<Vector2Int>();
        foreach (Vector2Int offset in car.offsets)
        {
            Vector2Int cell = car.anchor + offset;
            while (bounds.Contains(cell))
            {
                cells.Add(cell);
                cell += car.direction;
            }
        }
        return cells;
    }

    static Vector2Int SimulateSlide(List<CarSpec> cars, Vector2Int[] state, int index, RectInt bounds, HashSet<Vector2Int> staticObstacles)
    {
        Vector2Int direction = cars[index].direction;
        return Grid.SlideUntilBlocked(state[index], _ => direction, candidate => FootprintFreeInState(cars, state, index, candidate, bounds, staticObstacles));
    }

    //if the current car is placed here, would it overlap anything
    static bool FootprintFreeInState(List<CarSpec> cars, Vector2Int[] state, int movingIndex, Vector2Int anchor, RectInt bounds, HashSet<Vector2Int> staticObstacles)
    {
        foreach (Vector2Int offset in cars[movingIndex].offsets)
        {
            Vector2Int cell = anchor + offset;
            if (!bounds.Contains(cell)) return false;
            if (staticObstacles.Contains(cell)) return false;

            for (int j = 0; j < cars.Count; j++)
            {
                if (j == movingIndex) continue;
                foreach (Vector2Int otherOffset in cars[j].offsets)
                {
                    if (state[j] + otherOffset == cell) return false;
                }
            }
        }
        return true;
    }

    static string Key(Vector2Int[] state)
    {
        var builder = new StringBuilder();
        foreach (Vector2Int position in state)
        {
            builder.Append(position.x).Append(',').Append(position.y).Append('|');
        }
        return builder.ToString();
    }
}
