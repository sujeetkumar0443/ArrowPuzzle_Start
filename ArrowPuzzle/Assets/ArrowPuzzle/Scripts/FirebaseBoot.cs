using System;
using Firebase;
using Firebase.Extensions;
using UnityEngine;

namespace ArrowPuzzle
{
    /// <summary>
    /// The only place that starts Firebase (CheckAndFixDependenciesAsync). Never blocks: Init() returns at once and
    /// Ready / OnReady follow on the main thread. If Firebase can't start, Failed is set and every Firebase wrapper
    /// (AnalyticsService, CrashReporter) silently does nothing.
    /// </summary>
    public static class FirebaseBoot
    {
        public static bool Ready { get; private set; }
        public static bool Failed { get; private set; }

        /// <summary>Raised once, on the main thread, when Firebase is ready.</summary>
        public static event Action OnReady;

        static event Action<bool> done; // WhenDone listeners: true = ready, false = failed
        static bool started;

        public static void Init()
        {
            if (started) return;
            started = true;
            try
            {
                FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
                {
                    bool ok = false;
                    string why = null;
                    try
                    {
                        if (task.IsFaulted || task.IsCanceled) why = task.Exception != null ? task.Exception.GetBaseException().Message : "canceled";
                        else if (task.Result == DependencyStatus.Available) ok = true;
                        else why = task.Result.ToString();
                    }
                    catch (Exception e) { why = e.Message; }
                    Finish(ok, why);
                });
            }
            catch (Exception e)
            {
                Finish(false, e.Message);
            }
        }

        /// <summary>Runs a once Firebase is ready (now if it already is); never if it failed.</summary>
        public static void WhenReady(Action a)
        {
            if (Ready) Safe(a);
            else if (!Failed) OnReady += a;
        }

        /// <summary>Runs a with the outcome once Init has finished (now if it already has).</summary>
        public static void WhenDone(Action<bool> a)
        {
            if (Ready || Failed) Safe(() => a(Ready));
            else done += a;
        }

        static void Finish(bool ok, string why)
        {
            if (Ready || Failed) return;
            Ready = ok;
            Failed = !ok;
            Debug.Log("[Firebase] " + (ok ? "ready" : "not available: " + why + " (analytics and crash reports are off)"));

            var r = OnReady;
            OnReady = null;
            var d = done;
            done = null;
            if (ok && r != null)
                foreach (Action a in r.GetInvocationList()) Safe(a);
            if (d != null)
                foreach (Action<bool> a in d.GetInvocationList()) Safe(() => a(ok));
        }

        /// <summary>One listener failing must not stop the others (or the game).</summary>
        static void Safe(Action a)
        {
            try { a(); }
            catch (Exception e) { Debug.LogWarning("[Firebase] listener failed: " + e.Message); }
        }
    }
}
