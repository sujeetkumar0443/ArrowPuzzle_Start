using System.Collections;
using UnityEngine;

namespace ArrowPuzzle
{
    /// <summary>
    /// Launch order, run from GameApp.Awake without blocking the splash:
    /// 1. UMP consent (ConsentService, with its own timeouts), with Firebase (FirebaseBoot: Analytics, Crashlytics)
    ///    starting in parallel; it never delays ads, and Analytics still waits for the consent result.
    /// 2. Ads.Init (held until now; loads asked for meanwhile, e.g. the splash's app open ad, start then) the moment
    ///    UMP allows ad requests (a previous session's consent counts).
    /// Ads start only if UMP says ads may be requested; a late consent answer can still start them.
    /// </summary>
    public static class AppStartup
    {
        static bool started, adsStarted, sequenceDone, consentDone;
        static float beganAt;

        /// <summary>Consent is known and does not allow ad requests (ads not started): no ad is coming now.</summary>
        public static bool AdsBlocked { get { return consentDone && !adsStarted && !ConsentService.CanRequestAds; } }

        public static void Begin()
        {
            if (started) return;
            started = true;
            Ads.HoldInit();
            GameApp.I.StartCoroutine(Run());
        }

        static IEnumerator Run()
        {
            beganAt = Time.realtimeSinceStartup;
            FirebaseBoot.Init();
            ConsentService.Gather(OnConsentDone);

            // the moment UMP allows ad requests (early, from a previous session's consent, or this launch's result)
            while (!consentDone && !ConsentService.CanRequestAds) yield return null;
            sequenceDone = true;
            if (!ConsentService.CanRequestAds)
            {
                Debug.LogWarning("[Startup] ads not started: UMP consent does not allow ad requests yet");
                yield break;
            }
            Debug.Log("[Startup] ad requests allowed after " + Since() + " s" + (consentDone ? "" : " (previous session's consent)"));
            StartAds();
        }

        static void OnConsentDone()
        {
            consentDone = true;
            AnalyticsService.ApplyConsent(ConsentService.Result);
            Ads.ApplyConsent(ConsentService.Result); // LevelPlay GDPR flag (kept until LevelPlay starts)
            ConsentService.Changed += OnLateConsent;
        }

        static string Since() { return (Time.realtimeSinceStartup - beganAt).ToString("0.00"); }

        static void StartAds()
        {
            if (adsStarted) return;
            if (!ConsentService.CanRequestAds)
            {
                Debug.LogWarning("[Startup] ads not started: UMP consent does not allow ad requests yet");
                return;
            }
            adsStarted = true;
            Ads.ReleaseInit();
        }

        /// <summary>The user answered the consent form after the timeout: new signals, and ads if they were waiting.</summary>
        static void OnLateConsent()
        {
            AnalyticsService.ApplyConsent(ConsentService.Result);
            Ads.ApplyConsent(ConsentService.Result);
            if (sequenceDone) StartAds();
        }
    }
}
