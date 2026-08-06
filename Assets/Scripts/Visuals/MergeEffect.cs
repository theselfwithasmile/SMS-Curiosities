using System.Collections;
using UnityEngine;

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

        Token merged = TokenSpawner.Instance.SpawnColoredToken(group, target, byTier: true);
        merged.Tier = nextTier;
        merged.CanExitBoard = isFinalTier;
        TokenSpawner.Instance.ApplyAnimation(merged, byTier: true); // re-resolve now that Tier no longer matches the tier SpawnColoredToken applied at
        TweenRunner.Instance.PopIn(merged.transform);
        container.TryAccept(merged);
    }
}
