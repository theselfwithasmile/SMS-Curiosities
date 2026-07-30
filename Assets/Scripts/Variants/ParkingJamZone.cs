using System.Collections.Generic;
using UnityEngine;

// Parking Jam (arrow-maze variant): every piece is a bent, self-avoiding body of cells (a plain
// CellOffsets shape, same mechanism Block Puzzle already uses for non-rectangular pieces) with a
// fixed escape lane running from its head to the grid boundary. A drag either clears a piece all
// the way out (every lane cell currently free of every other still-present piece) or reverts it
// completely - no intermediate resting position ever persists, so the whole layout is monotone: a
// piece with a clear lane now will still have one later (nothing ever repositions to block it),
// which is exactly why solvability only needs a simple greedy check instead of a search over
// joint piece positions.
public class ParkingJamZone : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] float fillRatio = 0.55f;
    [SerializeField] int maxGenerationAttempts = 60;
    [SerializeField] int minLength = 2;
    [SerializeField] int maxLength = 8;

    static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    struct CarSpec
    {
        public List<Vector2Int> body; // absolute cells, tail to head
        public List<Vector2Int> escapeLane; // absolute cells, beyond the head, up to the boundary
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
            if (candidate != null && IsSolvable(candidate))
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
        Vector2Int anchor = car.body[0];
        var offsets = new List<Vector2Int>(car.body.Count);
        foreach (Vector2Int cell in car.body) offsets.Add(cell - anchor); //applies local offset

        Token token = TokenSpawner.Instance.SpawnMultiCellToken(car.group, grid.CellToWorld(anchor), offsets);
        token.EscapeLane = car.escapeLane;

        foreach (Vector2Int cell in car.body)
        {
            grid.SetOccupied(cell, true);
        }
    }

    // Fills free cells (in random order) with bent pieces until roughly fillRatio of the board is
    // occupied - walking actual remaining free cells rather than blind-guessing coordinates, so
    // density scales reliably instead of degrading as the board fills up.
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
        int targetFilled = Mathf.RoundToInt(fillRatio * bounds.width * bounds.height); //percentage of grid to be filled
        foreach (Vector2Int cell in freeCells)
        {
            if (occupied.Count >= targetFilled) break;
            if (occupied.Contains(cell)) continue;

            cars.Add(BuildCarAt(cell, bounds, occupied));
        }

        return cars.Count > 0 ? cars : null;
    }

    // Grows a bent, self-avoiding body from `start` - longer target lengths bend more often, so
    // short pieces read as plain straight/L blockers while long ones wind like a real maze
    // corridor. Falls back to whatever length it manages if boxed in early (a shorter piece,
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
            if (!next.HasValue) break;

            direction = next.Value - head; 
            body.Add(next.Value);
        }
        foreach (Vector2Int cell in body) occupied.Add(cell);

        List<Vector2Int> lane = BuildEscapeLane(body[body.Count - 1], direction, bounds);
        int groupRange = Mathf.Max(1, GameState.Instance.GroupCount);
        return new CarSpec { body = body, escapeLane = lane, group = Random.Range(0, groupRange) };
    }

    // Prefers turning (or not) per turnChance, tries the other perpendicular next, then straight
    // ahead as a last resort - self-avoiding against every cell already claimed this generation
    // pass (other pieces' bodies) and this piece's own body so far.
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

    // Continues straight past the body's head in whatever direction the body's own last segment
    // was already heading - these cells (never the body's own) are what must be clear of every
    // other still-present piece for this one to escape. The direction has to stay exactly what
    // the piece's own shape visibly implies (the last segment's heading is the only escape-facing
    // cue a player can actually see) - a lane that bent on its own past that, invisibly, would let
    // pieces get blocked or escape for reasons nothing on screen explains.
    static List<Vector2Int> BuildEscapeLane(Vector2Int head, Vector2Int direction, RectInt bounds)
    {
        var lane = new List<Vector2Int>();
        Vector2Int current = head;

        while (true)
        {
            Vector2Int next = current + direction;
            if (!bounds.Contains(next)) return lane;

            lane.Add(next);
            current = next;
        }
    }

    static Vector2Int Perpendicular(Vector2Int direction, bool clockwise)
    {
        return clockwise ? new Vector2Int(direction.y, -direction.x) : new Vector2Int(-direction.y, direction.x);
    }

    // Monotone solvability check: repeatedly find any remaining piece whose lane is currently
    // entirely free of every other remaining piece's body, remove it, repeat. Correct because
    // nothing ever repositions to block a piece - a lane that's clear now stays clear (or becomes
    // clear as other pieces are removed) regardless of removal order, so a single greedy pass (no
    // backtracking, no joint-state search) is sufficient.
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
