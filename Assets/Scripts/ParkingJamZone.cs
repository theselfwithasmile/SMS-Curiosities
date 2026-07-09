using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Parking Jam (arrow variant): a target car must reach a matching exit lane by tapping cars in
// the right order - each car's direction is fixed at generation time, never player-chosen.
// Generation retries random layouts until a BFS solver over car positions confirms the target
// can actually escape, so every generated puzzle is guaranteed solvable.
public class ParkingJamZone : MonoBehaviour
{
    [SerializeField] int blockerCarCount = 3;
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

        int targetRow = grid.Rows / 2;
        const int targetLength = 2;
        Vector2Int exitAnchor = new Vector2Int(bounds.xMax - targetLength, targetRow);

        List<CarSpec> layout = null;
        for (int attempt = 0; attempt < maxGenerationAttempts; attempt++)
        {
            List<CarSpec> candidate = GenerateLayout(bounds, targetRow, targetLength);
            if (candidate != null && IsSolvable(candidate, bounds, exitAnchor))
            {
                layout = candidate;
                break;
            }
        }

        if (layout == null)
        {
            Debug.LogWarning("ParkingJamZone: no solvable layout found within the attempt budget; falling back to just the target car.");
            layout = new List<CarSpec>
            {
                new CarSpec { anchor = new Vector2Int(bounds.xMin, targetRow), direction = Vector2Int.right, offsets = StraightOffsets(targetLength), group = targetGroup }
            };
        }

        Container exitLane = containers.CreateFixedContainer(new RectInt(exitAnchor.x, exitAnchor.y, targetLength, 1), GameState.Instance.GroupColor(targetGroup));
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

        // Cars are tap-driven, not free-draggable - swap out the prefab's baked-in Draggable.
        Destroy(token.GetComponent<Draggable>());
        TapToSlide slider = token.gameObject.AddComponent<TapToSlide>();
        slider.Initialize(car.direction);

        foreach (Vector2Int offset in car.offsets)
        {
            grid.SetOccupied(car.anchor + offset, true);
        }
    }

    List<CarSpec> GenerateLayout(RectInt bounds, int targetRow, int targetLength)
    {
        var cars = new List<CarSpec>();
        var occupied = new HashSet<Vector2Int>();

        var target = new CarSpec
        {
            anchor = new Vector2Int(bounds.xMin, targetRow),
            direction = Vector2Int.right,
            offsets = StraightOffsets(targetLength),
            group = targetGroup,
        };
        if (!TryClaim(target, bounds, occupied)) return null;
        cars.Add(target);

        for (int i = 0; i < blockerCarCount; i++)
        {
            CarSpec? blocker = TryPlaceRandomBlocker(bounds, occupied);
            if (blocker.HasValue) cars.Add(blocker.Value);
        }

        return cars;
    }

    static CarSpec? TryPlaceRandomBlocker(RectInt bounds, HashSet<Vector2Int> occupied)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            bool horizontal = Random.value < 0.5f;
            int length = Random.Range(1, 4);
            Vector2Int direction = horizontal
                ? (Random.value < 0.5f ? Vector2Int.left : Vector2Int.right)
                : (Random.value < 0.5f ? Vector2Int.up : Vector2Int.down);
            List<Vector2Int> offsets = horizontal ? StraightOffsets(length) : StraightOffsetsVertical(length);

            var car = new CarSpec
            {
                anchor = new Vector2Int(Random.Range(bounds.xMin, bounds.xMax), Random.Range(bounds.yMin, bounds.yMax)),
                direction = direction,
                offsets = offsets,
                group = Random.Range(1, 6),
            };

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

    // BFS over car-position tuples: each move slides one car maximally in its fixed direction.
    // Each car's position only ever advances (never backtracks), so the state space is small and
    // finite at realistic car counts - no visited-state cap needed, this always terminates.
    static bool IsSolvable(List<CarSpec> cars, RectInt bounds, Vector2Int exitAnchor)
    {
        var start = new Vector2Int[cars.Count];
        for (int i = 0; i < cars.Count; i++) start[i] = cars[i].anchor;

        var visited = new HashSet<string> { Key(start) };
        var queue = new Queue<Vector2Int[]>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            Vector2Int[] state = queue.Dequeue();
            if (state[0] == exitAnchor) return true;

            for (int i = 0; i < cars.Count; i++)
            {
                Vector2Int moved = SimulateSlide(cars, state, i, bounds);
                if (moved == state[i]) continue;

                var next = (Vector2Int[])state.Clone();
                next[i] = moved;
                if (visited.Add(Key(next))) queue.Enqueue(next);
            }
        }
        return false;
    }

    static Vector2Int SimulateSlide(List<CarSpec> cars, Vector2Int[] state, int index, RectInt bounds)
    {
        Vector2Int direction = cars[index].direction;
        return TapToSlide.SlideUntilBlocked(state[index], _ => direction, candidate => FootprintFreeInState(cars, state, index, candidate, bounds));
    }

    static bool FootprintFreeInState(List<CarSpec> cars, Vector2Int[] state, int movingIndex, Vector2Int anchor, RectInt bounds)
    {
        foreach (Vector2Int offset in cars[movingIndex].offsets)
        {
            Vector2Int cell = anchor + offset;
            if (!bounds.Contains(cell)) return false;

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
