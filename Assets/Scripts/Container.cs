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

public class Container
{
    public readonly HashSet<Vector2Int> Cells;
    public readonly Color Color;
    public readonly List<Token> Members = new List<Token>();

    public readonly List<IEntryConstraint> EntryConstraints = new List<IEntryConstraint>();
    public readonly List<IExitConstraint> ExitConstraints = new List<IExitConstraint>();
    public ICompletionPredicate CompletionPredicate;
    public IResolution Resolution;

    public int Capacity => Cells.Count;

    public Container(HashSet<Vector2Int> cells, Color color)
    {
        Cells = cells;
        Color = color;
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

    public bool TryAccept(Token token)
    {
        if (!CanAccept(token)) return false;

        Members.Add(token);
        token.CurrentContainer = this;
        CheckCompletion();
        return true;
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

    void CheckCompletion()
    {
        if (CompletionPredicate != null && CompletionPredicate.IsComplete(this))
        {
            Resolution?.Resolve(this);
        }
    }
}
