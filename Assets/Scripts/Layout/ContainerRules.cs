using System.Collections.Generic;
using UnityEngine;

public class GroupMatchConstraint : IEntryConstraint
{
    public bool CanAccept(Token token, Container container)
    {
        return container.Members.Count == 0 || container.Members[0].Group == token.Group;
    }
}

public class NoEntryConstraint : IEntryConstraint
{
    public bool CanAccept(Token token, Container container) => false;
}

public class FullPredicate : ICompletionPredicate
{
    public bool IsComplete(Container container) => container.Members.Count >= container.Capacity;
}

// Completes only when the container is full AND every member shares the same group. Needed
// wherever a container can end up full-but-mixed without going through the normal entry-gated
// path - e.g. Water Sort's buried-token reveal deliberately bypasses GroupMatchConstraint (a
// buried token isn't a new player move, so nothing there guarantees it matches its tube), so
// plain FullPredicate can't tell a genuinely solved tube apart from one that just happens to be
// full.
public class FullAndSameGroupPredicate : ICompletionPredicate
{
    public bool IsComplete(Container container)
    {
        if (container.Members.Count < container.Capacity) return false;
        int group = container.Members[0].Group;
        for (int i = 1; i < container.Members.Count; i++)
        {
            if (container.Members[i].Group != group) return false;
        }
        return true;
    }
}

public class EmptyPredicate : ICompletionPredicate
{
    public bool IsComplete(Container container) => container.Members.Count == 0;
}

public class ClearResolution : IResolution
{
    public void Resolve(Container container)
    {
        TweenRunner.Instance.ShrinkAndDestroySequential(container.Members);
        container.ClearMembers();
    }
}

//consumes a completed container's members and spawns one token into a destination
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

        TweenRunner.Instance.ShrinkAndDestroySequential(container.Members);
        container.ClearMembers();

        //spawn token on the destination container if it has space, otherwise spawn on the original container
        Vector2Int spawnCell = destination.Members.Count < destination.Capacity
            ? destination.NextAvailableCell()
            : container.NextAvailableCell();
        Token grouped = TokenSpawner.Instance.SpawnColoredToken(group, Grid.Instance.CellToWorld(spawnCell));
        destination.TryAccept(grouped);

        if (consumeContainer)
        {
            ContainerManager.Instance.RemoveContainer(container);
        }
    }
}

public class MergeInteraction : IOccupantInteraction
{
    // The tier at which a merge result becomes the zone's final tile - passed through to
    // MergeEffect so it can flag the spawned token as exitable (Token.CanExitBoard), rather than
    // this zone needing its own per-cell completion predicate/resolution to notice a tier was
    // reached (MergeBaseZone's win check is now the same generic "board fully cleared" every
    // other zone uses, satisfied once every final tile has been dragged off).
    readonly int targetTier;

    public MergeInteraction(int targetTier)
    {
        this.targetTier = targetTier;
    }

    public bool TryInteract(Token incoming, Container incomingOrigin, Token occupant, Container container)
    {
        if (incoming.Group != occupant.Group || incoming.Tier != occupant.Tier) return false;

        Vector2Int cell = container.OrderedCells[0];
        int group = incoming.Group;
        int nextTier = incoming.Tier + 1;

        container.ClearMembers();
        MergeEffect.Play(incoming, occupant, container, cell, group, nextTier, nextTier >= targetTier);
        return true;
    }
}

//dropping onto a different-group occupant swaps the two
// tokens instead of being rejected, then flood-fills same-group neighbors from both affected
// cells and clears any run of minMatchSize+ into one grouped output token.
// Matching is checked once, after the swap lands, against the drop cell's real grid neighbors -
// not previewed mid-drag - so the swap has to land grid-adjacent to a cluster to connect it.
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
        TweenRunner.Instance.MoveTo(occupant.transform, Grid.Instance.CellToWorld(occupantCell));

        container.TryAccept(incoming);
        TweenRunner.Instance.MoveTo(incoming.transform, Grid.Instance.CellToWorld(container.OrderedCells[0]));

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

        //adds all same group tokens adjacent to the cell and its neighbors
        while (frontier.Count > 0)
        {
            Container next = frontier[frontier.Count - 1];
            frontier.RemoveAt(frontier.Count - 1);
            if (!claimed.Add(next)) continue;
            AddSameGroupNeighbors(next, group, claimed, frontier);
        }

        if (claimed.Count < minMatchSize) return;

        //registers and destroys said tokens
        var matchedTokens = new List<Token>(claimed.Count);
        foreach (Container matched in claimed)
        {
            Token token = matched.Members[0];
            matched.Consume(token);
            matchedTokens.Add(token);
        }
        TweenRunner.Instance.ShrinkAndDestroySequential(matchedTokens);

        Vector2Int spawnCell = outputDestination.NextAvailableCell();
        //Token grouped = TokenSpawner.Instance.SpawnColoredToken(group, Grid.Instance.CellToWorld(spawnCell));
        //outputDestination.TryAccept(grouped);
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

//dropping a token onto a same-group occupant clears both if a collision-free route
//exists between the token's origin cell and the occupant's cell (turn limit to be implemented)
public class PathConnectInteraction : IOccupantInteraction
{
    public bool TryInteract(Token incoming, Container incomingOrigin, Token occupant, Container container)
    {
        if (incoming.Group != occupant.Group || incomingOrigin == null) return false;

        Vector2Int origin = incomingOrigin.OrderedCells[0];
        Vector2Int target = container.OrderedCells[0];
        if (!TryFindPath(origin, target, out List<Vector2Int> path)) return false;

        container.Consume(occupant);
        ConnectPathEffect.ShowThenDestroy(path, GameState.Instance.GroupColor(incoming.Group), occupant, incoming);
        return true;
    }

    //reconstructs BFS via cameFrom so the route can be drawn
    static bool TryFindPath(Vector2Int origin, Vector2Int target, out List<Vector2Int> path)
    {
        var visited = new HashSet<Vector2Int> { origin };
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var frontier = new Queue<Vector2Int>();
        frontier.Enqueue(origin);

        while (frontier.Count > 0)
        {
            Vector2Int cell = frontier.Dequeue();
            foreach (Vector2Int neighbor in ContainerManager.Instance.Neighbors(cell))
            {
                if (neighbor == target)
                {
                    path = ReconstructPath(cameFrom, origin, cell);
                    path.Add(target);
                    return true;
                }
                if (visited.Contains(neighbor) || !IsPassable(neighbor)) continue;
                visited.Add(neighbor);
                cameFrom[neighbor] = cell;
                frontier.Enqueue(neighbor);
            }
        }
        path = null;
        return false;
    }

    static List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int origin, Vector2Int end)
    {
        var path = new List<Vector2Int> { end };
        Vector2Int current = end;
        while (current != origin)
        {
            current = cameFrom[current];
            path.Add(current);
        }
        path.Reverse();
        return path;
    }

    static bool IsPassable(Vector2Int cell)
    {
        foreach (Container container in ContainerManager.Instance.GetContainersAt(cell))
        {
            if (container.Members.Count > 0) return false;
        }
        return true;
    }
}

// Runs an inner resolution then an extra callback - lets a zone piggyback tracking (e.g. Block
// Puzzle's clear-count win quota) onto a shared resolution like ClearResolution without needing a
// bespoke IResolution of its own just to add one counter increment.
public class CompositeResolution : IResolution
{
    readonly IResolution inner;
    readonly System.Action onResolved;

    public CompositeResolution(IResolution inner, System.Action onResolved)
    {
        this.inner = inner;
        this.onResolved = onResolved;
    }

    public void Resolve(Container container)
    {
        inner?.Resolve(container);
        onResolved?.Invoke();
    }
}

