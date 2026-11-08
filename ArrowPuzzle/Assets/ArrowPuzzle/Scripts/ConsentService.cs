using System;
using System.Collections.Generic;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace ArrowPuzzle
{
    /// <summary>What the user allowed (Google consent mode signals), read from the UMP / IAB TCF result.</summary>
    public struct ConsentResult
    {
        public bool AnalyticsStorage, AdStorage, AdUserData, AdPersonalization;

        public static ConsentResult AllGranted
        {
            get { return new ConsentResult { AnalyticsStorage = true, AdStorage = true, AdUserData = true, AdPersonalization = true }; }
        }

        public override string ToString()
        {
            return "analytics " + G(AnalyticsStorage) + ", ad storage " + G(AdStorage) + ", ad user data " + G(AdUserData) +
                   ", ad personalization " + G(AdPersonalization);
        }

        static string G(bool b) { return b ? "granted" : "denied"; }
    }

    /// <summary>
    /// Google UMP consent at launch (AppStartup): updates the consent info and shows Google's form only where it is
    /// required (EEA / UK / Switzerland), then reports CanRequestAds and the consent signals. Never blocks the game:
    /// the info update times out after UpdateTimeout s and the form wait after FormTimeout s (the result still applies
    /// when the user finishes later: Changed). Editor and non-Android: no UMP, everything granted.
    /// </summary>
    public static class ConsentService
    {
        const float UpdateTimeout = 8f, FormTimeout = 60f; // s, unscaled

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // ---- UMP debug (Editor / development builds only; not compiled into release) ----
        /// <summary>true = UMP acts as if this device were in the EEA (the form shows), for testing outside the EEA.</summary>
        const bool DebugForceEea = false;

        /// <summary>
        /// This test device's hashed ID, required for DebugForceEea. Run a development build once with DebugForceEea
        /// = true: logcat prints "Use new ConsentDebugSettings.Builder().addTestDeviceHashedId("...")"; paste it here.
        /// </summary>
        const string DebugTestDeviceHashedId = "";

        static bool ForceEea { get { return DebugForceEea; } } // property: no unreachable-code warning
#endif

        public static bool CanRequestAds { get; private set; }
        public static ConsentResult Result { get; private set; }

        /// <summary>Raised when the consent result arrives after Gather has already reported (late form answer).</summary>
        public static event Action Changed;

        static bool started, reported;
        static int stage; // 1 = info update, 2 = form: a watchdog only fires for the stage it was started for
        static Action onDone;

        static bool UseUmp { get { return Application.platform == RuntimePlatform.Android; } }

        /// <summary>Once per launch. done runs once: when consent is known, or at a timeout.</summary>
        public static void Gather(Action done)
        {
            if (started) { if (reported) done(); else onDone += done; return; }
            started = true;
            onDone = done;

            if (!UseUmp)
            {
                CanRequestAds = true;
                Result = ConsentResult.AllGranted;
                Debug.Log("[Consent] no UMP on " + Application.platform + ": " + Result);
                Report();
                return;
            }

            stage = 1;
            GameApp.I.StartCoroutine(Watchdog(UpdateTimeout, "consent info update", 1));
            try
            {
                var request = new ConsentRequestParameters { TagForUnderAgeOfConsent = false };
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (ForceEea)
                {
                    request.ConsentDebugSettings = new ConsentDebugSettings
                    {
                        DebugGeography = DebugGeography.EEA,
                        TestDeviceHashedIds = new List<string> { DebugTestDeviceHashedId },
                    };
                    Debug.LogWarning("[Consent] DEBUG: geography forced to EEA" +
                                     (DebugTestDeviceHashedId.Length == 0 ? " (no test device ID set: UMP ignores it)" : ""));
                }
#endif
                ConsentInformation.Update(request, error => Ads.OnMain(() =>
                {
                    if (error != null) Debug.Log("[Consent] info update failed: " + error.Message);
                    ShowFormIfRequired();
                }));

                // Google's recommended early start: consent gathered in an earlier session already allows ad requests
                // now, while this launch's update runs. Finish() re-reads it when the update / form is done.
                bool early = false;
                try { early = ConsentInformation.CanRequestAds(); } catch (Exception) { }
                if (early && !reported)
                {
                    CanRequestAds = true;
                    Debug.Log("[Consent] ad requests allowed now (consent from a previous session); update still running");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Consent] UMP unavailable: " + e.Message);
                Finish();
            }
        }

        static void ShowFormIfRequired()
        {
            stage = 2;
            if (!reported) GameApp.I.StartCoroutine(Watchdog(FormTimeout, "consent form", 2));
            try
            {
                ConsentForm.LoadAndShowConsentFormIfRequired(error => Ads.OnMain(() =>
                {
                    if (error != null) Debug.Log("[Consent] form: " + error.Message);
                    Finish();
                }));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Consent] form failed: " + e.Message);
                Finish();
            }
        }

        /// <summary>Reads the outcome. A late outcome (after a timeout already reported) raises Changed.</summary>
        static void Finish()
        {
            try { CanRequestAds = ConsentInformation.CanRequestAds(); }
            catch (Exception) { CanRequestAds = false; }
            Result = ReadTcf();
            Debug.Log("[Consent] done: can request ads " + CanRequestAds + "; " + Result);
            if (reported)
            {
                var c = Changed;
                if (c != null) c();
                return;
            }
            Report();
        }

        static void Report()
        {
            if (reported) return;
            reported = true;
            var d = onDone;
            onDone = null;
            if (d != null) d();
        }

        static System.Collections.IEnumerator Watchdog(float seconds, string what, int forStage)
        {
            float t = 0f;
            while (t < seconds && !reported && stage == forStage) { t += Time.unscaledDeltaTime; yield return null; }
            if (reported || stage != forStage) yield break;
            Debug.LogWarning("[Consent] " + what + " gave no answer in " + seconds + " s: continuing without waiting");
            try { CanRequestAds = ConsentInformation.CanRequestAds(); } catch (Exception) { CanRequestAds = false; }
            Result = ReadTcf();
            Report();
        }

        /// <summary>True when UMP says this user must be able to reopen the consent choices (Settings shows the row).</summary>
        public static bool PrivacyOptionsRequired
        {
            get
            {
                if (!UseUmp) return false;
                try { return ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required; }
                catch (Exception) { return false; }
            }
        }

        /// <summary>
        /// Settings "Privacy options": shows UMP's privacy options form; when it closes, re-reads the consent state and
        /// raises Changed (AppStartup applies it to Analytics and starts ads if they now may run). closed runs either way.
        /// </summary>
        public static void ShowPrivacyOptions(Action closed)
        {
            if (!UseUmp) { if (closed != null) closed(); return; }
            try
            {
                ConsentForm.ShowPrivacyOptionsForm(error => Ads.OnMain(() =>
                {
                    if (error != null) Debug.Log("[Consent] privacy options: " + error.Message);
                    try { CanRequestAds = ConsentInformation.CanRequestAds(); } catch (Exception) { CanRequestAds = false; }
                    Result = ReadTcf();
                    Debug.Log("[Consent] privacy options closed: can request ads " + CanRequestAds + "; " + Result);
                    var c = Changed;
                    if (c != null) c();
                    if (closed != null) closed();
                }));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Consent] privacy options failed: " + e.Message);
                if (closed != null) closed();
            }
        }

        /// <summary>
        /// Consent mode from the IAB TCF v2 strings UMP stores in the default SharedPreferences. GDPR not applying =
        /// all granted. Otherwise purpose 1 (store / access) = analytics and ad storage, purposes 1 + 7 = ad user data,
        /// purposes 3 + 4 = ad personalization (Google's documented mapping). No TCF data yet (the form is still open,
        /// or the update failed): granted only if UMP says consent is not required, otherwise all denied.
        /// </summary>
        static ConsentResult ReadTcf()
        {
            int gdpr = -1;
            string purposes = "";
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var pm = new AndroidJavaClass("android.preference.PreferenceManager"))
                using (var prefs = pm.CallStatic<AndroidJavaObject>("getDefaultSharedPreferences", activity))
                {
                    gdpr = prefs.Call<int>("getInt", "IABTCF_gdprApplies", -1);
                    purposes = prefs.Call<string>("getString", "IABTCF_PurposeConsents", "") ?? "";
                }
            }
            catch (Exception e)
            {
                Debug.Log("[Consent] TCF data unavailable: " + e.Message);
                gdpr = -1;
            }
            if (gdpr == 0) return ConsentResult.AllGranted;
            if (gdpr < 0)
            {
                bool notRequired = false;
                try { notRequired = ConsentInformation.ConsentStatus == ConsentStatus.NotRequired; } catch (Exception) { }
                Debug.Log("[Consent] no TCF data yet: UMP status says consent " + (notRequired ? "not required -> granted" : "required or unknown -> denied"));
                return notRequired ? ConsentResult.AllGranted : new ConsentResult();
            }

            Func<int, bool> p = n => purposes.Length >= n && purposes[n - 1] == '1';
            return new ConsentResult
            {
                AnalyticsStorage = p(1),
                AdStorage = p(1),
                AdUserData = p(1) && p(7),
                AdPersonalization = p(3) && p(4),
            };
        }
    }
}
