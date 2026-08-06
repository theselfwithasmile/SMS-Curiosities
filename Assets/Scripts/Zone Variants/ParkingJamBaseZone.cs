using System.Collections.Generic;
using UnityEngine;
using Variants;

//very piece is a bent, self-avoiding body of cells with a fixed escape lane
//running from its head to the grid boundary. A drag either clears a piece all
//the way out or reverts it completely
public class ParkingJamBaseZone : BaseZone
{
    [SerializeField, Range(0f, 1f)] float fillRatio = 0.55f;
    [SerializeField] int minLength = 2;
    [SerializeField] int maxLength = 8;
    protected override int maxGenerationAttempts => 60;

    static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    struct CarSpec
    {
        public List<Vector2Int> body; // absolute cells, tail to head
        public List<Vector2Int> escapeLane; // absolute cells, beyond the head, up to the boundary
        public Vector2Int direction;
        public int group;
    }

    List<CarSpec> pendingLayout;
    int remainingCars;

    //Nudging the fill ratio itself is the knob for difficulty
    //Clamped well short of 1 so generation (which retries up to maxGenerationAttempts on an unsolvable pack)
    //doesn't start starving for free cells to grow escape lanes through.
    float EffectiveFillRatio => Mathf.Clamp(fillRatio + Difficulty * 0.03f, 0f, 0.85f);

    protected override List<Container> GenerateContainers() => new List<Container>();

    protected override bool TryGenerateLayout()
    {
        var bounds = new RectInt(0, 0, boardSize, boardSize);
        pendingLayout = GenerateLayout(bounds);
        return pendingLayout != null;
    }

    protected override bool IsSolvable() => IsSolvable(pendingLayout);

    // Board-cleared (BaseZone's default) always reads false here since Parking Jam never uses
    // Container at all (see the class comment above) - win is "every car has escaped" instead,
    // tracked via the count set in CommitLayout and decremented by each token's OnEscaped.
    protected override bool CheckWinCondition() => remainingCars <= 0;

    protected override void CommitLayout()
    {
        remainingCars = pendingLayout.Count;

        foreach (CarSpec car in pendingLayout)
        {
            //anchored at the head
            Vector2Int anchor = car.body[car.body.Count - 1];
            var offsets = new List<Vector2Int>(car.body.Count);
            for (int i = car.body.Count - 1; i >= 0; i--) offsets.Add(car.body[i] - anchor);

            Token token = TokenSpawner.Instance.SpawnMultiCellToken(car.group, grid.CellToWorld(anchor), offsets);
            token.IsEscapePiece = true;
            token.EscapeLane = car.escapeLane;
            token.EscapeDirection = car.direction;
            token.AnchorCell = anchor;
            token.OnEscaped += () => remainingCars--;
            TokenSpawner.Instance.ApplyDirectionAnimation(token, car.direction);

            foreach (Vector2Int cell in car.body)
            {
                grid.SetOccupied(cell, true);
            }
        }
    }

    //fills free cells (in random order) with bent pieces until roughly fillRatio of the board is occupied
    List<CarSpec> GenerateLayout(RectInt bounds)
    {
        var cars = new List<CarSpec>();
        var occupied = new HashSet<Vector2Int>();

        //builds shuffled list of coords
        var freeCells = new List<Vector2Int>();
        for (int y = bounds.yMin; y < bounds.yMax; y++)
        {
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                freeCells.Add(new Vector2Int(x, y));
            }
        }
        ContainerManager.Shuffle(freeCells);

        //fills grid until targetFilled is reached
        //walking actual remaining free cells rather than blind-guessing coordinates, so
        //density scales reliably instead of degrading as the board fills up
        int targetFilled = Mathf.RoundToInt(EffectiveFillRatio * bounds.width * bounds.height); //percentage of grid to be filled
        foreach (Vector2Int cell in freeCells)
        {
            if (occupied.Count >= targetFilled) break;
            if (occupied.Contains(cell)) continue;

            cars.Add(BuildCarAt(cell, bounds, occupied));
        }

        return cars.Count > 0 ? cars : null;
    }

    //grows a bent, self-avoiding body from `start` - longer target lengths bend more often, so
    // short pieces read as plain straight/L blockers while long ones wind like a real maze
    // corridor.  (a shorter piece,
    // rather than failing the whole cell).
    CarSpec BuildCarAt(Vector2Int start, RectInt bounds, HashSet<Vector2Int> occupied)
    {
        int targetLength = Random.Range(minLength, maxLength + 1);
        float turnChance = Mathf.Clamp01((targetLength - minLength) / (float)Mathf.Max(1, maxLength - minLength));  //normalized chance

        var body = new List<Vector2Int> { start };
        Vector2Int direction = Directions[Random.Range(0, Directions.Length)];

        //extends body
        for (int i = 1; i < targetLength; i++)
        {
            Vector2Int head = body[body.Count - 1];
            Vector2Int? next = StepBody(head, direction, turnChance, bounds, occupied, body);
            if (!next.HasValue) break;  //falls back to whatever length it manages if boxed in early

            direction = next.Value - head;
            body.Add(next.Value);
        }
        foreach (Vector2Int cell in body) occupied.Add(cell);

        List<Vector2Int> lane = BuildEscapeLane(body, direction, bounds);
        int groupRange = Mathf.Max(1, GameState.Instance.GroupCount);
        return new CarSpec { body = body, escapeLane = lane, direction = direction, group = Random.Range(0, groupRange) };
    }
    
    static Vector2Int? StepBody(Vector2Int from, Vector2Int direction, float turnChance, RectInt bounds, HashSet<Vector2Int> occupied, List<Vector2Int> body)
    {
        //builds direction priority list instead of naively shuffling
        //which would otherwise make self collision much more probable, making longer tokens much less likely
        var candidates = new List<Vector2Int>();
        if (Random.value < turnChance)
        {
            candidates.Add(Perpendicular(direction, true));
            candidates.Add(Perpendicular(direction, false));
            candidates.Add(direction);
        }
        else
        {
            candidates.Add(direction);
            candidates.Add(Perpendicular(direction, true));
            candidates.Add(Perpendicular(direction, false));
        }

        foreach (Vector2Int candidateDir in candidates)
        {
            Vector2Int candidate = from + candidateDir;

            //registers the first valid direction
            if (bounds.Contains(candidate) && !occupied.Contains(candidate) && !body.Contains(candidate))
            {
                return candidate;
            }

            //if invalid, try again with the direction of lower priority, aka the ones later in the candidate list
        }
        return null;
    }

    //continues straight past the body's head in whatever direction the body's own last segment
    //was already heading, these cells (never the body's own) are what must be clear of every
    //other still-present piece for this one to escape.
    static List<Vector2Int> BuildEscapeLane(List<Vector2Int> body, Vector2Int direction, RectInt bounds)
    {
        var lane = new List<Vector2Int>();
        Vector2Int current = body[body.Count - 1];

        while (true)
        {
            Vector2Int next = current + direction;
            if (!bounds.Contains(next) || body.Contains(next)) return lane;

            lane.Add(next);
            current = next;
        }
    }

    static Vector2Int Perpendicular(Vector2Int direction, bool clockwise)
    {
        return clockwise ? new Vector2Int(direction.y, -direction.x) : new Vector2Int(-direction.y, direction.x);
    }

    //repeatedly find any remaining piece whose lane is currently
    //entirely free of every other remaining piece's body, remove it, repeat
    static bool IsSolvable(List<CarSpec> cars)
    {
        var remaining = new List<CarSpec>(cars);
        var occupied = new HashSet<Vector2Int>();
        foreach (CarSpec car in remaining)
        {
            foreach (Vector2Int cell in car.body) occupied.Add(cell);
        }

        bool progress = true;

        //keeps iterating over the list of remaining cars until there is none left
        while (progress && remaining.Count > 0)
        {
            progress = false;
            for (int i = remaining.Count - 1; i >= 0; i--)
            {
                CarSpec car = remaining[i];
                if (ReachesEdge(car, occupied))
                {
                    foreach (Vector2Int cell in car.body) occupied.Remove(cell);
                    remaining.RemoveAt(i);
                    progress = true;
                }
            }
        }
        return remaining.Count == 0;
    }

    static bool ReachesEdge(CarSpec car, HashSet<Vector2Int> occupied)
    {
        foreach (Vector2Int cell in car.escapeLane)
        {
            if (occupied.Contains(cell)) return false;
        }
        return true;
    }
}
