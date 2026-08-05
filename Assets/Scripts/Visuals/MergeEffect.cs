using System.Collections;
using UnityEngine;

// Sells "combined into" rather than "replaced by": both source tokens converge on the target
// cell and shrink together, then the higher-tier result pops in - instead of the previous
// instant destroy-both/spawn-new swap.
public static class MergeEffect
{
    public static void Play(Token incoming, Token occupant, Container container, Vector2Int cell, int group, int nextTier, bool isFinalTier)
    {
        TweenRunner.Instance.StartCoroutine(Routine(incoming, occupant, container, cell, group, nextTier, isFinalTier));
    }

    static IEnumerator Routine(Token incoming, Token occupant, Container container, Vector2Int cell, int group, int nextTier, bool isFinalTier)
    {
        Vector3 target = Grid.Instance.CellToWorld(cell);
        yield return TweenRunner.Instance.ConvergeAndShrink(incoming, occupant, target);

        Token merged = TokenSpawner.Instance.SpawnColoredToken(group, target);
        merged.Tier = nextTier;
        merged.CanExitBoard = isFinalTier;
        TokenSpawner.Instance.ApplyAnimation(merged); // re-resolve now that Tier no longer matches the tier SpawnColoredToken applied at
        TweenRunner.Instance.PopIn(merged.transform);
        container.TryAccept(merged);
    }
}
