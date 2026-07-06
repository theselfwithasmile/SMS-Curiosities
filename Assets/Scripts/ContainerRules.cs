using UnityEngine;

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
