using System;
using System.Collections.Generic;
using Firebase.Analytics;
using UnityEngine;

namespace ArrowPuzzle
{
    /// <summary>
    /// The only place that touches Firebase Analytics. Events logged before Firebase is ready are queued (at most
    /// MaxQueue, oldest dropped) and sent once it is, after the UMP consent is applied: analytics consent denied =
    /// SetAnalyticsCollectionEnabled(false) and nothing is sent. Every call is wrapped: Analytics never breaks the game.
    /// Event and parameter names: lowercase with underscores.
    /// </summary>
    public static class AnalyticsService
    {
        const int MaxQueue = 50;

        struct Pending
        {
            public string name;
            public KeyValuePair<string, object>[] args;
        }

        static readonly Queue<Pending> queue = new Queue<Pending>();
        static ConsentResult consent = ConsentResult.AllGranted;
        static bool consentKnown, hooked;

        // ------------------------------------------------------------------ events

        public static void LevelStart(int level, string type, string difficulty)
        {
            Log("level_start", P("level", level), P("type", type), P("difficulty", difficulty));
        }

        public static void LevelComplete(int level, string type, int stars, float timeSec, bool newBest)
        {
            Log("level_complete", P("level", level), P("type", type), P("stars", stars),
                P("time_sec", Mathf.RoundToInt(timeSec)), P("new_best", newBest ? 1 : 0));
        }

        public static void LevelFail(int level, string type, int arrowsLeft)
        {
            Log("level_fail", P("level", level), P("type", type), P("arrows_left", arrowsLeft));
        }

        public static void HintUsed(int level) { Log("hint_used", P("level", level)); }
        public static void ReviveAdOffered(int level) { Log("revive_ad_offered", P("level", level)); }
        public static void ReviveAdDone(int level) { Log("revive_ad_done", P("level", level)); }

        /// <summary>kind win / lose, size medium / small / none, reason shown / no_fit / off / load_failed / timeout / ...</summary>
        public static void NativeAdResult(string kind, string size, string reason)
        {
            Log("native_ad_result", P("kind", kind), P("size", size), P("reason", reason));
        }

        /// <summary>
        /// One per result popup show: screen bonus / win / lose; result shown / no_fit / off / load_failed / timeout /
        /// show_failed / closed_early; load_ms = how long the ad's load took (-1 when it never loaded).
        /// </summary>
        public static void MrecAdResult(string screen, string result, int loadMs)
        {
            Log("mrec_ad_result", P("screen", screen), P("result", result), P("load_ms", loadMs));
        }

        public static void SettingsChanged(string setting, string value)
        {
            Log("settings_changed", P("setting", setting), P("value", value));
        }

        // Level complete bonus wheel (PopupWin): shown once per win when the pointer starts, the Claim tap, then exactly
        // one of completed (coins = the bonus added on top of the base win reward) / failed.
        public static void WinWheelShown(int level) { Log("win_wheel_shown", P("level", level)); }
        public static void WinWheelClaimTap(int level, int mult) { Log("win_wheel_claim_tap", P("level", level), P("mult", mult)); }
        public static void WinWheelAdCompleted(int level, int mult, int coins)
        {
            Log("win_wheel_ad_completed", P("level", level), P("mult", mult), P("coins", coins));
        }
        public static void WinWheelAdFailed(int level, int mult) { Log("win_wheel_ad_failed", P("level", level), P("mult", mult)); }
        /// <summary>NEXT LEVEL tapped; waited = it unlocked by its countdown ("true") or by the collected bonus ("false").</summary>
        public static void WinNextTap(int level, bool waited) { Log("win_next_tap", P("level", level), P("waited", waited ? "true" : "false")); }

        /// <summary>placement first (launch) / resume (back in the app).</summary>
        public static void AppOpenAdShown(string placement) { Log("app_open_ad_shown", P("placement", placement)); }

        // ------------------------------------------------------------------ consent

        /// <summary>The UMP result (AppStartup; again if the user answers the form late). Applied once Firebase is ready.</summary>
        public static void ApplyConsent(ConsentResult c)
        {
            consent = c;
            consentKnown = true;
            Hook();
            if (FirebaseBoot.Ready) Flush(); // consent + anything queued
        }

        static void PushConsent()
        {
            try
            {
                FirebaseAnalytics.SetAnalyticsCollectionEnabled(consent.AnalyticsStorage);
                FirebaseAnalytics.SetConsent(new Dictionary<ConsentType, ConsentStatus>
                {
                    { ConsentType.AnalyticsStorage, S(consent.AnalyticsStorage) },
                    { ConsentType.AdStorage, S(consent.AdStorage) },
                    { ConsentType.AdUserData, S(consent.AdUserData) },
                    { ConsentType.AdPersonalization, S(consent.AdPersonalization) },
                });
                Debug.Log("[Analytics] consent applied: " + consent + (consent.AnalyticsStorage ? "" : " (collection off)"));
            }
            catch (Exception e) { Debug.LogWarning("[Analytics] consent failed: " + e.Message); }
        }

        static ConsentStatus S(bool granted) { return granted ? ConsentStatus.Granted : ConsentStatus.Denied; }

        // ------------------------------------------------------------------ plumbing

        static KeyValuePair<string, object> P(string key, object value)
        {
            return new KeyValuePair<string, object>(key, value);
        }

        static void Log(string name, params KeyValuePair<string, object>[] args)
        {
            Hook();
            if (FirebaseBoot.Failed) return;
            if (!FirebaseBoot.Ready || !consentKnown)
            {
                if (queue.Count >= MaxQueue) queue.Dequeue();
                queue.Enqueue(new Pending { name = name, args = args });
                return;
            }
            Send(name, args);
        }

        static void Send(string name, KeyValuePair<string, object>[] args)
        {
            if (!consent.AnalyticsStorage) return; // collection is off: the SDK would drop it anyway
            try
            {
                var ps = new Parameter[args.Length];
                for (int i = 0; i < args.Length; i++)
                {
                    var a = args[i];
                    if (a.Value is int) ps[i] = new Parameter(a.Key, (long)(int)a.Value);
                    else if (a.Value is long) ps[i] = new Parameter(a.Key, (long)a.Value);
                    else if (a.Value is float || a.Value is double) ps[i] = new Parameter(a.Key, Convert.ToDouble(a.Value));
                    else ps[i] = new Parameter(a.Key, a.Value != null ? a.Value.ToString() : "");
                }
                FirebaseAnalytics.LogEvent(name, ps);
            }
            catch (Exception e) { Debug.LogWarning("[Analytics] " + name + " failed: " + e.Message); }
        }

        /// <summary>Once: when Firebase is ready, apply consent and send the queue (if consent is known by then).</summary>
        static void Hook()
        {
            if (hooked) return;
            hooked = true;
            FirebaseBoot.WhenReady(Flush);
        }

        static void Flush()
        {
            if (!consentKnown) return; // ApplyConsent flushes when it arrives
            PushConsent();
            int n = queue.Count;
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                Send(p.name, p.args);
            }
            if (n > 0) Debug.Log("[Analytics] sent " + n + " queued event(s)" + (consent.AnalyticsStorage ? "" : " (dropped: no consent)"));
        }
    }
}
