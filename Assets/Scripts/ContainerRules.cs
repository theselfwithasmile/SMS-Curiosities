using System.Collections.Generic;
using UnityEngine;

public class GroupMatchConstraint : IEntryConstraint
{
    public bool CanAccept(Token token, Container container)
    {
        return container.Members.Count == 0 || container.Members[0].Group == token.Group;
    }
}

// Unlike GroupMatchConstraint (matches whatever's already inside), this checks against a fixed
// expected group regardless of current members - e.g. an exit lane that should only ever
// consume the one car it's meant for, not whichever car happens to rest there first.
public class TargetGroupConstraint : IEntryConstraint
{
    readonly int group;

    public TargetGroupConstraint(int group)
    {
        this.group = group;
    }

    public bool CanAccept(Token token, Container container) => token.Group == group;
}

public class FullPredicate : ICompletionPredicate
{
    public bool IsComplete(Container container) => container.Members.Count >= container.Capacity;
}

public class EmptyPredicate : ICompletionPredicate
{
    public bool IsComplete(Container container) => container.Members.Count == 0;
}

// Merge's win condition can't be "empty" (a merge consumes 2 and produces 1, so a cell never
// reaches zero occupants on its own) - checked locally per cell instead of globally, since a
// merge's result is TryAccept-ed back into the same single-cell container that triggered it.
public class TierReachedPredicate : ICompletionPredicate
{
    readonly int targetTier;

    public TierReachedPredicate(int targetTier)
    {
        this.targetTier = targetTier;
    }

    public bool IsComplete(Container container) => container.Members.Count > 0 && container.Members[0].Tier >= targetTier;
}

public class LogResolution : IResolution
{
    public void Resolve(Container container)
    {
        if (container.Members.Count > 0)
        {
            Debug.Log($"Reached tier {container.Members[0].Tier}!");
        }
    }
}

public class ClearResolution : IResolution
{
    public void Resolve(Container container)
    {
        foreach (Token token in container.Members)
        {
            if (token != null) Object.Destroy(token.gameObject);
        }
        container.Members.Clear();
    }
}

// Consumes a completed container's members and spawns one token (same group) into a destination
// container - the "completed container becomes a grouped token" resolution.
public class SpawnTokenResolution : IResolution
{
    readonly Container destination;
    readonly bool consumeContainer;

    public SpawnTokenResolution(Container destination, bool consumeContainer = true)
    {
        this.destination = destination;
        this.consumeContainer = consumeContainer;
    }

    public void Resolve(Container container)
    {
        int group = container.Members.Count > 0 ? container.Members[0].Group : 0;

        //destroy each member
        foreach (Token member in container.Members)
        {
            if (member != null) Object.Destroy(member.gameObject);
        }
        container.Members.Clear();

        //spawn token on the destination container if it has space, otherwise spawn on the original container
        Vector2Int spawnCell = destination.Members.Count < destination.Capacity
            ? destination.NextAvailableCell()
            : container.NextAvailableCell();
        Token grouped = ContainerManager.Instance.SpawnColoredToken(group, Grid.Instance.CellToWorld(spawnCell));
        destination.TryAccept(grouped);

        if (consumeContainer)
        {
            ContainerManager.Instance.RemoveContainer(container);
        }
    }
}

// Merge (2048-style): dropping onto a same-group, same-tier occupant combines them into one
// higher-tier token in place, instead of the drop being rejected outright.
public class MergeInteraction : IOccupantInteraction
{
    public bool TryInteract(Token incoming, Container incomingOrigin, Token occupant, Container container)
    {
        if (incoming.Group != occupant.Group || incoming.Tier != occupant.Tier) return false;

        Vector2Int cell = container.OrderedCells[0];
        int group = incoming.Group;
        int nextTier = incoming.Tier + 1;

        Object.Destroy(incoming.gameObject);
        Object.Destroy(occupant.gameObject);
        container.Members.Clear();

        Token merged = ContainerManager.Instance.SpawnColoredToken(group, Grid.Instance.CellToWorld(cell));
        merged.Tier = nextTier;
        container.TryAccept(merged);
        return true;
    }
}

// Toon Blast-style free rearrangement: dropping onto a different-group occupant swaps the two
// tokens instead of being rejected, then flood-fills same-group neighbors from both affected
// cells and clears any run of minMatchSize+ into one grouped output token.
public class SwapInteraction : IOccupantInteraction
{
    readonly Container outputDestination;
    readonly int minMatchSize;

    public SwapInteraction(Container outputDestination, int minMatchSize = 3)
    {
        this.outputDestination = outputDestination;
        this.minMatchSize = minMatchSize;
    }

    public bool TryInteract(Token incoming, Container incomingOrigin, Token occupant, Container container)
    {
        if (incomingOrigin == null || incomingOrigin == container) return false;

        Vector2Int occupantCell = incomingOrigin.NextAvailableCell();
        container.TryRemove(occupant);
        incomingOrigin.TryAccept(occupant);
        occupant.transform.position = Grid.Instance.CellToWorld(occupantCell);

        container.TryAccept(incoming);
        incoming.transform.position = Grid.Instance.CellToWorld(container.OrderedCells[0]);

        CheckMatch(container);
        CheckMatch(incomingOrigin);
        return true;
    }

    void CheckMatch(Container cell)
    {
        if (cell.Members.Count == 0) return;

        int group = cell.Members[0].Group;
        var claimed = new HashSet<Container> { cell };
        var frontier = new List<Container>();
        AddSameGroupNeighbors(cell, group, claimed, frontier);

        while (frontier.Count > 0)
        {
            Container next = frontier[frontier.Count - 1];
            frontier.RemoveAt(frontier.Count - 1);
            if (!claimed.Add(next)) continue;
            AddSameGroupNeighbors(next, group, claimed, frontier);
        }

        if (claimed.Count < minMatchSize) return;

        foreach (Container matched in claimed)
        {
            Token token = matched.Members[0];
            matched.TryRemove(token);
            Object.Destroy(token.gameObject);
        }

        Vector2Int spawnCell = outputDestination.NextAvailableCell();
        Token grouped = ContainerManager.Instance.SpawnColoredToken(group, Grid.Instance.CellToWorld(spawnCell));
        outputDestination.TryAccept(grouped);
    }

    void AddSameGroupNeighbors(Container from, int group, HashSet<Container> claimed, List<Container> frontier)
    {
        foreach (Vector2Int neighborCell in ContainerManager.Instance.Neighbors(from.OrderedCells[0]))
        {
            foreach (Container neighbor in ContainerManager.Instance.GetContainersAt(neighborCell))
            {
                if (claimed.Contains(neighbor) || neighbor.Members.Count == 0) continue;
                if (neighbor.Members[0].Group == group) frontier.Add(neighbor);
            }
        }
    }
}

// Parking Jam (arrow variant), reworked as an ephemeral container instead of a bespoke tap
// component: at drag-start, computes how far this car could currently slide in its fixed
// direction (raw Grid occupancy, same as before) and creates a container spanning exactly that
// reachable strip - CreateFixedContainer already skips cells owned by another container (e.g.
// an exit lane), so the path naturally stops just short of one, and a drop whose footprint
// straddles both is still validated atomically by the existing multi-container TryClaimFootprint.
// The container is discarded at drag-end regardless of outcome; it was only ever a snapshot for
// that one gesture, recomputed fresh next time this car is grabbed.
public class CarPathProvider : IEphemeralContainerProvider
{
    readonly Vector2Int direction;
    Container currentPath;

    public CarPathProvider(Vector2Int direction)
    {
        this.direction = direction;
    }

    public void BeginGesture(Token token)
    {
        Vector2Int anchor = Grid.Instance.WorldToCell(token.transform.position);

        // Temporarily free the car's own cells so its own footprint doesn't block its own slide.
        SetOccupied(anchor, token.CellOffsets, false);
        Vector2Int farAnchor = Grid.SlideUntilBlocked(anchor, _ => direction, candidate => FootprintFree(candidate, token.CellOffsets));
        SetOccupied(anchor, token.CellOffsets, true);

        RectInt bounds = FootprintSpan(anchor, farAnchor, token.CellOffsets);
        currentPath = ContainerManager.Instance.CreateFixedContainer(bounds, new Color(1f, 1f, 0.6f, 1f));
    }

    public void EndGesture(Token token)
    {
        if (currentPath != null)
        {
            ContainerManager.Instance.RemoveContainer(currentPath);
            currentPath = null;
        }

        // RemoveContainer just freed every cell of the (now-discarded) path, including wherever
        // the token actually ended up - reclaim its own final footprint so other cars still see
        // it as an obstacle. Skipped if the token was destroyed (it reached a real exit).
        if (token == null) return;

        Vector2Int restingAnchor = Grid.Instance.WorldToCell(token.transform.position);
        SetOccupied(restingAnchor, token.CellOffsets, true);
    }

    static bool FootprintFree(Vector2Int anchor, List<Vector2Int> offsets)
    {
        foreach (Vector2Int offset in offsets)
        {
            Vector2Int cell = anchor + offset;
            if (!Grid.Instance.IsInBounds(cell) || Grid.Instance.IsOccupied(cell)) return false;
        }
        return true;
    }

    static void SetOccupied(Vector2Int anchor, List<Vector2Int> offsets, bool occupied)
    {
        foreach (Vector2Int offset in offsets)
        {
            Grid.Instance.SetOccupied(anchor + offset, occupied);
        }
    }

    static RectInt FootprintSpan(Vector2Int nearAnchor, Vector2Int farAnchor, List<Vector2Int> offsets)
    {
        Vector2Int min = Vector2Int.Min(nearAnchor, farAnchor);
        Vector2Int max = Vector2Int.Max(nearAnchor, farAnchor);
        foreach (Vector2Int offset in offsets)
        {
            min = Vector2Int.Min(min, Vector2Int.Min(nearAnchor + offset, farAnchor + offset));
            max = Vector2Int.Max(max, Vector2Int.Max(nearAnchor + offset, farAnchor + offset));
        }
        return new RectInt(min.x, min.y, max.x - min.x + 1, max.y - min.y + 1);
    }
}
