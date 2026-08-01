using System.Collections;
using UnityEngine;

// Sells "combined into" rather than "replaced by": both source tokens converge on the target
// cell and shrink together, then the higher-tier result pops in - instead of the previous
// instant destroy-both/spawn-new swap.
public static class MergeEffect
{
    public static void Play(Token incoming, Token occupant, Container container, Vector2Int cell, int group, int nextTier)
    {
        TweenRunner.Instance.StartCoroutine(Routine(incoming, occupant, container, cell, group, nextTier));
    }

    static IEnumerator Routine(Token incoming, Token occupant, Container container, Vector2Int cell, int group, int nextTier)
    {
        Vector3 target = Grid.Instance.CellToWorld(cell);
        yield return TweenRunner.Instance.ConvergeAndShrink(incoming, occupant, target);

        Token merged = TokenSpawner.Instance.SpawnColoredToken(group, target);
        merged.Tier = nextTier;
        TweenRunner.Instance.PopIn(merged.transform);
        container.TryAccept(merged);
    }
}
