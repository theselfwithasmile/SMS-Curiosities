using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Small coroutine-based tween helper. The project has no tweening library and doesn't need
// one yet - every effect here is a plain lerp over a couple hundred milliseconds. Self-
// instantiating (unlike the other manager singletons) since it has no serialized fields and
// nothing to wire up in the scene - callers just reach for TweenRunner.Instance cold.
public class TweenRunner : MonoBehaviour
{
    static TweenRunner instance;
    public static TweenRunner Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new GameObject("TweenRunner").AddComponent<TweenRunner>();
                DontDestroyOnLoad(instance.gameObject);
            }
            return instance;
        }
    }

    // Returns the Coroutine handle so a caller can cancel it (via StopCoroutine on this same
    // instance) if the target gets grabbed again mid-flight - otherwise the tween and whatever
    // repositions the target next (a fresh drag) fight over transform.position every frame.
    public Coroutine MoveTo(Transform target, Vector3 destination, float duration = 0.15f)
    {
        return StartCoroutine(MoveRoutine(target, destination, duration));
    }

    // Grab feedback: a quick up-then-back bounce on the current scale, not an absolute one -
    // so it still reads correctly on multi-cell tokens whose prefab scale isn't 1.
    public void PickupPop(Transform target, float peakScale = 1.12f, float duration = 0.12f)
    {
        StartCoroutine(PopRoutine(target, peakScale, duration));
    }

    // Reveal-from-buried: grows in from nothing rather than just appearing, since the token was
    // fully hidden (SetRevealed(false)) up to this point, not merely offscreen. Grows to the
    // target's own current scale (not a hardcoded Vector3.one) - same reasoning as PickupPop -
    // so it still reads correctly on a prefab whose authored scale isn't 1.
    public void GrowIn(Transform target, float duration = 0.15f)
    {
        StartCoroutine(ScaleRoutine(target, Vector3.zero, target.localScale, duration, EaseOutCubic));
    }

    public void ShrinkAndDestroy(Token token, float duration = 0.15f)
    {
        StartCoroutine(ShrinkAndDestroyRoutine(token, duration));
    }

    // Starts each token's shrink a beat apart instead of all at once, so a container clearing
    // several members at once reads as a small cascade rather than a single flash. Copies
    // `tokens` synchronously (before the first yield) since callers typically pass
    // Container.Members and clear it themselves right after calling this. Colliders are disabled
    // for every token right here, up front - these tokens are already logically Consume()'d
    // (CurrentContainer null) by the time this is called, so leaving later-staggered ones
    // clickable would let a fast drag grab an already-cleared "zombie" token before its own turn
    // in the stagger loop reaches it.
    public void ShrinkAndDestroySequential(IEnumerable<Token> tokens, float stagger = 0.05f, float duration = 0.15f)
    {
        var list = new List<Token>(tokens);
        foreach (Token token in list) DisableCollider(token);
        StartCoroutine(ShrinkAndDestroySequentialRoutine(list, stagger, duration));
    }

    // Parking Jam's escape arrow: briefly pulses the arrow child, then shrinks the whole token
    // (arrow included, since it's parented) and destroys it - the pulse is the "about to vanish"
    // cue that plain ShrinkAndDestroy doesn't give.
    public void PulseThenShrinkAndDestroy(Token token, Transform pulseTarget, float pulsePeak = 1.4f, float pulseDuration = 0.15f, float shrinkDuration = 0.15f)
    {
        StartCoroutine(PulseThenShrinkAndDestroyRoutine(token, pulseTarget, pulsePeak, pulseDuration, shrinkDuration));
    }

    // Grow-in with a slight overshoot before settling - for a token that's the *result* of
    // something (a merge), as opposed to GrowIn's plain 0-to-1 used for a buried reveal.
    public void PopIn(Transform target, float overshoot = 1.15f, float duration = 0.18f)
    {
        StartCoroutine(PopInRoutine(target, overshoot, duration));
    }

    // Both source tokens move onto the same target point and shrink together, then get
    // destroyed - reads as "combined into" rather than "replaced by". Returns the IEnumerator
    // itself (not a Coroutine handle) so a caller already running as a coroutine can `yield
    // return` it directly and pick up right after both tokens are gone.
    public IEnumerator ConvergeAndShrink(Token a, Token b, Vector3 target, float duration = 0.15f)
    {
        DisableCollider(a);
        DisableCollider(b);

        Vector3 startA = a != null ? a.transform.position : target;
        Vector3 startB = b != null ? b.transform.position : target;
        Vector3 scaleA = a != null ? a.transform.localScale : Vector3.one;
        Vector3 scaleB = b != null ? b.transform.localScale : Vector3.one;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float e = EaseInCubic(Mathf.Clamp01(t / duration));
            if (a != null)
            {
                a.transform.position = Vector3.Lerp(startA, target, e);
                a.transform.localScale = Vector3.Lerp(scaleA, Vector3.zero, e);
            }
            if (b != null)
            {
                b.transform.position = Vector3.Lerp(startB, target, e);
                b.transform.localScale = Vector3.Lerp(scaleB, Vector3.zero, e);
            }
            yield return null;
        }

        if (a != null) Destroy(a.gameObject);
        if (b != null) Destroy(b.gameObject);
    }

    IEnumerator MoveRoutine(Transform target, Vector3 destination, float duration)
    {
        Vector3 start = target.position;
        float t = 0f;
        while (t < duration)
        {
            if (target == null) yield break;
            t += Time.deltaTime;
            target.position = Vector3.Lerp(start, destination, EaseOutCubic(Mathf.Clamp01(t / duration)));
            yield return null;
        }
        if (target != null) target.position = destination;
    }

    IEnumerator PopRoutine(Transform target, float peakScale, float duration)
    {
        Vector3 baseScale = target.localScale;
        float half = duration * 0.5f;

        yield return ScaleRoutine(target, baseScale, baseScale * peakScale, half, EaseOutCubic);
        if (target != null) yield return ScaleRoutine(target, target.localScale, baseScale, half, EaseInCubic);
    }

    IEnumerator ScaleRoutine(Transform target, Vector3 from, Vector3 to, float duration, System.Func<float, float> ease)
    {
        float t = 0f;
        while (t < duration)
        {
            if (target == null) yield break;
            t += Time.deltaTime;
            target.localScale = Vector3.Lerp(from, to, ease(Mathf.Clamp01(t / duration)));
            yield return null;
        }
        if (target != null) target.localScale = to;
    }

    IEnumerator ShrinkAndDestroyRoutine(Token token, float duration)
    {
        if (token == null) yield break;

        // Destroy is deferred until the shrink finishes, so the GameObject (and its Collider2D)
        // is still technically alive for the duration - disable it immediately so a fast re-grab
        // can't drag a token that's already been logically consumed back into play.
        DisableCollider(token);

        yield return ScaleRoutine(token.transform, token.transform.localScale, Vector3.zero, duration, EaseInCubic);
        if (token != null) Destroy(token.gameObject);
    }

    IEnumerator ShrinkAndDestroySequentialRoutine(List<Token> tokens, float stagger, float duration)
    {
        foreach (Token token in tokens)
        {
            if (token != null) StartCoroutine(ShrinkAndDestroyRoutine(token, duration));
            yield return new WaitForSeconds(stagger);
        }
    }

    IEnumerator PulseThenShrinkAndDestroyRoutine(Token token, Transform pulseTarget, float pulsePeak, float pulseDuration, float shrinkDuration)
    {
        if (token == null) yield break;
        DisableCollider(token);

        yield return PopRoutine(pulseTarget, pulsePeak, pulseDuration);
        if (token != null) yield return ScaleRoutine(token.transform, token.transform.localScale, Vector3.zero, shrinkDuration, EaseInCubic);
        if (token != null) Destroy(token.gameObject);
    }

    IEnumerator PopInRoutine(Transform target, float overshoot, float duration)
    {
        // Overshoot/settle relative to the token's own current scale (not a hardcoded
        // Vector3.one) - same reasoning as GrowIn/PickupPop - so a merge result whose prefab
        // scale isn't 1 doesn't snap to the wrong size once it settles.
        Vector3 baseScale = target.localScale;
        float growDuration = duration * 0.7f;
        float settleDuration = duration - growDuration;

        yield return ScaleRoutine(target, Vector3.zero, baseScale * overshoot, growDuration, EaseOutCubic);
        if (target != null) yield return ScaleRoutine(target, target.localScale, baseScale, settleDuration, EaseInCubic);
    }

    static void DisableCollider(Token token)
    {
        if (token == null) return;
        Collider2D collider = token.GetComponent<Collider2D>();
        if (collider != null) collider.enabled = false;
    }

    static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
    static float EaseInCubic(float t) => t * t * t;
}
