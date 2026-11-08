using System;
using System.Collections.Generic;
using Firebase.Crashlytics;
using UnityEngine;

namespace ArrowPuzzle
{
    /// <summary>
    /// The only place that touches Firebase Crashlytics. Log / SetKey / Breadcrumb calls made before Firebase is ready
    /// are kept (keys: last value wins; logs: the last MaxLogs) and sent once it is. Keys set by the game: level, type,
    /// ads_test_mode, screen. Every call is wrapped: Crashlytics never breaks the game.
    /// </summary>
    public static class CrashReporter
    {
        const int MaxLogs = 30;

        static readonly Dictionary<string, string> pendingKeys = new Dictionary<string, string>();
        static readonly Queue<string> pendingLogs = new Queue<string>();
        static bool hooked;

        public static void Log(string message)
        {
            Hook();
            if (FirebaseBoot.Failed) return;
            if (!FirebaseBoot.Ready)
            {
                if (pendingLogs.Count >= MaxLogs) pendingLogs.Dequeue();
                pendingLogs.Enqueue(message);
                return;
            }
            try { Crashlytics.Log(message); }
            catch (Exception e) { Debug.LogWarning("[Crashlytics] log failed: " + e.Message); }
        }

        public static void SetKey(string key, string value)
        {
            Hook();
            if (FirebaseBoot.Failed) return;
            if (!FirebaseBoot.Ready)
            {
                pendingKeys[key] = value;
                return;
            }
            try { Crashlytics.SetCustomKey(key, value ?? ""); }
            catch (Exception e) { Debug.LogWarning("[Crashlytics] key failed: " + e.Message); }
        }

        /// <summary>Every screen / popup change (GameApp): a log line and the "screen" key.</summary>
        public static void Breadcrumb(string screenName)
        {
            Log("screen: " + screenName);
            SetKey("screen", screenName);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Development only (never compiled into release, called from nowhere): a test exception for the dashboard.</summary>
        public static void TestCrash()
        {
            Log("TestCrash requested");
            throw new InvalidOperationException("CrashReporter.TestCrash: test exception for Crashlytics");
        }
#endif

        static void Hook()
        {
            if (hooked) return;
            hooked = true;
            FirebaseBoot.WhenReady(Flush);
        }

        static void Flush()
        {
            try
            {
                Crashlytics.SetCustomKey("ads_test_mode", Ads.TestMode ? "true" : "false");
                foreach (var kv in pendingKeys) Crashlytics.SetCustomKey(kv.Key, kv.Value ?? "");
                while (pendingLogs.Count > 0) Crashlytics.Log(pendingLogs.Dequeue());
            }
            catch (Exception e) { Debug.LogWarning("[Crashlytics] flush failed: " + e.Message); }
            pendingKeys.Clear();
            pendingLogs.Clear();
        }
    }
}
