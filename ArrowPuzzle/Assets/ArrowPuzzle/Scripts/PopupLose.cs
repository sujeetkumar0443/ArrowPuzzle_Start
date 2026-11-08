using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ArrowPuzzle
{
    /// <summary>
    /// Full-screen level failed (cream over the whole screen, content in the safe area): slate LEVEL FAILED ribbon,
    /// three sad grey stars (WinArt sprites, closed eyes, sad mouth, a sliding tear), "So close! Skip it and keep
    /// going.", the teal "Watch ad · +1 life" revive (rewarded, once per loss), the Skip button (rewarded; teal outline
    /// with "N left" while skips are left, gold "Get +2 skips" / "watch ad" at 0), the "Skips today" dots, then Home +
    /// TRY AGAIN and the 300x250 MREC (MrecSlot, MrecKind.Lose). No interstitials on this screen. Every element has a fixed
    /// place; a failed or unavailable video shows a short toast that covers nothing important. Rises in (content,
    /// then message, then buttons); the revive button pulses once visible. Unscaled time.
    /// </summary>
    public class PopupLose : AppScreen
    {
        public override bool IsPopup { get { return true; } }

        // rise animation (s since OnShow / design px)
        const float RiseFrom = 90f, CardTime = 0.6f;
        const float MsgT0 = 0.75f, MsgTime = 0.3f;
        const float ButtonsT0 = 0.85f, ButtonsTime = 0.4f;
        const float PulseAmp = 0.03f, PulseSpeed = 5f;
        const float WaitAlpha = 0.55f; // a reward button while its video loads / plays
        const string WatchText = "Watch ad · +1 life", WaitText = "Loading video...";
        const string SkipText = "Skip level", BonusText = "Get +2 skips", ToastText = "Ad not ready, try again";
        const int SkipBonus = 2;

        // sizes (design px)
        const float BannerW = 280f, BannerH = 48f, BannerEdgeD = 6f, BannerFont = 24f;
        const float MidD = 74f, SideD = 54f, SideX = 80f, SideTilt = 10f;
        const float MsgW = 350f, MsgH = 28f, MsgFont = 19f;
        const float BtnW = 280f, BtnH = 46f, BtnEdgeD = 5f, BtnFont = 20f, BtnIconD = 18f, OutlineD = 3f;
        const float BadgeH = 24f, BadgePadX = 9f, BadgeFont = 14f;
        const float DotD = 14f, DotGap = 6f, DotsLabelFont = 15f;
        const int MaxDots = 8;
        const float ButtonW = 342f, RowH = 54f, RowGap = 12f, HomeW = 64f, RowEdgeD = 4f, RowFont = 22f;
        const float RetryW = ButtonW - HomeW - RowGap; // 266
        const float ToastW = 260f, ToastH = 40f, ToastIn = 0.15f, ToastHold = 1.4f, ToastOut = 0.3f;
        const float TearLoop = 2.2f, TearSlide = 0.07f;

        // layout: design px from the safe-area top (y = centre unless named Top), normal / compact (MrecSlot.Plan).
        // The spacing and the sad stars shrink (stars x 0.75); text and button sizes never change, the ad never does.
        //   normal : ribbon 12..66, stars 69..143, message 166, revive 186..237, Skip 247..298, dots 316, row 336..394, MREC top 412
        //   compact: ribbon 6..60,  stars 63..119, message 134, revive 148..199, Skip 206..257, dots 270, row 286..344, MREC top 362
        const float BannerCY = 36f, CBannerCY = 30f;
        const float MidY = 108f, CMidY = 92f, SideY = 116f, CSideY = 98f, CStarScale = 0.75f;
        const float MsgY = 166f, CMsgY = 134f;
        const float ReviveTop = 186f, CReviveTop = 148f;
        const float SkipTop = 247f, CSkipTop = 206f;
        const float DotsY = 316f, CDotsY = 270f;
        const float RowTop = 336f, CRowTop = 286f;

        static readonly Color BannerEdge = Theme.Hex("#3E4753");
        static readonly Color GoldEdge = Theme.Hex("#C98410");
        static readonly Color DotUsed = Theme.Hex("#D9D2C4");
        static readonly Color TearColor = Theme.Hex("#7CC4F2");
        static readonly Color RoundEdge = Theme.Hex("#D9D1C1"); // round Home button's raised edge (as the ad frame)

        enum Pending { None, Revive, Skip, Bonus }

        sealed class SadStar
        {
            public RectTransform root, tear;
            public Image tearImg;
            public Vector2 tearBase;
            public float size;
        }

        RectTransform safe, card, banner, msgRt, reviveBox, skipRt, dotsRt, restartRt, homeRt, toast;
        readonly SadStar[] stars = new SadStar[3];
        CanvasGroup cardGroup, msgGroup, toastGroup;
        CanvasGroup reviveGroup, skipGroup, rowGroup;
        Coroutine run;
        Text reviveLabel, skipLabel, skipBadgeText;
        Image skipImg, skipBadge;
        Image[] skipIcon;
        Shadow skipShadow;
        GameObject skipFill;
        readonly Image[] dots = new Image[MaxDots];
        bool used;                // this loss was already revived
        Pending pending;          // a video request is running (all reward buttons wait)
        int request;              // bumped on every show / request, so a late ad result can't touch a newer state
        float showT0, toastT0 = -100f;
        MrecSlot mrec;            // 300x250 MREC (or its shimmer) under the row, once the content has risen

        protected override void Build()
        {
            // full screen: cream over the whole screen (blocks taps to the game), content inside the safe area
            var bg = Ui.Img(Root, "Bg", null, Theme.Bg);
            Ui.Fill(bg.rectTransform);
            safe = Ui.New(Root, "Safe");
            Ui.FitSafeArea(safe);

            card = Ui.New(safe, "Body");
            Ui.Fill(card);
            cardGroup = card.gameObject.AddComponent<CanvasGroup>();

            // 1. slate ribbon
            var bImg = Ui.Img(card, "Banner", Theme.Rounded, Theme.InkSoft, 12f);
            bImg.raycastTarget = false;
            banner = bImg.rectTransform;
            Ui.PlaceD(banner, Ui.TC, Ui.C, new Vector2(0f, -BannerCY), new Vector2(BannerW, BannerH));
            AddEdge(bImg, BannerEdge, BannerEdgeD);
            Ui.Fill(Ui.Label(banner, "LEVEL FAILED", BannerFont, Theme.Card, Weight.Bold).rectTransform);

            // 2. three sad grey stars (left, middle, right)
            float[] tilt = { SideTilt, 0f, -SideTilt };
            for (int i = 0; i < 3; i++) stars[i] = BuildSadStar(card, i, i == 1 ? MidD : SideD, tilt[i]);

            // 3. message
            var msg = Ui.Label(card, "So close! Skip it and keep going.", MsgFont, Theme.InkSoft, Weight.Medium);
            msgRt = msg.rectTransform;
            Ui.PlaceD(msgRt, Ui.TC, Ui.C, new Vector2(0f, -MsgY), new Vector2(MsgW, MsgH));
            msgGroup = msg.gameObject.AddComponent<CanvasGroup>();

            // 4. revive (pulses inside its box: centre pivot, so the press squash still works)
            reviveBox = Ui.New(card, "ReviveBox");
            Ui.PlaceD(reviveBox, Ui.TC, Ui.C, new Vector2(0f, -(ReviveTop + BtnH * 0.5f)), new Vector2(BtnW, BtnH));
            var revive = Ui.Btn(reviveBox, WatchText, Theme.Teal, Theme.Ink, BtnFont, BtnH * 0.5f, Theme.TealShadow, BtnEdgeD,
                                WatchAd, Icon.Play, BtnIconD);
            Ui.Fill((RectTransform)revive.transform);
            reviveLabel = revive.GetComponentInChildren<Text>();
            Ui.AddShimmer((RectTransform)revive.transform, BtnH * 0.5f, ButtonsT0 + ButtonsTime); // first sweep once visible
            reviveGroup = reviveBox.gameObject.AddComponent<CanvasGroup>();

            // 5. Skip + the "Skips today" dots
            BuildSkip(card);
            BuildDots(card);

            // 6. row: round-ish Home (64) on the left, coral TRY AGAIN on the right
            var rowBox = Ui.New(card, "Row");
            Ui.Fill(rowBox);
            rowGroup = rowBox.gameObject.AddComponent<CanvasGroup>();
            var home = Ui.Btn(rowBox, "", Theme.Card, Theme.Ink, 20f, RowH * 0.5f, RoundEdge, RowEdgeD,
                              () => GameApp.I.Show("ScreenHome"), Icon.Home, 26f, Weight.Semi);
            home.name = "Btn_Home";
            homeRt = (RectTransform)home.transform;
            Ui.PlaceD(homeRt, Ui.TC, new Vector2(-(ButtonW - HomeW) * 0.5f, -RowTop), new Vector2(HomeW, RowH));
            var retry = Ui.Btn(rowBox, "TRY AGAIN", Theme.Accent, Theme.Card, RowFont, 20f, Theme.AccentShadow, RowEdgeD, () =>
            {
                GameApp.I.Close("PopupLose");
                var game = GameApp.I.Get<ScreenGame>();
                if (game != null) game.Restart();
            }, Icon.Restart, 22f);
            restartRt = (RectTransform)retry.transform;
            Ui.PlaceD(restartRt, Ui.TC, new Vector2((ButtonW - RetryW) * 0.5f, -RowTop), new Vector2(RetryW, RowH));

            // MREC frame under the row, in the safe rect (not the body: the ad is a native view and never rises);
            // placed per show (OnShow), shown once the body has risen
            mrec = MrecSlot.Create(safe, MrecKind.Lose, "PopupLose", PlanMrec, t => ApplyLayout(t, false));

            BuildToast();
        }

        // ------------------------------------------------------------------ build parts

        /// <summary>Grey rounded star (WinArt.StarGrey), closed eyes, a sad mouth and a tear under the left eye.</summary>
        static SadStar BuildSadStar(RectTransform parent, int i, float s, float tilt)
        {
            var v = new SadStar { size = s };
            v.root = Ui.New(parent, "SadStar" + i);
            Ui.PlaceD(v.root, Ui.TC, Ui.C, Vector2.zero, new Vector2(s, s));
            v.root.localEulerAngles = new Vector3(0f, 0f, tilt);
            Art(v.root, "Body", WinArt.StarGrey, Color.white, new Vector2(0f, WinArt.StarBoxCY * s), new Vector2(WinArt.StarBox * s, WinArt.StarBox * s));
            for (int k = -1; k <= 1; k += 2)
                Art(v.root, k < 0 ? "EyeL" : "EyeR", WinArt.SleepyEye, Theme.InkSoft, new Vector2(k * 0.12f * s, 0.02f * s), new Vector2(0.13f * s, 0.065f * s));
            Art(v.root, "Mouth", WinArt.SadMouth, Theme.InkSoft, new Vector2(0f, -0.13f * s), new Vector2(0.16f * s, 0.08f * s));
            v.tearBase = new Vector2(-0.15f * s, -0.06f * s);
            v.tearImg = Art(v.root, "Tear", WinArt.Tear, TearColor, v.tearBase, new Vector2(0.07f * s, 0.098f * s));
            v.tear = v.tearImg.rectTransform;
            return v;
        }

        /// <summary>
        /// Skip: one Ui.Btn restyled per state (RefreshSkip). Skips left: teal outline (white fill inset 3 px, teal-dark
        /// text and icon) with a teal "N left" badge. None left: gold with an ink "watch ad" badge.
        /// </summary>
        void BuildSkip(RectTransform parent)
        {
            var b = Ui.Btn(parent, SkipText, Theme.Teal, Theme.TealShadow, BtnFont, BtnH * 0.5f, Theme.TealShadow, BtnEdgeD,
                           OnSkipTap, Icon.Play, BtnIconD);
            skipRt = (RectTransform)b.transform;
            Ui.PlaceD(skipRt, Ui.TC, new Vector2(0f, -SkipTop), new Vector2(BtnW, BtnH));
            skipImg = b.GetComponent<Image>();
            skipShadow = b.GetComponent<Shadow>();
            skipLabel = b.GetComponentInChildren<Text>();
            var icon = skipRt.Find("Icon_" + Icon.Play);
            skipIcon = icon != null ? icon.GetComponentsInChildren<Image>() : new Image[0];

            var fill = Ui.Img(skipRt, "Fill", Theme.Rounded, Theme.Card, BtnH * 0.5f - OutlineD);
            fill.raycastTarget = false;
            fill.gameObject.AddComponent<LayoutElement>().ignoreLayout = true; // the button has a layout group
            var f = fill.rectTransform;
            f.anchorMin = Vector2.zero;
            f.anchorMax = Vector2.one;
            f.offsetMin = new Vector2(Theme.D(OutlineD), Theme.D(OutlineD));
            f.offsetMax = new Vector2(-Theme.D(OutlineD), -Theme.D(OutlineD));
            f.SetAsFirstSibling(); // under the icon and the label
            skipFill = fill.gameObject;

            skipBadge = Ui.Img(skipRt, "Badge", Theme.Rounded, Theme.Teal, BadgeH * 0.5f);
            skipBadge.raycastTarget = false;
            Ui.Size(skipBadge, 0f, BadgeH);
            var row = Ui.Row(skipBadge.rectTransform, 0f);
            int pad = Mathf.RoundToInt(Theme.D(BadgePadX));
            row.padding = new RectOffset(pad, pad, 0, 0);
            skipBadgeText = Ui.Label(skipBadge.transform, "", BadgeFont, Theme.Card, Weight.Bold);

            Ui.AddShimmer(skipRt, BtnH * 0.5f, ButtonsT0 + ButtonsTime + 1.3f); // out of step with the revive's sweep
            skipGroup = b.gameObject.AddComponent<CanvasGroup>();
        }

        /// <summary>"Skips today" and one 14 px dot per skip of the day (4, more with bonus skips): teal = left, grey = used.</summary>
        void BuildDots(RectTransform parent)
        {
            dotsRt = Ui.New(parent, "SkipsToday");
            Ui.PlaceD(dotsRt, Ui.TC, Ui.C, new Vector2(0f, -DotsY), new Vector2(BtnW, 20f));
            Ui.Row(dotsRt, DotGap);
            var label = Ui.Label(dotsRt, "Skips today", DotsLabelFont, Theme.InkSoft, Weight.Semi);
            Ui.Size(label, 0f, 20f);
            var space = Ui.New(dotsRt, "Space");
            Ui.Size(space, 2f, DotD);
            for (int i = 0; i < MaxDots; i++)
            {
                var d = Ui.Img(dotsRt, "Dot" + i, WinArt.Disc, Theme.Teal);
                d.raycastTarget = false;
                Ui.Size(d, DotD, DotD);
                dots[i] = d;
            }
        }

        /// <summary>Dark pill over the dots row (covers nothing tappable), fades in, holds, fades out.</summary>
        void BuildToast()
        {
            var pill = Ui.Img(card, "Toast", Theme.Rounded, Theme.Ink, ToastH * 0.5f);
            pill.raycastTarget = false;
            toast = pill.rectTransform;
            Ui.PlaceD(toast, Ui.TC, Ui.C, new Vector2(0f, -DotsY), new Vector2(ToastW, ToastH));
            Ui.Fill(Ui.Label(toast, ToastText, 16f, Theme.Card, Weight.Semi).rectTransform);
            toastGroup = pill.gameObject.AddComponent<CanvasGroup>();
            toastGroup.blocksRaycasts = false;
            toastGroup.alpha = 0f;
        }

        static Image Art(RectTransform parent, string name, Sprite sprite, Color c, Vector2 posD, Vector2 sizeD)
        {
            var img = Ui.Img(parent, name, sprite, c);
            img.raycastTarget = false;
            Ui.PlaceD(img.rectTransform, Ui.C, Ui.C, posD, sizeD);
            return img;
        }

        static void AddEdge(Graphic g, Color c, float d)
        {
            var edge = g.gameObject.AddComponent<Shadow>();
            edge.effectColor = c;
            edge.effectDistance = new Vector2(0f, -Theme.D(d));
            edge.useGraphicAlpha = false;
        }

        // ------------------------------------------------------------------ show / hide / layout

        public override void OnShow()
        {
            MusicPlayer.Duck(MusicPlayer.LoseDuck);
            Ads.Banner(false, "PopupLose"); // back in OnHide (every route)
            Ui.FitSafeArea(safe);
            Canvas.ForceUpdateCanvases();
            // medium first: normal spacing, or the compact step at which the medium ad fits under the button row
            ApplyLayout(mrec.Plan());
            request++;
            used = false; // a new loss: the ad may revive once
            pending = Pending.None;
            var lost = GameApp.I.Get<ScreenGame>();
            AnalyticsService.LevelFail(GameApp.I.CurrentLevel, GameApp.I.CurrentMode.ToString(),
                                       lost != null && lost.Board != null ? lost.Board.Remaining : -1);
            AnalyticsService.ReviveAdOffered(GameApp.I.CurrentLevel); // the Watch ad button is on this screen
            Save.SkipsChanged -= RefreshSkip; // never twice
            Save.SkipsChanged += RefreshSkip;
            RefreshSkip();
            RefreshLabels();
            toastT0 = -100f;
            showT0 = Time.unscaledTime;
            Apply(0f);
            if (run != null) GameApp.I.StopCoroutine(run);
            run = GameApp.I.StartCoroutine(Run());
        }

        public override void OnHide()
        {
            MusicPlayer.Duck(1f);
            mrec.End(); // Try again / Home / Back / revive / skip all close the screen here
            Save.SkipsChanged -= RefreshSkip;
            request++;
            pending = Pending.None;
            if (run != null) GameApp.I.StopCoroutine(run);
            run = null;
            Apply(CardTime + ButtonsT0 + ButtonsTime); // leave it at rest
            reviveBox.localScale = Vector3.one;
            toastGroup.alpha = 0f;
            var game = GameApp.I.Get<ScreenGame>();
            if (game != null) game.RestoreBanner("PopupLose");
            else Ads.Banner(true, "PopupLose");
        }

        /// <summary>Lays the rows out at compact amount t (0 = normal, 1 = tight spacing, stars x 0.75). Positions and the star scale only.</summary>
        void ApplyLayout(float t, bool log = true)
        {
            SetY(banner, Mathf.Lerp(BannerCY, CBannerCY, t));
            float ss = Mathf.Lerp(1f, CStarScale, t);
            for (int i = 0; i < 3; i++)
            {
                stars[i].root.anchoredPosition = new Vector2(Theme.D((i - 1) * SideX * ss), -Theme.D(Mathf.Lerp(i == 1 ? MidY : SideY, i == 1 ? CMidY : CSideY, t)));
                stars[i].root.localScale = new Vector3(ss, ss, 1f);
            }
            SetY(msgRt, Mathf.Lerp(MsgY, CMsgY, t));
            SetY(reviveBox, Mathf.Lerp(ReviveTop, CReviveTop, t) + BtnH * 0.5f); // centre pivot (pulse)
            SetY(skipRt, Mathf.Lerp(SkipTop, CSkipTop, t));
            float dotsY = Mathf.Lerp(DotsY, CDotsY, t);
            SetY(dotsRt, dotsY);
            SetY(toast, dotsY);
            float row = Mathf.Lerp(RowTop, CRowTop, t);
            SetY(restartRt, row);
            SetY(homeRt, row);
            if (log && t > 0f)
                Debug.Log("[ArrowPuzzle] PopupLose compact layout t " + t.ToString("0.##") + ": stars x" + ss.ToString("0.00") +
                          ", row bottom " + (row + RowH + RowEdgeD).ToString("0"));
        }

        static void SetY(RectTransform rt, float yD)
        {
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -Theme.D(yD));
        }

        /// <summary>
        /// Where the MREC goes on this phone: its top at MrecFit.LoseAdTopD (412), moving up with the button row as the
        /// layout compacts (row 336 -> 286), at the smallest compact amount that keeps the whole frame on screen.
        /// </summary>
        MrecFit.Result PlanMrec()
        {
            return MrecFit.UnderRow(Ads.MrecAvailableDesign, Ads.ScreenWidthDp, MrecFit.LoseAdTopD, MrecFit.LoseAdTopD - (RowTop - CRowTop));
        }

        /// <summary>Closing with back would leave the board without lives, so back goes Home.</summary>
        public override bool OnBack()
        {
            if (pending != Pending.None) return true; // the video request decides first
            GameApp.I.Show("ScreenHome");
            return true;
        }

        // ------------------------------------------------------------------ per frame

        IEnumerator Run()
        {
            while (true) // keeps pulsing while the screen is open
            {
                Apply(Time.unscaledTime - showT0);
                yield return null;
            }
        }

        void Apply(float t)
        {
            float k = EaseOutCubic(Mathf.Clamp01(t / CardTime));
            card.anchoredPosition = new Vector2(0f, -Theme.D(RiseFrom) * (1f - k));
            cardGroup.alpha = k;
            if (t >= CardTime) mrec.Begin(); // risen into place; once per show

            msgGroup.alpha = Mathf.Clamp01((t - MsgT0) / MsgTime);

            float b = Mathf.Clamp01((t - ButtonsT0) / ButtonsTime);
            bool ready = b >= 1f && pending == Pending.None; // only tappable once visible, locked while a video runs
            SetGroup(reviveGroup, b * (pending == Pending.Revive ? WaitAlpha : 1f), ready && !used);
            SetGroup(skipGroup, b * (pending == Pending.Skip || pending == Pending.Bonus ? WaitAlpha : 1f), ready);
            SetGroup(rowGroup, b, ready);

            float visibleFor = t - (ButtonsT0 + ButtonsTime);
            float pulse = visibleFor > 0f && !used ? 1f + PulseAmp * Mathf.Sin(visibleFor * PulseSpeed) : 1f;
            reviveBox.localScale = new Vector3(pulse, pulse, 1f);

            // tears slide down a little and fade, each star on its own beat
            for (int i = 0; i < 3; i++)
            {
                var s = stars[i];
                float u = Mathf.Repeat(t + i * 0.7f, TearLoop) / TearLoop;
                s.tear.anchoredPosition = new Vector2(Theme.D(s.tearBase.x), Theme.D(s.tearBase.y - TearSlide * s.size * u));
                s.tearImg.color = WithAlpha(TearColor, Mathf.Sin(Mathf.PI * u));
            }

            // toast: in, hold, out
            float a = Time.unscaledTime - toastT0;
            toastGroup.alpha = a < 0f ? 0f : a < ToastIn ? a / ToastIn : a < ToastIn + ToastHold ? 1f
                             : Mathf.Clamp01(1f - (a - ToastIn - ToastHold) / ToastOut);
        }

        static void SetGroup(CanvasGroup g, float alpha, bool on)
        {
            g.alpha = alpha;
            g.interactable = g.blocksRaycasts = on;
        }

        void ShowToast()
        {
            toastT0 = Time.unscaledTime;
        }

        // ------------------------------------------------------------------ Skip states

        /// <summary>Restyles Skip for SkipsLeft (teal outline / gold) and recolours the dots. Also on Save.SkipsChanged.</summary>
        void RefreshSkip()
        {
            int left = Save.SkipsLeft;
            bool any = left > 0;
            skipFill.SetActive(any);
            skipImg.color = any ? Theme.Teal : Theme.Gold;               // outline colour / gold face
            if (skipShadow != null) skipShadow.effectColor = any ? Theme.TealShadow : GoldEdge;
            Color fg = any ? Theme.TealShadow : Theme.Ink;
            skipLabel.color = fg;
            foreach (var img in skipIcon) img.color = fg;
            skipBadge.color = any ? Theme.Teal : Theme.Ink;
            skipBadgeText.text = any ? left + " left" : "watch ad";

            int total = Mathf.Clamp(Save.SkipsPerDay + Save.SkipsBonusToday, Save.SkipsPerDay, MaxDots);
            int shownLeft = Mathf.Min(left, total);
            for (int i = 0; i < MaxDots; i++)
            {
                dots[i].gameObject.SetActive(i < total);
                dots[i].color = i < shownLeft ? Theme.Teal : DotUsed;
            }
            RefreshLabels();
        }

        void RefreshLabels()
        {
            reviveLabel.text = pending == Pending.Revive ? WaitText : WatchText;
            skipLabel.text = pending == Pending.Skip || pending == Pending.Bonus ? WaitText : Save.SkipsLeft > 0 ? SkipText : BonusText;
        }

        void SetPending(Pending p)
        {
            pending = p;
            RefreshLabels();
        }

        // ------------------------------------------------------------------ rewarded: revive

        void WatchAd()
        {
            if (pending != Pending.None || used) return;
            if (Ads.IsRewardedBusy) { ShowToast(); return; } // ShowRewarded would ignore the call: never wait for it
            int id = ++request;
            SetPending(Pending.Revive);
            Ads.ShowRewarded(() => OnRevive(id), () => OnFailed(id));
        }

        /// <summary>Reward earned: revive this loss once (the same +1 life as before) and close.</summary>
        void OnRevive(int id)
        {
            if (id != request || used || !GameApp.I.IsOpen<PopupLose>()) return;
            used = true;
            AnalyticsService.ReviveAdDone(GameApp.I.CurrentLevel);
            SetPending(Pending.None);
            GameApp.I.Close("PopupLose");
            var game = GameApp.I.Get<ScreenGame>();
            if (game != null) game.Board.ReviveLife(); // ReviveLife resets the collapse and scales the arrows in
        }

        // ------------------------------------------------------------------ rewarded: skip / bonus skips

        /// <summary>Skips left: a rewarded ad skips the level. None left: a rewarded ad adds SkipBonus skips for today.</summary>
        void OnSkipTap()
        {
            if (pending != Pending.None) return;
            if (Ads.IsRewardedBusy) { ShowToast(); return; }
            int id = ++request;
            if (Save.SkipsLeft > 0)
            {
                SetPending(Pending.Skip);
                Ads.ShowRewarded(() => OnSkipReward(id), () => OnFailed(id));
            }
            else
            {
                SetPending(Pending.Bonus);
                Ads.ShowRewarded(() => OnBonusReward(id), () => OnFailed(id));
            }
        }

        /// <summary>
        /// Ad completed: one skip is used, the level counts as passed (Save.SkipLevel: 1 star only if it had none, next
        /// level unlocked; no coins, no best time, no win popup) and the next level starts right away, no interstitial.
        /// </summary>
        void OnSkipReward(int id)
        {
            if (id != request || !GameApp.I.IsOpen<PopupLose>()) return; // left already: nothing is spent
            int g = GameApp.I.CurrentLevel;
            if (Save.SkipsLeft <= 0) { SetPending(Pending.None); RefreshSkip(); return; }
            Save.UseSkip();
            Save.SkipLevel(g);
            pending = Pending.None;
            GameApp.I.Close("PopupLose");
            int next = g + 1;
            var game = GameApp.I.Get<ScreenGame>();
            if (next > LevelPath.Count || !Save.IsUnlocked(next) || game == null)
            {
                GameApp.I.Show("ScreenHome"); // the last level skipped (or no game screen): Home
                return;
            }
            GameApp.I.CurrentLevel = next;
            game.StartLevel(next); // ScreenGame is already visible, so Show would not reload it
        }

        /// <summary>Ad completed with no skips left: +2 skips for today; the button and the dots follow (SkipsChanged).</summary>
        void OnBonusReward(int id)
        {
            if (id != request || !GameApp.I.IsOpen<PopupLose>()) return;
            SetPending(Pending.None);
            Save.AddSkipBonus(SkipBonus); // raises SkipsChanged -> RefreshSkip
        }

        /// <summary>Not ready, failed to show or closed early: nothing is given or spent; a short toast; the screen stays.</summary>
        void OnFailed(int id)
        {
            if (id != request) return;
            SetPending(Pending.None);
            ShowToast();
        }

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
    }
}
