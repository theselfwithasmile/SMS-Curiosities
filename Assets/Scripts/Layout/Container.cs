using System.Collections.Generic;
using UnityEngine;

public interface IEntryConstraint
{
    bool CanAccept(Token token, Container container);
}

public interface IExitConstraint
{
    bool CanRemove(Token token, Container container);
}

public interface ICompletionPredicate
{
    bool IsComplete(Container container);
}

public interface IResolution
{
    void Resolve(Container container);
}

//what happens when a token is dropped onto a cell that's already occupied
public interface IOccupantInteraction
{
    // incomingOrigin is where the dragged token was removed from at drag-start
    bool TryInteract(Token incoming, Container incomingOrigin, Token occupant, Container container);
}

public class ContainerRuleSet
{
    public IEntryConstraint EntryConstraint;
    public IExitConstraint ExitConstraint;
    public ICompletionPredicate CompletionPredicate;
    public IResolution Resolution;
    public IOccupantInteraction OccupantInteraction;

    public void ApplyTo(Container container)
    {
        if (EntryConstraint != null) container.EntryConstraints.Add(EntryConstraint);
        if (ExitConstraint != null) container.ExitConstraints.Add(ExitConstraint);
        if (CompletionPredicate != null) container.CompletionPredicate = CompletionPredicate;
        if (Resolution != null) container.Resolution = Resolution;
        if (OccupantInteraction != null) container.OccupantInteraction = OccupantInteraction;
    }

    public void ApplyToAll(IEnumerable<Container> containers)
    {
        foreach (Container container in containers) ApplyTo(container);
    }
}

public class Container
{
    public readonly List<Vector2Int> OrderedCells;
    public readonly HashSet<Vector2Int> Cells;
    public readonly Color Color;
    public readonly RectInt Bounds;
    public readonly List<Token> Members = new List<Token>();

    //which token (if any) currently occupies each of this container's cells
    readonly Dictionary<Vector2Int, Token> occupantByCell = new Dictionary<Vector2Int, Token>();
    readonly Dictionary<Vector2Int, Queue<Token>> buriedByCell = new Dictionary<Vector2Int, Queue<Token>>();

    public readonly List<IEntryConstraint> EntryConstraints = new List<IEntryConstraint>();
    public readonly List<IExitConstraint> ExitConstraints = new List<IExitConstraint>();
    public ICompletionPredicate CompletionPredicate;
    public IResolution Resolution;
    public IOccupantInteraction OccupantInteraction;

    public int Capacity => OrderedCells.Count;

    public int BuriedCount
    {
        get
        {
            int total = 0;
            foreach (Queue<Token> queue in buriedByCell.Values) total += queue.Count;
            return total;
        }
    }

    public Container(List<Vector2Int> orderedCells, Color color, RectInt bounds)
    {
        OrderedCells = orderedCells;
        Cells = new HashSet<Vector2Int>(orderedCells);
        Color = color;
        Bounds = bounds;
    }

    //first cell not currently occupied
    public Vector2Int NextAvailableCell()
    {
        foreach (Vector2Int cell in OrderedCells)
        {
            if (!occupantByCell.ContainsKey(cell)) return cell;
        }
        return OrderedCells[OrderedCells.Count - 1];
    }

    public bool CanAccept(Token token)
    {
        if (Members.Count >= Capacity) return false;

        foreach (IEntryConstraint constraint in EntryConstraints)
        {
            if (!constraint.CanAccept(token, this)) return false;
        }
        return true;
    }

    // Whether this specific cell (one of the container's own) is free to accept a token - the
    // arbitrary-slot counterpart to CanAccept's "is there room somewhere" check.
    public bool CanAcceptAt(Token token, Vector2Int cell)
    {
        return Cells.Contains(cell) && !occupantByCell.ContainsKey(cell) && CanAccept(token);
    }

    //bypasses entry constraints
    public void ForceAccept(Token token) => ForceAcceptAt(token, NextAvailableCell());

    public void ForceAcceptAt(Token token, Vector2Int cell)
    {
        Members.Add(token);
        occupantByCell[cell] = token;
        token.CurrentContainer = this;
    }

    public bool TryAccept(Token token) => TryAcceptAt(token, NextAvailableCell());

    public bool TryAcceptAt(Token token, Vector2Int cell)
    {
        if (!CanAcceptAt(token, cell)) return false;

        Members.Add(token);
        occupantByCell[cell] = token;
        token.CurrentContainer = this;

        if (CompletionPredicate != null && CompletionPredicate.IsComplete(this))
        {
            Resolution?.Resolve(this);
        }

        return true;
    }

    //only tried by Token when CanAccept fails due to capacity
    public bool TryInteractWithOccupant(Token incoming, Container incomingOrigin)
    {
        return OccupantInteraction != null && Members.Count == 1
            && OccupantInteraction.TryInteract(incoming, incomingOrigin, Members[0], this);
    }

    public bool CanRemove(Token token)
    {
        foreach (IExitConstraint constraint in ExitConstraints)
        {
            if (!constraint.CanRemove(token, this)) return false;
        }
        return true;
    }
    
    public bool TryRemove(Token token)
    {
        if (!Members.Contains(token) || !CanRemove(token)) return false;

        Members.Remove(token);
        RemoveOccupantEntry(token);
        if (token.CurrentContainer == this) token.CurrentContainer = null;
        return true;
    }

    //permanently removes singular token that's being taken out of play entirely
    public void Consume(Token token)
    {
        Vector2Int? cell = CellOf(token);
        TryRemove(token);
        CleanupOtherMemberships(token);
        if (cell.HasValue) RevealAt(cell.Value);
    }

    //clears every current member outright
    public void ClearMembers()
    {
        var cleared = new List<Token>(Members);
        var clearedCells = new List<Vector2Int>(occupantByCell.Keys);
        Members.Clear();
        occupantByCell.Clear();
        foreach (Token token in cleared) CleanupOtherMemberships(token);
        foreach (Vector2Int cell in clearedCells) RevealAt(cell);
    }

    // Strips a token from this container's bookkeeping without the CanRemove exit-constraint gate
    // - used only to clean up stale cross-membership entries (a token overlapping multiple
    // containers via a shared cell), never for an ordinary player-initiated removal.
    public void ForceRemoveMember(Token token)
    {
        Members.Remove(token);
        RemoveOccupantEntry(token);
    }

    void CleanupOtherMemberships(Token token)
    {
        if (token == null) return;
        Vector2Int cell = Grid.Instance.WorldToCell(token.transform.position);
        foreach (Container other in ContainerManager.Instance.GetContainersAt(cell))
        {
            if (other != this) other.ForceRemoveMember(token);  //fine to call since actual destruction is deferred
        }
    }

    Vector2Int? CellOf(Token token)
    {
        foreach (KeyValuePair<Vector2Int, Token> entry in occupantByCell)
        {
            if (entry.Value == token) return entry.Key;
        }
        return null;
    }

    void RemoveOccupantEntry(Token token)
    {
        Vector2Int? cell = CellOf(token);
        if (cell.HasValue) occupantByCell.Remove(cell.Value);
    }

    // Buries `token` under `cell` - revealed later, in the order buried, once that specific cell
    // is vacated (RevealAt).
    public void BuryAt(Vector2Int cell, Token token)
    {
        if (!buriedByCell.TryGetValue(cell, out Queue<Token> queue))
        {
            queue = new Queue<Token>();
            buriedByCell[cell] = queue;
        }
        queue.Enqueue(token);
    }

    // Promotes the next token buried under this specific cell, if any, once the cell is actually
    // vacated - a no-op for cells with nothing buried, so ordinary containers pay nothing.
    public void RevealAt(Vector2Int cell)
    {
        if (occupantByCell.ContainsKey(cell)) return;
        if (!buriedByCell.TryGetValue(cell, out Queue<Token> queue) || queue.Count == 0) return;

        Token next = queue.Dequeue();
        next.SetRevealed(true);
        next.transform.position = Grid.Instance.CellToWorld(cell);
        TweenRunner.Instance.GrowIn(next.transform);
        ForceAcceptAt(next, cell);

        // Bypassing entry constraints is correct here (the token was already "inside" the
        // container, just hidden - this isn't a new player-initiated entry), but the completion
        // check itself must still run: a reveal can be exactly what completes the container (e.g.
        // it was already full-minus-one and this fills the last cell with a matching group), and
        // that shouldn't have to wait for an unrelated future placement to notice.
        if (CompletionPredicate != null && CompletionPredicate.IsComplete(this))
        {
            Resolution?.Resolve(this);
        }
    }
}
