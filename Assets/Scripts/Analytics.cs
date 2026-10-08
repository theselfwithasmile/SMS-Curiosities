using System.Collections.Generic;
using UnityEngine;
#if FIREBASE_ANALYTICS && !UNITY_EDITOR
using Firebase;
using Firebase.Extensions;
using FA = Firebase.Analytics;
#endif

//thin facade so gameplay code never touches the Firebase SDK directly. Without the FIREBASE_ANALYTICS
//scripting define (or in the Editor, where Firebase Analytics is a no-op anyway) events only go to the console
public static class Analytics
{
    static bool ready;
    static bool failed;
    static readonly Queue<(string name, (string key, object value)[] parameters)> pending = new Queue<(string, (string, object)[])>();

    public static void Initialize()
    {
#if FIREBASE_ANALYTICS && !UNITY_EDITOR
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result != DependencyStatus.Available)
            {
                Debug.LogWarning($"[Analytics] Firebase unavailable: {task.Result}");
                failed = true;
                pending.Clear();
                return;
            }

            ready = true;
            while (pending.Count > 0)
            {
                var e = pending.Dequeue();
                Send(e.name, e.parameters);
            }
        });
#else
        ready = true;
#endif
    }

    public static void LogEvent(string name, params (string key, object value)[] parameters)
    {
        if (failed) return;

        //Firebase init is async, so anything logged before it finishes waits here instead of being dropped
        if (!ready)
        {
            pending.Enqueue((name, parameters));
            return;
        }

        Send(name, parameters);
    }

    static void Send(string name, (string key, object value)[] parameters)
    {
#if FIREBASE_ANALYTICS && !UNITY_EDITOR
        var converted = new FA.Parameter[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            var (key, value) = parameters[i];
            converted[i] = value switch
            {
                int v    => new FA.Parameter(key, v),
                long v   => new FA.Parameter(key, v),
                float v  => new FA.Parameter(key, v),
                double v => new FA.Parameter(key, v),
                bool v   => new FA.Parameter(key, v ? 1L : 0L),
                _        => new FA.Parameter(key, value?.ToString() ?? ""),
            };
        }
        FA.FirebaseAnalytics.LogEvent(name, converted);
#else
        var parts = new List<string>();
        foreach (var (key, value) in parameters) parts.Add($"{key}={value}");
        Debug.Log($"[Analytics] {name} {{{string.Join(", ", parts)}}}");
#endif
    }
}
