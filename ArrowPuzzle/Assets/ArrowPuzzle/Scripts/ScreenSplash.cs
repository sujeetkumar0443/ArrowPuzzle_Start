using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ArrowPuzzle
{
    /// <summary>
    /// Splash: cream background with dots and faint drifting arrows. A R R O W drop in one by one, a coral arrow
    /// grows under the word and its head pops, PUZZLE slides up while its letter spacing closes. A loading bar fills
    /// while the app boots. The splash ends when the intro has played once AND boot is done (consent settled,
    /// Firebase ready or failed) AND the launch app open ad wait is over; then the content fades out and Home opens
    /// (background and dots stay: Home has the same). All timing is unscaled, t = seconds since OnShow.
    /// </summary>
    public class ScreenSplash : AppScreen
    {
        const float IntroEnd = Wordmark.End;  // 2.5 s: the intro has played once
        const float MinHold = 0.5f;           // end state on screen at least this long before the fade
        const float FadeTime = 0.4f;          // total about 3.4 s when boot is quick
        const float AdWait = 4f;              // max hold after the intro for the app open ad (and for boot)
        const float BarIn0 = 0.2f, BarIn1 = 0.6f;

        // loading block (design px from the screen bottom)
        const float BarWD = 220f, BarHD = 16f, BarBorderD = 2f, BarBottomD = 136f, LoadingBottomD = 106f;

        static bool appOpenTried;             // at most one app open ad from the splash per launch

        public override bool UseSafeArea { get { return false; } }

        Wordmark mark;
        CanvasGroup content, barGroup;
        RectTransform barFill;
        float barShown;
        Coroutine anim;

        protected override void Build()
        {
            // background: cream + dot grid over the whole canvas (stays during the fade-out)
            var bg = Ui.Img(Root, "Bg", null, Theme.Bg);
            bg.raycastTarget = false;
            Ui.Fill(bg.rectTransform);

            var dots = Ui.Img(Root, "Dots", Theme.DotTile, Theme.CardShadow);
            dots.type = Image.Type.Tiled;
            dots.pixelsPerUnitMultiplier = 64f / (34f * Theme.K);
            dots.raycastTarget = false;
            Ui.Fill(dots.rectTransform);

            var holder = Ui.New(Root, "Content");
            Ui.Fill(holder);
            content = holder.gameObject.AddComponent<CanvasGroup>();
            content.blocksRaycasts = false;

            DriftingArrows.Add(holder, 7);

            mark = Wordmark.Build(holder);
            Ui.PlaceD(mark.Root, Ui.C, new Vector2(0f, 40f), new Vector2(Wordmark.WidthD, Wordmark.HeightD));

            BuildLoading(holder);
        }

        void BuildLoading(RectTransform parent)
        {
            var holder = Ui.New(parent, "Loading");
            Ui.Fill(holder);
            barGroup = holder.gameObject.AddComponent<CanvasGroup>();

            // 220 x 16: beige border, white inside, coral fill
            var border = Ui.Img(holder, "BarBorder", Theme.Rounded, Theme.CardShadow, BarHD * 0.5f);
            border.raycastTarget = false;
            Ui.PlaceD(border.rectTransform, Ui.BC, new Vector2(0f, BarBottomD), new Vector2(BarWD, BarHD));

            float innerH = BarHD - 2f * BarBorderD;
            var track = Ui.Img(border.transform, "BarTrack", Theme.Rounded, Theme.Card, innerH * 0.5f);
            track.raycastTarget = false;
            Ui.PlaceD(track.rectTransform, Ui.C, Vector2.zero, new Vector2(BarWD - 2f * BarBorderD, innerH));

            var fill = Ui.Img(track.transform, "BarFill", Theme.Rounded, Theme.Accent, innerH * 0.5f);
            fill.raycastTarget = false;
            barFill = fill.rectTransform;
            barFill.anchorMin = new Vector2(0f, 0f);
            barFill.anchorMax = new Vector2(0f, 1f);
            barFill.pivot = new Vector2(0f, 0.5f);
            barFill.anchoredPosition = Vector2.zero;
            barFill.sizeDelta = Vector2.zero;

            var label = Ui.Label(holder, "LOADING", 13f, Theme.InkSoft, Weight.Semi);
            Ui.PlaceD(label.rectTransform, Ui.BC, new Vector2(0f, LoadingBottomD), new Vector2(BarWD, 18f));
        }

        // ------------------------------------------------------------------ timeline

        public override void OnShow()
        {
            content.alpha = 1f;
            barGroup.alpha = 0f;
            SetBar(0f);
            mark.Apply(0f);
            Ads.SetSplashOnScreen(true);

            if (!appOpenTried)
            {
                Ads.Init();
                Ads.LoadAppOpen();
            }

            if (anim != null) GameApp.I.StopCoroutine(anim);
            anim = GameApp.I.StartCoroutine(Run());
        }

        public override void OnHide()
        {
            Ads.SetSplashOnScreen(false);
            if (anim != null) GameApp.I.StopCoroutine(anim);
            anim = null;
        }

        static bool FirebaseSettled { get { return FirebaseBoot.Ready || FirebaseBoot.Failed; } }

        static bool BootDone { get { return FirebaseSettled && AppStartup.ConsentSettled; } }

        IEnumerator Run()
        {
            float t = 0f, hold = 0f;
            bool slide = false, click = false, adDone = appOpenTried, capLogged = false;
            while (true)
            {
                float dt = Time.unscaledDeltaTime;
                t += dt;

                if (!slide && t >= Wordmark.ShaftStart) { slide = true; Sfx.Play(Sfx.Id.Slide); }
                if (!click && t >= Wordmark.HeadStart) { click = true; Sfx.Play(Sfx.Id.Click); }

                mark.Apply(t);
                barGroup.alpha = Mathf.Clamp01((t - BarIn0) / (BarIn1 - BarIn0));

                bool introDone = t >= IntroEnd;
                if (introDone) hold += dt;

                // app open hand-off (once per launch), held on the full splash after the intro: wait up to AdWait for
                // the load, then show the ad and go Home when it closes; without an ad just carry on
                if (introDone && !adDone)
                {
                    appOpenTried = true;
                    bool waiting = !Ads.IsAppOpenReady && Ads.IsAppOpenLoading && !AppStartup.AdsBlocked && hold < AdWait;
                    if (!waiting)
                    {
                        adDone = true;
                        if (Ads.IsAppOpenReady)
                        {
                            Ads.Log("app open at launch: loaded, showing (held the splash " + hold.ToString("0.00") + " s)");
                            SetBar(1f);
                            anim = null;
                            Ads.ShowAppOpen(() => GameApp.I.Show("ScreenHome"));
                            yield break;
                        }
                        // a load that finishes later is kept and only used under the return rules (Ads.ReturnSkipReason)
                        Ads.Log("app open skipped at launch: " + (
                            AppStartup.AdsBlocked ? "consent does not allow ad requests" :
                            Ads.IsAppOpenLoading ? "not loaded within " + AdWait + " s (still loading, kept for a later return)" :
                            Ads.AppOpenLastError != null ? "load failed (" + Ads.AppOpenLastError + ")" :
                            "no ad loaded"));
                    }
                }

                // boot never holds the splash longer than the ad wait (consent / Firebase keep going in the background)
                bool boot = BootDone;
                if (!boot && hold >= AdWait)
                {
                    boot = true;
                    if (!capLogged)
                    {
                        capLogged = true;
                        Debug.Log("[Splash] boot not finished after " + AdWait + " s hold (consent settled: " + AppStartup.ConsentSettled +
                                  ", Firebase settled: " + FirebaseSettled + "), going Home");
                    }
                }

                UpdateBar(t, dt, adDone, boot);
                if (introDone && adDone && boot && hold >= MinHold && barShown >= 0.999f) break;
                yield return null;
            }

            // fade the content out; background and dots stay (Home draws the same)
            for (float f = 0f; f < FadeTime; )
            {
                f += Time.unscaledDeltaTime;
                content.alpha = 1f - Mathf.SmoothStep(0f, 1f, f / FadeTime);
                yield return null;
            }
            anim = null;
            GameApp.I.Show("ScreenHome");
        }

        /// <summary>
        /// The bar follows the intro clock (70 %) and the boot steps (Firebase, consent, app open wait; 25 %), stays
        /// below 95 % until everything is done, then fills.
        /// </summary>
        void UpdateBar(float t, float dt, bool adDone, bool boot)
        {
            float target;
            if (adDone && boot && t >= IntroEnd) target = 1f;
            else
            {
                int steps = (FirebaseSettled ? 1 : 0) + (AppStartup.ConsentSettled ? 1 : 0) + (adDone ? 1 : 0);
                target = Mathf.Min(0.95f, 0.7f * Mathf.Clamp01(t / IntroEnd) + 0.25f * steps / 3f);
            }
            barShown = Mathf.MoveTowards(barShown, Mathf.Max(barShown, target), dt * 1.6f);
            SetBar(barShown);
        }

        void SetBar(float p)
        {
            barShown = p;
            float innerW = Theme.D(BarWD - 2f * BarBorderD);
            float minW = Theme.D(BarHD - 2f * BarBorderD); // a round nub at least as wide as it is high
            barFill.sizeDelta = new Vector2(p <= 0.001f ? 0f : Mathf.Max(minW, innerW * p), 0f);
        }
    }
}
