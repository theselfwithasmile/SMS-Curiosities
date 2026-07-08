using UnityEngine;

public class GroupMatchConstraint : IEntryConstraint
{
    public bool CanAccept(Token token, Container container)
    {
        return container.Members.Count == 0 || container.Members[0].Group == token.Group;
    }
}

public class FullPredicate : ICompletionPredicate
{
    public bool IsComplete(Container container) => container.Members.Count >= container.Capacity;
}

public class EmptyPredicate : ICompletionPredicate
{
    public bool IsComplete(Container container) => container.Members.Count == 0;
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
        Token grouped = Grid.Instance.SpawnToken(group, Grid.Instance.CellToWorld(spawnCell));
        destination.TryAccept(grouped);

        if (consumeContainer)
        {
            Grid.Instance.RemoveContainer(container);
        }
    }
}
