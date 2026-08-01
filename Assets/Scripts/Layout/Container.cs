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
    public readonly List<Token> Members = new List<Token>();
    public readonly Queue<Token> BuriedTokens = new Queue<Token>();

    public readonly List<IEntryConstraint> EntryConstraints = new List<IEntryConstraint>();
    public readonly List<IExitConstraint> ExitConstraints = new List<IExitConstraint>();
    public ICompletionPredicate CompletionPredicate;
    public IResolution Resolution;
    public IOccupantInteraction OccupantInteraction;

    public int Capacity => OrderedCells.Count;

    public Container(List<Vector2Int> orderedCells, Color color)
    {
        OrderedCells = orderedCells;
        Cells = new HashSet<Vector2Int>(orderedCells);
        Color = color;
    }
    
    public Vector2Int NextAvailableCell() => OrderedCells[Members.Count];

    public bool CanAccept(Token token)
    {
        if (Members.Count >= Capacity) return false;

        foreach (IEntryConstraint constraint in EntryConstraints)
        {
            if (!constraint.CanAccept(token, this)) return false;
        }
        return true;
    }

    //bypasses entry constraints
    public void ForceAccept(Token token)
    {
        Members.Add(token);
        token.CurrentContainer = this;
    }

    public bool TryAccept(Token token)
    {
        if (!CanAccept(token)) return false;

        Members.Add(token);
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
        if (token.CurrentContainer == this) token.CurrentContainer = null;
        return true;
    }

    //permanently removes singular token that's being taken out of play entirely
    public void Consume(Token token)
    {
        TryRemove(token);
        CleanupOtherMemberships(token);
        RevealNext();
    }

    //clears every current member outright
    public void ClearMembers()
    {
        var cleared = new List<Token>(Members);
        Members.Clear();
        foreach (Token token in cleared) CleanupOtherMemberships(token);
        RevealNext();
    }
    
    void CleanupOtherMemberships(Token token)
    {
        if (token == null) return;
        Vector2Int cell = Grid.Instance.WorldToCell(token.transform.position);  
        foreach (Container other in ContainerManager.Instance.GetContainersAt(cell))
        {
            if (other != this) other.Members.Remove(token);  //fine to call since actual destruction is deferred
        }
    }

    // Promotes the next buried token into this cell once it has room - only relevant for layered
    // containers (BuriedTokens non-empty); a no-op otherwise, so ordinary containers pay nothing.
    void RevealNext()
    {
        if (Members.Count >= Capacity || BuriedTokens.Count == 0) return;

        Token next = BuriedTokens.Dequeue();
        next.SetRevealed(true);
        next.transform.position = Grid.Instance.CellToWorld(OrderedCells[Members.Count]);
        TweenRunner.Instance.GrowIn(next.transform);
        ForceAccept(next);
    }
}
