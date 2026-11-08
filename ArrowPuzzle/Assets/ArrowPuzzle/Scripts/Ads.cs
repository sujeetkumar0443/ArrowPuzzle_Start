using System;
using System.Collections;
using System.Collections.Generic;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using Unity.Services.LevelPlay;
using UnityEngine;

namespace ArrowPuzzle
{
    /// <summary>The nine ad placements, one ad unit ID each (AdIds). NativeBonus = the Bonus Spin screen's small native.</summary>
    public enum AdSlot { BannerHome, BannerGame, AppOpenFirst, AppOpenResume, NativeWin, NativeLose, Interstitial, Rewarded, NativeBonus }

    /// <summary>
    /// Every ad unit ID, in one place. TestMode = true uses Google's test units; false uses the real set, where an
    /// empty ID means "this ad is not available" (a test ID is never used with TestMode = false).
    /// The IDs come only from the constants below.
    /// </summary>
    public static class AdIds
    {
        // Google's Android test units
        const string TestAppOpen = "ca-app-pub-3940256099942544/9257395921";
        const string TestBanner = "ca-app-pub-3940256099942544/6300978111";
        const string TestInterstitial = "ca-app-pub-3940256099942544/1033173712";
        const string TestRewarded = "ca-app-pub-3940256099942544/5224354917";
        const string TestNative = "ca-app-pub-3940256099942544/2247696110";

        // paste real AdMob IDs here; empty = that ad is off
        const string RealBannerHome = "";
        const string RealBannerGame = "";
        const string RealAppOpenFirst = "";
        const string RealAppOpenResume = "";
        const string RealNativeWin = "";
        const string RealNativeLose = "";
        const string RealInterstitial = "";
        const string RealRewarded = "";
        const string RealNativeBonus = "";

        /// <summary>
        /// LevelPlay (mediation) Android App Key, from the LevelPlay dashboard (Apps). The same key in test and release:
        /// LevelPlay test devices are set in the dashboard. Empty = LevelPlay off (banner / interstitial / rewarded /
        /// MREC), with a warning in the log. Leave "Ads Mediation > LevelPlay Settings > App Key / init on start" empty
        /// and off: Ads.Init starts LevelPlay itself, after UMP consent.
        /// </summary>
        public const string LevelPlayAppKey = "";

        // LevelPlay ad unit IDs (LevelPlay dashboard > Ad units), the same in test and release: test ads come from
        // the test devices (LevelPlay dashboard, AdMobTestDevices below). Empty = that ad is off (a log says so).
        public const string LevelPlayBanner = "";       // the bottom banner on every banner screen (Home, Settings, game)
        public const string LevelPlayInterstitial = "";
        public const string LevelPlayRewarded = "";
        public const string LevelPlayMrecBonus = "";    // 300x250 under the Bonus Spin wheel
        public const string LevelPlayMrecWin = "";      // 300x250 on Level complete
        public const string LevelPlayMrecLose = "";     // 300x250 on Level failed

        /// <summary>
        /// AdMob test devices (Ads.TestMode only): the hashed IDs logcat prints on a real device as
        /// "Use RequestConfiguration.Builder().setTestDeviceIds(Arrays.asList("...")". AdMob then serves test ads to
        /// these devices, also through the LevelPlay AdMob adapter. Never used with TestMode = false.
        /// </summary>
        public static readonly string[] AdMobTestDevices = { };

        static readonly string[] Test =
            { TestBanner, TestBanner, TestAppOpen, TestAppOpen, TestNative, TestNative, TestInterstitial, TestRewarded, TestNative };
        static readonly string[] RealIds =
            { RealBannerHome, RealBannerGame, RealAppOpenFirst, RealAppOpenResume, RealNativeWin, RealNativeLose, RealInterstitial, RealRewarded,
              RealNativeBonus };

        /// <summary>The ID used for a slot: the test unit in TestMode, else the real const (empty = off).</summary>
        public static string Get(AdSlot slot)
        {
            int i = (int)slot;
            return Ads.TestMode ? Test[i] : RealIds[i];
        }

        public static string BannerHome { get { return Get(AdSlot.BannerHome); } }
        public static string BannerGame { get { return Get(AdSlot.BannerGame); } }
        public static string AppOpenFirst { get { return Get(AdSlot.AppOpenFirst); } }
        public static string AppOpenResume { get { return Get(AdSlot.AppOpenResume); } }
        public static string NativeWin { get { return Get(AdSlot.NativeWin); } }
        public static string NativeLose { get { return Get(AdSlot.NativeLose); } }
        public static string Interstitial { get { return Get(AdSlot.Interstitial); } }
        public static string Rewarded { get { return Get(AdSlot.Rewarded); } }
        public static string NativeBonus { get { return Get(AdSlot.NativeBonus); } }
    }

    /// <summary>
    /// The native ad slots: under the Level complete card (Win), under the Level failed card (Lose), and the small
    /// full-width ad at the bottom of the Bonus Spin screen (Bonus, NativeAdStrip). One on screen at a time.
    /// </summary>
    public enum NativeKind { Win, Lose, Bonus }

    /// <summary>The three 300x250 MREC placements (LevelPlay), one ad unit each. One MREC on screen at a time.</summary>
    public enum MrecKind { Bonus, Win, Lose }

    /// <summary>
    /// Off: no MREC possible (no ad unit ID, LevelPlay off). Idle: nothing loaded or loading. Failed: the last load
    /// failed (a retry may be pending). Ready: loaded (hidden until ShowMrec).
    /// </summary>
    public enum MrecState { Off, Idle, Loading, Ready, Failed }

    /// <summary>
    /// Off: no native ad possible (Editor / not Android, SDK init failed, empty ID). Idle: nothing loaded or loading.
    /// Failed: the last load failed (a retry may be pending).
    /// </summary>
    public enum NativeState { Off, Idle, Loading, Ready, Failed }

    /// <summary>Native template on a result screen: none (no room), small or medium.</summary>
    public enum NativeSize { None, Small, Medium }

    /// <summary>
    /// Where the native ad goes on a result screen (Ads.PlanNative). X / Y / W and MediumH / SmallH are the dp values
    /// given to the SDK; InnerTop / InnerWidth are the same rectangle in design px (from the safe-area top) for the frame
    /// and its placeholder.
    /// </summary>
    public struct NativePlan
    {
        public NativeSize Size;                     // what fits
        public float FreeDp, MediumDp, SmallDp;     // free height (frame top -> bottom inset), template heights
        public float NeedMediumDp, NeedSmallDp;     // template + 2 x padding + bottom margin
        public int X, Y, W, MediumH, SmallH;        // dp: ad left, top, width, planned height per template
        public int MediumMaxH;                      // dp: the tallest medium ad that keeps the frame above the inset
        public float BottomInsetPx;                 // Unity px kept free at the bottom (safe area / nav bar / gesture area)
        public float InnerTop, InnerWidth;          // design px: ad top from the safe-area top, ad width
    }

    /// <summary>
    /// The only class that talks to the Google Mobile Ads SDK (on every platform; in the Editor the plugin shows its
    /// own placeholder ads). Every SDK callback is moved to the main thread (OnMain). Init() once at start, then the
    /// ad types below. A missing or failing ad never blocks the game: the caller's callback still runs.
    /// Set TestMode = false before publishing (and paste the real IDs into AdIds).
    /// </summary>
    public static class Ads
    {
        public static bool TestMode = true;

        /// <summary>One-line banner state at the top-left of Home and the game screen (BannerDebug). Set false before release.</summary>
        public const bool ShowBannerDebug = false;

        /// <summary>Raise this if you want a minimum time (s) between two interstitials.</summary>
        public const int InterstitialMinGapSeconds = 0;

        /// <summary>No interstitial within this many s after an app open ad closed.</summary>
        const float AfterAppOpenGap = 10f;

        static float appOpenClosedAt = -1000f; // unscaled time the last app open ad closed

        /// <summary>Background time (s) needed before a return shows the app open ad. Raise this to show it less often.</summary>
        public const int AppOpenMinBackgroundSeconds = 10;

        /// <summary>Minimum time (s) between two app open ads (closed to next shown).</summary>
        public const int AppOpenMinGapSeconds = 60;

        const float AfterFullScreenIgnore = 3f; // s: a foreground event this soon after any full-screen ad closed is that ad's
        const float AfterSplashIgnore = 10f;    // s: no return ad this soon after the splash (and its launch ad) ended

        // real time (DateTime.UtcNow): unscaled game time stops while the app is in the background
        static DateTime backgroundAt, fullScreenEndedAt = DateTime.MinValue, appOpenClosedReal = DateTime.MinValue;
        static DateTime splashEndedAt = DateTime.MinValue;
        static DateTime appOpenSuppressedUntil = DateTime.MinValue; // SuppressAppOpenFor
        static bool inBackground, backgroundDuringAd, backgroundForLink, splashOnScreen;
        static int awayUntilFrame = -1;

        // ------------------------------------------------------------------ SDK start

        /// <summary>True once MobileAds.Initialize has finished.</summary>
        public static bool Ready { get; private set; }

        /// <summary>True when Initialize threw; no ad loads after that.</summary>
        public static bool InitFailed { get; private set; }

        /// <summary>What Initialize threw (InitFailed), for the debug line.</summary>
        public static string InitError { get; private set; }

        static bool initStarted, initHeld, initAsked;
        static event Action initDone; // loads that were asked for before Ready

        /// <summary>
        /// Startup (AppStartup): Init and every load wait until ReleaseInit, so the SDK starts only once UMP allows ad
        /// requests. Loads asked for meanwhile are queued (WhenReady).
        /// </summary>
        public static void HoldInit()
        {
            if (!initStarted) initHeld = true;
        }

        /// <summary>UMP allows ad requests: starts the SDK now.</summary>
        public static void ReleaseInit()
        {
            if (!initHeld) return;
            initHeld = false;
            Log("init released" + (initAsked ? " (asked for earlier)" : ""));
            Init();
        }

        /// <summary>Starts the SDK once (later calls do nothing; deferred while held). Never throws.</summary>
        public static void Init()
        {
            if (initStarted) return;
            if (initHeld)
            {
                if (!initAsked) Log("init deferred: waiting for consent");
                initAsked = true;
                return;
            }
            initStarted = true;
            Log("init start (TestMode " + TestMode + ")");
            AdsRunner.Ensure(); // app pause / resume tracking
            ApplyAdMobTestDevices(); // before either SDK starts (the LevelPlay AdMob adapter uses the same AdMob SDK)
            Mediation.Init();        // LevelPlay: independent of the Google Mobile Ads init below (app open)
            BannerAd.Preload();      // LevelPlay, first in line: Home shows it right after the splash
            LoadInterstitial();      // LevelPlay: these start once LevelPlay is ready: one of each always ready
            LoadRewarded();
            try
            {
                MobileAds.Initialize(status => OnMain(() =>
                {
                    Ready = true;
                    Log("init done");
                    var pending = initDone;
                    initDone = null;
                    if (pending != null) pending();
                }));
            }
            catch (Exception e)
            {
                InitFailed = true;
                initDone = null;
                InitError = e.GetType().Name + ": " + e.Message;
                Debug.LogWarning("[Ads] init failed: " + e);
                return;
            }
            // separate from Initialize: if the app state events are not available only the return ad is lost
            try { AppStateEventNotifier.AppStateChanged += state => OnMain(() => OnAppState(state)); }
            catch (Exception e) { Debug.LogWarning("[Ads] app state events unavailable (no app open ad on return): " + e); }
        }

        /// <summary>TestMode only: registers AdIds.AdMobTestDevices with the AdMob SDK (keeps the rest of its request configuration).</summary>
        static void ApplyAdMobTestDevices()
        {
            if (!TestMode) return;
            if (AdIds.AdMobTestDevices.Length == 0)
            {
                Log("TestMode: no AdMob test device IDs in AdIds.AdMobTestDevices (AdMob test units still serve test ads; " +
                    "real units through LevelPlay need this device registered)");
                return;
            }
            try
            {
                var config = MobileAds.GetRequestConfiguration() ?? new RequestConfiguration();
                config.TestDeviceIds = new List<string>(AdIds.AdMobTestDevices);
                MobileAds.SetRequestConfiguration(config);
                Log("TestMode: " + AdIds.AdMobTestDevices.Length + " AdMob test device ID(s) registered");
            }
            catch (Exception e) { Debug.LogWarning("[Ads] AdMob test devices not set: " + e.Message); }
        }

        /// <summary>Runs a load now when the SDK is ready, after Init otherwise. False when ads can't load at all.</summary>
        internal static bool WhenReady(Action load)
        {
            if (InitFailed) return false;
            if (!initStarted) Init();
            if (InitFailed) return false;
            if (Ready) load();
            else initDone += load;
            return true;
        }

        /// <summary>SDK callbacks can arrive on a background thread: run them in the next Unity Update.</summary>
        internal static void OnMain(Action a)
        {
            MobileAdsEventExecutor.ExecuteInUpdate(a);
        }

        internal static void Log(string msg)
        {
            Debug.Log("[Ads] " + msg);
        }

        // ------------------------------------------------------------------ LevelPlay (mediation)

        /// <summary>
        /// TestMode only: opens the LevelPlay test suite right after LevelPlay has started (to check that AdMob loads).
        /// Leave false for normal testing: the suite covers the game. Ignored with TestMode = false.
        /// </summary>
        public const bool OpenTestSuiteAtLaunch = false;

        /// <summary>
        /// The app's audience, passed to LevelPlay before it starts (COPPA, and AdMob's child-directed / under-age tags
        /// through the adapter). The game is not directed at children; UMP uses the same "not under age" setting.
        /// </summary>
        static readonly bool ChildDirected = false, UnderAgeOfConsent = false; // readonly, not const: no constant-condition warnings

        /// <summary>True once LevelPlay.Init has reported success. Banner / interstitial / rewarded / MREC load only then.</summary>
        public static bool LevelPlayReady { get { return Mediation.Ready; } }

        /// <summary>LevelPlay can't run this session: no App Key, or its init failed for good (after the retries).</summary>
        public static bool LevelPlayOff { get { return Mediation.Off; } }

        /// <summary>Why LevelPlay is off or not ready yet (for logs / debug line), null when it is ready.</summary>
        public static string LevelPlayError { get { return Mediation.Error; } }

        /// <summary>
        /// UMP result (AppStartup, on the first result and on every later change): passed to LevelPlay as its GDPR
        /// consent. Before LevelPlay starts it is kept and applied right before Init, as LevelPlay requires.
        /// </summary>
        public static void ApplyConsent(ConsentResult result) { Mediation.ApplyConsent(result); }

        /// <summary>TestMode only: opens the LevelPlay test suite (LevelPlay must be ready). A no-op with a log otherwise.</summary>
        public static void LaunchTestSuite() { Mediation.LaunchTestSuite(); }

        /// <summary>Runs a LevelPlay load now when LevelPlay is ready, after its init otherwise. False when it is off.</summary>
        internal static bool WhenLevelPlayReady(Action load) { return Mediation.WhenReady(load); }

        /// <summary>
        /// LevelPlay start-up. Started from Ads.Init, so only after UMP allows ad requests (AppStartup's hold). Order,
        /// as LevelPlay requires: privacy flags (GDPR consent, COPPA, AdMob child / under-age tags) and, in TestMode, the
        /// test suite flag and adapter debug logs, then LevelPlay.Init. A failed init retries after 30 s, then 60 s.
        /// LevelPlay posts its callbacks to the Unity main thread itself.
        /// </summary>
        static class Mediation
        {
            static readonly float[] RetryDelays = { 30f, 60f }; // after failed try 1 and 2; try 3 failing stops

            static bool started, consentKnown;
            static int failures;
            static ConsentResult consent;
            static event Action readyQueue; // loads asked for before init success

            public static bool Ready { get; private set; }
            public static bool Off { get; private set; }
            public static string Error { get; private set; }

            public static void Init()
            {
                if (started) return;
                started = true;
                string key = AdIds.LevelPlayAppKey;
                if (string.IsNullOrEmpty(key))
                {
                    Off = true;
                    Error = "no App Key";
                    Debug.LogWarning("[Ads] LevelPlay OFF: AdIds.LevelPlayAppKey is empty (Ads.cs). Paste the Android App Key " +
                                     "from the LevelPlay dashboard; until then no banner, interstitial, rewarded or MREC ad loads.");
                    RaiseBannerHeightChanged(); // BannerHeightDesign is 0 now: screens drop the banner gap
                    return;
                }
                try
                {
                    LevelPlay.OnInitSuccess += OnInitSuccess; // static handlers: LevelPlay ignores a second add
                    LevelPlay.OnInitFailed += OnInitFailed;
                    SetPrivacy("before init");
                    if (TestMode)
                    {
                        LevelPlay.SetMetaData("is_test_suite", "enable"); // must be set before init for LaunchTestSuite
                        LevelPlay.SetAdaptersDebug(true);
                        Log("LevelPlay TestMode: test suite enabled, adapter debug logs on");
                    }
                }
                catch (Exception e)
                {
                    Fail("setup threw " + e.GetType().Name + ": " + e.Message, false);
                    return;
                }
                Start();
            }

            static void Start()
            {
                Error = "starting";
                Log("LevelPlay init start (package " + LevelPlay.PluginVersion + ", TestMode " + TestMode + ")");
                try { LevelPlay.Init(AdIds.LevelPlayAppKey); }
                catch (Exception e) { Fail("Init threw " + e.GetType().Name + ": " + e.Message, true); }
            }

            static void OnInitSuccess(LevelPlayConfiguration config)
            {
                if (Ready) return;
                Ready = true;
                Off = false;
                Error = null;
                failures = 0;
                Log("LevelPlay init done");
                var pending = readyQueue;
                readyQueue = null;
                if (pending != null) pending();
                if (TestMode && OpenTestSuiteAtLaunch) LaunchTestSuite();
            }

            static void OnInitFailed(LevelPlayInitError error)
            {
                Fail(error != null ? "code " + error.ErrorCode + ": " + error.ErrorMessage : "unknown error", true);
            }

            /// <summary>Logs the failure; retries after 30 s, then 60 s; after that LevelPlay is off for this session.</summary>
            static void Fail(string why, bool retry)
            {
                if (Ready) return;
                failures++;
                if (!retry || failures > RetryDelays.Length)
                {
                    Off = true;
                    Error = "init failed: " + why;
                    readyQueue = null;
                    Debug.LogWarning("[Ads] LevelPlay OFF for this session: init failed (" + why + ", try " + failures + ")");
                    RaiseBannerHeightChanged(); // BannerHeightDesign is 0 now: screens drop the banner gap
                    return;
                }
                float delay = RetryDelays[failures - 1];
                Error = "init failed, retrying";
                Debug.LogWarning("[Ads] LevelPlay init failed: " + why + " (try " + failures + ", retry in " + delay + " s)");
                AdsRunner.Run(RetryAfter(delay));
            }

            static IEnumerator RetryAfter(float delay)
            {
                float t = 0f;
                while (t < delay) { t += Time.unscaledDeltaTime; yield return null; }
                if (!Ready && !Off) Start();
            }

            public static bool WhenReady(Action load)
            {
                if (Off) return false;
                if (!started) Ads.Init(); // held until consent allows ads (Init logs that)
                if (Off) return false;
                if (Ready) load();
                else readyQueue += load;
                return true;
            }

            public static void ApplyConsent(ConsentResult result)
            {
                consent = result;
                consentKnown = true;
                if (started && !Off) SetPrivacy("consent update");
            }

            /// <summary>
            /// GDPR consent = the user allowed ad storage (TCF purpose 1) and personalised ads (purposes 3 + 4); GDPR not
            /// applying counts as granted (ConsentService). Until UMP has answered no GDPR flag is sent at all, so
            /// LevelPlay and the AdMob adapter read the TCF string UMP stored. COPPA and the AdMob tags from the
            /// constants above (keys from LevelPlay's AdMob integration guide; not checkable in the package code).
            /// </summary>
            static void SetPrivacy(string when)
            {
                try
                {
                    if (consentKnown)
                    {
                        bool gdpr = consent.AdStorage && consent.AdPersonalization;
                        LevelPlayPrivacySettings.SetGDPRConsent(gdpr);
                        Log("LevelPlay GDPR consent " + gdpr + " (" + when + "; " + consent + ")");
                    }
                    else Log("LevelPlay GDPR consent not set yet (" + when + "): waiting for the UMP result");
                    LevelPlayPrivacySettings.SetCOPPA(ChildDirected);
                    LevelPlay.SetMetaData("AdMob_TFCD", ChildDirected ? "true" : "false");
                    LevelPlay.SetMetaData("AdMob_TFUA", UnderAgeOfConsent ? "true" : "false");
                }
                catch (Exception e) { Debug.LogWarning("[Ads] LevelPlay privacy flags not set (" + when + "): " + e.Message); }
            }

            public static void LaunchTestSuite()
            {
                if (!TestMode) { Log("LevelPlay test suite not opened: TestMode is false"); return; }
                if (!Ready) { Log("LevelPlay test suite not opened: LevelPlay not ready (" + (Error ?? "?") + ")"); return; }
                try { LevelPlay.LaunchTestSuite(); Log("LevelPlay test suite opened"); }
                catch (Exception e) { Debug.LogWarning("[Ads] LevelPlay test suite failed: " + e.Message); }
            }
        }

        // ------------------------------------------------------------------ full-screen state (all ad types)

        /// <summary>True from the moment a full-screen ad is shown until it closes (or fails to show).</summary>
        public static bool IsFullScreenAdShowing { get; private set; }

        static bool audioWasPaused;

        /// <summary>Marks a full-screen ad as showing and pauses the game audio. Time.timeScale is not touched.</summary>
        internal static void BeginFullScreen()
        {
            if (IsFullScreenAdShowing) return;
            IsFullScreenAdShowing = true;
            audioWasPaused = AudioListener.pause;
            AudioListener.pause = true;
        }

        internal static void EndFullScreen()
        {
            if (!IsFullScreenAdShowing) return;
            IsFullScreenAdShowing = false;
            AudioListener.pause = audioWasPaused;
            fullScreenEndedAt = DateTime.UtcNow;
            MarkReturn();
        }

        // ------------------------------------------------------------------ app away (background / full-screen ad)

        /// <summary>
        /// True on the frame(s) right after the app came back from the background or a full-screen ad closed: their
        /// unscaled delta time is time away, not play time (the solve timer skips them).
        /// </summary>
        public static bool JustReturned { get { return Time.frameCount <= awayUntilFrame; } }

        /// <summary>This frame and the next may carry the time away in Time.unscaledDeltaTime.</summary>
        internal static void MarkReturn()
        {
            awayUntilFrame = Time.frameCount + 1;
        }

        /// <summary>ScreenSplash reports when it is on screen (no return ad then, nor right after it).</summary>
        public static void SetSplashOnScreen(bool on)
        {
            if (splashOnScreen && !on) splashEndedAt = DateTime.UtcNow;
            splashOnScreen = on;
        }

        static void OnAppState(AppState state)
        {
            if (state == AppState.Background)
            {
                if (inBackground) return;
                inBackground = true;
                backgroundAt = DateTime.UtcNow;
                backgroundDuringAd = IsFullScreenAdShowing; // a full-screen ad opening also sends the app to the background
                backgroundForLink = DateTime.UtcNow < appOpenSuppressedUntil; // the player opened a link (share / store / policy)
                Log("background" + (backgroundDuringAd ? " (full-screen ad opened)" : backgroundForLink ? " (player opened a link)" : ""));
                return;
            }
            if (state != AppState.Foreground || !inBackground) return;
            inBackground = false;
            MarkReturn();
            double away = (DateTime.UtcNow - backgroundAt).TotalSeconds;
            Log("foreground after " + away.ToString("0.0") + " s away");

            string skip = ReturnSkipReason(away);
            if (skip != null)
            {
                Log("app open skipped on return: " + skip);
                return;
            }
            ShowAppOpen(null);
        }

        /// <summary>
        /// Call right before sending the player to another app (share sheet, Play Store, browser). If the app goes to
        /// the background within the next `seconds`, the return from that trip shows no app open ad, however long the
        /// player stayed away.
        /// </summary>
        public static void SuppressAppOpenFor(float seconds)
        {
            appOpenSuppressedUntil = DateTime.UtcNow.AddSeconds(seconds);
            Log("app open on return suppressed for " + seconds + " s (player opened a link)");
        }

        /// <summary>Why a return to the app must not show the app open ad, or null when it may.</summary>
        static string ReturnSkipReason(double away)
        {
            var now = DateTime.UtcNow;
            if (backgroundDuringAd) return "the return is from a full-screen ad";
            if (backgroundForLink) return "the return is from a link the player opened (share / rate)";
            if (now < appOpenSuppressedUntil) return "suppressed after the player opened a link";
            if (IsFullScreenAdShowing) return "a full-screen ad is showing";
            if ((now - fullScreenEndedAt).TotalSeconds < AfterFullScreenIgnore)
                return "a full-screen ad closed less than " + AfterFullScreenIgnore + " s ago";
            if (away < AppOpenMinBackgroundSeconds)
                return "away " + away.ToString("0.0") + " s, less than " + AppOpenMinBackgroundSeconds + " s";
            if (splashOnScreen) return "splash on screen";
            if ((now - splashEndedAt).TotalSeconds < AfterSplashIgnore)
                return "splash / launch ad ended less than " + AfterSplashIgnore + " s ago";
            if ((now - appOpenClosedReal).TotalSeconds < AppOpenMinGapSeconds)
                return "last app open ad closed less than " + AppOpenMinGapSeconds + " s ago";
            if (!IsAppOpenReady)
            {
                LoadAppOpen(); // for next time; nothing is shown now
                return "no fresh app open ad loaded (loading one for next time)";
            }
            return null;
        }

        /// <summary>Wraps a callback so it runs at most once (null-safe).</summary>
        internal static Action Once(Action a)
        {
            bool called = false;
            return () =>
            {
                if (called) return;
                called = true;
                if (a != null) a();
            };
        }

        // ------------------------------------------------------------------ app open

        /// <summary>Loads one app open ad (no-op if a fresh one is loaded or loading; an expired one is replaced).</summary>
        public static void LoadAppOpen() { AppOpen.Load(); }

        /// <summary>
        /// Shows the app open ad if one is loaded and fresh and no other full-screen ad is up. onClosed is called
        /// exactly once in every case: closed, failed to show, or nothing to show.
        /// </summary>
        public static void ShowAppOpen(Action onClosed) { AppOpen.Show(onClosed); }

        /// <summary>A fresh app open ad is loaded and can be shown now.</summary>
        public static bool IsAppOpenReady { get { return AppOpen.IsReady; } }

        /// <summary>A load is running (or waiting for Init), so IsAppOpenReady may still turn true.</summary>
        public static bool IsAppOpenLoading { get { return AppOpen.IsLoading; } }

        /// <summary>Why the last app open load gave no ad (no ID, SDK init failed, load error), or null.</summary>
        public static string AppOpenLastError { get { return AppOpen.LastError; } }

        /// <summary>One app open ad: load, 4 hour expiry, show.</summary>
        static class AppOpen
        {
            static readonly TimeSpan MaxAge = TimeSpan.FromHours(4);
            const float OpenTimeout = 5f; // s (unscaled) for the ad to appear after Show before we give up

            static AppOpenAd ad;
            static DateTime loadedAt;
            static bool loading, waitingForInit, opened, forLaunch;
            static Action closed;

            /// <summary>Why the last load ended without an ad (null while loading or after a good load).</summary>
            public static string LastError { get; private set; }

            public static bool IsLoading { get { return loading || waitingForInit; } }

            public static bool IsReady
            {
                get { return ad != null && !Expired && ad.CanShowAd(); }
            }

            static bool Expired { get { return DateTime.UtcNow - loadedAt > MaxAge; } }

            public static void Load()
            {
                if (IsLoading) return;
                if (ad != null)
                {
                    if (!Expired) return;
                    Log("app open expired (over 4 h old), loading a new one");
                    Discard();
                }
                // launch (splash on screen) = the "first" unit, later loads = the "resume" unit. The ID itself is read
                // when the request goes out (Request), after the startup hold.
                forLaunch = splashOnScreen;
                LastError = null;
                waitingForInit = true;
                Log("app open load requested (" + (forLaunch ? "launch" : "resume") + " unit" +
                    (Ready ? ")" : ", starts when the SDK is initialised)"));
                if (!WhenReady(() => { waitingForInit = false; Request(); }))
                {
                    waitingForInit = false;
                    LastError = "SDK init failed";
                    Log("app open load failed: SDK init failed");
                }
            }

            static void Request()
            {
                string id = forLaunch ? AdIds.AppOpenFirst : AdIds.AppOpenResume;
                if (string.IsNullOrEmpty(id))
                {
                    LastError = "no ad unit ID";
                    Log("app open load failed: no ad unit ID for the " + (forLaunch ? "launch" : "resume") + " unit");
                    return;
                }
                loading = true;
                Log("app open load start");
                try
                {
                    AppOpenAd.Load(id, new AdRequest(), (loaded, error) => OnMain(() =>
                    {
                        loading = false;
                        if (error != null || loaded == null)
                        {
                            LastError = error != null ? "code " + error.GetCode() + ": " + error.GetMessage() : "no ad";
                            Log("app open load failed: " + LastError);
                            return;
                        }
                        ad = loaded;
                        loadedAt = DateTime.UtcNow;
                        Hook(ad);
                        Log("app open loaded");
                    }));
                }
                catch (Exception e)
                {
                    loading = false;
                    LastError = e.Message;
                    Log("app open load failed: " + e.Message);
                }
            }

            static void Hook(AppOpenAd a)
            {
                a.OnAdFullScreenContentOpened += () => OnMain(() =>
                {
                    opened = true;
                    Log("app open shown");
                    AnalyticsService.AppOpenAdShown(splashOnScreen ? "first" : "resume");
                });
                a.OnAdFullScreenContentClosed += () => OnMain(() =>
                {
                    Log("app open closed");
                    appOpenClosedAt = Time.unscaledTime;
                    appOpenClosedReal = DateTime.UtcNow;
                    Finish();
                });
                a.OnAdFullScreenContentFailed += err => OnMain(() =>
                {
                    Log("app open failed to show: " + (err != null ? err.GetMessage() : "unknown"));
                    Finish();
                });
            }

            public static void Show(Action onClosed)
            {
                var done = Once(onClosed);
                if (IsFullScreenAdShowing)
                {
                    Log("app open skipped: another full-screen ad is showing");
                    done();
                    return;
                }
                if (!IsReady)
                {
                    bool stale = ad != null && Expired;
                    Log(stale ? "app open skipped: expired" : "app open skipped: none loaded");
                    if (stale) Load();
                    done();
                    return;
                }

                closed = done;
                opened = false;
                BeginFullScreen();
                Log("app open show");
                try
                {
                    ad.Show();
                    if (GameApp.I != null) GameApp.I.StartCoroutine(OpenWatch(ad));
                }
                catch (Exception e)
                {
                    Log("app open failed to show: " + e.Message);
                    Finish();
                }
            }

            /// <summary>
            /// Safety net: if neither "opened" nor "failed" arrives after Show, release the game. While the ad is
            /// really on screen the Unity player is paused on Android, so this only runs out when nothing appeared.
            /// </summary>
            static IEnumerator OpenWatch(AppOpenAd shown)
            {
                float t = 0f;
                while (!opened && t < OpenTimeout && closed != null && ad == shown)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (!opened && closed != null && ad == shown)
                {
                    Log("app open did not appear in " + OpenTimeout + " s, continuing");
                    Finish();
                }
            }

            /// <summary>After close or a failed show: drop the ad, restore audio, call the caller back once.</summary>
            static void Finish()
            {
                Discard();
                EndFullScreen();
                var cb = closed;
                closed = null;
                Load(); // the next one, for the next return to the app
                if (cb != null) cb();
            }

            static void Discard()
            {
                if (ad == null) return;
                var a = ad;
                ad = null;
                try { a.Destroy(); } catch (Exception) { }
            }
        }

        // ------------------------------------------------------------------ interstitial (LevelPlay)

        /// <summary>Loads one interstitial (no-op if one is loaded or loading). Also resets the retry count.</summary>
        public static void LoadInterstitial() { Interstitial.Request(); }

        /// <summary>
        /// Shows the interstitial if one is loaded and no other full-screen ad is up. onClosed is called exactly once
        /// in every case: closed, failed to show, nothing loaded, another ad showing, skipped, or no response in 5 s.
        /// </summary>
        public static void ShowInterstitial(Action onClosed) { Interstitial.Show(onClosed); }

        /// <summary>
        /// One LevelPlay interstitial kept ready (ad unit AdIds.LevelPlayInterstitial): loaded once LevelPlay is ready
        /// and again right after each one closes or fails to show. A failed load retries after 30 s, then 60 s, then
        /// waits for the next request. One LevelPlayInterstitialAd object for the session, reloaded after every show.
        /// </summary>
        static class Interstitial
        {
            static readonly float[] RetryDelays = { 30f, 60f }; // after failed try 1 and 2; try 3 failing stops
            const float OpenTimeout = 5f;

            static LevelPlayInterstitialAd ad;
            static bool loading, waitingForInit, opened, showing;
            static int failures, retryId, showId;
            static Action closed;
            static float closedAt = -1000f;

            static bool IsReady
            {
                get
                {
                    if (ad == null) return false;
                    try { return ad.IsAdReady(); } catch (Exception) { return false; }
                }
            }

            /// <summary>A new request (app start, Show with nothing loaded, after a close): fresh retry budget.</summary>
            public static void Request()
            {
                failures = 0;
                retryId++; // a pending retry is replaced by this request
                Load();
            }

            static void Load()
            {
                if (loading || waitingForInit || showing || IsReady) return;
                if (string.IsNullOrEmpty(AdIds.LevelPlayInterstitial))
                {
                    Log("interstitial not available (no LevelPlay ad unit ID in AdIds.LevelPlayInterstitial)");
                    return;
                }
                waitingForInit = true;
                if (!WhenLevelPlayReady(() => { waitingForInit = false; Fetch(); }))
                {
                    waitingForInit = false;
                    Log("interstitial not loaded: LevelPlay off (" + (LevelPlayError ?? "?") + ")");
                }
            }

            /// <summary>Creates the session's ad object once (events hooked once), then LoadAd.</summary>
            static void Fetch()
            {
                if (loading || showing || IsReady) return;
                try
                {
                    if (ad == null)
                    {
                        ad = new LevelPlayInterstitialAd(AdIds.LevelPlayInterstitial);
                        Hook(ad);
                    }
                    loading = true;
                    Log("interstitial load");
                    ad.LoadAd();
                }
                catch (Exception e)
                {
                    loading = false;
                    Failed(e.Message);
                }
            }

            static void Failed(string why)
            {
                failures++;
                if (failures > RetryDelays.Length)
                {
                    Log("interstitial load failed: " + why + " (try " + failures + ", stopped until the next request)");
                    return;
                }
                float delay = RetryDelays[failures - 1];
                Log("interstitial load failed: " + why + " (try " + failures + ", retry in " + delay + " s)");
                AdsRunner.Run(Retry(++retryId, delay));
            }

            static IEnumerator Retry(int id, float delay)
            {
                float t = 0f;
                while (t < delay) { t += Time.unscaledDeltaTime; yield return null; }
                if (id == retryId) Load();
            }

            /// <summary>LevelPlay posts these on the Unity main thread.</summary>
            static void Hook(LevelPlayInterstitialAd a)
            {
                a.OnAdLoaded += info =>
                {
                    loading = false;
                    failures = 0;
                    Log("interstitial load ok" + Network(info));
                };
                a.OnAdLoadFailed += err =>
                {
                    loading = false;
                    Failed(err != null ? "code " + err.ErrorCode + ": " + err.ErrorMessage : "no ad");
                };
                a.OnAdDisplayed += info =>
                {
                    if (!showing) return;
                    opened = true;
                    Log("interstitial shown" + Network(info));
                };
                a.OnAdClosed += info =>
                {
                    if (!showing) return;
                    Log("interstitial closed");
                    closedAt = Time.unscaledTime;
                    Finish();
                };
                a.OnAdDisplayFailed += (info, err) =>
                {
                    if (!showing) return;
                    Log("interstitial failed to show: " + (err != null ? "code " + err.ErrorCode + ": " + err.ErrorMessage : "unknown"));
                    Finish();
                };
            }

            public static void Show(Action onClosed)
            {
                var done = Once(onClosed);
                float now = Time.unscaledTime;
                float gap = InterstitialMinGapSeconds; // a variable, so a 0 gap gives no unreachable-code warning
                string skip = null;
                if (showing || IsFullScreenAdShowing) skip = "another full-screen ad is showing";
                else if (now - appOpenClosedAt < AfterAppOpenGap) skip = "app open ad closed less than " + AfterAppOpenGap + " s ago";
                else if (gap > 0f && now - closedAt < gap) skip = "less than " + gap + " s since the last one";
                else if (LevelPlayOff) skip = "LevelPlay off (" + (LevelPlayError ?? "?") + ")";
                else if (!IsReady) skip = "none loaded";
                if (skip != null)
                {
                    Log("interstitial skipped: " + skip);
                    if (!IsReady && !showing) Request(); // expired / never loaded: get one for next time
                    done();
                    return;
                }

                closed = done;
                opened = false;
                showing = true;
                int id = ++showId;
                BeginFullScreen();
                Log("interstitial show");
                try
                {
                    ad.ShowAd();
                    AdsRunner.Run(OpenWatch(id));
                }
                catch (Exception e)
                {
                    Log("interstitial failed to show: " + e.Message);
                    Finish();
                }
            }

            /// <summary>
            /// Safety net: no "displayed" / "failed" within OpenTimeout s releases the game. While the ad is really on
            /// screen the Unity player is paused on Android, so this only runs out when nothing appeared.
            /// </summary>
            static IEnumerator OpenWatch(int id)
            {
                float t = 0f;
                while (!opened && t < OpenTimeout && showing && id == showId)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (!opened && showing && id == showId)
                {
                    Log("interstitial did not appear in " + OpenTimeout + " s, continuing");
                    Finish();
                }
            }

            /// <summary>After close or a failed show (once per show): restore audio, call back once, load the next.</summary>
            static void Finish()
            {
                if (!showing) return;
                showing = false;
                showId++; // a late watchdog of this show does nothing
                EndFullScreen();
                var cb = closed;
                closed = null;
                Request();
                if (cb != null) cb();
            }
        }

        /// <summary>" (network)" for the logs, from a LevelPlay ad info.</summary>
        static string Network(LevelPlayAdInfo info)
        {
            return info != null && !string.IsNullOrEmpty(info.AdNetwork) ? " (" + info.AdNetwork + ")" : "";
        }

        // ------------------------------------------------------------------ rewarded (LevelPlay)

        /// <summary>Always true: ShowRewarded reports every failure through onFailed (kept for older callers).</summary>
        public static bool ReportsFailure { get { return true; } }

        /// <summary>Loads one rewarded ad (no-op if one is loaded or loading). Also resets the retry count.</summary>
        public static void LoadRewarded() { Rewarded.Request(); }

        /// <summary>True while a ShowRewarded request is waiting for its ad or the ad is on screen.</summary>
        public static bool IsRewardedBusy { get { return Rewarded.Busy; } }

        public static void ShowRewarded(Action onReward)
        {
            ShowRewarded(onReward, null);
        }

        /// <summary>
        /// Shows a rewarded ad (loading one first if needed, up to 6 s). After the ad has closed, exactly one of the two
        /// runs, on the main thread: onReward when the reward was earned, onFailed otherwise (not ready, failed to
        /// show, closed early, another full-screen ad up). A call while a request is busy is ignored.
        /// </summary>
        public static void ShowRewarded(Action onReward, Action onFailed)
        {
            Rewarded.Show(onReward, onFailed);
        }

        /// <summary>
        /// One LevelPlay rewarded ad kept ready (ad unit AdIds.LevelPlayRewarded): loaded once LevelPlay is ready and
        /// again after each one closes or fails to show. A failed load retries after 30 s, then 60 s. One
        /// LevelPlayRewardedAd object for the session, reloaded after every show.
        /// </summary>
        static class Rewarded
        {
            static readonly float[] RetryDelays = { 30f, 60f }; // after failed try 1 and 2; try 3 failing stops
            const float WaitForLoad = 6f, OpenTimeout = 5f;
            const float LateRewardWait = 0.5f; // s after close: the reward callback may arrive just after "closed"

            static LevelPlayRewardedAd ad;
            static bool loading, waitingForInit, opened, earned, busy, showing, lastLoadFailed;
            static int failures, retryId, showId;
            static Action reward, fail;

            public static bool Busy { get { return busy; } }

            static bool Ready
            {
                get
                {
                    if (ad == null) return false;
                    try { return ad.IsAdReady(); } catch (Exception) { return false; }
                }
            }

            public static void Request()
            {
                failures = 0;
                retryId++; // a pending retry is replaced by this request
                Load();
            }

            static void Load()
            {
                if (loading || waitingForInit || showing || Ready) return;
                if (string.IsNullOrEmpty(AdIds.LevelPlayRewarded))
                {
                    Log("rewarded not available (no LevelPlay ad unit ID in AdIds.LevelPlayRewarded)");
                    return;
                }
                waitingForInit = true;
                if (!WhenLevelPlayReady(() => { waitingForInit = false; Fetch(); }))
                {
                    waitingForInit = false;
                    Log("rewarded not loaded: LevelPlay off (" + (LevelPlayError ?? "?") + ")");
                }
            }

            /// <summary>Creates the session's ad object once (events hooked once), then LoadAd.</summary>
            static void Fetch()
            {
                if (loading || showing || Ready) return;
                try
                {
                    if (ad == null)
                    {
                        ad = new LevelPlayRewardedAd(AdIds.LevelPlayRewarded);
                        Hook(ad);
                    }
                    loading = true;
                    lastLoadFailed = false;
                    Log("rewarded load");
                    ad.LoadAd();
                }
                catch (Exception e)
                {
                    loading = false;
                    Failed(e.Message);
                }
            }

            static void Failed(string why)
            {
                lastLoadFailed = true;
                failures++;
                if (failures > RetryDelays.Length)
                {
                    Log("rewarded load failed: " + why + " (try " + failures + ", stopped until the next request)");
                    return;
                }
                float delay = RetryDelays[failures - 1];
                Log("rewarded load failed: " + why + " (try " + failures + ", retry in " + delay + " s)");
                AdsRunner.Run(Retry(++retryId, delay));
            }

            static IEnumerator Retry(int id, float delay)
            {
                float t = 0f;
                while (t < delay) { t += Time.unscaledDeltaTime; yield return null; }
                if (id == retryId) Load();
            }

            /// <summary>LevelPlay posts these on the Unity main thread.</summary>
            static void Hook(LevelPlayRewardedAd a)
            {
                a.OnAdLoaded += info =>
                {
                    loading = false;
                    lastLoadFailed = false;
                    failures = 0;
                    Log("rewarded load ok" + Network(info));
                };
                a.OnAdLoadFailed += err =>
                {
                    loading = false;
                    Failed(err != null ? "code " + err.ErrorCode + ": " + err.ErrorMessage : "no ad");
                };
                a.OnAdDisplayed += info =>
                {
                    if (!showing) return;
                    opened = true;
                    Log("rewarded shown" + Network(info));
                };
                a.OnAdRewarded += (info, r) =>
                {
                    if (!busy || earned) return; // only the request that is showing can earn
                    earned = true;
                    Log("rewarded earned reward" + (r != null ? ": " + r.Amount + " " + r.Name : ""));
                };
                a.OnAdClosed += info =>
                {
                    if (!showing) return;
                    Log("rewarded closed");
                    Finish();
                };
                a.OnAdDisplayFailed += (info, err) =>
                {
                    if (!showing) return;
                    Log("rewarded failed to show: " + (err != null ? "code " + err.ErrorCode + ": " + err.ErrorMessage : "unknown"));
                    Finish();
                };
            }

            public static void Show(Action onReward, Action onFailed)
            {
                if (busy)
                {
                    Log("rewarded request ignored: another one is loading or showing");
                    return;
                }
                busy = true;
                reward = onReward;
                fail = onFailed;
                earned = false;

                if (IsFullScreenAdShowing) { Resolve("another full-screen ad is showing"); return; }
                if (LevelPlayOff) { Resolve("LevelPlay off (" + (LevelPlayError ?? "?") + ")"); return; }
                if (Ready) { Present(); return; }
                Log("rewarded not loaded yet, waiting up to " + WaitForLoad + " s");
                lastLoadFailed = false; // only a failure of this request's own load ends the wait early
                Request();
                AdsRunner.Run(WaitThenShow());
            }

            static IEnumerator WaitThenShow()
            {
                float t = 0f;
                while (!Ready && t < WaitForLoad && (loading || waitingForInit) && !lastLoadFailed)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (!busy) yield break;
                if (IsFullScreenAdShowing) Resolve("another full-screen ad is showing");
                else if (Ready) Present();
                else Resolve(loading || waitingForInit ? "not loaded within " + WaitForLoad + " s" : "load failed");
            }

            static void Present()
            {
                opened = false;
                showing = true;
                int id = ++showId;
                BeginFullScreen();
                Log("rewarded show");
                try
                {
                    ad.ShowAd();
                    AdsRunner.Run(OpenWatch(id));
                }
                catch (Exception e)
                {
                    Log("rewarded failed to show: " + e.Message);
                    Finish();
                }
            }

            /// <summary>Safety net like the other full-screen ads: nothing on screen within OpenTimeout s releases the game.</summary>
            static IEnumerator OpenWatch(int id)
            {
                float t = 0f;
                while (!opened && t < OpenTimeout && showing && id == showId)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (!opened && showing && id == showId)
                {
                    Log("rewarded did not appear in " + OpenTimeout + " s, continuing");
                    Finish();
                }
            }

            /// <summary>After close or a failed show (once per show): restore audio, load the next, then pay out or fail.</summary>
            static void Finish()
            {
                if (!showing) return; // already finished (e.g. the watchdog ran first)
                showing = false;
                showId++;
                EndFullScreen();
                Request();
                AdsRunner.Run(PayOut());
            }

            static IEnumerator PayOut()
            {
                float t = 0f;
                while (!earned && t < LateRewardWait)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
                Resolve(earned ? null : "closed without the reward");
            }

            /// <summary>Ends the request: onReward when why is null, onFailed (with the reason logged) otherwise. Once.</summary>
            static void Resolve(string why)
            {
                if (!busy) return;
                busy = false;
                var r = reward;
                var f = fail;
                reward = fail = null;
                if (why == null)
                {
                    if (r != null) r();
                    return;
                }
                Log("rewarded onFailed: " + why);
                if (f != null) f();
            }
        }

        // ------------------------------------------------------------------ display (banner + native)

        static float density = -1f, physicalWidth = -1f;
        static float pluginDensity = -1f; // Resources.getSystem() density: what the plugin's convertDpToPixel uses for x / y

        /// <summary>
        /// One display source for every dp / px conversion (banner and native): Android DisplayMetrics.density and
        /// widthPixels, which the SDK itself uses; Screen.dpi / 160 and Screen.width elsewhere (Editor). Unity may
        /// render at a lower resolution than the window, so Unity px are scaled by widthPixels / Screen.width.
        /// </summary>
        static void ReadDisplay()
        {
            if (density > 0f) return;
            float d = 0f, w = 0f, sd = 0f;
            if (Application.platform == RuntimePlatform.Android)
            {
                try
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (var res = activity.Call<AndroidJavaObject>("getResources"))
                    using (var metrics = res.Call<AndroidJavaObject>("getDisplayMetrics"))
                    {
                        d = metrics.Get<float>("density");
                        w = metrics.Get<int>("widthPixels");
                    }
                }
                catch (Exception e) { Log("display metrics unavailable, using Screen.dpi: " + e.Message); }
                try
                {
                    using (var resClass = new AndroidJavaClass("android.content.res.Resources"))
                    using (var sys = resClass.CallStatic<AndroidJavaObject>("getSystem"))
                    using (var metrics = sys.Call<AndroidJavaObject>("getDisplayMetrics"))
                        sd = metrics.Get<float>("density");
                }
                catch (Exception) { }
            }
            if (d <= 0f) d = (Screen.dpi > 0f ? Screen.dpi : 160f * Screen.width / 360f) / 160f; // unknown dpi: 360 dp wide
            if (w <= 0f) w = Screen.width;
            if (sd <= 0f) sd = d;
            if (Screen.width <= 0) return; // not ready yet: read again next time
            density = d;
            physicalWidth = w;
            pluginDensity = sd;
            if (Mathf.Abs(sd - d) > 0.001f)
                Log("display: system density " + sd.ToString("0.###") + " differs from the activity's " + d.ToString("0.###") +
                    " (native ad x / y follow the system one, as the plugin does)");
            Log("display: density " + d.ToString("0.###") + ", window " + w + " px wide = " + (w / d).ToString("0.#") +
                " dp; Unity Screen " + Screen.width + "x" + Screen.height + ", dpi " + Screen.dpi.ToString("0") +
                ", safe area " + Screen.safeArea);
        }

        static float Density { get { ReadDisplay(); return density > 0f ? density : 1f; } }
        static float PhysicalWidth { get { ReadDisplay(); return physicalWidth > 0f ? physicalWidth : Mathf.Max(1, Screen.width); } }
        static float PluginDensity { get { ReadDisplay(); return pluginDensity > 0f ? pluginDensity : Density; } }

        /// <summary>The display density used for every dp / px conversion (for logs).</summary>
        public static float DisplayDensity { get { return Density; } }

        /// <summary>
        /// Bottom of the window the system keeps for itself, in device px: the navigation bar when shown and, from
        /// Android 10, the mandatory gesture area (the home handle), even while the bars are hidden. 0 elsewhere.
        /// </summary>
        static int AndroidBottomInsetPx()
        {
            if (Application.platform != RuntimePlatform.Android) return 0;
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var window = activity.Call<AndroidJavaObject>("getWindow"))
                using (var decor = window.Call<AndroidJavaObject>("getDecorView"))
                using (var insets = decor.Call<AndroidJavaObject>("getRootWindowInsets"))
                {
                    if (insets == null) return 0;
                    int b = insets.Call<int>("getSystemWindowInsetBottom");
                    int sdk;
                    using (var version = new AndroidJavaClass("android.os.Build$VERSION")) sdk = version.GetStatic<int>("SDK_INT");
                    if (sdk >= 29)
                        using (var g = insets.Call<AndroidJavaObject>("getMandatorySystemGestureInsets"))
                            if (g != null) b = Mathf.Max(b, g.Get<int>("bottom"));
                    return b;
                }
            }
            catch (Exception e)
            {
                Log("bottom inset unavailable (safe area only): " + e.Message);
                return 0;
            }
        }

        /// <summary>Unity screen px (Screen.width / height, RectTransform screen points) to dp and back.</summary>
        internal static float UnityPxToDp(float px) { return px * PhysicalWidth / Mathf.Max(1, Screen.width) / Density; }
        static float DpToUnityPx(float dp) { return dp * Density * Mathf.Max(1, Screen.width) / PhysicalWidth; }

        /// <summary>Device px (Android views) to Unity screen px: Unity may render below the window's resolution.</summary>
        static float DeviceToUnityPx(float px) { return px * Mathf.Max(1, Screen.width) / PhysicalWidth; }

        /// <summary>Design px (390 = Screen.width) to Unity px and back.</summary>
        internal static float DesignToUnityPx(float d) { return d * Mathf.Max(1, Screen.width) / 390f; }
        internal static float UnityPxToDesign(float px) { return px * 390f / Mathf.Max(1, Screen.width); }

        // ------------------------------------------------------------------ native (result screens)

        /// <summary>
        /// Real medium template height (dp), measured from the plugin's own layout (googlemobileads-unity.aar,
        /// res/layout/gnt_medium_template_view.xml): its root NativeAdView is layout_height="350dp", width match_parent,
        /// so it is 350 dp at every screen width (360 / 393 / 411 dp alike); less than that cuts off its CTA.
        /// </summary>
        public const float NativeMediumDp = 350f;

        /// <summary>
        /// Smallest AdSize height (dp) at which the medium template still renders clean (the squeezed-medium floor).
        /// TemplateView is a FrameLayout and the template's root keeps its exact 350dp inside a shorter parent, so 300,
        /// 280 and 260 dp all clip the bottom (CTA / Install bar and body text): the floor is the full 350. Lower this
        /// only if a future plugin version lets the template shrink; PlanNative then squeezes medium down to it.
        /// </summary>
        public const float NativeMediumMinDp = 350f;

        /// <summary>
        /// Small template fallback height (dp). The real small card (gnt_small_template_view.xml) is wrap_content around
        /// a 4:1 block with 10 dp margins, at the top of its view: (width - 20) / 4 + 20 dp. PlanNative and Render use
        /// that (SmallCardPx), so the AdSize is exactly the card and nothing transparent or clipped is left in the frame.
        /// </summary>
        public const float NativeSmallDp = 100f;

        const float SmallMarginDp = 10f; // gnt_default_margin around the small template's 4:1 block

        /// <summary>The small card's real height in device px for a template width in device px.</summary>
        static int SmallCardPx(int wPx)
        {
            int m = (int)(SmallMarginDp * Density + 0.5f); // getDimensionPixelSize
            return Mathf.RoundToInt((wPx - 2 * m) / 4f) + 2 * m;
        }

        /// <summary>The medium card's real height in device px (its root is a fixed 350dp, at the top of its view).</summary>
        static int MediumCardPx { get { return (int)(NativeMediumDp * Density + 0.5f); } }

        /// <summary>Frame around the ad: 342 design px wide (24 from each side), 7 px padding (edge 3 inside it).</summary>
        public const float NativeFrameWidth = 342f, NativeFramePad = 7f;

        const float NativeGapDp = 12f;     // bottom of the button row -> frame top
        const float NativeBottomDp = 8f;   // frame bottom -> safe-area bottom (above the gesture bar), at least

        /// <summary>Template height (dp) used for planning.</summary>
        public static float NativeTemplateDp(NativeSize size)
        {
            return size == NativeSize.Medium ? NativeMediumDp : size == NativeSize.Small ? NativeSmallDp : 0f;
        }

        /// <summary>The template size on screen now (it can change from Medium to Small after the layout check).</summary>
        public static NativeSize ShownNativeSize { get { return NativeUnit.ShownSize; } }

        /// <summary>
        /// The native ad card on screen now, in Unity screen px (origin bottom-left): predicted at render, then the
        /// measured size a few frames later (measured = true). False when no native ad is shown.
        /// </summary>
        public static bool TryGetNativeCard(out Rect unityScreen, out NativeSize size, out bool measured)
        {
            return NativeUnit.TryGetCard(out unityScreen, out size, out measured);
        }

        /// <summary>The card rect on screen in dp and device px, for the logs.</summary>
        public static string NativeCardDescription() { return NativeUnit.CardDescription(); }

        /// <summary>Loads the native ad of this kind if none is loaded or loading (one per kind; rendered medium or small
        /// when shown). Failed loads retry after 30 s, then 60 s.</summary>
        public static void PreloadNative(NativeKind kind) { NativeUnit.Request(kind); }

        public static NativeState GetNativeState(NativeKind kind) { return NativeUnit.State(kind); }

        /// <summary>
        /// Plans the ad under a result screen's content. contentBottom = bottom of its Home button, design px from the
        /// safe-area top. Free space = from contentBottom + 16 dp to the safe-area bottom. Medium if free >= medium +
        /// 14 + 16, else Small if free >= small + 14 + 16, else None. The ad is the frame's inner rectangle: 328 design
        /// px wide (design -> px -> dp), centred, its top 7 px under the frame top. Logs every number.
        /// </summary>
        public static NativePlan PlanNative(float contentBottom, string who, string step = "")
        {
            var p = new NativePlan();
            if (Screen.width <= 0 || Screen.height <= 0) return p;
            Rect safe = Screen.safeArea;
            float safeTopPx = Screen.height - safe.yMax;  // Unity px from the screen top
            float sysBottomPx = DeviceToUnityPx(AndroidBottomInsetPx());
            p.BottomInsetPx = Mathf.Max(safe.yMin, sysBottomPx);
            float screenWDp = UnityPxToDp(Screen.width), screenHDp = UnityPxToDp(Screen.height);
            var r = NativeFit.Evaluate(screenWDp, screenHDp, UnityPxToDp(safeTopPx), UnityPxToDp(p.BottomInsetPx), contentBottom);

            // even sides: the plugin puts the left edge at (int)(X x density) px, so the width is the window minus
            // twice that (Render), and the planned W is that width in dp
            p.X = Mathf.RoundToInt((screenWDp - r.AdWDp) * 0.5f);
            int wPx = NativeWidthPx(p.X);
            p.W = Mathf.RoundToInt(wPx / Density);
            p.Y = Mathf.RoundToInt(r.AdTopDp);
            float yShift = p.Y - r.AdTopDp; // the rounded top moves the ad's bottom by the same amount
            p.FreeDp = r.FreeDp + NativeFit.BottomMarginDp;
            p.MediumDp = NativeFit.MediumPlanDp;
            p.SmallDp = SmallCardPx(wPx) / Density;
            p.NeedMediumDp = p.MediumDp + 2f * r.PadDp + NativeFit.BottomMarginDp;
            p.NeedSmallDp = p.SmallDp + 2f * r.PadDp + NativeFit.BottomMarginDp;
            p.MediumMaxH = Mathf.FloorToInt(r.MediumMaxDp - yShift);
            p.Size = p.MediumMaxH >= p.MediumDp ? NativeSize.Medium : p.MediumMaxH >= p.SmallDp ? NativeSize.Small : NativeSize.None;
            p.MediumH = Mathf.RoundToInt(p.MediumDp);
            p.SmallH = Mathf.CeilToInt(p.SmallDp - 0.01f);
            // the shimmer frame follows the rounded dp the SDK gets; the real ad then takes over (NativeAdSlot)
            p.InnerTop = UnityPxToDesign(DpToUnityPx(p.Y) - safeTopPx);
            p.InnerWidth = UnityPxToDesign(DeviceToUnityPx(wPx));

            Log(who + " native plan" + step + ": screen " + screenWDp.ToString("0") + "x" + screenHDp.ToString("0") + " dp (" +
                Screen.width + "x" + Screen.height + " Unity px), safe area top " + safeTopPx.ToString("0") + " px, bottom inset " +
                p.BottomInsetPx.ToString("0") + " px (safe area " + safe.yMin.ToString("0") + ", nav / gesture " + sysBottomPx.ToString("0") +
                "); row bottom " + contentBottom.ToString("0") + " design px; free " + p.FreeDp.ToString("0") + " dp; medium needs " +
                p.NeedMediumDp.ToString("0") + " (planned " + p.MediumDp.ToString("0") + ", room for up to " + p.MediumMaxH +
                "), small needs " + p.NeedSmallDp.ToString("0") + " (card " + p.SmallDp.ToString("0.#") + ") -> " + p.Size +
                "; ad rect x " + p.X + " y " + p.Y + " w " + p.W + " dp (" + wPx + " px; inner width " + p.InnerWidth.ToString("0.#") +
                " design px, density " + Density.ToString("0.###") + ", px scale " + (PhysicalWidth / Mathf.Max(1, Screen.width)).ToString("0.###") + ")");
            return p;
        }

        /// <summary>
        /// Plans the small template full width at the bottom (the Bonus Spin screen, NativeAdStrip): x 0, the whole
        /// window width, its card height from the template's own rule (a 4:1 block + 10 dp margins, SmallCardPx: about
        /// 105 - 118 dp, by width), its bottom on the top of the bottom inset (safe area / nav bar / gesture area), so
        /// the CTA is never under the gesture bar. InnerTop = the ad top in design px from the safe-area top.
        /// </summary>
        public static NativePlan PlanNativeBottom(string who)
        {
            var p = new NativePlan();
            if (Screen.width <= 0 || Screen.height <= 0) return p;
            Rect safe = Screen.safeArea;
            float safeTopPx = Screen.height - safe.yMax;  // Unity px from the screen top
            float sysBottomPx = DeviceToUnityPx(AndroidBottomInsetPx());
            p.BottomInsetPx = Mathf.Max(safe.yMin, sysBottomPx);
            p.X = 0;
            int wPx = NativeWidthPx(0);
            p.W = Mathf.RoundToInt(wPx / Density);
            p.SmallDp = SmallCardPx(wPx) / Density;
            p.SmallH = Mathf.CeilToInt(p.SmallDp - 0.01f);
            p.NeedSmallDp = p.SmallDp;
            float screenHDp = UnityPxToDp(Screen.height), insetDp = UnityPxToDp(p.BottomInsetPx);
            p.Y = Mathf.FloorToInt(screenHDp - insetDp - p.SmallH); // floor: never into the inset
            p.FreeDp = screenHDp - insetDp - p.Y;
            p.Size = p.Y > 0 ? NativeSize.Small : NativeSize.None;
            p.InnerTop = UnityPxToDesign(DpToUnityPx(p.Y) - safeTopPx);
            p.InnerWidth = UnityPxToDesign(DeviceToUnityPx(wPx));
            Log(who + " native bottom plan: screen " + UnityPxToDp(Screen.width).ToString("0") + "x" + screenHDp.ToString("0") +
                " dp, bottom inset " + insetDp.ToString("0.#") + " dp (" + p.BottomInsetPx.ToString("0") + " Unity px), small card " +
                p.SmallDp.ToString("0.#") + " dp -> ad x 0 y " + p.Y + " w " + p.W + " h " + p.SmallH + " dp (" + wPx +
                " px); ad top " + p.InnerTop.ToString("0.#") + " design px from the safe-area top");
            return p;
        }

        /// <summary>Ad width in device px for a left edge at x dp: the window width minus the same gap on both sides.</summary>
        static int NativeWidthPx(int xDp)
        {
            int left = (int)(xDp * PluginDensity);
            return Mathf.Max(1, Mathf.RoundToInt(PhysicalWidth) - 2 * left);
        }

        /// <summary>Inner (ad) height in design px for a planned size (frame height = this + 14).</summary>
        public static float NativeInnerHeight(NativePlan plan, NativeSize size)
        {
            int h = size == NativeSize.Medium ? plan.MediumH : size == NativeSize.Small ? plan.SmallH : 0;
            return UnityPxToDesign(DpToUnityPx(h));
        }

        /// <summary>
        /// Renders the ready native ad of this kind in the plan's rectangle with the given template. A medium ad that
        /// comes out taller than planned is re-rendered small. Returns the size actually on screen (None = not shown).
        /// </summary>
        public static NativeSize ShowNative(NativeKind kind, NativePlan plan, NativeSize size)
        {
            return NativeUnit.Show(kind, plan, size);
        }

        /// <summary>Hides and destroys the native ad on screen (if any), then preloads the next one of that kind.</summary>
        public static void HideNative() { NativeUnit.HideShown(); }

        /// <summary>
        /// One NativeOverlayAd per kind, loaded ahead and rendered (medium or small template, app colours) only when a
        /// result screen has room for it. One on screen at a time; hidden = destroyed, then the next one loads.
        /// Android only.
        /// </summary>
        static class NativeUnit
        {
            static readonly float[] RetryDelays = { 30f, 60f }; // after failed try 1 and 2; try 3 failing stops
            static readonly TimeSpan MaxAge = TimeSpan.FromHours(1); // a loaded native ad is not shown after this
            const float TooTallDp = 2f; // a medium template measured this much over the plan falls back to small

            const int Kinds = 3; // NativeKind: Win, Lose, Bonus
            static readonly NativeOverlayAd[] ads = new NativeOverlayAd[Kinds];
            static readonly DateTime[] loadedAt = new DateTime[Kinds];
            static readonly bool[] loading = new bool[Kinds], waitingForInit = new bool[Kinds], failed = new bool[Kinds];
            static readonly int[] failures = new int[Kinds], retryIds = new int[Kinds];
            static bool loggedUnsupported;
            static int shown = -1; // kind on screen, -1 = none

            static bool Supported { get { return Application.platform == RuntimePlatform.Android; } }

            static string Id(int k) { return k == 0 ? AdIds.NativeWin : k == 1 ? AdIds.NativeLose : AdIds.NativeBonus; }
            static string Name(int k) { return k == 0 ? "native Win" : k == 1 ? "native Lose" : "native Bonus"; }
            static bool Expired(int k) { return DateTime.UtcNow - loadedAt[k] > MaxAge; }

            /// <summary>Why no native ad is possible, null when it is.</summary>
            static string OffReason(int k)
            {
                if (!Supported) return "not Android (" + Application.platform + ")";
                if (InitFailed) return "SDK init failed";
                if (string.IsNullOrEmpty(Id(k))) return "no ad unit ID";
                return null;
            }

            public static NativeState State(NativeKind kind)
            {
                int k = (int)kind;
                if (OffReason(k) != null) return NativeState.Off;
                if (ads[k] != null && (shown == k || !Expired(k))) return NativeState.Ready;
                if (loading[k] || waitingForInit[k]) return NativeState.Loading;
                if (failed[k]) return NativeState.Failed;
                return NativeState.Idle;
            }

            /// <summary>Ignored while one is loading, ready or waiting for a retry; after the retries ran out it starts over.</summary>
            public static void Request(NativeKind kind)
            {
                int k = (int)kind;
                string off = OffReason(k);
                if (off != null)
                {
                    if (!Supported)
                    {
                        if (!loggedUnsupported) Log("native ads off: " + off + " (no-op)");
                        loggedUnsupported = true;
                    }
                    else Log(Name(k) + " not loaded: " + off);
                    return;
                }
                if (ads[k] != null && shown != k && Expired(k))
                {
                    Log(Name(k) + " expired (loaded over " + MaxAge.TotalHours + " h ago): reloading");
                    Discard(k);
                }
                if (loading[k] || waitingForInit[k] || ads[k] != null) return;
                if (failed[k] && failures[k] <= RetryDelays.Length) return; // a retry is pending
                failures[k] = 0;
                retryIds[k]++;
                Load(k);
            }

            static void Load(int k)
            {
                if (loading[k] || waitingForInit[k] || ads[k] != null) return;
                string id = Id(k);
                if (string.IsNullOrEmpty(id) || InitFailed) return;
                waitingForInit[k] = true;
                if (!WhenReady(() => { waitingForInit[k] = false; Fetch(k, id); }))
                {
                    waitingForInit[k] = false;
                    Log(Name(k) + " not loaded: SDK init failed");
                }
            }

            static void Fetch(int k, string id)
            {
                loading[k] = true;
                failed[k] = false;
                Log(Name(k) + " load");
                try
                {
                    var options = new NativeAdOptions { AdChoicesPlacement = AdChoicesPlacement.TopRightCorner };
                    NativeOverlayAd.Load(id, new AdRequest(), options, (loaded, error) => OnMain(() =>
                    {
                        loading[k] = false;
                        if (error != null || loaded == null)
                        {
                            Failed(k, error != null ? error.GetMessage() : "no ad");
                            return;
                        }
                        failures[k] = 0;
                        ads[k] = loaded;
                        loadedAt[k] = DateTime.UtcNow;
                        Hook(k, loaded);
                        Log(Name(k) + " load ok");
                    }));
                }
                catch (Exception e)
                {
                    loading[k] = false;
                    Failed(k, e.Message);
                }
            }

            static void Failed(int k, string why)
            {
                failed[k] = true;
                failures[k]++;
                if (failures[k] > RetryDelays.Length)
                {
                    Log(Name(k) + " load failed: " + why + " (try " + failures[k] + ", stopped until the next level start)");
                    return;
                }
                float delay = RetryDelays[failures[k] - 1];
                Log(Name(k) + " load failed: " + why + " (try " + failures[k] + ", retry in " + delay + " s)");
                AdsRunner.Run(Retry(k, ++retryIds[k], delay));
            }

            static IEnumerator Retry(int k, int id, float delay)
            {
                float t = 0f;
                while (t < delay) { t += Time.unscaledDeltaTime; yield return null; }
                if (id == retryIds[k]) Load(k);
            }

            static void Hook(int k, NativeOverlayAd a)
            {
                a.OnAdPaid += value => OnMain(() =>
                    Log(Name(k) + " paid: " + (value != null ? value.Value + " micros " + value.CurrencyCode : "?")));
                a.OnAdClicked += () => OnMain(() => Log(Name(k) + " clicked"));
                a.OnAdImpressionRecorded += () => OnMain(() => Log(Name(k) + " impression"));
            }

            /// <summary>
            /// White card, ink text, white call to action text on teal (the app's Watch ad colours); the Bonus kind's
            /// call to action is coral (Theme.Accent, #E4572E). Same fonts and sizes for every kind.
            /// </summary>
            static NativeTemplateStyle Style(int k, NativeSize size)
            {
                bool medium = size == NativeSize.Medium;
                Color cta = k == (int)NativeKind.Bonus ? Theme.Accent : Theme.Teal;
                return new NativeTemplateStyle
                {
                    TemplateId = medium ? NativeTemplateId.Medium : NativeTemplateId.Small,
                    MainBackgroundColor = Theme.Card,
                    PrimaryText = new NativeTemplateTextStyle
                    {
                        TextColor = Theme.Ink, BackgroundColor = Theme.Card, FontSize = medium ? 16 : 15, Style = NativeTemplateFontStyle.Bold
                    },
                    SecondaryText = new NativeTemplateTextStyle
                    {
                        TextColor = Theme.InkSoft, BackgroundColor = Theme.Card, FontSize = 13, Style = NativeTemplateFontStyle.Normal
                    },
                    TertiaryText = new NativeTemplateTextStyle
                    {
                        TextColor = Theme.InkSoft, BackgroundColor = Theme.Card, FontSize = 12, Style = NativeTemplateFontStyle.Normal
                    },
                    CallToActionText = new NativeTemplateTextStyle
                    {
                        TextColor = Theme.Card, BackgroundColor = cta, FontSize = medium ? 16 : 15, Style = NativeTemplateFontStyle.Bold
                    },
                };
            }

            static NativeSize shownSize = NativeSize.None;
            static int renderId; // bumped on every render / hide, so a late layout check never touches a newer ad

            // the visible card on screen, in device px from the window's top-left (the plugin's FrameLayout origin):
            // predicted at render, replaced by the measured size once the view is laid out
            static int cardX, cardY, cardW, cardH;
            static bool cardMeasured;

            public static NativeSize ShownSize { get { return shown >= 0 ? shownSize : NativeSize.None; } }

            /// <summary>The card's rect in Unity screen px (origin bottom-left), or false when no ad is on screen.</summary>
            public static bool TryGetCard(out Rect unity, out NativeSize size, out bool measured)
            {
                unity = new Rect();
                size = ShownSize;
                measured = cardMeasured;
                if (size == NativeSize.None || cardW <= 0 || cardH <= 0) return false;
                float x = DeviceToUnityPx(cardX), top = DeviceToUnityPx(cardY);
                float w = DeviceToUnityPx(cardW), h = DeviceToUnityPx(cardH);
                unity = new Rect(x, Screen.height - top - h, w, h);
                return true;
            }

            public static string CardDescription()
            {
                float d = Density;
                return "x " + (cardX / d).ToString("0.#") + " y " + (cardY / d).ToString("0.#") + " w " + (cardW / d).ToString("0.#") +
                       " h " + (cardH / d).ToString("0.#") + " dp (" + cardX + ", " + cardY + ", " + cardW + "x" + cardH + " px)";
            }

            /// <summary>Where the plugin puts a view at (x, y) dp: (int)(dp x system density), as its updatePosition.</summary>
            static int PluginPx(int dp) { return (int)(dp * PluginDensity); }

            /// <summary>
            /// Renders in the plan's rectangle. The plugin (11.6, Android) puts AdSize.getWidth() / getHeight() straight
            /// into the view's LayoutParams, which are PIXELS, while x / y go through convertDpToPixel. So the size is
            /// passed in device px (dp x DisplayMetrics.density) and the position in dp: both then land on the frame.
            /// Width: the window minus the same gap on both sides (NativeWidthPx). Small: the card is wrap_content
            /// (SmallCardPx), so the AdSize height is that card exactly. Medium goes through PlaceMedium instead.
            /// </summary>
            static bool Render(int k, NativePlan plan, NativeSize size)
            {
                int wPx = NativeWidthPx(plan.X);
                int hPx = size == NativeSize.Medium ? Mathf.RoundToInt(plan.MediumH * Density) : SmallCardPx(wPx);
                if (!RenderAt(k, size, wPx, hPx, plan.X, plan.Y)) return false;
                cardX = PluginPx(plan.X);
                cardY = PluginPx(plan.Y);
                cardW = wPx;
                cardH = hPx;
                cardMeasured = false;
                AdsRunner.Run(CheckLayout(k, ++renderId, plan, size));
                return true;
            }

            /// <summary>One RenderTemplate + Show at (x, y) dp with an AdSize in device px (the plugin re-inflates the view).</summary>
            static bool RenderAt(int k, NativeSize size, int wPx, int hPx, int x, int y)
            {
                try
                {
                    ads[k].RenderTemplate(Style(k, size), new AdSize(wPx, hPx), x, y);
                    ads[k].SetTemplatePosition(x, y);
                    ads[k].Show();
                }
                catch (Exception e)
                {
                    Log(Name(k) + " render " + size + " failed: " + e.Message);
                    return false;
                }
                Log(Name(k) + " rendered " + size + " at x " + x + " y " + y + " dp, AdSize " + wPx + "x" + hPx + " px = " +
                    (wPx / Density).ToString("0.#") + "x" + (hPx / Density).ToString("0.#") + " dp (density " + Density.ToString("0.###") + ")");
                return true;
            }

            // ---- medium: measured off screen, shown only when the whole ad fits

            /// <summary>y (dp) far below the window: the view is inflated and laid out there but cannot be seen.</summary>
            static int OffscreenY { get { return Mathf.RoundToInt(UnityPxToDp(Screen.height)) + 400; } }

            static readonly object probeLock = new object();
            static int probeSeq, probeDoneSeq, probeMinSeq, probeH, probeW, probeParentH;
            static bool probeWrap, probeLaidOut;
            static string probeError;

            /// <summary>
            /// On the Android UI thread: finds the template's NativeAdView (one native ad on screen at a time), turns its
            /// fixed 350dp root into wrap_content (so the INSTALL button gets its full height and the view reports
            /// what it really needs), and reads its laid-out size.
            /// </summary>
            static void PostProbe()
            {
                int seq;
                lock (probeLock) seq = ++probeSeq;
                try
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        activity.Call("runOnUiThread", new AndroidJavaRunnable(() => ProbeOnUiThread(seq)));
                }
                catch (Exception e) { lock (probeLock) { probeDoneSeq = seq; probeError = e.Message; } }
            }

            static void ProbeOnUiThread(int seq)
            {
                int h = -1, w = -1, ph = -1;
                bool wrap = false, laidOut = false;
                string err = null;
                try
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (var res = activity.Call<AndroidJavaObject>("getResources"))
                    using (var window = activity.Call<AndroidJavaObject>("getWindow"))
                    using (var decor = window.Call<AndroidJavaObject>("getDecorView"))
                    {
                        int id = res.Call<int>("getIdentifier", "native_ad_view", "id", activity.Call<string>("getPackageName"));
                        if (id == 0) err = "no native_ad_view id";
                        else
                            using (var view = decor.Call<AndroidJavaObject>("findViewById", id))
                            {
                                if (view == null) err = "view not added yet";
                                else
                                {
                                    using (var lp = view.Call<AndroidJavaObject>("getLayoutParams"))
                                    {
                                        if (lp != null && lp.Get<int>("height") != -2) // ViewGroup.LayoutParams.WRAP_CONTENT
                                        {
                                            lp.Set<int>("height", -2);
                                            view.Call("setLayoutParams", lp); // requests a new layout
                                        }
                                        wrap = lp != null;
                                    }
                                    laidOut = !view.Call<bool>("isLayoutRequested");
                                    h = view.Call<int>("getHeight");
                                    w = view.Call<int>("getWidth");
                                    using (var parent = view.Call<AndroidJavaObject>("getParent"))
                                        if (parent != null) ph = parent.Call<int>("getHeight");
                                }
                            }
                    }
                }
                catch (Exception e) { err = e.Message; }
                lock (probeLock)
                {
                    if (seq < probeDoneSeq || seq < probeMinSeq) return; // older, or posted before this measurement
                    probeDoneSeq = seq;
                    probeH = h; probeW = w; probeParentH = ph; probeWrap = wrap; probeLaidOut = laidOut; probeError = err;
                }
            }

            /// <summary>Probes every frame until the wrapped root has the same laid-out height twice. -1 = no answer.</summary>
            static IEnumerator Measure(int k, int id, Action<int, int> done)
            {
                int last = -1, same = 0;
                lock (probeLock)
                {
                    probeMinSeq = probeSeq + 1; // answers about an earlier view do not count
                    probeH = probeW = probeParentH = -1;
                    probeWrap = probeLaidOut = false;
                    probeError = "waiting";
                }
                for (int frame = 0; frame < 90; frame++)
                {
                    PostProbe();
                    yield return null;
                    if (id != renderId || ads[k] == null) yield break;
                    int h, w;
                    bool ok;
                    lock (probeLock) { h = probeH; w = probeW; ok = probeWrap && probeLaidOut && probeError == null; }
                    if (!ok || h <= 0) { same = 0; continue; }
                    same = h == last ? same + 1 : 0;
                    last = h;
                    if (same >= 1) { done(h, w); yield break; }
                }
                string err;
                lock (probeLock) err = probeError;
                Log(Name(k) + " medium: no stable layout in 90 frames" + (err != null ? " (" + err + ")" : ""));
                done(-1, -1);
            }

            /// <summary>
            /// Medium: rendered off screen with room up to plan.MediumMaxH, root set to wrap its content, measured;
            /// then rendered again at the measured height + SafetyDp (still off screen) and moved into place. If the
            /// whole ad (full INSTALL button included) does not fit the room above the bottom inset, the small
            /// template is shown instead: a clipped medium is never shown.
            /// </summary>
            static IEnumerator PlaceMedium(int k, int id, NativePlan plan)
            {
                float d = Density;
                int wPx = NativeWidthPx(plan.X);
                int roomPx = Mathf.FloorToInt(Mathf.Min(plan.MediumMaxH, NativeFit.MediumProbeMaxDp) * d);
                int safety = (int)(NativeFit.SafetyDp * d + 0.5f);
                int needPx = -1;
                yield return Measure(k, id, (h, w) => needPx = h);
                if (id != renderId || ads[k] == null) yield break;
                if (needPx <= 0 || needPx >= roomPx - 1)
                {
                    Debug.LogWarning("[Ads] " + Name(k) + " MEDIUM DOES NOT FIT: needs " +
                                     (needPx > 0 ? "more than " + (roomPx / d).ToString("0") + " dp" : "an unknown height (not measured)") +
                                     ", room " + plan.MediumMaxH + " dp above the bottom inset -> small template");
                    MediumToSmall(k, plan);
                    yield break;
                }

                int reqPx = Mathf.Min(needPx + safety, roomPx);
                if (Mathf.Abs(reqPx - roomPx) > 1)
                {
                    // the AdSize the ad really needs (+ safety), off screen again: the plugin re-inflates the 350dp root
                    if (!RenderAt(k, NativeSize.Medium, wPx, reqPx, plan.X, OffscreenY)) { MediumToSmall(k, plan); yield break; }
                    id = ++renderId;
                    int again = -1;
                    yield return Measure(k, id, (h, w) => again = h);
                    if (id != renderId || ads[k] == null) yield break;
                    if (again <= 0 || again >= reqPx + 1)
                    {
                        Debug.LogWarning("[Ads] " + Name(k) + " medium measured " + again + " px at its final size (asked " + reqPx +
                                         ") -> small template");
                        MediumToSmall(k, plan);
                        yield break;
                    }
                    needPx = again;
                }

                try { ads[k].SetTemplatePosition(plan.X, plan.Y); }
                catch (Exception e) { Log(Name(k) + " medium move failed: " + e.Message); MediumToSmall(k, plan); yield break; }
                cardX = PluginPx(plan.X);
                cardY = PluginPx(plan.Y);
                cardW = wPx;
                cardH = needPx; // the visible ad, not the padded request
                cardMeasured = true;
                Log(Name(k) + " medium placed: visible " + (needPx / d).ToString("0.#") + " dp (" + needPx + " px), AdSize height " +
                    (reqPx / d).ToString("0.#") + " dp (" + reqPx + " px = visible + " + NativeFit.SafetyDp + " dp), room " +
                    plan.MediumMaxH + " dp; " + CardDescription());
            }

            static void MediumToSmall(int k, NativePlan plan)
            {
                if (Render(k, plan, NativeSize.Small)) shownSize = NativeSize.Small;
                else { Log(Name(k) + " small fallback failed: hiding"); HideShown(); }
            }

            /// <summary>
            /// The template view is added on the Android UI thread, so its real size is only known a few frames later:
            /// logs it, takes it as the card size (the frame follows it), re-centres and re-places the overlay, and, if a
            /// medium template came out taller than planned, re-renders it small (the plugin removes the old view first).
            /// </summary>
            static IEnumerator CheckLayout(int k, int id, NativePlan plan, NativeSize size)
            {
                float hPx = 0f, wPx = 0f;
                for (int frame = 0; frame < 60 && id == renderId && hPx <= 0f; frame++)
                {
                    yield return null;
                    if (id != renderId || ads[k] == null) yield break;
                    try { hPx = ads[k].GetTemplateHeightInPixels(); wPx = ads[k].GetTemplateWidthInPixels(); } catch (Exception) { }
                }
                if (id != renderId || ads[k] == null) yield break;
                if (hPx <= 0f) { Log(Name(k) + " " + size + " layout: no size reported in 60 frames (frame keeps the predicted rect)"); yield break; }
                float hDp = hPx / Density, wDp = wPx / Density;
                int planned = size == NativeSize.Medium ? plan.MediumH : plan.SmallH;
                Log(Name(k) + " " + size + " laid out at " + wDp.ToString("0.#") + "x" + hDp.ToString("0.#") + " dp (" +
                    wPx.ToString("0") + "x" + hPx.ToString("0") + " px), planned " + plan.W + "x" + planned + " dp");
                if (size == NativeSize.Medium && hDp > planned + TooTallDp)
                {
                    Log(Name(k) + " medium is " + hDp.ToString("0") + " dp, over the planned " + planned + ": falling back to small");
                    if (Render(k, plan, NativeSize.Small)) shownSize = NativeSize.Small;
                    yield break;
                }

                // the real card: the view's width; its height capped by the template's own card height
                int w = Mathf.RoundToInt(wPx), h = Mathf.RoundToInt(hPx);
                int card = size == NativeSize.Medium ? Mathf.Min(h, MediumCardPx) : Mathf.Min(h, SmallCardPx(w));
                int x = plan.X;
                if (Mathf.Abs(w - cardW) > 1) // not the width asked for: centre what is really there
                    x = Mathf.RoundToInt((PhysicalWidth / Density - w / Density) * 0.5f);
                try { ads[k].SetTemplatePosition(x, plan.Y); } catch (Exception e) { Log(Name(k) + " re-place failed: " + e.Message); }
                cardX = PluginPx(x);
                cardY = PluginPx(plan.Y);
                cardW = w;
                cardH = card;
                cardMeasured = true;
                Log(Name(k) + " " + size + " re-placed on its measured card: " + CardDescription());
            }

            public static NativeSize Show(NativeKind kind, NativePlan plan, NativeSize size)
            {
                int k = (int)kind;
                string off = OffReason(k);
                if (off != null) { Log(Name(k) + " not shown: " + off); return NativeSize.None; }
                if (size == NativeSize.None) return NativeSize.None;
                if (ads[k] == null || Expired(k)) { Log(Name(k) + " not shown: no ad ready"); return NativeSize.None; }
                if (shown >= 0 && shown != k) HideShown(); // one native ad on screen at a time

                if (size == NativeSize.Medium)
                {
                    // off screen first: measured, then moved into place only if all of it fits (PlaceMedium)
                    int roomPx = Mathf.FloorToInt(Mathf.Min(plan.MediumMaxH, NativeFit.MediumProbeMaxDp) * Density);
                    if (!RenderAt(k, NativeSize.Medium, NativeWidthPx(plan.X), roomPx, plan.X, OffscreenY))
                    {
                        Discard(k);
                        Request(kind);
                        return NativeSize.None;
                    }
                    shown = k;
                    shownSize = NativeSize.Medium;
                    cardW = cardH = 0; // nothing on screen until it is placed
                    cardMeasured = false;
                    AdsRunner.Run(PlaceMedium(k, ++renderId, plan));
                    return size;
                }

                if (!Render(k, plan, size))
                {
                    Discard(k);
                    Request(kind);
                    return NativeSize.None;
                }
                shown = k;
                shownSize = size;
                return size;
            }

            public static void HideShown()
            {
                if (shown < 0) return;
                int k = shown;
                shown = -1;
                shownSize = NativeSize.None;
                cardW = cardH = 0;
                cardMeasured = false;
                renderId++;
                Discard(k);
                Log(Name(k) + " hidden and destroyed");
                Request((NativeKind)k); // the next one, ready for the next result screen
            }

            static void Discard(int k)
            {
                var a = ads[k];
                ads[k] = null;
                if (a == null) return;
                try { a.Hide(); } catch (Exception) { }
                try { a.Destroy(); } catch (Exception) { }
            }
        }

        // ------------------------------------------------------------------ MREC (LevelPlay, result popups)

        /// <summary>
        /// The unit of the custom position LevelPlay gets for the MREC. LevelPlay's code that applies it is not in the
        /// Unity package, so this is checked on the device: the "MREC landing" log shows where the ad view really is.
        /// false = dp (the Android default), true = device pixels.
        /// </summary>
        public static bool MrecPositionInPixels = false;

        /// <summary>Height from the safe-area top down to the bottom limit (above the nav / gesture bar), design px.</summary>
        public static float MrecAvailableDesign
        {
            get
            {
                if (Screen.width <= 0 || Screen.height <= 0) return 0f;
                Rect safe = Screen.safeArea;
                float bottomPx = Mathf.Max(safe.yMin, DeviceToUnityPx(AndroidBottomInsetPx()));
                return UnityPxToDesign(safe.yMax - bottomPx);
            }
        }

        /// <summary>The window width in dp (the MREC is 300 dp of it).</summary>
        public static float ScreenWidthDp { get { return UnityPxToDp(Mathf.Max(1, Screen.width)); } }

        /// <summary>
        /// Loads the MREC of this kind, hidden, with its top at adTopD (design px from the safe-area top), centred.
        /// A loaded or loading one at the same place is kept; one at another place is replaced. Returns the ad top
        /// actually used (rounded to whole dp), in design px. Failed loads retry after 30 s, then 60 s.
        /// </summary>
        public static float PreloadMrec(MrecKind kind, float adTopD) { return MrecUnit.Request((int)kind, adTopD); }

        public static MrecState GetMrecState(MrecKind kind) { return MrecUnit.State((int)kind); }

        /// <summary>Shows the loaded MREC of this kind (destroys any other MREC on screen first). False when none is ready.</summary>
        public static bool ShowMrec(MrecKind kind) { return MrecUnit.Show((int)kind); }

        /// <summary>Hides and destroys the MREC of this kind, loaded, loading or on screen (cancels its retries).</summary>
        public static void DestroyMrec(MrecKind kind) { MrecUnit.Destroy((int)kind, "screen exit"); }

        /// <summary>How long the last load of this kind took (ms), -1 while it has not loaded.</summary>
        public static int MrecLoadMs(MrecKind kind) { return MrecUnit.LoadMs((int)kind); }

        /// <summary>The loaded ad's size in dp (300x250 unless LevelPlay reports another).</summary>
        public static Vector2 MrecSizeDp(MrecKind kind) { return MrecUnit.SizeDp((int)kind); }

        /// <summary>
        /// One LevelPlayBannerAd (MEDIUM_RECTANGLE) per kind, at a custom position fixed when it is created (LevelPlay
        /// has no move API), no display on load, auto refresh paused until shown. Shown = ShowAd + refresh resumed;
        /// exit = DestroyAd. Callbacks run on the Unity main thread (LevelPlay posts them).
        /// </summary>
        static class MrecUnit
        {
            static readonly float[] RetryDelays = { 30f, 60f }; // after failed try 1 and 2; try 3 failing stops
            const int Kinds = 3;

            static readonly LevelPlayBannerAd[] ads = new LevelPlayBannerAd[Kinds];
            static readonly Vector2[] posDp = new Vector2[Kinds];
            static readonly Vector2[] sizeDp = new Vector2[Kinds];
            static readonly bool[] waiting = new bool[Kinds], loading = new bool[Kinds], ready = new bool[Kinds], failed = new bool[Kinds];
            static readonly bool[] refreshPaused = new bool[Kinds], offLogged = new bool[Kinds];
            static readonly int[] failures = new int[Kinds], retryIds = new int[Kinds], gen = new int[Kinds], loadMs = new int[Kinds];
            static readonly float[] loadStart = new float[Kinds];
            static int shown = -1;

            static string Id(int k)
            {
                return k == (int)MrecKind.Bonus ? AdIds.LevelPlayMrecBonus : k == (int)MrecKind.Win ? AdIds.LevelPlayMrecWin : AdIds.LevelPlayMrecLose;
            }

            static string Name(int k) { return "MREC " + (MrecKind)k; }

            static string OffReason(int k)
            {
                if (string.IsNullOrEmpty(Id(k))) return "no LevelPlay ad unit ID (AdIds.LevelPlayMrec" + (MrecKind)k + ")";
                if (LevelPlayOff) return "LevelPlay off (" + (LevelPlayError ?? "?") + ")";
                return null;
            }

            public static MrecState State(int k)
            {
                if (OffReason(k) != null) return MrecState.Off;
                if (ready[k]) return MrecState.Ready;
                if (loading[k] || waiting[k]) return MrecState.Loading;
                if (failed[k]) return MrecState.Failed;
                return MrecState.Idle;
            }

            public static int LoadMs(int k) { return ready[k] ? loadMs[k] : -1; }

            public static Vector2 SizeDp(int k)
            {
                return sizeDp[k].x > 0f && sizeDp[k].y > 0f ? sizeDp[k] : new Vector2(MrecFit.AdWDp, MrecFit.AdHDp);
            }

            /// <summary>Whole-dp position for an ad top in design px (centred), and that top back in design px.</summary>
            static Vector2 Position(float adTopD, out float roundedTopD)
            {
                float safeTopPx = Screen.height - Screen.safeArea.yMax;
                float y = Mathf.Round(UnityPxToDp(safeTopPx + DesignToUnityPx(adTopD)));
                float x = Mathf.Round((UnityPxToDp(Screen.width) - MrecFit.AdWDp) * 0.5f);
                roundedTopD = UnityPxToDesign(DpToUnityPx(y) - safeTopPx);
                return new Vector2(x, y);
            }

            public static float Request(int k, float adTopD)
            {
                float topD;
                Vector2 p = Position(adTopD, out topD);
                string off = OffReason(k);
                if (off != null)
                {
                    if (!offLogged[k]) Log(Name(k) + " off: " + off);
                    offLogged[k] = true;
                    return topD;
                }
                if ((ads[k] != null || waiting[k]) && (posDp[k] - p).sqrMagnitude > 0.25f)
                {
                    Log(Name(k) + " position changes from " + posDp[k] + " to " + p + " dp: new ad");
                    Destroy(k, "moved");
                }
                posDp[k] = p; // a pending retry loads at the newest place
                if (ads[k] != null || waiting[k]) return topD;                        // loading or loaded here
                if (failed[k] && failures[k] <= RetryDelays.Length) return topD;      // a retry is pending
                failures[k] = 0;
                retryIds[k]++;
                Load(k);
                return topD;
            }

            static void Load(int k)
            {
                if (ads[k] != null || waiting[k]) return;
                int g = gen[k];
                waiting[k] = true;
                if (!WhenLevelPlayReady(() => { if (g == gen[k]) { waiting[k] = false; Create(k); } }))
                {
                    waiting[k] = false;
                    Log(Name(k) + " not loaded: LevelPlay off (" + (LevelPlayError ?? "?") + ")");
                }
            }

            static void Create(int k)
            {
                if (ads[k] != null) return;
                Vector2 p = posDp[k];
                Vector2 sent = MrecPositionInPixels ? p * Density : p;
                LevelPlayBannerAd a;
                try
                {
                    var config = new LevelPlayBannerAd.Config.Builder()
                        .SetSize(LevelPlayAdSize.MEDIUM_RECTANGLE)
                        .SetPosition(new LevelPlayBannerPosition(new Vector2(Mathf.Round(sent.x), Mathf.Round(sent.y))))
                        .SetRespectSafeArea(false) // the position is from the screen's top-left, safe area already counted
                        .SetDisplayOnLoad(false)   // hidden until its screen shows it
                        .Build();
                    a = new LevelPlayBannerAd(Id(k), config);
                }
                catch (Exception e)
                {
                    Failed(k, "create threw " + e.Message);
                    return;
                }
                ads[k] = a;
                int g = gen[k];
                a.OnAdLoaded += info => { if (g == gen[k] && ads[k] == a) OnLoaded(k, info); };
                a.OnAdLoadFailed += err =>
                {
                    if (g == gen[k] && ads[k] == a) Failed(k, err != null ? "code " + err.ErrorCode + ": " + err.ErrorMessage : "no ad");
                };
                a.OnAdDisplayFailed += (info, err) =>
                {
                    if (g == gen[k] && ads[k] == a)
                        Log(Name(k) + " display failed: " + (err != null ? "code " + err.ErrorCode + ": " + err.ErrorMessage : "unknown"));
                };
                loading[k] = true;
                failed[k] = false;
                loadStart[k] = Time.realtimeSinceStartup;
                Log(Name(k) + " load at x " + p.x + " y " + p.y + " dp (sent as " + Mathf.Round(sent.x) + ", " + Mathf.Round(sent.y) +
                    (MrecPositionInPixels ? " px" : " dp") + ", density " + Density.ToString("0.###") + ")");
                try { a.LoadAd(); }
                catch (Exception e) { Failed(k, "LoadAd threw " + e.Message); }
            }

            static void OnLoaded(int k, LevelPlayAdInfo info)
            {
                if (ready[k])
                {
                    Log(Name(k) + " refreshed" + Network(info));
                    return;
                }
                loading[k] = false;
                ready[k] = true;
                failed[k] = false;
                failures[k] = 0;
                loadMs[k] = Mathf.RoundToInt((Time.realtimeSinceStartup - loadStart[k]) * 1000f);
                if (info != null && info.AdSize != null && info.AdSize.Width > 0 && info.AdSize.Height > 0)
                    sizeDp[k] = new Vector2(info.AdSize.Width, info.AdSize.Height);
                Log(Name(k) + " load ok in " + loadMs[k] + " ms" + Network(info) + ", size " + SizeDp(k).x + "x" + SizeDp(k).y + " dp");
                if (shown != k) SetRefresh(k, false); // hidden: no refresh until it shows
            }

            static void Failed(int k, string why)
            {
                if (ready[k])
                {
                    Log(Name(k) + " refresh failed: " + why + " (the current ad stays)");
                    return;
                }
                loading[k] = false;
                failed[k] = true;
                failures[k]++;
                DropObject(k);
                if (failures[k] > RetryDelays.Length)
                {
                    Log(Name(k) + " load failed: " + why + " (try " + failures[k] + ", stopped until the next request)");
                    return;
                }
                float delay = RetryDelays[failures[k] - 1];
                Log(Name(k) + " load failed: " + why + " (try " + failures[k] + ", retry in " + delay + " s)");
                AdsRunner.Run(Retry(k, ++retryIds[k], delay));
            }

            static IEnumerator Retry(int k, int id, float delay)
            {
                float t = 0f;
                while (t < delay) { t += Time.unscaledDeltaTime; yield return null; }
                if (id == retryIds[k] && ads[k] == null && OffReason(k) == null) Load(k);
            }

            static void SetRefresh(int k, bool on)
            {
                if (ads[k] == null || refreshPaused[k] == !on) return;
                try
                {
                    if (on) ads[k].ResumeAutoRefresh();
                    else ads[k].PauseAutoRefresh();
                    refreshPaused[k] = !on;
                }
                catch (Exception e) { Log(Name(k) + " " + (on ? "resume" : "pause") + " refresh failed: " + e.Message); }
            }

            public static bool Show(int k)
            {
                if (!ready[k] || ads[k] == null) return false;
                if (shown >= 0 && shown != k) Destroy(shown, "another MREC shows"); // one on screen at a time
                try { ads[k].ShowAd(); }
                catch (Exception e)
                {
                    Log(Name(k) + " show failed: " + e.Message);
                    return false;
                }
                shown = k;
                SetRefresh(k, true);
                Log(Name(k) + " shown");
                AdsRunner.Run(Landing(k, gen[k]));
                return true;
            }

            public static void Destroy(int k, string why)
            {
                gen[k]++;       // late callbacks / queued loads of the old one do nothing
                retryIds[k]++;  // and no pending retry
                bool had = ads[k] != null || waiting[k];
                DropObject(k);
                waiting[k] = loading[k] = ready[k] = failed[k] = false;
                failures[k] = 0;
                if (shown == k) shown = -1;
                if (had) Log(Name(k) + " destroyed (" + why + ")");
            }

            static void DropObject(int k)
            {
                var a = ads[k];
                ads[k] = null;
                refreshPaused[k] = false;
                sizeDp[k] = Vector2.zero;
                if (a == null) return;
                try { a.HideAd(); } catch (Exception) { }
                try { a.DestroyAd(); } catch (Exception) { }
            }

            // ---- where the ad really landed (dp versus px check)

            /// <summary>
            /// A few frames after ShowAd: logs the requested rect (dp and device px), then, on Android, scans the view
            /// tree on the UI thread for ad views and logs where each one is on screen (getGlobalVisibleRect, device px
            /// and dp). If the found view sits at the requested px, the position unit is right; if it sits at the
            /// requested value x density (or / density), flip MrecPositionInPixels.
            /// </summary>
            static IEnumerator Landing(int k, int g)
            {
                for (int i = 0; i < 20; i++) yield return null;
                if (shown != k || g != gen[k]) yield break;
                Vector2 p = posDp[k], s = SizeDp(k);
                float d = Density;
                string who = Name(k);
                Log(who + " landing expected: x " + p.x + " y " + p.y + " w " + s.x + " h " + s.y + " dp = x " + Mathf.RoundToInt(p.x * d) +
                    " y " + Mathf.RoundToInt(p.y * d) + " w " + Mathf.RoundToInt(s.x * d) + " h " + Mathf.RoundToInt(s.y * d) +
                    " device px (density " + d.ToString("0.###") + ", position sent as " + (MrecPositionInPixels ? "px" : "dp") + ")");
                if (Application.platform != RuntimePlatform.Android) yield break;
                try
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        activity.Call("runOnUiThread", new AndroidJavaRunnable(() => ScanViews(who, d)));
                }
                catch (Exception e) { Log(who + " landing scan not started: " + e.Message); }
            }

            static void ScanViews(string who, float d)
            {
                try
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (var window = activity.Call<AndroidJavaObject>("getWindow"))
                    using (var decor = window.Call<AndroidJavaObject>("getDecorView"))
                    {
                        int found = 0;
                        Scan(decor, who, d, 0, ref found);
                        if (found == 0) Debug.Log("[Ads] " + who + " landing: no ad view found in the view tree");
                    }
                }
                catch (Exception e) { Debug.Log("[Ads] " + who + " landing scan failed: " + e.Message); }
            }

            /// <summary>Depth-first: logs views whose class looks like an ad view (LevelPlay banner or AdMob AdView), up to 6.</summary>
            static void Scan(AndroidJavaObject v, string who, float d, int depth, ref int found)
            {
                if (v == null || depth > 30 || found >= 6) return;
                string cls = "";
                try { using (var c = v.Call<AndroidJavaObject>("getClass")) cls = c.Call<string>("getName") ?? ""; } catch (Exception) { }
                if (cls.IndexOf("Banner", StringComparison.OrdinalIgnoreCase) >= 0 || cls.EndsWith("AdView", StringComparison.Ordinal))
                {
                    try
                    {
                        using (var r = new AndroidJavaObject("android.graphics.Rect"))
                        {
                            bool vis = v.Call<bool>("getGlobalVisibleRect", r);
                            int l = r.Get<int>("left"), t = r.Get<int>("top"), rr = r.Get<int>("right"), b = r.Get<int>("bottom");
                            int w = v.Call<int>("getWidth"), h = v.Call<int>("getHeight");
                            found++;
                            Debug.Log("[Ads] " + who + " landing: " + cls + " at x " + l + " y " + t + " w " + w + " h " + h + " px = x " +
                                      (l / d).ToString("0.#") + " y " + (t / d).ToString("0.#") + " w " + (w / d).ToString("0.#") + " h " +
                                      (h / d).ToString("0.#") + " dp (visible " + vis + ", visible rect to " + rr + ", " + b + ")");
                        }
                    }
                    catch (Exception e) { Debug.Log("[Ads] " + who + " landing: " + cls + " (rect unreadable: " + e.Message + ")"); }
                }
                int n = 0;
                try { n = v.Call<int>("getChildCount"); } catch (Exception) { return; } // not a ViewGroup
                for (int i = 0; i < n && found < 6; i++)
                {
                    AndroidJavaObject child = null;
                    try { child = v.Call<AndroidJavaObject>("getChildAt", i); } catch (Exception) { }
                    if (child == null) continue;
                    using (child) Scan(child, who, d, depth + 1, ref found);
                }
            }
        }

        // ------------------------------------------------------------------ banner

        /// <summary>
        /// Shows (true) or hides (false) the bottom banner; screen = the calling screen, for the logs. A show in a frame
        /// always wins over a hide in the same frame, in either order, and the hide itself is applied at the end of the
        /// frame: a screen switch between two banner screens never hides or flickers it, whatever order GameApp calls
        /// the two screens in.
        /// </summary>
        public static void Banner(bool show, string screen = "?")
        {
            BannerAd.Set(show, screen);
        }

        /// <summary>The debug line for BannerDebug (screen = the screen showing it).</summary>
        public static string BannerDebugLine(string screen)
        {
            return BannerAd.DebugLine(screen);
        }

        /// <summary>
        /// Height the banner takes from the screen bottom, in design px (390 wide, as Theme.D / Ui.PlaceD): the bottom
        /// safe-area inset (the banner sits above it) + the adaptive banner's height (a 56 dp estimate until known),
        /// 0 when there is no banner (no LevelPlay banner ID, or LevelPlay off).
        /// </summary>
        public static float BannerHeightDesign
        {
            get { return string.IsNullOrEmpty(AdIds.LevelPlayBanner) || LevelPlayOff ? 0f : BannerAd.HeightDesign; }
        }

        /// <summary>Raised when BannerHeightDesign changes (the first real ad replaces the estimate).</summary>
        public static event Action BannerHeightChanged;

        /// <summary>A screen currently wants the banner on screen (Banner(true)).</summary>
        public static bool IsBannerWanted { get { return BannerAd.Want; } }

        /// <summary>The banner has loaded an ad (a later refresh keeps the old ad on screen while it loads).</summary>
        public static bool HasBannerAd { get { return BannerAd.HasAd; } }

        /// <summary>Seconds since the banner started waiting for its first ad (0 before that; frozen once an ad loaded).</summary>
        public static float BannerWaitSeconds { get { return BannerAd.WaitSeconds; } }

        static void RaiseBannerHeightChanged()
        {
            var h = BannerHeightChanged;
            if (h != null) h();
        }

        /// <summary>Device px (what the SDK reports) to design px: 390 design px = the window width.</summary>
        static float PxToDesign(float px)
        {
            return px * 390f / PhysicalWidth;
        }

        /// <summary>dp to design px, from the same display source as the native ads (ReadDisplay).</summary>
        static float DpToDesign(float dp)
        {
            return PxToDesign(dp * Density);
        }

        /// <summary>Bottom safe-area inset (the space under a safe-area banner) in design px, from Screen.safeArea.</summary>
        static float BottomInsetDesign
        {
            get { return Screen.width > 0 ? UnityPxToDesign(Mathf.Max(0f, Screen.safeArea.yMin)) : 0f; }
        }

        /// <summary>
        /// One LevelPlay adaptive banner (ad unit AdIds.LevelPlayBanner) at the bottom centre, inside the safe area
        /// (above the navigation / gesture bar). Created hidden (no display on load) and loaded as soon as LevelPlay is
        /// ready (during the splash), then reused for every screen: HideAd / ShowAd, never destroyed, so Banner(true)
        /// shows the loaded ad at once. Refresh is LevelPlay's own auto refresh (interval set in the LevelPlay
        /// dashboard); it is paused whenever the banner is hidden (a screen without banner, a popup with an MREC, a
        /// full-screen ad) and resumed when it shows again. Before the first ad, failed loads retry quickly
        /// (FirstRetry), also while hidden; after that LevelPlay keeps the old ad up and retries on its own.
        /// The height screens keep free (HeightDesign) = the bottom safe-area inset + the adaptive banner height.
        /// </summary>
        static class BannerAd
        {
            const float LoadTimeout = 15f, EstimateDp = 56f;
            static readonly float[] FirstRetry = { 3f, 6f, 15f, 30f }; // until the first ad has loaded

            static LevelPlayBannerAd view;
            static bool want, shown, creating, loading, hasAd, appPaused, visiblePending, refreshPaused;
            static float timer, nextDelay;
            static float loadCalledAt, showAskedAt; // realtimeSinceStartup, for the timing logs
            static float waitStartedAt = -1f, waitEndedAt = -1f; // first wait for an ad (BannerPlaceholder)
            static float adDp = -1f; // the adaptive container's height (dp), known when the size is created
            static int failures;
            static float heightDesign = -1f;
            static int showFrame = -1;                // frame of the last Banner(true)
            static bool decisionPending;              // a Banner call happened this frame: log the end-of-frame result
            static string showBy = "none", hideBy = "none", lastError = "none";

            public static bool Want { get { return want; } }
            public static bool HasAd { get { return hasAd; } }

            public static float WaitSeconds
            {
                get
                {
                    if (waitStartedAt < 0f) return 0f;
                    return (waitEndedAt >= 0f ? waitEndedAt : Now) - waitStartedAt;
                }
            }

            static void StartWait()
            {
                if (waitStartedAt < 0f && !hasAd) waitStartedAt = Now;
            }

            /// <summary>Inset + banner in design px: the real adaptive height once known, a 56 dp estimate before.</summary>
            public static float HeightDesign
            {
                get { return heightDesign > 0f ? heightDesign : BottomInsetDesign + DpToDesign(EstimateDp); }
            }

            static float Now { get { return Time.realtimeSinceStartup; } }

            static string Ms(float since)
            {
                return Mathf.RoundToInt((Now - since) * 1000f) + " ms";
            }

            /// <summary>Creates the banner hidden and starts the first load once LevelPlay is ready (Ads.Init).</summary>
            public static void Preload()
            {
                if (view != null || creating) return;
                if (string.IsNullOrEmpty(AdIds.LevelPlayBanner))
                {
                    Log("banner not available (no LevelPlay ad unit ID in AdIds.LevelPlayBanner)");
                    return;
                }
                AdsRunner.Ensure();
                creating = true;
                if (!WhenLevelPlayReady(Create))
                {
                    creating = false;
                    Log("banner not created: LevelPlay off (" + (LevelPlayError ?? "?") + ")");
                }
            }

            public static void Set(bool show, string who)
            {
                decisionPending = true;
                if (show)
                {
                    showFrame = Time.frameCount;
                    showBy = who;
                }
                else if (showFrame == Time.frameCount)
                {
                    // e.g. Home -> game: ScreenGame.OnShow asked first, then ScreenHome.OnHide; the new screen wins
                    Log("Banner(false) from " + who + " ignored: " + showBy + " asked to show it this frame");
                    return;
                }
                else hideBy = who;
                Log("Banner(" + (show ? "true" : "false") + ") from " + who + (show == want ? " (already " + (want ? "wanted)" : "hidden)") : ""));
                if (!show)
                {
                    want = false; // hidden in LateTick (refresh paused there)
                    return;
                }
                if (string.IsNullOrEmpty(AdIds.LevelPlayBanner))
                {
                    if (want) Log("banner off for " + who + " (no ad unit ID)");
                    want = false;
                    return;
                }
                if (want) return; // already wanted
                want = true;
                showAskedAt = Now;
                StartWait();
                visiblePending = true;
                if (view == null) Preload(); // normally already created during the splash
                // shown in LateTick (end of this frame), unless a full-screen ad is up
            }

            static void Create()
            {
                creating = false;
                if (view != null) return;
                LevelPlayAdSize size;
                try
                {
                    size = LevelPlayAdSize.CreateAdaptiveAdSize(); // full width, height from the ad networks' adaptive rule
                    var config = new LevelPlayBannerAd.Config.Builder()
                        .SetSize(size)
                        .SetPosition(LevelPlayBannerPosition.BottomCenter)
                        .SetRespectSafeArea(true)  // above the navigation / gesture bar
                        .SetDisplayOnLoad(false)   // shown only when a screen wants it (Reveal)
                        .Build();
                    view = new LevelPlayBannerAd(AdIds.LevelPlayBanner, config);
                }
                catch (Exception e)
                {
                    view = null;
                    Log("banner create failed: " + e.Message);
                    return;
                }
                var v = view; // a dropped view's late callbacks must not touch a new one
                view.OnAdLoaded += info => { if (v == view) OnLoaded(info); };
                view.OnAdLoadFailed += err =>
                {
                    if (v == view) Failed(err != null ? "code " + err.ErrorCode + ": " + err.ErrorMessage : "unknown");
                };
                view.OnAdDisplayFailed += (info, err) =>
                    Log("banner display failed: " + (err != null ? "code " + err.ErrorCode + ": " + err.ErrorMessage : "unknown"));

                int h = 0;
                try { h = size.Height; } catch (Exception) { }
                if (h > 0)
                {
                    adDp = h;
                    SetHeight(BottomInsetDesign + DpToDesign(adDp), "adaptive size " + h + " dp");
                }
                shown = false;
                Log("banner create (adaptive " + (h > 0 ? h + " dp high" : "height unknown until load") + ", " +
                    (want ? "wanted" : "hidden, preloading") + ")");
                Load();
            }

            /// <summary>
            /// Banner wanted and no full-screen ad up (every frame from LateTick; only the hidden -> shown change acts):
            /// show it now (with whatever ad it has) and resume its refresh.
            /// </summary>
            static void Reveal()
            {
                if (view == null) return;
                if (!shown)
                {
                    try { view.ShowAd(); } catch (Exception e) { Log("banner show failed: " + e.Message); }
                    shown = true;
                    Log("banner show");
                    if (!hasAd && !loading) Load(); // no ad yet and waiting for a retry: try now, a screen needs it
                }
                SetRefresh(true);
                if (hasAd) LogVisible("ad already loaded");
            }

            static void Conceal(string why)
            {
                if (view == null) return;
                SetRefresh(false); // no refresh while nobody can see it
                if (!shown) return;
                try { view.HideAd(); } catch (Exception e) { Log("banner hide failed: " + e.Message); }
                shown = false;
                Log("banner hide (" + why + ")");
            }

            static void SetRefresh(bool on)
            {
                if (view == null || !hasAd || refreshPaused == !on) return;
                try
                {
                    if (on) view.ResumeAutoRefresh();
                    else view.PauseAutoRefresh();
                    refreshPaused = !on;
                }
                catch (Exception e) { Log("banner " + (on ? "resume" : "pause") + " refresh failed: " + e.Message); }
            }

            /// <summary>LoadAd: only for the first ad and its retries; after that LevelPlay refreshes on its own.</summary>
            static void Load()
            {
                if (loading || view == null || hasAd) return;
                loading = true;
                loadCalledAt = Now;
                StartWait();
                Log("banner LoadAd (first ad)");
                try { view.LoadAd(); }
                catch (Exception e) { Failed(e.Message); }
            }

            static void OnLoaded(LevelPlayAdInfo info)
            {
                bool first = !hasAd;
                if (first) waitEndedAt = Now;
                hasAd = true;
                loading = false;
                failures = 0;
                timer = 0f;
                if (first) Log("banner load ok after " + Ms(loadCalledAt) + " from LoadAd (first ad" + Network(info) + ")");
                else Log("banner refreshed" + Network(info));

                if (adDp <= 0f && info != null && info.AdSize != null && info.AdSize.Height > 0)
                {
                    adDp = info.AdSize.Height;
                    SetHeight(BottomInsetDesign + DpToDesign(adDp), "loaded ad " + info.AdSize.Height + " dp");
                }

                bool visible = want && !IsFullScreenAdShowing;
                if (!visible)
                {
                    SetRefresh(false); // hidden: keep the ad, stop refreshing
                    return;
                }
                if (first)
                {
                    // ShowAd may have come before the ad existed (no display on load): ask again now that it does
                    try { view.ShowAd(); shown = true; } catch (Exception e) { Log("banner show failed: " + e.Message); }
                }
                LogVisible("waited for the load");
            }

            static void Failed(string why)
            {
                loading = false;
                timer = 0f;
                lastError = why.Length > 40 ? why.Substring(0, 40) : why;
                if (hasAd)
                {
                    Log("banner refresh failed: " + why + " (the current ad stays; LevelPlay retries)");
                    return;
                }
                failures++;
                nextDelay = FirstRetry[Mathf.Min(failures, FirstRetry.Length) - 1];
                Log("banner load failed after " + Ms(loadCalledAt) + ": " + why + " (retry in " + nextDelay + " s)");
            }

            /// <summary>Once per Banner(true): how long until an ad was actually on screen.</summary>
            static void LogVisible(string how)
            {
                if (!visiblePending) return;
                visiblePending = false;
                Log("banner visible " + Ms(showAskedAt) + " after Banner(true) (" + how + ")");
            }

            static void SetHeight(float d, string from)
            {
                if (Mathf.Abs(d - heightDesign) < 0.5f) return;
                heightDesign = d;
                Log("banner height " + d.ToString("0.#") + " design px (" + from + " + bottom safe-area inset " +
                    BottomInsetDesign.ToString("0.#") + ")");
                RaiseBannerHeightChanged();
            }

            /// <summary>Every frame (AdsRunner.Update): first-load timeout and retries.</summary>
            public static void Tick()
            {
                if (loading)
                {
                    if (Now - loadCalledAt > LoadTimeout) Failed("no answer in " + LoadTimeout + " s");
                    return;
                }
                if (view == null || appPaused || hasAd) return;
                timer += Time.unscaledDeltaTime;
                if (timer < nextDelay) return;
                Log("banner retry");
                Load();
            }

            /// <summary>
            /// End of frame (AdsRunner.LateUpdate): visible = a screen wants it and no full-screen ad is up. Applies a hide
            /// that no Banner(true) cancelled this frame, hides it under a full-screen ad and brings it back after.
            /// </summary>
            public static void LateTick()
            {
                if (decisionPending)
                {
                    decisionPending = false;
                    Log("banner end of frame: " + (want ? "show (wanted by " + showBy + ")"
                                                        : "hide (" + hideBy + " hid it, no screen asked to show)"));
                }
                if (!want) Conceal("no screen wants it");
                else if (IsFullScreenAdShowing) Conceal("full-screen ad");
                else Reveal();
            }

            public static string DebugLine(string screen)
            {
                string init = LevelPlayReady ? "ok" : LevelPlayOff ? "OFF" : "not";
                string err = LevelPlayOff && LevelPlayError != null ? LevelPlayError : lastError;
                if (err.Length > 40) err = err.Substring(0, 40);
                return "wanted=" + (want ? "Y" : "N") + " loaded=" + (hasAd ? "Y" : "N") +
                       " visible=" + (want && shown && hasAd ? "Y" : "N") +
                       " height=" + Mathf.RoundToInt(BannerHeightDesign) + " screen=" + screen +
                       " id=" + (string.IsNullOrEmpty(AdIds.LevelPlayBanner) ? "empty" : "set") + " levelplay=" + init +
                       " lastError=" + err;
            }

            public static void AppPaused(bool paused)
            {
                appPaused = paused;
                if (!paused) timer = 0f; // back in the foreground: the retry wait starts again from zero
            }
        }

        /// <summary>Hidden host for per-frame ad work (banner timer, app pause).</summary>
        class AdsRunner : MonoBehaviour
        {
            static AdsRunner inst;

            public static void Ensure()
            {
                if (inst != null) return;
                var go = new GameObject("AdsRunner");
                DontDestroyOnLoad(go);
                inst = go.AddComponent<AdsRunner>();
            }

            /// <summary>Runs a coroutine on the runner (retries, show watchdogs).</summary>
            public static void Run(IEnumerator routine)
            {
                Ensure();
                inst.StartCoroutine(routine);
            }

            void Update() { BannerAd.Tick(); }
            void LateUpdate() { BannerAd.LateTick(); }
            void OnApplicationPause(bool paused)
            {
                if (!paused) MarkReturn();
                BannerAd.AppPaused(paused);
            }
        }
    }
}
