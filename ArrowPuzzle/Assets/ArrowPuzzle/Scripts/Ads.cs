using System;
using System.Collections;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using UnityEngine;

namespace ArrowPuzzle
{
    /// <summary>
    /// Every ad unit ID, in one place. TestMode = true uses Google's test units; false uses the real set, where an
    /// empty ID means "this ad is not available" (a test ID is never used with TestMode = false).
    /// </summary>
    public static class AdIds
    {
        // Google's Android test units
        const string TestAppOpen = "ca-app-pub-3940256099942544/9257395921";
        const string TestBanner = "ca-app-pub-3940256099942544/6300978111";
        const string TestInterstitial = "ca-app-pub-3940256099942544/1033173712";
        const string TestRewarded = "ca-app-pub-3940256099942544/5224354917";

        // paste real AdMob IDs here
        const string RealAppOpen = "";
        const string RealBanner = "";
        const string RealInterstitial = "";
        const string RealRewarded = "";

        public static string AppOpen { get { return Ads.TestMode ? TestAppOpen : RealAppOpen; } }
        public static string Banner { get { return Ads.TestMode ? TestBanner : RealBanner; } }
        public static string Interstitial { get { return Ads.TestMode ? TestInterstitial : RealInterstitial; } }
        public static string Rewarded { get { return Ads.TestMode ? TestRewarded : RealRewarded; } }
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

        static bool initStarted;
        static event Action initDone; // loads that were asked for before Ready

        /// <summary>Starts the SDK once (later calls do nothing). Never throws.</summary>
        public static void Init()
        {
            if (initStarted) return;
            initStarted = true;
            Log("init start (TestMode " + TestMode + ")");
            AdsRunner.Ensure(); // app pause / resume tracking
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
            BannerAd.Preload(); // first in line: Home shows it right after the splash
            LoadInterstitial(); // these start once Initialize has finished: one of each always ready
            LoadRewarded();
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
                Log("app open on return skipped: " + skip);
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
            if (backgroundForLink) return "the return is from a link the player opened (share / rate / privacy policy)";
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

        /// <summary>One app open ad: load, 4 hour expiry, show.</summary>
        static class AppOpen
        {
            static readonly TimeSpan MaxAge = TimeSpan.FromHours(4);
            const float OpenTimeout = 5f; // s (unscaled) for the ad to appear after Show before we give up

            static AppOpenAd ad;
            static DateTime loadedAt;
            static bool loading, waitingForInit, opened;
            static Action closed;

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
                string id = AdIds.AppOpen;
                if (string.IsNullOrEmpty(id))
                {
                    Log("app open not available (no ad unit ID)");
                    return;
                }
                waitingForInit = true;
                if (!WhenReady(() => { waitingForInit = false; Request(id); }))
                {
                    waitingForInit = false;
                    Log("app open not loaded: SDK init failed");
                }
            }

            static void Request(string id)
            {
                loading = true;
                Log("app open load start");
                try
                {
                    AppOpenAd.Load(id, new AdRequest(), (loaded, error) => OnMain(() =>
                    {
                        loading = false;
                        if (error != null || loaded == null)
                        {
                            Log("app open load failed: " + (error != null ? error.GetMessage() : "no ad"));
                            return;
                        }
                        ad = loaded;
                        loadedAt = DateTime.UtcNow;
                        Hook(ad);
                        Log("app open load ok");
                    }));
                }
                catch (Exception e)
                {
                    loading = false;
                    Log("app open load failed: " + e.Message);
                }
            }

            static void Hook(AppOpenAd a)
            {
                a.OnAdFullScreenContentOpened += () => OnMain(() =>
                {
                    opened = true;
                    Log("app open shown");
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
                    Log("app open not shown: another full-screen ad is showing");
                    done();
                    return;
                }
                if (!IsReady)
                {
                    bool stale = ad != null && Expired;
                    Log(stale ? "app open not shown: expired" : "app open not shown: none loaded");
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

        // ------------------------------------------------------------------ interstitial

        /// <summary>Loads one interstitial (no-op if one is loaded or loading). Also resets the retry count.</summary>
        public static void LoadInterstitial() { Interstitial.Request(); }

        /// <summary>
        /// Shows the interstitial if one is loaded and no other full-screen ad is up. onClosed is called exactly once
        /// in every case: closed, failed to show, nothing loaded, another ad showing, skipped, or no response in 5 s.
        /// </summary>
        public static void ShowInterstitial(Action onClosed) { Interstitial.Show(onClosed); }

        /// <summary>
        /// One interstitial kept ready: loaded after Init and again right after each one closes or fails to show.
        /// A failed load retries after 30 s, then 60 s, then waits for the next request.
        /// </summary>
        static class Interstitial
        {
            static readonly float[] RetryDelays = { 30f, 60f }; // after failed try 1 and 2; try 3 failing stops
            const float OpenTimeout = 5f;

            static InterstitialAd ad;
            static bool loading, waitingForInit, opened;
            static int failures, retryId;
            static Action closed;
            static float closedAt = -1000f;

            /// <summary>A new request (app start, Show with nothing loaded, after a close): fresh retry budget.</summary>
            public static void Request()
            {
                failures = 0;
                retryId++; // a pending retry is replaced by this request
                Load();
            }

            static void Load()
            {
                if (loading || waitingForInit || ad != null) return;
                string id = AdIds.Interstitial;
                if (string.IsNullOrEmpty(id))
                {
                    Log("interstitial not available (no ad unit ID)");
                    return;
                }
                waitingForInit = true;
                if (!WhenReady(() => { waitingForInit = false; Fetch(id); }))
                {
                    waitingForInit = false;
                    Log("interstitial not loaded: SDK init failed");
                }
            }

            static void Fetch(string id)
            {
                loading = true;
                try
                {
                    InterstitialAd.Load(id, new AdRequest(), (loaded, error) => OnMain(() =>
                    {
                        loading = false;
                        if (error != null || loaded == null)
                        {
                            Failed(error != null ? error.GetMessage() : "no ad");
                            return;
                        }
                        failures = 0;
                        ad = loaded;
                        Hook(ad);
                        Log("interstitial load ok");
                    }));
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

            static void Hook(InterstitialAd a)
            {
                a.OnAdFullScreenContentOpened += () => OnMain(() =>
                {
                    opened = true;
                    Log("interstitial shown");
                });
                a.OnAdFullScreenContentClosed += () => OnMain(() =>
                {
                    Log("interstitial closed");
                    closedAt = Time.unscaledTime;
                    Finish();
                });
                a.OnAdFullScreenContentFailed += err => OnMain(() =>
                {
                    Log("interstitial failed to show: " + (err != null ? err.GetMessage() : "unknown"));
                    Finish();
                });
            }

            public static void Show(Action onClosed)
            {
                var done = Once(onClosed);
                float now = Time.unscaledTime;
                float gap = InterstitialMinGapSeconds; // a variable, so a 0 gap gives no unreachable-code warning
                string skip = null;
                if (IsFullScreenAdShowing) skip = "another full-screen ad is showing";
                else if (now - appOpenClosedAt < AfterAppOpenGap) skip = "app open ad closed less than " + AfterAppOpenGap + " s ago";
                else if (gap > 0f && now - closedAt < gap) skip = "less than " + gap + " s since the last one";
                else if (ad == null || !ad.CanShowAd()) skip = "none loaded";
                if (skip != null)
                {
                    Log("interstitial skipped: " + skip);
                    if (ad != null && !ad.CanShowAd()) Drop(); // expired / used: replace it
                    if (ad == null) Request();
                    done();
                    return;
                }

                closed = done;
                opened = false;
                BeginFullScreen();
                Log("interstitial show");
                try
                {
                    ad.Show();
                    AdsRunner.Run(OpenWatch(ad));
                }
                catch (Exception e)
                {
                    Log("interstitial failed to show: " + e.Message);
                    Finish();
                }
            }

            /// <summary>Safety net like the app open ad: no "opened" / "failed" within OpenTimeout s releases the game.</summary>
            static IEnumerator OpenWatch(InterstitialAd shown)
            {
                float t = 0f;
                while (!opened && t < OpenTimeout && closed != null && ad == shown)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (!opened && closed != null && ad == shown)
                {
                    Log("interstitial did not appear in " + OpenTimeout + " s, continuing");
                    Finish();
                }
            }

            /// <summary>After close or a failed show: drop the ad, restore audio, call back once, load the next.</summary>
            static void Finish()
            {
                Drop();
                EndFullScreen();
                var cb = closed;
                closed = null;
                Request();
                if (cb != null) cb();
            }

            static void Drop()
            {
                if (ad == null) return;
                var a = ad;
                ad = null;
                try { a.Destroy(); } catch (Exception) { }
            }
        }

        // ------------------------------------------------------------------ rewarded

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

        /// <summary>One rewarded ad kept ready: loaded after Init and again after each one closes or fails to show.</summary>
        static class Rewarded
        {
            static readonly float[] RetryDelays = { 30f, 60f }; // after failed try 1 and 2; try 3 failing stops
            const float WaitForLoad = 6f, OpenTimeout = 5f;
            const float LateRewardWait = 0.5f; // s after close: the reward callback may arrive just after "closed"

            static RewardedAd ad;
            static bool loading, waitingForInit, opened, earned, busy;
            static int failures, retryId;
            static Action reward, fail;

            public static bool Busy { get { return busy; } }

            static bool Ready { get { return ad != null && ad.CanShowAd(); } }

            public static void Request()
            {
                failures = 0;
                retryId++; // a pending retry is replaced by this request
                Load();
            }

            static void Load()
            {
                if (loading || waitingForInit) return;
                if (ad != null)
                {
                    if (ad.CanShowAd()) return;
                    Drop(); // expired / used: replace it
                }
                string id = AdIds.Rewarded;
                if (string.IsNullOrEmpty(id))
                {
                    Log("rewarded not available (no ad unit ID)");
                    return;
                }
                waitingForInit = true;
                if (!WhenReady(() => { waitingForInit = false; Fetch(id); }))
                {
                    waitingForInit = false;
                    Log("rewarded not loaded: SDK init failed");
                }
            }

            static void Fetch(string id)
            {
                loading = true;
                try
                {
                    RewardedAd.Load(id, new AdRequest(), (loaded, error) => OnMain(() =>
                    {
                        loading = false;
                        if (error != null || loaded == null)
                        {
                            Failed(error != null ? error.GetMessage() : "no ad");
                            return;
                        }
                        failures = 0;
                        ad = loaded;
                        Hook(ad);
                        Log("rewarded load ok");
                    }));
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

            static void Hook(RewardedAd a)
            {
                a.OnAdFullScreenContentOpened += () => OnMain(() =>
                {
                    opened = true;
                    Log("rewarded shown");
                });
                a.OnAdFullScreenContentClosed += () => OnMain(() =>
                {
                    Log("rewarded closed");
                    Finish();
                });
                a.OnAdFullScreenContentFailed += err => OnMain(() =>
                {
                    Log("rewarded failed to show: " + (err != null ? err.GetMessage() : "unknown"));
                    Finish();
                });
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
                if (Ready) { Present(); return; }
                Log("rewarded not loaded yet, waiting up to " + WaitForLoad + " s");
                Request();
                AdsRunner.Run(WaitThenShow());
            }

            static IEnumerator WaitThenShow()
            {
                float t = 0f;
                while (!Ready && t < WaitForLoad && (loading || waitingForInit || ad != null))
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
                BeginFullScreen();
                Log("rewarded show");
                var shown = ad;
                try
                {
                    shown.Show(r => OnMain(() =>
                    {
                        if (earned) return;
                        earned = true;
                        Log("rewarded earned reward" + (r != null ? ": " + r.Amount + " " + r.Type : ""));
                    }));
                    AdsRunner.Run(OpenWatch(shown));
                }
                catch (Exception e)
                {
                    Log("rewarded failed to show: " + e.Message);
                    Finish();
                }
            }

            /// <summary>Safety net like the other full-screen ads: nothing on screen within OpenTimeout s releases the game.</summary>
            static IEnumerator OpenWatch(RewardedAd shown)
            {
                float t = 0f;
                while (!opened && t < OpenTimeout && busy && ad == shown)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (!opened && busy && ad == shown)
                {
                    Log("rewarded did not appear in " + OpenTimeout + " s, continuing");
                    Finish();
                }
            }

            /// <summary>After close or a failed show: drop the ad, restore audio, load the next, then pay out or fail.</summary>
            static void Finish()
            {
                if (ad == null && !IsFullScreenAdShowing) return; // already finished (e.g. the watchdog ran first)
                Drop();
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

            static void Drop()
            {
                if (ad == null) return;
                var a = ad;
                ad = null;
                try { a.Destroy(); } catch (Exception) { }
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
        /// Height the banner takes at the bottom, in design px (390 wide, as Theme.D / Ui.PlaceD): the loaded ad's
        /// real height, a 56 dp estimate before that, 0 when there is no banner (no ad unit ID or SDK init failed).
        /// </summary>
        public static float BannerHeightDesign
        {
            get { return string.IsNullOrEmpty(AdIds.Banner) || InitFailed ? 0f : BannerAd.HeightDesign; }
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

        /// <summary>Screen px to design px (the canvas matches width: 390 design px = Screen.width).</summary>
        static float PxToDesign(float px)
        {
            return px * 390f / Mathf.Max(1, Screen.width);
        }

        static float DpToDesign(float dp)
        {
            float dpi = Screen.dpi > 0f ? Screen.dpi : 160f * Screen.width / 360f; // unknown dpi: a 360 dp wide phone
            return PxToDesign(dp * dpi / 160f);
        }

        /// <summary>
        /// One anchored adaptive banner at the bottom centre. Created hidden and loaded as soon as the SDK is ready
        /// (during the splash), then reused for every screen: Hide / Show, never Destroy, so Banner(true) shows the
        /// loaded ad at once. Refresh: a new ad every RefreshTime s of unscaled foreground time while it is meant to be
        /// visible, counted from the end of the last load; the old ad stays up until the new one has loaded. Before the
        /// first ad, failed loads retry quickly (FirstRetry), also while hidden. Never two loads at once.
        /// </summary>
        static class BannerAd
        {
            const float RefreshTime = 30f, LoadTimeout = 15f, EstimateDp = 56f;
            static readonly float[] FirstRetry = { 3f, 6f, 15f, 30f }; // until the first ad has loaded; then RefreshTime

            static BannerView view;
            static bool want, shown, creating, loading, hasAd, appPaused, visiblePending;
            static float timer, nextDelay = RefreshTime, adLoadedAt;
            static float loadCalledAt, showAskedAt; // realtimeSinceStartup, for the timing logs
            static float waitStartedAt = -1f, waitEndedAt = -1f; // first wait for an ad (BannerPlaceholder)

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
            static int failures;
            static float heightDesign = -1f;
            static int showFrame = -1;                // frame of the last Banner(true)
            static bool decisionPending;              // a Banner call happened this frame: log the end-of-frame result
            static string showBy = "none", hideBy = "none", lastError = "none";

            public static float HeightDesign
            {
                get { return heightDesign > 0f ? heightDesign : DpToDesign(EstimateDp); }
            }

            static float Now { get { return Time.realtimeSinceStartup; } }

            static string Ms(float since)
            {
                return Mathf.RoundToInt((Now - since) * 1000f) + " ms";
            }

            /// <summary>Creates the banner hidden and starts the first load once Initialize is done (Ads.Init).</summary>
            public static void Preload()
            {
                if (view != null || creating) return;
                string id = AdIds.Banner;
                if (string.IsNullOrEmpty(id))
                {
                    Log("banner not available (no ad unit ID)");
                    return;
                }
                AdsRunner.Ensure();
                creating = true;
                if (!WhenReady(() => Create(id)))
                {
                    creating = false;
                    Log("banner not created: SDK init failed");
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
                if (show == want) return;
                want = show;
                if (!show) return; // hidden in LateTick; the refresh timer stops counting right away

                if (string.IsNullOrEmpty(AdIds.Banner))
                {
                    want = false;
                    return;
                }
                showAskedAt = Now;
                StartWait();
                visiblePending = true;
                if (view != null) Reveal();
                else Preload(); // normally already created during the splash
            }

            static void Create(string id)
            {
                creating = false;
                if (view != null) return;
                try
                {
                    var size = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(AdSize.FullWidth);
                    view = new BannerView(id, size, AdPosition.Bottom);
                }
                catch (Exception e)
                {
                    view = null;
                    Log("banner create failed: " + e.Message);
                    return;
                }
                view.OnBannerAdLoaded += () => OnMain(OnLoaded);
                view.OnBannerAdLoadFailed += err => OnMain(() =>
                    Failed(err != null ? err.GetMessage() : "unknown"));
                view.OnAdPaid += value => OnMain(() =>
                    Log("banner paid: " + (value != null ? value.Value + " micros " + value.CurrencyCode : "?")));

                // a new BannerView starts visible: hide it at once unless a screen already wants it
                shown = true;
                if (want) Log("banner create (shown)");
                else
                {
                    ForceHide();
                    Log("banner create (hidden, preloading)");
                }
                Load();
            }

            /// <summary>Banner(true) with a view: show it now (with whatever ad it has, even an old one) and refresh in the background if due.</summary>
            static void Reveal()
            {
                if (!shown)
                {
                    try { view.Show(); } catch (Exception e) { Log("banner show failed: " + e.Message); }
                    shown = true;
                    Log("banner show");
                }
                if (hasAd)
                {
                    LogVisible("ad already loaded");
                    if (!loading && Time.unscaledTime - adLoadedAt >= RefreshTime)
                    {
                        Log("banner refresh (ad older than " + RefreshTime + " s, old ad stays up)");
                        Load();
                    }
                }
                else if (!loading) Load(); // no ad yet and waiting for a retry: try now, a screen needs it
            }

            static void ForceHide()
            {
                try { view.Hide(); } catch (Exception e) { Log("banner hide failed: " + e.Message); }
                shown = false;
            }

            static void Conceal()
            {
                if (!shown || view == null) return;
                ForceHide();
                Log("banner hide");
            }

            /// <summary>LoadAd on the same view: the current ad stays on screen until the new one replaces it.</summary>
            static void Load()
            {
                if (loading || view == null) return;
                loading = true;
                loadCalledAt = Now;
                StartWait();
                Log("banner LoadAd" + (hasAd ? " (refresh)" : " (first ad)"));
                try { view.LoadAd(new AdRequest()); }
                catch (Exception e) { Failed(e.Message); }
            }

            static void OnLoaded()
            {
                bool first = !hasAd;
                if (first) waitEndedAt = Now;
                hasAd = true;
                loading = false;
                failures = 0;
                timer = 0f;
                nextDelay = RefreshTime; // from now on only the normal refresh
                adLoadedAt = Time.unscaledTime;
                Log("banner load ok after " + Ms(loadCalledAt) + " from LoadAd" + (first ? " (first ad)" : ""));

                float px = 0f;
                try { if (view != null) px = view.GetHeightInPixels(); } catch (Exception) { px = 0f; }
                if (px > 0f) SetHeight(PxToDesign(px));

                // the SDK may show the view when an ad arrives: keep it hidden unless a screen wants it
                if (!want) { if (view != null) ForceHide(); }
                else LogVisible("waited for the load");
            }

            static void Failed(string why)
            {
                loading = false;
                timer = 0f;
                failures++;
                nextDelay = hasAd ? RefreshTime : FirstRetry[Mathf.Min(failures, FirstRetry.Length) - 1];
                lastError = why.Length > 40 ? why.Substring(0, 40) : why;
                Log("banner load failed after " + Ms(loadCalledAt) + ": " + why + " (retry in " + nextDelay + " s)");
            }

            /// <summary>Once per Banner(true): how long until an ad was actually on screen.</summary>
            static void LogVisible(string how)
            {
                if (!visiblePending) return;
                visiblePending = false;
                Log("banner visible " + Ms(showAskedAt) + " after Banner(true) (" + how + ")");
            }

            static void SetHeight(float d)
            {
                if (Mathf.Abs(d - heightDesign) < 0.5f) return;
                heightDesign = d;
                Log("banner height " + d.ToString("0.#") + " design px");
                RaiseBannerHeightChanged();
            }

            /// <summary>Every frame (AdsRunner.Update): load timeout, first-load retries and the refresh timer.</summary>
            public static void Tick()
            {
                if (loading)
                {
                    if (Now - loadCalledAt > LoadTimeout) Failed("no answer in " + LoadTimeout + " s");
                    return;
                }
                if (view == null || appPaused) return;
                // refreshes only while it is meant to be visible and no full-screen ad is up; first-load retries always
                if (hasAd && (!want || IsFullScreenAdShowing)) return;
                timer += Time.unscaledDeltaTime;
                if (timer < nextDelay) return;
                Log(hasAd ? "banner refresh" : "banner retry");
                Load();
            }

            /// <summary>End of frame (AdsRunner.LateUpdate): apply a hide that no Banner(true) cancelled this frame.</summary>
            public static void LateTick()
            {
                if (decisionPending)
                {
                    decisionPending = false;
                    Log("banner end of frame: " + (want ? "show (wanted by " + showBy + ")"
                                                        : "hide (" + hideBy + " hid it, no screen asked to show)"));
                }
                if (!want) Conceal();
            }

            public static string DebugLine(string screen)
            {
                string init = Ready ? "ok" : InitFailed ? "FAILED" : "not";
                string err = InitFailed && InitError != null ? InitError : lastError;
                if (err.Length > 40) err = err.Substring(0, 40);
                return "wanted=" + (want ? "Y" : "N") + " loaded=" + (hasAd ? "Y" : "N") +
                       " visible=" + (want && shown && hasAd ? "Y" : "N") +
                       " height=" + Mathf.RoundToInt(BannerHeightDesign) + " screen=" + screen +
                       " id=" + (string.IsNullOrEmpty(AdIds.Banner) ? "empty" : "set") + " init=" + init +
                       " lastError=" + err;
            }

            public static void AppPaused(bool paused)
            {
                appPaused = paused;
                if (!paused) timer = 0f; // back in the foreground: the wait starts again from zero
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
