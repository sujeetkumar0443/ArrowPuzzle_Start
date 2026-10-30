using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArrowPuzzle
{
    /// <summary>
    /// "Banner Stamp" level complete: an arrow sweeps across, a LEVEL COMPLETE! banner stamps down (shake + gold
    /// ring) and moves up, the card slides in, earned stars drop and bounce, confetti, then the info line and
    /// NEXT LEVEL / Home fade in. Below the info line the solve time counts up from 00:00 and, on a new best, a NEW BEST!
    /// badge stamps in, then the previous best fades in. Tapping before the buttons appear skips to the end. Unscaled time, t = s since OnShow.
    /// Everything except one-shot events (sounds, landing rings) is a pure function of t (Apply), so skipping is exact.
    /// </summary>
    public class PopupWin : AppScreen
    {
        public override bool IsPopup { get { return true; } }

        // ---- timeline (s) and sizes (design px)
        const float SweepT0 = 0.15f, SweepT1 = 0.75f, SweepFrom = -260f, SweepTo = 520f;   // arrow tail x from the screen left
        const float SweepBody = 300f, SweepThick = 26f, SweepHead = 56f, GhostLag = 0.04f;
        const int Ghosts = 3;
        const float BannerYFrac = 0.43f;                                                    // banner / sweep y, of the screen height from the top
        const float BannerW = 500f, BannerH = 96f, BannerTilt = 6f;                         // Unity z (CSS -6deg)
        const float StampT0 = 0.7f, StampT1 = 0.95f, StampScale = 1.6f, StampBack = 2.4f;
        const float ShakeT0 = 0.95f, ShakeT1 = 1.3f, ShakeAmp = 7f, ShakeFreq = 70f;
        const float RingT0 = 0.95f, RingT1 = 1.45f, RingR0 = 60f, RingR1 = 360f, RingW0 = 8f, RingW1 = 1f;
        const float MoveT0 = 1.5f, MoveT1 = 2.0f, BannerTop = 120f, BannerSmall = 0.72f;
        const float CardT0 = 1.7f, CardT1 = 2.3f, CardTop = 190f, CardW = 326f, CardH = 520f;
        const float StarsY = 85f, SideX = 86f, SideR = 34f, MidR = 45f, MidRaise = 14f, SideTilt = 10f;
        const float DropT0 = 2.05f, DropEvery = 0.3f, DropTime = 0.5f;
        const float StarRingR0 = 24f, StarRingR1 = 90f, StarRingW0 = 5f, StarRingW1 = 1f, StarRingTime = 0.5f;
        const float LandShake = 3f, LandShakeTime = 0.2f;
        const float InfoY = 185f, InfoT0 = 2.9f, InfoT1 = 3.2f;
        const float NextY = 354f, HomeY = 440f, ButtonsT0 = 3.0f, ButtonsT1 = 3.4f;
        const float SkipBefore = 3.0f;
        const int ConfettiCount = 50;
        const float ConfettiT0 = 2.0f, ConfettiLife = 2.0f, ConfettiFade = 0.4f, ConfettiSpread = 1.3f;
        const float ConfettiSpeed = 500f, ConfettiGravity = 700f;
        const int ConfettiSeed = 7;
        // solve time block, y below TimeTop (the block's origin, below the card top)
        const float TimeTop = 205f;
        const float TimeLabelY = 14f, TimeLabelSpacing = 1.5f;                              // "YOUR TIME", letter spacing (design px)
        const float TimeValueY = 38f, TimeValueH = 64f;                                     // big time centre: baseline at about +60
        const float TimeT0 = 2.9f, TimeT1 = 3.2f, CountT0 = 3.0f, CountT1 = 4.0f;           // fade in, count up 00:00 -> time
        const float PulseTime = 0.33f, PulseScale = 1.1f;                                   // after the count
        const float BadgeY = 96f, BadgeW = 116f, BadgeH = 34f, BadgeR = 10f, BadgeEdgeD = 3f;
        const float BadgeTilt = 3.5f;                                                       // Unity z (CSS -3.5deg)
        const float BadgeT0 = 4.0f, BadgeT1 = 4.35f, BadgeScale = 1.8f, BadgeBack = 2.6f;
        const float PrevY = 134f, PrevT0 = 4.3f, PrevT1 = 4.6f;
        static readonly Color BadgeEdge = Theme.Hex("#B97C00");
        static readonly Color BadgeInk = Theme.Hex("#3A2A00");

        const float End = PrevT1 + 0.05f;                                                   // last moving thing done (confetti ends at 4.0)

        RectTransform content, card, banner;
        CanvasGroup bannerGroup, buttonsGroup;
        bool waitingForAd; // a Next / Home tap is waiting for the interstitial
        Text info, timeLabel, timeValue, prevBest;
        RectTransform badge;
        CanvasGroup badgeGroup;
        ArrowGraphic badgeRing;
        float resultTime;
        bool newBest, badgeFired;
        int shownSeconds = -1;
        readonly ArrowGraphic[] sweep = new ArrowGraphic[Ghosts + 1]; // [0] = the arrow, [1..3] = ghosts
        ArrowGraphic bigRing;
        readonly RectTransform[] drops = new RectTransform[3];
        readonly ArrowGraphic[] starRings = new ArrowGraphic[3];
        readonly float[] slotY = new float[3];
        readonly bool[] landed = new bool[3];
        readonly float[] landT = new float[3];
        readonly Image[] confetti = new Image[ConfettiCount];
        readonly Vector2[] cVel = new Vector2[ConfettiCount];
        readonly float[] cSpin = new float[ConfettiCount];
        readonly List<Vector2> pts = new List<Vector2>(65);
        GameObject skipCatcher;

        float screenH, t0;
        int earned;
        bool confettiOn, skipped;
        Vector2 confettiOrigin;
        Coroutine run;

        protected override void Build()
        {
            content = Ui.New(Root, "Content");
            Ui.Fill(content);

            BuildCard();

            // confetti pool above the card
            var layer = Ui.New(content, "Confetti");
            Ui.Fill(layer);
            for (int i = 0; i < ConfettiCount; i++)
            {
                bool round = i % 2 == 0;
                var c = Ui.Img(layer, "c", round ? Theme.Circle : Theme.Rounded, Theme.Palette[i % Theme.Palette.Length], round ? 0f : 2f);
                c.raycastTarget = false;
                c.rectTransform.anchorMin = c.rectTransform.anchorMax = Ui.TC;
                c.rectTransform.pivot = Ui.C;
                c.gameObject.SetActive(false);
                confetti[i] = c;
            }

            // arrow sweep (ghosts behind the arrow)
            for (int g = Ghosts; g >= 0; g--)
            {
                var rt = Ui.New(content, g == 0 ? "Sweep" : "Ghost" + g);
                Ui.PlaceD(rt, Ui.TL, Vector2.zero, Vector2.zero); // points are measured from the screen's top-left
                var a = rt.gameObject.AddComponent<ArrowGraphic>();
                a.raycastTarget = false;
                a.thickness = Theme.D(SweepThick);
                a.headSize = Theme.D(SweepHead);
                var col = Theme.Palette[0];
                col.a = g == 0 ? 1f : 0.5f / g;
                a.color = col;
                sweep[g] = a;
            }

            // banner
            var bImg = Ui.Img(content, "Banner", Theme.Rounded, Theme.Accent, 10f);
            bImg.raycastTarget = false;
            banner = bImg.rectTransform;
            Ui.PlaceD(banner, Ui.TC, Ui.C, Vector2.zero, new Vector2(BannerW, BannerH));
            var edge = bImg.gameObject.AddComponent<Shadow>();
            edge.effectColor = Theme.AccentShadow;
            edge.effectDistance = new Vector2(0f, -Theme.D(8f));
            edge.useGraphicAlpha = false;
            bannerGroup = bImg.gameObject.AddComponent<CanvasGroup>();
            var bText = Ui.Label(banner, "LEVEL COMPLETE!", 44f, Theme.Card, Weight.Bold);
            Ui.Fill(bText.rectTransform);

            bigRing = Ring(content, Ui.TC, Vector2.zero);

            // tap anywhere before the buttons show = skip
            var catcher = Ui.Img(Root, "Skip", null, Color.clear);
            Ui.Fill(catcher.rectTransform);
            var skipBtn = catcher.gameObject.AddComponent<Button>();
            skipBtn.transition = Selectable.Transition.None;
            skipBtn.onClick.AddListener(Skip);
            skipCatcher = catcher.gameObject;
        }

        void BuildCard()
        {
            var cImg = Ui.Img(content, "Card", Theme.Rounded, Theme.Card, 32f);
            card = cImg.rectTransform;
            Ui.PlaceD(card, Ui.TC, new Vector2(0f, -CardTop), new Vector2(CardW, CardH));
            var edge = cImg.gameObject.AddComponent<Shadow>();
            edge.effectColor = Theme.PopupShadow;
            edge.effectDistance = new Vector2(0f, -Theme.D(10f));
            edge.useGraphicAlpha = false;

            // star slots (left, middle, right), empty colour; earned stars drop on top of them
            float[] xs = { -SideX, 0f, SideX };
            float[] rs = { SideR, MidR, SideR };
            float[] tilt = { SideTilt, 0f, -SideTilt }; // Unity z (CSS -10 / +10 deg)
            for (int i = 0; i < 3; i++)
            {
                slotY[i] = -StarsY + (i == 1 ? MidRaise : 0f);
                float size = rs[i] * 2f;
                var slot = Icons.Make(card, Icon.Star, Theme.Locked, size);
                Ui.PlaceD(slot, Ui.TC, Ui.C, new Vector2(xs[i], slotY[i]), new Vector2(size, size));
                slot.localEulerAngles = new Vector3(0f, 0f, tilt[i]);

                drops[i] = Icons.Make(card, Icon.Star, Theme.Gold, size);
                Ui.PlaceD(drops[i], Ui.TC, Ui.C, new Vector2(xs[i], slotY[i]), new Vector2(size, size));
                drops[i].localEulerAngles = new Vector3(0f, 0f, tilt[i]);

                starRings[i] = Ring(card, Ui.TC, new Vector2(xs[i], slotY[i]));
            }

            info = Ui.Label(card, "", 20f, Theme.InkSoft, Weight.Medium);
            Ui.PlaceD(info.rectTransform, Ui.TC, Ui.C, new Vector2(0f, -InfoY), new Vector2(CardW - 48f, 26f));

            BuildTime();

            var buttons = Ui.New(card, "Buttons");
            Ui.Fill(buttons);
            buttonsGroup = buttons.gameObject.AddComponent<CanvasGroup>();

            var next = Ui.Btn(buttons, "NEXT LEVEL", Theme.Accent, Theme.Card, 24f, 24f, Theme.AccentShadow, 5f,
                              () => AfterAd(NextLevel));
            Ui.PlaceD((RectTransform)next.transform, Ui.TC, new Vector2(0f, -NextY), new Vector2(CardW - 48f, 72f));
            var arrow = Icons.Make(next.transform, Icon.ArrowRight, Theme.Card, 24f, 3f);
            Ui.Size(arrow, 24f, 24f);
            // first sweep when the button has faded in (the shimmer's clock starts when the popup opens)
            Ui.AddShimmer((RectTransform)next.transform, 24f, ButtonsT1);

            var home = Ui.Btn(buttons, "Home", Theme.Bg, Theme.Ink, 20f, 20f, Theme.CardShadow, 0f,
                              () => AfterAd(GoHome), Icon.Home, 24f, Weight.Semi);
            Ui.PlaceD((RectTransform)home.transform, Ui.TC, new Vector2(0f, -HomeY), new Vector2(CardW - 48f, 60f));
        }

        /// <summary>YOUR TIME label, the big mm:ss, the NEW BEST! badge (+ its ring) and the previous-best line.</summary>
        void BuildTime()
        {
            timeLabel = Ui.Label(card, "YOUR TIME", 13f, Theme.InkSoft, Weight.Bold);
            Ui.PlaceD(timeLabel.rectTransform, Ui.TC, Ui.C, new Vector2(0f, -(TimeTop + TimeLabelY)), new Vector2(CardW - 48f, 18f));
            timeLabel.gameObject.AddComponent<LetterSpacing>().spacing = Theme.D(TimeLabelSpacing);

            timeValue = Ui.Label(card, "00:00", 56f, Theme.Ink, Weight.Bold);
            Ui.PlaceD(timeValue.rectTransform, Ui.TC, Ui.C, new Vector2(0f, -(TimeTop + TimeValueY)), new Vector2(CardW - 48f, TimeValueH));

            var bImg = Ui.Img(card, "NewBest", Theme.Rounded, Theme.Gold, BadgeR);
            bImg.raycastTarget = false;
            badge = bImg.rectTransform;
            Ui.PlaceD(badge, Ui.TC, Ui.C, new Vector2(0f, -(TimeTop + BadgeY)), new Vector2(BadgeW, BadgeH));
            badge.localEulerAngles = new Vector3(0f, 0f, BadgeTilt);
            var edge = bImg.gameObject.AddComponent<Shadow>();
            edge.effectColor = BadgeEdge;
            edge.effectDistance = new Vector2(0f, -Theme.D(BadgeEdgeD));
            edge.useGraphicAlpha = false;
            badgeGroup = bImg.gameObject.AddComponent<CanvasGroup>();
            badgeGroup.blocksRaycasts = false;
            var bText = Ui.Label(badge, "NEW BEST!", 17f, BadgeInk, Weight.Bold);
            Ui.Fill(bText.rectTransform);

            badgeRing = Ring(card, Ui.TC, new Vector2(0f, -(TimeTop + BadgeY)));

            prevBest = Ui.Label(card, "", 14f, Theme.InkSoft, Weight.Medium);
            Ui.PlaceD(prevBest.rectTransform, Ui.TC, Ui.C, new Vector2(0f, -(TimeTop + PrevY)), new Vector2(CardW - 48f, 20f));
        }

        static ArrowGraphic Ring(RectTransform parent, Vector2 anchor, Vector2 posD)
        {
            var rt = Ui.New(parent, "Ring");
            Ui.PlaceD(rt, anchor, Ui.C, posD, Vector2.zero);
            var g = rt.gameObject.AddComponent<ArrowGraphic>();
            g.drawHead = false;
            g.raycastTarget = false;
            g.color = Theme.Gold;
            rt.gameObject.SetActive(false);
            return g;
        }

        // ------------------------------------------------------------------ flow

        public override void OnShow()
        {
            Canvas.ForceUpdateCanvases();
            screenH = Root.rect.height;
            earned = Mathf.Clamp(GameApp.I.ResultLives, 1, 3);

            int left = GameApp.I.ResultLives;
            info.text = "Level " + GameApp.I.CurrentLevel + " · " + left + (left == 1 ? " life left" : " lives left");

            resultTime = GameApp.I.ResultTime;
            newBest = GameApp.I.ResultNewBest;
            float prev = GameApp.I.ResultPrevBest;
            badge.gameObject.SetActive(newBest);
            prevBest.gameObject.SetActive(prev > 0f);
            prevBest.text = (newBest ? "Previous best  " : "Best  ") + ScreenGame.FormatTime(prev);
            badgeFired = false;
            shownSeconds = -1;

            for (int i = 0; i < 3; i++) { landed[i] = false; landT[i] = -100f; }
            confettiOn = false;
            skipped = false;
            waitingForAd = false;
            SeedConfetti();

            t0 = Time.unscaledTime;
            Apply(0f);
            if (run != null) GameApp.I.StopCoroutine(run);
            run = GameApp.I.StartCoroutine(Run());
        }

        public override void OnHide()
        {
            if (run != null) GameApp.I.StopCoroutine(run);
            run = null;
            content.anchoredPosition = Vector2.zero;
            foreach (var c in confetti) c.gameObject.SetActive(false);
        }

        /// <summary>Closing with back would leave the finished (locked) board, so back goes Home (after the ad, like the button).</summary>
        public override bool OnBack()
        {
            AfterAd(GoHome);
            return true;
        }

        /// <summary>
        /// Interstitial, then the action. The buttons stay locked from the tap until the ad is done (the popup's dim
        /// backdrop already blocks the screen behind). No ad after the very first level a player ever completes.
        /// </summary>
        void AfterAd(System.Action action)
        {
            if (waitingForAd) return;
            waitingForAd = true;
            LockButtons();
            if (Save.CompletedLevels() <= 1)
            {
                Debug.Log("[Ads] interstitial skipped: first level ever completed");
                Done(action);
                return;
            }
            Ads.ShowInterstitial(() => Done(action));
        }

        void Done(System.Action action)
        {
            waitingForAd = false;
            if (GameApp.I.IsOpen<PopupWin>()) action(); // gone already (e.g. another screen took over): nothing to do
        }

        void LockButtons()
        {
            buttonsGroup.interactable = buttonsGroup.blocksRaycasts = false;
        }

        static void GoHome()
        {
            GameApp.I.Show("ScreenHome");
        }

        void NextLevel()
        {
            int next = GameApp.I.CurrentLevel + 1;
            GameApp.I.Close("PopupWin");
            if (GameApp.I.UnlockPending)
            {
                // a new level was unlocked: show it on the level map (it plays the unlock sequence)
                GameApp.I.CurrentMode = GameApp.I.ClearedMode;
                GameApp.I.Show("ScreenLevels");
                return;
            }
            if (next > ModeInfo.LevelsPerMode)
            {
                // last level of this mode done: back to the mode cards
                GameApp.I.Show("ScreenHome");
                return;
            }
            // ScreenGame is already visible, so Show would not reload it; start the level directly (same mode)
            var game = GameApp.I.Get<ScreenGame>();
            if (game != null) game.StartLevel(next);
        }

        /// <summary>Jump to the final state: stars in place, no landing rings or confetti, buttons visible.</summary>
        void Skip()
        {
            if (skipped || Time.unscaledTime - t0 >= SkipBefore) return;
            skipped = true;
            for (int i = 0; i < 3; i++) { landed[i] = true; landT[i] = -100f; }
            badgeFired = true; // no stamp sound / ring: the time block jumps to its end state (ApplyTime)
            confettiOn = false;
            foreach (var c in confetti) c.gameObject.SetActive(false);
            t0 = Time.unscaledTime - ButtonsT1;
            Apply(ButtonsT1);
        }

        IEnumerator Run()
        {
            while (true)
            {
                float t = Time.unscaledTime - t0;
                Apply(t);
                if (t > End) break;
                yield return null;
            }
            run = null;
        }

        // ------------------------------------------------------------------ the timeline

        void Apply(float t)
        {
            skipCatcher.SetActive(!skipped && t < SkipBefore);

            ApplySweep(t);
            ApplyBanner(t);
            ApplyCard(t);
            ApplyStars(t);
            ApplyConfetti(t);
            ApplyTime(t);

            // shakes: banner impact + a tiny one per landed star
            float dx = 0f;
            if (t >= ShakeT0 && t < ShakeT1)
                dx += Mathf.Sin((t - ShakeT0) * ShakeFreq) * ShakeAmp * (1f - (t - ShakeT0) / (ShakeT1 - ShakeT0));
            for (int i = 0; i < 3; i++)
            {
                float s = t - landT[i];
                if (s >= 0f && s < LandShakeTime) dx += Mathf.Sin(s * ShakeFreq) * LandShake * (1f - s / LandShakeTime);
            }
            content.anchoredPosition = new Vector2(Theme.D(dx), 0f);

            info.color = WithAlpha(Theme.InkSoft, Mathf.Clamp01((t - InfoT0) / (InfoT1 - InfoT0)));
            float b = Mathf.Clamp01((t - ButtonsT0) / (ButtonsT1 - ButtonsT0));
            buttonsGroup.alpha = b;
            buttonsGroup.interactable = buttonsGroup.blocksRaycasts = b >= 0.5f && !waitingForAd; // tappable once visible
        }

        void ApplySweep(float t)
        {
            float y = -screenH * BannerYFrac;
            for (int g = 0; g <= Ghosts; g++)
            {
                float tg = t - g * GhostLag;
                bool on = tg >= SweepT0 && tg <= SweepT1;
                if (sweep[g].gameObject.activeSelf != on) sweep[g].gameObject.SetActive(on);
                if (!on) continue;
                float tail = Mathf.Lerp(SweepFrom, SweepTo, EaseInOutCubic((tg - SweepT0) / (SweepT1 - SweepT0)));
                pts.Clear();
                pts.Add(new Vector2(Theme.D(tail), y));
                pts.Add(new Vector2(Theme.D(tail + SweepBody), y));
                sweep[g].SetPoints(pts);
            }
        }

        void ApplyBanner(float t)
        {
            float stamp = Mathf.Clamp01((t - StampT0) / (StampT1 - StampT0));
            float move = EaseInOutCubic(Mathf.Clamp01((t - MoveT0) / (MoveT1 - MoveT0)));

            bannerGroup.alpha = stamp;
            float scale = Mathf.LerpUnclamped(StampScale, 1f, EaseOutBack(stamp, StampBack)) * Mathf.Lerp(1f, BannerSmall, move);
            banner.localScale = Vector3.one * scale;
            banner.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(BannerTilt, 0f, move));
            banner.anchoredPosition = new Vector2(0f, -Mathf.Lerp(screenH * BannerYFrac, Theme.D(BannerTop), move));

            // gold ring from the banner centre (still at its stamp position)
            DrawRing(bigRing, (t - RingT0) / (RingT1 - RingT0), RingR0, RingR1, RingW0, RingW1);
            bigRing.rectTransform.anchoredPosition = new Vector2(0f, -screenH * BannerYFrac);
        }

        void ApplyCard(float t)
        {
            card.anchoredPosition = new Vector2(0f, -CardTopAt(t));
        }

        /// <summary>Card top in canvas units from the screen top: slides up from below the screen.</summary>
        float CardTopAt(float t)
        {
            float k = EaseOutCubic(Mathf.Clamp01((t - CardT0) / (CardT1 - CardT0)));
            return Mathf.Lerp(screenH + Theme.D(20f), Theme.D(CardTop), k);
        }

        void ApplyStars(float t)
        {
            float dropFrom = Theme.D(CardTop + StarsY + 120f); // start above the top of the screen
            for (int i = 0; i < 3; i++)
            {
                int order = i; // earned stars fill left, middle, right
                bool isEarned = order < earned;
                float s = DropT0 + order * DropEvery;
                float x = (t - s) / DropTime;
                bool on = isEarned && (landed[i] || x >= 0f);
                if (drops[i].gameObject.activeSelf != on) drops[i].gameObject.SetActive(on);
                if (on)
                {
                    float k = landed[i] && skipped ? 1f : EaseOutBounce(Mathf.Clamp01(x));
                    var p = drops[i].anchoredPosition;
                    drops[i].anchoredPosition = new Vector2(p.x, Mathf.Lerp(Theme.D(slotY[i]) + dropFrom, Theme.D(slotY[i]), k));
                }

                if (isEarned && !landed[i] && x >= 1f)
                {
                    landed[i] = true;
                    landT[i] = s + DropTime;
                    Sfx.Play(Sfx.Id.Star);
                }

                DrawRing(starRings[i], (t - landT[i]) / StarRingTime, StarRingR0, StarRingR1, StarRingW0, StarRingW1);
            }
        }

        /// <summary>Time fades in, counts up (ease-out cubic), turns Accent + pulses; badge stamps; previous best fades in.</summary>
        void ApplyTime(float t)
        {
            if (skipped) t = End; // skip shows the final time, badge and previous-best line

            float fade = Mathf.Clamp01((t - TimeT0) / (TimeT1 - TimeT0));
            timeLabel.color = WithAlpha(Theme.InkSoft, fade);

            float count = Mathf.Clamp01((t - CountT0) / (CountT1 - CountT0));
            int secs = Mathf.FloorToInt(count >= 1f ? resultTime : resultTime * EaseOutCubic(count));
            if (secs != shownSeconds)
            {
                shownSeconds = secs;
                timeValue.text = ScreenGame.FormatTime(secs);
            }
            timeValue.color = WithAlpha(count >= 1f ? Theme.Accent : Theme.Ink, fade);
            float pulse = (t - CountT1) / PulseTime;
            float ps = pulse > 0f && pulse < 1f ? 1f + (PulseScale - 1f) * Mathf.Sin(pulse * Mathf.PI) : 1f;
            timeValue.rectTransform.localScale = Vector3.one * ps;

            if (newBest)
            {
                float stamp = Mathf.Clamp01((t - BadgeT0) / (BadgeT1 - BadgeT0));
                badgeGroup.alpha = stamp;
                badge.localScale = Vector3.one * Mathf.LerpUnclamped(BadgeScale, 1f, EaseOutBack(stamp, BadgeBack));
                if (!badgeFired && t >= BadgeT0)
                {
                    badgeFired = true;
                    Sfx.Play(Sfx.Id.Star);
                }
                DrawRing(badgeRing, skipped ? -1f : (t - BadgeT0) / StarRingTime, StarRingR0, StarRingR1, StarRingW0, StarRingW1);
            }
            else DrawRing(badgeRing, -1f, 0f, 0f, 0f, 0f);

            prevBest.color = WithAlpha(Theme.InkSoft, Mathf.Clamp01((t - PrevT0) / (PrevT1 - PrevT0)));
        }

        void SeedConfetti()
        {
            var rng = new System.Random(ConfettiSeed);
            for (int i = 0; i < ConfettiCount; i++)
            {
                float size = 6f + (float)rng.NextDouble() * 8f;
                bool round = i % 2 == 0;
                confetti[i].rectTransform.sizeDelta = new Vector2(Theme.D(size), Theme.D(round ? size : size * 0.6f));
                float ang = Mathf.PI * 0.5f + ((float)rng.NextDouble() * 2f - 1f) * ConfettiSpread; // up +- spread
                float speed = ConfettiSpeed * (0.4f + (float)rng.NextDouble() * 0.7f);
                cVel[i] = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * speed;
                cSpin[i] = ((float)rng.NextDouble() * 2f - 1f) * 540f;
                confetti[i].gameObject.SetActive(false);
            }
        }

        void ApplyConfetti(float t)
        {
            float age = t - ConfettiT0;
            if (skipped || age < 0f) return;
            if (!confettiOn)
            {
                if (age >= ConfettiLife) return;
                confettiOn = true;
                confettiOrigin = new Vector2(0f, -CardTopAt(ConfettiT0)); // top centre of the card when they fire
                foreach (var c in confetti) c.gameObject.SetActive(true);
            }
            if (age >= ConfettiLife)
            {
                foreach (var c in confetti) c.gameObject.SetActive(false);
                confettiOn = false;
                return;
            }

            float alpha = Mathf.Clamp01((ConfettiLife - age) / ConfettiFade);
            for (int i = 0; i < ConfettiCount; i++)
            {
                var v = cVel[i];
                var pos = new Vector2(v.x * age, v.y * age - 0.5f * ConfettiGravity * age * age);
                var c = confetti[i];
                c.rectTransform.anchoredPosition = confettiOrigin + pos * Theme.K;
                c.rectTransform.localEulerAngles = new Vector3(0f, 0f, cSpin[i] * age);
                c.color = WithAlpha(c.color, alpha);
            }
        }

        /// <summary>Closed ArrowGraphic line (exact stroke). x = progress 0..1; hidden outside it.</summary>
        void DrawRing(ArrowGraphic ring, float x, float r0, float r1, float w0, float w1)
        {
            bool on = x >= 0f && x < 1f;
            if (ring.gameObject.activeSelf != on) ring.gameObject.SetActive(on);
            if (!on) return;
            float r = Theme.D(Mathf.Lerp(r0, r1, x));
            pts.Clear();
            const int seg = 64;
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                pts.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
            ring.thickness = Theme.D(Mathf.Lerp(w0, w1, x));
            ring.color = WithAlpha(Theme.Gold, 1f - x);
            ring.SetPoints(pts);
        }

        /// <summary>Letter spacing for a single-line centred Text: spreads its glyph quads evenly around the centre.</summary>
        sealed class LetterSpacing : BaseMeshEffect
        {
            public float spacing; // canvas units between neighbouring glyphs
            static readonly List<UIVertex> verts = new List<UIVertex>();

            public override void ModifyMesh(VertexHelper vh)
            {
                if (!IsActive() || spacing == 0f) return;
                verts.Clear();
                vh.GetUIVertexStream(verts); // 6 vertices per glyph quad
                float mid = (verts.Count / 6 - 1) * 0.5f;
                for (int i = 0; i < verts.Count; i++)
                {
                    var v = verts[i];
                    v.position.x += (i / 6 - mid) * spacing;
                    verts[i] = v;
                }
                vh.Clear();
                vh.AddUIVertexTriangleStream(verts);
            }
        }

        // ------------------------------------------------------------------ easing

        static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        static float EaseOutCubic(float x)
        {
            float a = 1f - x;
            return 1f - a * a * a;
        }

        static float EaseInOutCubic(float x)
        {
            x = Mathf.Clamp01(x);
            return x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) * 0.5f;
        }

        static float EaseOutBack(float x, float c1)
        {
            float c3 = c1 + 1f, a = x - 1f;
            return 1f + c3 * a * a * a + c1 * a * a;
        }

        static float EaseOutBounce(float x)
        {
            const float n1 = 7.5625f, d1 = 2.75f;
            if (x < 1f / d1) return n1 * x * x;
            if (x < 2f / d1) { x -= 1.5f / d1; return n1 * x * x + 0.75f; }
            if (x < 2.5f / d1) { x -= 2.25f / d1; return n1 * x * x + 0.9375f; }
            x -= 2.625f / d1;
            return n1 * x * x + 0.984375f;
        }
    }
}
