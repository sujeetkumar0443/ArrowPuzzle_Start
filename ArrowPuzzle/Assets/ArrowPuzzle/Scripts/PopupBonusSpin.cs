using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ArrowPuzzle
{
    /// <summary>
    /// Full-screen BONUS SPIN after a win, before Level complete (ScreenGame.OnWon opens it; the win is already saved,
    /// +25 coins included). The game stays visible behind a navy tint. Gold JACKPOT SPIN ribbon, the big "xN" under
    /// the pointer, the half wheel (WinArt gold-rim wheel, R 135, 8 blinking bulbs), Claim xN (rewarded ad), "No thanks"
    /// (3 s after the swing starts), twinkling sparkles, and the 300x250 MREC under "No thanks" (MrecSlot,
    /// MrecKind.Bonus; Level complete's MREC preloads while the wheel spins). Claim completed: base x (N - 1) more coins, "Collected!" for 1 s, then Level
    /// complete with GameApp.BonusMult = N (and its interstitial skipped once). No thanks: Level complete with
    /// BonusMult = 0. The hand-over is Close then Open (never from OnHide), and the banner stays hidden across it.
    /// Unscaled time; everything runs in one coroutine that OnHide stops.
    /// </summary>
    public class PopupBonusSpin : AppScreen
    {
        public override bool IsPopup { get { return true; } }

        // ---- look
        static readonly Color Navy = new Color(12f / 255f, 22f / 255f, 42f / 255f, 0.80f);
        static readonly Color RibbonFace = Theme.Hex("#FFC82E"), RibbonEdge = Theme.Hex("#8A5A0A"), RibbonInk = Theme.Hex("#3A2600");
        static readonly Color XnColor = Theme.Hex("#FFE08A");
        static readonly Color XnGlow = new Color(1f, 200f / 255f, 46f / 255f, 0.45f);
        static readonly Color XnShadow = new Color(0f, 0f, 0f, 0.5f);
        static readonly Color BulbColor = Theme.Hex("#FFF8E8");
        static readonly Color SparkleColor = Theme.Hex("#FFC82E");
        static readonly Color CoinEdge = Theme.Hex("#D98E00");
        static readonly int[] Mult = { 1, 2, 8, 10, 8, 2, 1 };

        // ---- sizes (design px; wheel parts in WinArt's 115 geometry, scaled to radius 135)
        const float RibbonW = 300f, RibbonH = 52f, RibbonEdgeD = 6f, RibbonFont = 26f, RibbonR = 14f;
        const float XnFont = 66f, XnH = 84f;
        const float WheelBaseScale = 135f / WinArt.WheelR;                     // 1.174
        const float BoxTopL = WinArt.WheelOuter, BoxBottomL = -WinArt.BaseH;   // wheel box, local (y up from the hub)
        const float BoxCentreL = (BoxTopL + BoxBottomL) * 0.5f;                // 56.5 above the hub
        const float BoxHL = BoxTopL - BoxBottomL;                              // 133
        const float LabelR = 0.74f * WinArt.WheelR, LabelFont = 21f, LabelFontHi = 25f, GlowAlpha = 0.32f;
        const float CoralD = 36f, CoralY = 3f, CoralEdgeD = 3f;
        const float BulbD = 10f, BulbEvery = 0.6f, BulbDim = 0.35f;
        const float ClaimW = 300f, ClaimH = 58f, ClaimEdgeD = 5f, ClaimFont = 23f, ClaimIconD = 18f;
        const float PulseAmp = 0.03f, PulseSpeed = 5f;
        const float NoFont = 19f, NoDelay = 3f, NoFade = 0.4f, NoSlide = 14f;
        const float ToastW = 260f, ToastH = 40f, ToastIn = 0.15f, ToastHold = 1.4f, ToastOut = 0.3f;
        const string ToastText = "Ad not ready, try again";
        // ---- layout: design px from the safe-area top (centres), normal / compact (t = 0 .. 1). The MREC top is at
        // MrecFit.BonusAdTopD (564; higher only when the screen bottom forces it); the gap from "No thanks" to its frame
        // shrinks down to 24 first, then the wheel group compacts (radius 135 -> 97). Text and button sizes never
        // change; the ad never shrinks (MrecSlot.Plan, MrecFit.Bonus).
        //   normal : ribbon 70, xN 146, wheel box 194..350 (hub 338), Claim 419..477, No thanks 510..554
        //   compact: ribbon 50, xN 116, wheel box 154..266 (hub 257), Claim 287..345, No thanks 363..407
        const float RibbonY = 70f, CRibbonY = 50f;
        const float XnY = 146f, CXnY = 116f;
        const float WheelCY = 272f, CWheelCY = 210f, CWheelS = 0.72f;
        const float ClaimY = 448f, CClaimY = 316f;
        const float NoY = 532f, CNoY = 385f;

        // ---- opening (s since OnShow) and wheel timing
        const float FadeT = 0.25f, RibbonT0 = 0.1f, RibbonT1 = 0.45f, XnT0 = 0.3f, XnT1 = 0.55f;
        const float WheelT0 = 0.25f, WheelT1 = 0.65f, ClaimT0 = 0.5f, ClaimT1 = 0.8f, OpenDone = 0.8f, AdBeginT = 0.65f;
        const float SwingDeg = 78f, SwingPeriod = 2.2f, CollectedHold = 1f;
        const int Slices = WinArt.Slices;
        const float SliceDeg = WinArt.SliceDeg;

        enum Wheel { Idle, Swinging, Waiting, Collected } // Idle = before the opening is done (or after leaving)

        Image dim;
        RectTransform safe, ribbon, xnRt, wheel, glow, pointer, claimBox, noBox, toast;
        CanvasGroup ribbonGroup, xnGroup, wheelGroup, claimGroup, noGroup, toastGroup;
        Text xnText, claimLabel, claimAmount;
        GameObject claimPill;
        readonly Text[] labels = new Text[Slices];
        readonly Image[] bulbs = new Image[Slices + 1];
        readonly RectTransform[] sparkles = new RectTransform[6];
        readonly Image[] sparkleImg = new Image[6];
        MrecSlot mrec; // 300x250 MREC (or its shimmer) under "No thanks"

        float wheelS = 1f, noY = NoY;  // current layout
        Wheel state;
        int highlighted = -1, wheelId;
        float t0, swingT, swingStartT, collectedT0 = -100f, toastT0 = -100f;
        bool noVisible, handingOver;
        Coroutine run;

        protected override void Build()
        {
            // the game stays visible behind the navy tint (no blur); the tint blocks taps to it
            dim = Ui.Img(Root, "Dim", null, Navy);
            Ui.Fill(dim.rectTransform);
            safe = Ui.New(Root, "Safe");
            Ui.FitSafeArea(safe);

            BuildRibbon();
            BuildXn();
            BuildWheel();
            BuildClaim();
            BuildNoThanks();
            BuildToast();

            mrec = MrecSlot.Create(safe, MrecKind.Bonus, "PopupBonusSpin", PlanMrec, t => ApplyLayout(t, false));
        }

        /// <summary>
        /// Where the MREC goes on this phone (MrecFit.Bonus): top at 564 (higher only when the bottom forces it), the gap
        /// under "No thanks" down to 24, then the wheel group compacts. Returns the compact amount t for ApplyLayout.
        /// </summary>
        MrecFit.Result PlanMrec()
        {
            float half = WinArt.NoThanksH * 0.5f;
            return MrecFit.Bonus(Ads.MrecAvailableDesign, Ads.ScreenWidthDp, MrecFit.BonusAdTopD, NoY + half, CNoY + half);
        }

        // ------------------------------------------------------------------ build

        void BuildRibbon()
        {
            var img = Ui.Img(safe, "Ribbon", Theme.Rounded, RibbonFace, RibbonR);
            img.raycastTarget = false;
            ribbon = img.rectTransform;
            Ui.PlaceD(ribbon, Ui.TC, Ui.C, new Vector2(0f, -RibbonY), new Vector2(RibbonW, RibbonH));
            AddEdge(img, RibbonEdge, RibbonEdgeD);
            Ui.Fill(Ui.Label(ribbon, "JACKPOT SPIN", RibbonFont, RibbonInk, Weight.Bold).rectTransform);
            ribbonGroup = img.gameObject.AddComponent<CanvasGroup>();
        }

        /// <summary>The multiplier under the pointer: big, warm gold, a soft gold glow (Outline) and a dark shadow.</summary>
        void BuildXn()
        {
            xnText = Ui.Label(safe, "x10", XnFont, XnColor, Weight.Bold);
            xnRt = xnText.rectTransform;
            Ui.PlaceD(xnRt, Ui.TC, Ui.C, new Vector2(0f, -XnY), new Vector2(300f, XnH));
            var sh = xnText.gameObject.AddComponent<Shadow>();
            sh.effectColor = XnShadow;
            sh.effectDistance = new Vector2(0f, -Theme.D(3f));
            var glowFx = xnText.gameObject.AddComponent<Outline>();
            glowFx.effectColor = XnGlow;
            glowFx.effectDistance = new Vector2(Theme.D(3f), Theme.D(3f));
            xnGroup = xnText.gameObject.AddComponent<CanvasGroup>();
        }

        /// <summary>
        /// Gold-rim half wheel (origin = hub): wheel sprite, highlight wedge, upright labels at 74 % of the radius,
        /// 8 blinking bulbs on the rim at the slice boundaries, white half hub, pointer, coral hub, sparkles around.
        /// Built in WinArt's radius-115 geometry and scaled (ApplyLayout) to radius 135 (less on short phones).
        /// </summary>
        void BuildWheel()
        {
            wheel = Ui.New(safe, "Wheel");
            Ui.PlaceD(wheel, Ui.TC, Ui.C, Vector2.zero, Vector2.zero);
            wheelGroup = wheel.gameObject.AddComponent<CanvasGroup>();
            wheelGroup.blocksRaycasts = false;

            Art(wheel, "WheelArt", WinArt.WheelGold, Color.white, new Vector2(0f, BoxCentreL), new Vector2(2f * WinArt.WheelHalfW, BoxHL));

            var g = Ui.Img(wheel, "Glow", WinArt.Glow, new Color(1f, 1f, 1f, GlowAlpha));
            g.raycastTarget = false;
            glow = g.rectTransform;
            Ui.PlaceD(glow, Ui.C, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(2f * WinArt.GlowHalfW, WinArt.WheelR));

            for (int k = 0; k < Slices; k++)
            {
                float a = (180f - (k + 0.5f) * SliceDeg) * Mathf.Deg2Rad;
                bool gold = Mult[k] == 10;
                var l = Ui.Label(wheel, Mult[k] + "x", LabelFont, gold ? Theme.Ink : Theme.Card, Weight.Bold);
                Ui.PlaceD(l.rectTransform, Ui.C, Ui.C, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * LabelR, new Vector2(64f, 34f));
                var sh = l.gameObject.AddComponent<Shadow>();
                sh.effectDistance = new Vector2(0f, -Theme.D(1.5f));
                sh.effectColor = gold ? new Color(1f, 1f, 1f, 0.45f) : new Color(Theme.Ink.r, Theme.Ink.g, Theme.Ink.b, 0.6f);
                labels[k] = l;
            }

            // bulbs: 10 px on screen at full size, on the rim's centre line at every slice boundary
            float rc = WinArt.WheelR + WinArt.Ring * 0.5f, bd = BulbD / WheelBaseScale;
            for (int b = 0; b <= Slices; b++)
            {
                float a = (180f - b * SliceDeg) * Mathf.Deg2Rad;
                bulbs[b] = Art(wheel, "Bulb" + b, WinArt.Disc, BulbColor, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rc, new Vector2(bd, bd));
            }

            float hubW = WinArt.HubR + WinArt.HubEdge;
            Art(wheel, "Hub", WinArt.Hub, Color.white, new Vector2(0f, hubW * 0.5f), new Vector2(2f * hubW, hubW));

            var p = Ui.Img(wheel, "Pointer", WinArt.Pointer, Color.white);
            p.raycastTarget = false;
            pointer = p.rectTransform;
            float ph = WinArt.PtrTop - WinArt.PtrBottom;
            Ui.PlaceD(pointer, Ui.C, new Vector2(0.5f, -WinArt.PtrBottom / ph), Vector2.zero, new Vector2(2f * WinArt.PtrHalfW, ph));

            var coral = Art(wheel, "Coral", WinArt.Disc, Theme.Accent, new Vector2(0f, CoralY), new Vector2(CoralD, CoralD));
            AddEdge(coral, Theme.AccentShadow, CoralEdgeD);

            // twinkling gold sparkles around the wheel: (x, y, size) in wheel units
            float[,] sp = { { -150f, 42f, 20f }, { 148f, 60f, 16f }, { -118f, 128f, 14f }, { 122f, 132f, 18f }, { -160f, -2f, 13f }, { 160f, 6f, 15f } };
            for (int i = 0; i < sparkles.Length; i++)
            {
                sparkleImg[i] = Art(wheel, "Sparkle" + i, WinArt.Sparkle, SparkleColor, new Vector2(sp[i, 0], sp[i, 1]), new Vector2(sp[i, 2], sp[i, 2]));
                sparkles[i] = sparkleImg[i].rectTransform;
            }
        }

        /// <summary>Teal Claim xN with the coin pill (base x N); pulses inside its box (centre pivot) and has the shine sweep.</summary>
        void BuildClaim()
        {
            claimBox = Ui.New(safe, "ClaimBox");
            Ui.PlaceD(claimBox, Ui.TC, Ui.C, new Vector2(0f, -ClaimY), new Vector2(ClaimW, ClaimH));
            var claim = Ui.Btn(claimBox, "Claim x10", Theme.Teal, Theme.Card, ClaimFont, ClaimH * 0.5f, Theme.TealShadow, ClaimEdgeD,
                               OnClaim, Icon.Play, ClaimIconD);
            Ui.Fill((RectTransform)claim.transform);
            claimLabel = claim.GetComponentInChildren<Text>();

            var pill = Ui.Img(claim.transform, "CoinPill", Theme.Rounded, Theme.TealShadow, 16f);
            pill.raycastTarget = false;
            var row = Ui.Row(pill.rectTransform, 4f);
            row.padding = new RectOffset(Mathf.RoundToInt(Theme.D(5f)), Mathf.RoundToInt(Theme.D(11f)), 0, 0);
            Ui.Size(pill, 0f, 32f);
            Ui.Size(Coin(pill.transform, 24f), 24f, 24f);
            claimAmount = Ui.Label(pill.transform, "", 19f, Theme.Card, Weight.Bold);
            claimPill = pill.gameObject;

            Ui.AddShimmer((RectTransform)claim.transform, ClaimH * 0.5f, OpenDone);
            claimGroup = claimBox.gameObject.AddComponent<CanvasGroup>();
        }

        /// <summary>"No thanks": the generated pill (white 16 % fill, 2 px white 55 % outline), white text 19.</summary>
        void BuildNoThanks()
        {
            noBox = Ui.New(safe, "NoThanksBox");
            Ui.PlaceD(noBox, Ui.TC, Ui.C, new Vector2(0f, -NoY), new Vector2(WinArt.NoThanksW, WinArt.NoThanksH));
            var no = Ui.Btn(noBox, "No thanks", Color.white, Theme.Card, NoFont, WinArt.NoThanksH * 0.5f, Color.clear, 0f,
                            OnNoThanks, null, 24f, Weight.Semi);
            var img = no.GetComponent<Image>();
            img.sprite = WinArt.NoThanksPill; // colours baked into the sprite
            img.type = Image.Type.Simple;
            img.color = Color.white;
            Ui.Fill((RectTransform)no.transform);
            noGroup = noBox.gameObject.AddComponent<CanvasGroup>();
        }

        /// <summary>White pill with ink text over the ribbon (covers nothing tappable), fades in, holds, fades out.</summary>
        void BuildToast()
        {
            var pill = Ui.Img(safe, "Toast", Theme.Rounded, Theme.Card, ToastH * 0.5f);
            pill.raycastTarget = false;
            toast = pill.rectTransform;
            Ui.PlaceD(toast, Ui.TC, Ui.C, new Vector2(0f, -RibbonY), new Vector2(ToastW, ToastH));
            Ui.Fill(Ui.Label(toast, ToastText, 16f, Theme.Ink, Weight.Semi).rectTransform);
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

        /// <summary>Gold coin: darker rim, gold face slightly up, a small white shine (as on Level complete).</summary>
        static RectTransform Coin(Transform parent, float d)
        {
            var rim = Ui.Img(parent, "Coin", WinArt.Disc, CoinEdge);
            rim.raycastTarget = false;
            var rt = rim.rectTransform;
            rt.sizeDelta = new Vector2(Theme.D(d), Theme.D(d));
            Art(rt, "Face", WinArt.Disc, Theme.Gold, new Vector2(0f, 0.04f * d), new Vector2(0.76f * d, 0.76f * d));
            Art(rt, "Shine", WinArt.Disc, new Color(1f, 1f, 1f, 0.55f), new Vector2(-0.16f * d, 0.18f * d), new Vector2(0.2f * d, 0.2f * d));
            return rt;
        }

        // ------------------------------------------------------------------ layout

        /// <summary>Positions at compact amount t (0 = normal, 1 = compact: wheel radius 97, tighter gaps).</summary>
        void ApplyLayout(float t, bool log = true)
        {
            SetY(ribbon, Mathf.Lerp(RibbonY, CRibbonY, t));
            SetY(toast, Mathf.Lerp(RibbonY, CRibbonY, t));
            SetY(xnRt, Mathf.Lerp(XnY, CXnY, t));
            wheelS = Mathf.Lerp(1f, CWheelS, t);
            float hub = Mathf.Lerp(WheelCY, CWheelCY, t) + BoxCentreL * WheelBaseScale * wheelS; // box centre -> hub (y down)
            SetY(wheel, hub);
            SetY(claimBox, Mathf.Lerp(ClaimY, CClaimY, t));
            noY = Mathf.Lerp(NoY, CNoY, t);
            SetY(noBox, noY);
            if (log && t > 0f)
                Debug.Log("[ArrowPuzzle] PopupBonusSpin compact layout t " + t.ToString("0.##") + ": wheel radius " +
                          (135f * wheelS).ToString("0") + ", No thanks bottom " + (noY + WinArt.NoThanksH * 0.5f).ToString("0"));
        }

        static void SetY(RectTransform rt, float yD)
        {
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -Theme.D(yD));
        }

        // ------------------------------------------------------------------ show / hide

        public override void OnShow()
        {
            MusicPlayer.Duck(MusicPlayer.WinDuck);
            Ads.Banner(false, "PopupBonusSpin"); // stays hidden into Level complete; given back only on other exits (OnHide)
            Ui.FitSafeArea(safe);
            Canvas.ForceUpdateCanvases();
            // the ad's place decides how much room the wheel group has: shrink the gap, then the wheel, never the ad
            ApplyLayout(mrec.Plan());
            MrecSlot.Preload(MrecKind.Win); // Level complete's MREC loads while the wheel spins

            wheelId++;
            state = Wheel.Idle;
            swingT = 0f;
            noVisible = false;
            handingOver = false;
            collectedT0 = toastT0 = -100f;
            claimPill.SetActive(true);
            highlighted = -1;
            SetPointer(0f); // straight up (10x) until the swing starts; sets xN and Claim

            t0 = Time.unscaledTime;
            Apply(0f);
            if (run != null) GameApp.I.StopCoroutine(run);
            run = GameApp.I.StartCoroutine(Run());
        }

        public override void OnHide()
        {
            MusicPlayer.Duck(1f); // Level complete ducks again in its OnShow
            if (run != null) GameApp.I.StopCoroutine(run); // the swing, bulbs, toast and hand-over timer all live in Run
            run = null;
            if (state == Wheel.Swinging) state = Wheel.Idle;
            toastGroup.alpha = 0f;
            mrec.End(); // destroyed before Level complete shows its own MREC
            if (handingOver) return; // Level complete opens next and keeps the banner hidden
            var game = GameApp.I.Get<ScreenGame>();
            if (game != null) game.RestoreBanner("PopupBonusSpin");
            else Ads.Banner(true, "PopupBonusSpin");
        }

        /// <summary>Back: ignored until "No thanks" is visible (and while the video runs), then it acts like No thanks.</summary>
        public override bool OnBack()
        {
            if (noVisible && state == Wheel.Swinging) OnNoThanks();
            return true;
        }

        // ------------------------------------------------------------------ per frame

        IEnumerator Run()
        {
            while (true) // until OnHide
            {
                Apply(Time.unscaledTime - t0);
                yield return null;
            }
        }

        void Apply(float t)
        {
            float now = Time.unscaledTime;

            // opening: tint, ribbon pop, xN, wheel scale-in, Claim
            dim.color = WithAlpha(Navy, Navy.a * Mathf.Clamp01(t / FadeT));
            float r = Mathf.Clamp01((t - RibbonT0) / (RibbonT1 - RibbonT0));
            ribbonGroup.alpha = r;
            ribbon.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, EaseOutBack(r, 2.2f));
            xnGroup.alpha = Mathf.Clamp01((t - XnT0) / (XnT1 - XnT0));
            float w = Mathf.Clamp01((t - WheelT0) / (WheelT1 - WheelT0));
            wheelGroup.alpha = w;
            float ws = WheelBaseScale * wheelS * Mathf.LerpUnclamped(0.7f, 1f, EaseOutBack(w, 1.6f));
            wheel.localScale = new Vector3(ws, ws, 1f);
            float c = Mathf.Clamp01((t - ClaimT0) / (ClaimT1 - ClaimT0));
            claimGroup.alpha = c;
            if (t >= AdBeginT) mrec.Begin(); // once per show

            // wheel
            if (state == Wheel.Idle && t >= OpenDone && !handingOver)
            {
                state = Wheel.Swinging;
                swingStartT = now;
                AnalyticsService.WinWheelShown(GameApp.I.CurrentLevel);
            }
            if (state == Wheel.Swinging)
            {
                swingT += Time.unscaledDeltaTime;
                SetPointer(SwingDeg * Mathf.Sin(swingT * (2f * Mathf.PI / SwingPeriod)));
            }
            float pulse = state == Wheel.Swinging ? 1f + PulseAmp * Mathf.Sin((now - swingStartT) * PulseSpeed) : 1f;
            claimBox.localScale = new Vector3(pulse, pulse, 1f);
            claimGroup.interactable = claimGroup.blocksRaycasts = state == Wheel.Swinging;

            // No thanks: hidden for NoDelay s after the swing starts, then fades in with a 14 px slide up
            float u = state == Wheel.Idle ? -1f : (now - swingStartT - NoDelay) / NoFade;
            float nk = EaseOutCubic(Mathf.Clamp01(u));
            noVisible = u >= 0f;
            noGroup.alpha = nk;
            noBox.anchoredPosition = new Vector2(0f, -Theme.D(noY + NoSlide * (1f - nk)));
            noGroup.interactable = noGroup.blocksRaycasts = noVisible && state == Wheel.Swinging;

            // bulbs blink alternately (every BulbEvery s), sparkles twinkle
            bool even = Mathf.FloorToInt(now / BulbEvery) % 2 == 0;
            for (int b = 0; b < bulbs.Length; b++)
                bulbs[b].color = WithAlpha(BulbColor, (b % 2 == 0) == even ? 1f : BulbDim);
            for (int i = 0; i < sparkles.Length; i++)
            {
                float a = 0.5f + 0.5f * Mathf.Sin(3.6f * now + i * 1.9f);
                sparkles[i].localScale = Vector3.one * (0.4f + 0.6f * a);
                sparkleImg[i].color = WithAlpha(SparkleColor, a);
            }

            // toast
            float tt = now - toastT0;
            toastGroup.alpha = tt < 0f ? 0f : tt < ToastIn ? tt / ToastIn : tt < ToastIn + ToastHold ? 1f
                             : Mathf.Clamp01(1f - (tt - ToastIn - ToastHold) / ToastOut);

            // collected: "Collected!" for CollectedHold s, then Level complete
            if (state == Wheel.Collected && now - collectedT0 >= CollectedHold) HandOver();
        }

        // ------------------------------------------------------------------ wheel

        static int SliceAt(float deg)
        {
            return Mathf.Clamp(Mathf.FloorToInt((deg + 90f) / SliceDeg), 0, Slices - 1);
        }

        /// <summary>Turns the pointer; when the slice under it changes: highlight wedge, label 25, xN, Claim xN and its coins.</summary>
        void SetPointer(float deg)
        {
            pointer.localEulerAngles = new Vector3(0f, 0f, -deg);
            int k = SliceAt(deg);
            if (k == highlighted) return;
            highlighted = k;
            float centre = 180f - (k + 0.5f) * SliceDeg;
            glow.localEulerAngles = new Vector3(0f, 0f, centre - 90f);
            for (int i = 0; i < Slices; i++)
                labels[i].fontSize = Mathf.RoundToInt(Theme.D(i == k ? LabelFontHi : LabelFont));
            int mult = Mult[k];
            xnText.text = "x" + mult;
            claimLabel.text = "Claim x" + mult;
            claimAmount.text = (Save.WinCoins * mult).ToString();
        }

        /// <summary>Claim: the pointer stops on its slice at once, the buttons lock, one rewarded ad for that multiplier.</summary>
        void OnClaim()
        {
            if (state != Wheel.Swinging) return;
            int mult = Mult[highlighted];
            if (Ads.IsRewardedBusy) { toastT0 = Time.unscaledTime; return; } // ShowRewarded would ignore it: keep swinging
            state = Wheel.Waiting;
            AnalyticsService.WinWheelClaimTap(GameApp.I.CurrentLevel, mult);
            int id = wheelId;
            Ads.ShowRewarded(() => OnReward(id, mult), () => OnFailed(id, mult));
        }

        /// <summary>
        /// Video completed: the level pays base x mult in total, so base x (mult - 1) on top of the base already given
        /// in Save.LevelWon. "Collected!" for CollectedHold s, then Level complete with BonusMult = mult; its NEXT / Home
        /// skip the interstitial once.
        /// </summary>
        void OnReward(int id, int mult)
        {
            if (id != wheelId || state != Wheel.Waiting) return;
            int bonus = Save.WinCoins * (mult - 1);
            if (bonus > 0) Save.AddCoins(bonus);
            state = Wheel.Collected;
            GameApp.I.BonusMult = mult;
            GameApp.I.SkipInterstitialOnce = true;
            claimLabel.text = "Collected!";
            claimPill.SetActive(false);
            collectedT0 = Time.unscaledTime;
            AnalyticsService.WinWheelAdCompleted(GameApp.I.CurrentLevel, mult, bonus);
        }

        /// <summary>Not ready, failed to show or closed early: nothing is paid, the swing resumes, a short toast.</summary>
        void OnFailed(int id, int mult)
        {
            if (id != wheelId || state != Wheel.Waiting) return;
            state = GameApp.I.IsOpen<PopupBonusSpin>() ? Wheel.Swinging : Wheel.Idle;
            toastT0 = Time.unscaledTime;
            AnalyticsService.WinWheelAdFailed(GameApp.I.CurrentLevel, mult);
        }

        void OnNoThanks()
        {
            if (!noVisible || state != Wheel.Swinging) return;
            GameApp.I.BonusMult = 0;
            HandOver();
        }

        /// <summary>
        /// Close this popup, then open Level complete (never from OnHide, which also runs when another screen takes
        /// over). OnHide sees handingOver and keeps the banner hidden; Level complete hides it in its own OnShow.
        /// </summary>
        void HandOver()
        {
            if (handingOver) return;
            handingOver = true;
            state = Wheel.Idle;
            GameApp.I.Close("PopupBonusSpin");
            bool opened = GameApp.I.Open("PopupWin");
            handingOver = false;
            if (!opened)
            {
                var game = GameApp.I.Get<ScreenGame>();
                if (game != null) game.RestoreBanner("PopupBonusSpin");
            }
        }

        // ------------------------------------------------------------------ helpers

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

        static float EaseOutBack(float x, float c1)
        {
            float c3 = c1 + 1f, a = x - 1f;
            return 1f + c3 * a * a * a + c1 * a * a;
        }
    }
}
