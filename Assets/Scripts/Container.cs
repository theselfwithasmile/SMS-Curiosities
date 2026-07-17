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

// What happens when a token is dropped onto a cell that's already occupied (e.g. Merge's
// combine, Toon Blast's swap) - distinct from IEntryConstraint, which only gates empty slots.
public interface IOccupantInteraction
{
    // incomingOrigin is where the dragged token was removed from at drag-start (already vacated
    // by the time this runs) - null if it wasn't in a container. Needed for swaps, which put the
    // occupant there; ignored by non-swap interactions like Merge.
    bool TryInteract(Token incoming, Container incomingOrigin, Token occupant, Container container);
}

// A container computed fresh at the start of one drag gesture and discarded at the end of it
// (Parking Jam's reachable path), rather than one that persists across the whole game like every
// other container. BeginGesture runs before the drag's own accept/reject logic; EndGesture runs
// after, regardless of whether the drop succeeded.
public interface IEphemeralContainerProvider
{
    void BeginGesture(Token token);
    void EndGesture(Token token);
}


// Bundles the four pluggable rule slots so a zone can wire them onto many containers in one call
// instead of four separate property assignments per container. Rule instances here are almost
// always stateless (or share the same constructor args) across every container in a zone, so one
// ContainerRuleSet is normally shared, not rebuilt per container.
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
    // Claim order, not just membership, letting tokens stack into the next open slot
    // instead of whichever cell was literally dropped on (which could overlap another member).
    public readonly List<Vector2Int> OrderedCells;
    public readonly HashSet<Vector2Int> Cells;
    public readonly Color Color;
    public readonly List<Token> Members = new List<Token>();

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

    // Re-adds a token unconditionally, bypassing entry constraints entirely - used only to revert
    // a token to wherever it just came from after a failed drag. Reverting should never be
    // gate-kept by rules meant for genuinely new placements (NoEntryConstraint, for instance,
    // would otherwise also block a token from returning to the exact container it left moments
    // ago). Never trips completion either - the container's state is exactly what it was before
    // the drag started, which by definition wasn't already complete.
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

    // Only tried by Draggable when CanAccept fails due to capacity, not due to an entry
    // constraint rejecting the token outright.
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
}
