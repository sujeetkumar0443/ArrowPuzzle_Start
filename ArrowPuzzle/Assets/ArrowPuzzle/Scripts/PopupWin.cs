using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArrowPuzzle
{
    /// <summary>
    /// Full-screen Level complete, opened by PopupBonusSpin after the bonus wheel (cream over the whole screen,
    /// content in the safe area). Opening: an arrow sweeps across, the LEVEL COMPLETE! ribbon stamps down (shake +
    /// gold ring) and moves up, the content slides in, the earned smiley stars pop in one by one (confetti), the coin
    /// card pops in, then Home / NEXT LEVEL fade in. The coin card shows what this win paid: "+25 coins earned"
    /// (GameApp.BonusMult 0 = No thanks) or "+(25 x N) coins earned with bonus spin" with a gold xN badge. The medium
    /// 300x250 MREC (MrecSlot, MrecKind.Win) goes under the row. Tapping before the buttons appear skips to the end. Unscaled time,
    /// t = s since OnShow; the one-shot timeline is a pure function of t (Apply), the idle life (wiggle, blink,
    /// sparkles, "z") runs every frame after it (ApplyLife). NEXT / Home: interstitial (today's rules), skipped once
    /// right after the bonus wheel's rewarded ad (GameApp.SkipInterstitialOnce).
    /// </summary>
    public class PopupWin : AppScreen
    {
        public override bool IsPopup { get { return true; } }

        // ---- timeline (s) and sizes (design px)
        const float SweepT0 = 0.15f, SweepT1 = 0.75f, SweepFrom = -260f, SweepTo = 520f;   // arrow tail x from the screen left
        const float SweepBody = 300f, SweepThick = 26f, SweepHead = 56f, GhostLag = 0.04f;
        const int Ghosts = 3;
        const float BannerYFrac = 0.43f;                                                    // banner / sweep y while stamping, of the safe height
        const float BannerW = 330f, BannerH = 48f, BannerTilt = 6f, BannerFont = 28f, BannerEdgeD = 6f;
        const float BannerBig = 440f / 330f;                                                // stamp size: about 440 wide
        const float StampT0 = 0.7f, StampT1 = 0.95f, StampScale = 1.6f, StampBack = 2.4f;
        const float ShakeT0 = 0.95f, ShakeT1 = 1.3f, ShakeAmp = 7f, ShakeFreq = 70f;
        const float RingT0 = 0.95f, RingT1 = 1.45f, RingR0 = 60f, RingR1 = 360f, RingW0 = 8f, RingW1 = 1f;
        const float MoveT0 = 1.5f, MoveT1 = 2.0f;
        const float CardT0 = 1.7f, CardT1 = 2.3f;                                           // the content body slides up from below the screen
        // stars: 66 / 90 / 66, sides tilted +-10, centre 8 higher
        const float MidD = 90f, SideD = 66f, SideX = 96f, SideTilt = 10f;
        const float PopT0 = 2.05f, PopEvery = 0.5f, PopTime = 0.42f, PopPeak = 1.28f, PopPeakAt = 0.6f;
        const float WiggleDeg = 4f, WiggleScale = 0.03f, WiggleRamp = 0.4f;
        const float BlinkEvery = 3.2f, BlinkTime = 0.14f;
        const float ZLoop = 2.4f, ZRise = 12f;
        const float StarRingR0 = 24f, StarRingR1 = 90f, StarRingW0 = 5f, StarRingW1 = 1f, StarRingTime = 0.5f;
        const float LandShake = 3f, LandShakeTime = 0.2f;
        // coin card: white 326 x 92, radius 26, 7 px #E3DACB edge; coin 48, "+N" 40 bold, note 16; gold xN badge
        const float CoinCardW = 326f, CoinCardH = 92f, CoinCardR = 26f, CoinCardEdgeD = 7f;
        const float CoinD = 48f, CoinGap = 12f, AmountFont = 40f, NoteFont = 16f, AmountY = 11f, NoteY = -18f;
        const float BadgeW = 62f, BadgeH = 34f, BadgeEdgeD = 3f, BadgeFont = 20f, BadgeTilt = -8f, BadgeX = -26f;
        const float CoinCardT0 = 2.6f, CoinCardT1 = 2.95f;
        // the row: round-ish Home (64) + 12 + NEXT LEVEL
        const float ButtonW = 342f, NextH = 60f, RowGap = 12f, HomeW = 64f, HomeEdgeD = 3f, NextEdgeD = 5f;
        const float NextW = ButtonW - HomeW - RowGap;
        const float ButtonsT0 = 3.0f, ButtonsT1 = 3.4f;
        const float SkipBefore = 3.0f;
        const int ConfettiCount = 50;
        const float ConfettiT0 = 2.0f, ConfettiLife = 2.0f, ConfettiFade = 0.4f, ConfettiSpread = 1.3f;
        const float ConfettiSpeed = 500f, ConfettiGravity = 700f;
        const int ConfettiSeed = 7;

        // ---- vertical layout, design px from the safe-area top: normal / compact (MrecSlot.Plan, t = 0 .. 1).
        // The stars shrink (x 0.8) and the gaps tighten; text, card and button sizes never change, the ad never does.
        //   normal : banner 16..70 (incl. edge), stars 105..193, coin card 232..331, row 346..411, MREC top 444
        //   compact: banner 8..62,                stars 82..154, coin card 172..271, row 286..351, MREC top 384
        const float BannerCY = 40f, CBannerCY = 32f;
        const float MidY = 152f, CMidY = 120f, SideY = 160f, CSideY = 127f, CStarScale = 0.8f; // star centres
        const float CoinCardTop = 232f, CCoinCardTop = 172f;
        const float NextTop = 346f, CNextTop = 286f;

        const float End = ConfettiT0 + ConfettiLife + 0.05f;                                // last one-shot thing done

        // ---- colours (UI_SPEC palette via Theme; the rest only exist on this screen)
        static readonly Color RoundEdge = Theme.Hex("#D9D1C1");   // Home button's raised edge (as the ad frame)
        static readonly Color Cheek = new Color(1f, 122f / 255f, 102f / 255f, 0.75f); // #FF7A66 at 75 %
        static readonly Color SparkleColor = Theme.Hex("#FFC82E");
        static readonly Color CoinEdge = Theme.Hex("#D98E00");
        static readonly Color BadgeEdge = Theme.Hex("#C98410");

        RectTransform safe, content, card, banner;
        CanvasGroup bannerGroup, buttonsGroup;
        bool waitingForAd; // a Next / Home tap is waiting for the interstitial
        readonly ArrowGraphic[] sweep = new ArrowGraphic[Ghosts + 1]; // [0] = the arrow, [1..3] = ghosts
        ArrowGraphic bigRing;
        readonly ArrowGraphic[] starRings = new ArrowGraphic[3];
        readonly StarView[] happy = new StarView[3], sleepy = new StarView[3];
        readonly bool[] popped = new bool[3], landed = new bool[3];
        readonly float[] landT = new float[3], popScale = new float[3], popEnd = new float[3];
        readonly Image[] confetti = new Image[ConfettiCount];
        readonly Vector2[] cVel = new Vector2[ConfettiCount];
        readonly float[] cSpin = new float[ConfettiCount];
        readonly List<Vector2> pts = new List<Vector2>(65);
        GameObject skipCatcher;
        MrecSlot mrec; // 300x250 MREC (or its shimmer) under the button row, once the body is in place

        // coin card
        RectTransform coinCard, coinRt, amountRt, noteRt;
        CanvasGroup coinCardGroup;
        Text amountText, noteText;
        GameObject badge;
        Text badgeText;

        RectTransform homeRt, nextRt;

        float midY = MidY, sideY = SideY, bannerCY = BannerCY, starScale = 1f; // current layout (ApplyLayout)
        float screenH, t0; // screenH = safe-area height (canvas units)
        int earned;
        bool confettiOn, skipped, settled;
        Vector2 confettiOrigin;
        Coroutine run;

        sealed class StarView
        {
            public RectTransform root, eyeL, eyeR, z;
            public Text zText;
            public readonly List<RectTransform> sparkles = new List<RectTransform>();
            public readonly List<Image> sparkleG = new List<Image>();
            public float tilt;
            public Vector2 zBase;
        }

        protected override void Build()
        {
            // full screen: cream over the whole screen (blocks taps to the game), content inside the safe area
            var bg = Ui.Img(Root, "Bg", null, Theme.Bg);
            Ui.Fill(bg.rectTransform);
            safe = Ui.New(Root, "Safe");
            Ui.FitSafeArea(safe);
            content = Ui.New(safe, "Content");
            Ui.Fill(content);

            BuildCard();

            // confetti pool above the body
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

            // coral LEVEL COMPLETE! ribbon: stamps at the centre, then rests at the top
            var bImg = Ui.Img(content, "Banner", Theme.Rounded, Theme.Accent, 12f);
            bImg.raycastTarget = false;
            banner = bImg.rectTransform;
            Ui.PlaceD(banner, Ui.TC, Ui.C, Vector2.zero, new Vector2(BannerW, BannerH));
            AddEdge(bImg, Theme.AccentShadow, BannerEdgeD);
            bannerGroup = bImg.gameObject.AddComponent<CanvasGroup>();
            Ui.Fill(Ui.Label(banner, "LEVEL COMPLETE!", BannerFont, Theme.Card, Weight.Bold).rectTransform);

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
            // transparent body over the whole safe area (top = safe-area top); slides up from below the screen
            card = Ui.New(content, "Body");
            card.anchorMin = Vector2.zero;
            card.anchorMax = Vector2.one;
            card.pivot = Ui.TC;
            card.offsetMin = card.offsetMax = Vector2.zero;

            // stars: a sleepy grey and a happy gold one per slot (OnShow shows one of them); left, middle, right
            float[] tilt = { SideTilt, 0f, -SideTilt }; // Unity z (CSS -10 / +10 deg)
            for (int i = 0; i < 3; i++)
            {
                float d = i == 1 ? MidD : SideD;
                sleepy[i] = BuildSleepyStar(card, i, d, tilt[i]);
                happy[i] = BuildHappyStar(card, i, d, tilt[i]);
                starRings[i] = Ring(card, Ui.TC, Vector2.zero);
            }

            BuildCoinCard(card);

            var buttons = Ui.New(card, "Buttons");
            Ui.Fill(buttons);
            buttonsGroup = buttons.gameObject.AddComponent<CanvasGroup>();

            // Home on the left of the row: white, 3 px raised edge, home icon (same press squash, same interstitial rule)
            var home = Ui.Btn(buttons, "", Theme.Card, Theme.Ink, 20f, NextH * 0.5f, RoundEdge, HomeEdgeD,
                              () => AfterAd(GoHome), Icon.Home, 26f, Weight.Semi);
            home.name = "Btn_Home";
            homeRt = (RectTransform)home.transform;
            Ui.PlaceD(homeRt, Ui.TC, new Vector2(-(ButtonW - HomeW) * 0.5f, -NextTop), new Vector2(HomeW, NextH));

            var next = Ui.Btn(buttons, "NEXT LEVEL", Theme.Accent, Theme.Card, 24f, 20f, Theme.AccentShadow, NextEdgeD, OnNextTap);
            nextRt = (RectTransform)next.transform;
            Ui.PlaceD(nextRt, Ui.TC, new Vector2((ButtonW - NextW) * 0.5f, -NextTop), new Vector2(NextW, NextH));
            var arrow = Icons.Make(next.transform, Icon.ArrowRight, Theme.Card, 24f, 3f);
            Ui.Size(arrow, 24f, 24f);
            Ui.AddShimmer(nextRt, 20f, ButtonsT1); // first sweep once the button is in

            // MREC frame under the row, in the safe rect (not the body: the ad is a native view and never slides or
            // shakes); placed per show (OnShow), shown once the body is in place
            mrec = MrecSlot.Create(safe, MrecKind.Win, "PopupWin", PlanMrec, t => ApplyLayout(t, false));
        }

        /// <summary>White coin card: coin + "+N" + a note, centred as one block (SetCoins); a gold xN badge on its top-right corner.</summary>
        void BuildCoinCard(RectTransform parent)
        {
            var img = Ui.Img(parent, "CoinCard", Theme.Rounded, Theme.Card, CoinCardR);
            img.raycastTarget = false;
            coinCard = img.rectTransform;
            Ui.PlaceD(coinCard, Ui.TC, new Vector2(0f, -CoinCardTop), new Vector2(CoinCardW, CoinCardH));
            AddEdge(img, Theme.CardShadow, CoinCardEdgeD);
            coinCardGroup = img.gameObject.AddComponent<CanvasGroup>();

            coinRt = Coin(coinCard, CoinD);
            Ui.PlaceD(coinRt, Ui.C, Ui.C, Vector2.zero, new Vector2(CoinD, CoinD));

            amountText = Ui.Label(coinCard, "+25", AmountFont, Theme.Ink, Weight.Bold, TextAnchor.MiddleLeft);
            amountRt = amountText.rectTransform;
            Ui.PlaceD(amountRt, Ui.C, new Vector2(0f, 0.5f), new Vector2(0f, AmountY), new Vector2(240f, 50f));
            noteText = Ui.Label(coinCard, "coins earned", NoteFont, Theme.InkSoft, Weight.Medium, TextAnchor.MiddleLeft);
            noteRt = noteText.rectTransform;
            Ui.PlaceD(noteRt, Ui.C, new Vector2(0f, 0.5f), new Vector2(0f, NoteY), new Vector2(260f, 22f));

            var b = Ui.Img(coinCard, "Badge", Theme.Rounded, Theme.Gold, BadgeH * 0.5f);
            b.raycastTarget = false;
            Ui.PlaceD(b.rectTransform, Ui.TR, Ui.C, new Vector2(BadgeX, 0f), new Vector2(BadgeW, BadgeH));
            b.rectTransform.localEulerAngles = new Vector3(0f, 0f, BadgeTilt);
            AddEdge(b, BadgeEdge, BadgeEdgeD);
            badgeText = Ui.Label(b.rectTransform, "x10", BadgeFont, Theme.Ink, Weight.Bold);
            Ui.Fill(badgeText.rectTransform);
            badge = b.gameObject;
        }

        /// <summary>
        /// What this win paid: BonusMult 0 = "+25 coins earned"; N = "+(25 x N) coins earned with bonus spin" and the xN
        /// badge. Coin + text are centred as one block on the card (measured from the texts' preferred widths).
        /// </summary>
        void SetCoins(int mult)
        {
            bool bonus = mult > 0;
            amountText.text = "+" + Save.WinCoins * (bonus ? mult : 1);
            noteText.text = bonus ? "coins earned with bonus spin" : "coins earned";
            badge.SetActive(bonus);
            badgeText.text = "x" + mult;

            float textW = Mathf.Max(amountText.preferredWidth, noteText.preferredWidth) / Theme.K;
            float left = -(CoinD + CoinGap + textW) * 0.5f;
            coinRt.anchoredPosition = new Vector2(Theme.D(left + CoinD * 0.5f), 0f);
            float textX = Theme.D(left + CoinD + CoinGap);
            amountRt.anchoredPosition = new Vector2(textX, Theme.D(AmountY));
            noteRt.anchoredPosition = new Vector2(textX, Theme.D(NoteY));
        }

        // ------------------------------------------------------------------ stars

        /// <summary>
        /// Gold rounded star (WinArt.StarGold: back layer, body, lighter inner star, highlight arc) with the face:
        /// eyes 8.5 % of the size with a white shine (they blink), pink cheeks, an open smile 17 % wide with a tongue,
        /// and three twinkling 4-point sparkles around it.
        /// </summary>
        StarView BuildHappyStar(RectTransform parent, int i, float s, float tilt)
        {
            var v = StarRoot(parent, "Happy" + i, s, tilt, WinArt.StarGold);

            float eye = 0.085f * s;
            v.eyeL = Eye(v.root, "EyeL", new Vector2(-0.12f * s, 0.03f * s), eye);
            v.eyeR = Eye(v.root, "EyeR", new Vector2(0.12f * s, 0.03f * s), eye);
            Dot(v.root, "CheekL", Cheek, new Vector2(-0.245f * s, -0.075f * s), new Vector2(0.13f * s, 0.08f * s));
            Dot(v.root, "CheekR", Cheek, new Vector2(0.245f * s, -0.075f * s), new Vector2(0.13f * s, 0.08f * s));
            var smile = Ui.Img(v.root, "Smile", WinArt.Smile, Color.white);       // colours baked (dark + pink tongue)
            smile.raycastTarget = false;
            Ui.PlaceD(smile.rectTransform, Ui.C, Ui.C, new Vector2(0f, -0.085f * s), new Vector2(0.17f * s, 0.085f * s));

            // sparkles around the star: (x, y, size) relative to the star size
            float[,] sp = { { -0.62f, 0.46f, 0.22f }, { 0.63f, 0.5f, 0.17f }, { 0.7f, -0.24f, 0.18f } };
            for (int k = 0; k < sp.GetLength(0); k++)
            {
                var img = Ui.Img(v.root, "Sparkle" + k, WinArt.Sparkle, SparkleColor);
                img.raycastTarget = false;
                Ui.PlaceD(img.rectTransform, Ui.C, Ui.C, new Vector2(sp[k, 0] * s, sp[k, 1] * s), new Vector2(sp[k, 2] * s, sp[k, 2] * s));
                v.sparkles.Add(img.rectTransform);
                v.sparkleG.Add(img);
            }
            v.root.gameObject.SetActive(false);
            return v;
        }

        /// <summary>Grey rounded star (WinArt.StarGrey: back layer + body), closed eyes, a flat mouth and a floating "z".</summary>
        StarView BuildSleepyStar(RectTransform parent, int i, float s, float tilt)
        {
            var v = StarRoot(parent, "Sleepy" + i, s, tilt, WinArt.StarGrey);

            for (int k = -1; k <= 1; k += 2)
            {
                var eye = Ui.Img(v.root, k < 0 ? "EyeL" : "EyeR", WinArt.SleepyEye, Theme.InkSoft);
                eye.raycastTarget = false;
                Ui.PlaceD(eye.rectTransform, Ui.C, Ui.C, new Vector2(k * 0.12f * s, 0.01f * s), new Vector2(0.13f * s, 0.065f * s));
            }
            float th = Mathf.Max(2.5f, 0.035f * s);
            var mouth = Ui.Img(v.root, "Mouth", Theme.Rounded, Theme.InkSoft, th * 0.5f);
            mouth.raycastTarget = false;
            Ui.PlaceD(mouth.rectTransform, Ui.C, Ui.C, new Vector2(0f, -0.12f * s), new Vector2(0.14f * s, th));

            v.zText = Ui.Label(v.root, "z", Mathf.Max(13f, 0.3f * s), Theme.InkSoft, Weight.Bold);
            v.z = v.zText.rectTransform;
            v.zBase = new Vector2(0.46f * s, 0.42f * s);
            Ui.PlaceD(v.z, Ui.C, Ui.C, v.zBase, new Vector2(0.4f * s, 0.4f * s));
            v.root.gameObject.SetActive(false);
            return v;
        }

        /// <summary>Star root at the slot (ApplyLayout sets x / y; origin = the star's centre) with the generated body.</summary>
        static StarView StarRoot(RectTransform parent, string name, float s, float tilt, Sprite body)
        {
            var v = new StarView { tilt = tilt };
            v.root = Ui.New(parent, name);
            Ui.PlaceD(v.root, Ui.TC, Ui.C, Vector2.zero, new Vector2(s, s));
            v.root.localEulerAngles = new Vector3(0f, 0f, tilt);
            var img = Ui.Img(v.root, "Body", body, Color.white);
            img.raycastTarget = false;
            Ui.PlaceD(img.rectTransform, Ui.C, Ui.C, new Vector2(0f, WinArt.StarBoxCY * s), new Vector2(WinArt.StarBox * s, WinArt.StarBox * s));
            return v;
        }

        /// <summary>Dark eye with a small white shine (a child, so a blink squashes both).</summary>
        static RectTransform Eye(RectTransform parent, string name, Vector2 posD, float d)
        {
            var eye = Dot(parent, name, Theme.Ink, posD, new Vector2(d, d * 1.15f));
            Dot(eye, "Shine", Color.white, new Vector2(0.2f * d, 0.25f * d), new Vector2(0.38f * d, 0.38f * d));
            return eye;
        }

        static RectTransform Dot(RectTransform parent, string name, Color c, Vector2 posD, Vector2 sizeD)
        {
            var img = Ui.Img(parent, name, WinArt.Disc, c);
            img.raycastTarget = false;
            Ui.PlaceD(img.rectTransform, Ui.C, Ui.C, posD, sizeD);
            return img.rectTransform;
        }

        /// <summary>Gold coin: darker rim, gold face slightly up, a small white shine.</summary>
        static RectTransform Coin(Transform parent, float d)
        {
            var rim = Ui.Img(parent, "Coin", WinArt.Disc, CoinEdge);
            rim.raycastTarget = false;
            var rt = rim.rectTransform;
            rt.sizeDelta = new Vector2(Theme.D(d), Theme.D(d));
            Dot(rt, "Face", Theme.Gold, new Vector2(0f, 0.04f * d), new Vector2(0.76f * d, 0.76f * d));
            Dot(rt, "Shine", new Color(1f, 1f, 1f, 0.55f), new Vector2(-0.16f * d, 0.18f * d), new Vector2(0.2f * d, 0.2f * d));
            return rt;
        }

        // ------------------------------------------------------------------ layout

        /// <summary>
        /// Lays the content above the ad out at compact amount t (0 = normal, 1 = compact: stars x 0.8, tighter gaps).
        /// Positions and the star scale only; text, card and button sizes never change.
        /// </summary>
        void ApplyLayout(float t, bool log = true)
        {
            bannerCY = Mathf.Lerp(BannerCY, CBannerCY, t);
            midY = Mathf.Lerp(MidY, CMidY, t);
            sideY = Mathf.Lerp(SideY, CSideY, t);
            starScale = Mathf.Lerp(1f, CStarScale, t);
            for (int i = 0; i < 3; i++)
            {
                var pos = new Vector2(Theme.D((i - 1) * SideX * starScale), -Theme.D(i == 1 ? midY : sideY));
                happy[i].root.anchoredPosition = pos;
                sleepy[i].root.anchoredPosition = pos;
                sleepy[i].root.localScale = new Vector3(starScale, starScale, 1f);
                starRings[i].rectTransform.anchoredPosition = pos;
            }
            SetY(coinCard, Mathf.Lerp(CoinCardTop, CCoinCardTop, t));
            float next = Mathf.Lerp(NextTop, CNextTop, t);
            SetY(homeRt, next);
            SetY(nextRt, next);
            if (log && t > 0f)
                Debug.Log("[ArrowPuzzle] PopupWin compact layout t " + t.ToString("0.##") + ": stars x" + starScale.ToString("0.00") +
                          ", row bottom " + (next + NextH + NextEdgeD).ToString("0"));
        }

        /// <summary>
        /// Where the MREC goes on this phone: its top at MrecFit.WinAdTopD (444), moving up with the button row as the
        /// layout compacts (row 346 -> 286), at the smallest compact amount that keeps the whole frame on screen.
        /// </summary>
        MrecFit.Result PlanMrec()
        {
            return MrecFit.UnderRow(Ads.MrecAvailableDesign, Ads.ScreenWidthDp, MrecFit.WinAdTopD, MrecFit.WinAdTopD - (NextTop - CNextTop));
        }

        /// <summary>Moves a rect to y design px below the body top (its anchor is the body's top centre).</summary>
        static void SetY(RectTransform rt, float yD)
        {
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -Theme.D(yD));
        }

        static void AddEdge(Graphic g, Color c, float d)
        {
            var edge = g.gameObject.AddComponent<Shadow>();
            edge.effectColor = c;
            edge.effectDistance = new Vector2(0f, -Theme.D(d));
            edge.useGraphicAlpha = false;
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
            MusicPlayer.Duck(MusicPlayer.WinDuck);
            Ads.Banner(false, "PopupWin"); // back in OnHide (every route)
            Ui.FitSafeArea(safe);
            Canvas.ForceUpdateCanvases();
            screenH = safe.rect.height;
            // medium first: normal layout, or the compact step at which the medium ad fits under the button row
            ApplyLayout(mrec.Plan());
            earned = Mathf.Clamp(GameApp.I.ResultLives, 1, 3); // the stars ScreenGame.OnWon saved
            AnalyticsService.LevelComplete(GameApp.I.CurrentLevel, GameApp.I.CurrentMode.ToString(), earned,
                                           GameApp.I.ResultTime, GameApp.I.ResultNewBest);
            SetCoins(GameApp.I.BonusMult); // set by PopupBonusSpin: 0 = No thanks

            for (int i = 0; i < 3; i++)
            {
                popped[i] = landed[i] = false;
                landT[i] = -100f;
                popScale[i] = 0f;
                popEnd[i] = PopT0 + i * PopEvery + PopTime;
                happy[i].root.gameObject.SetActive(false);
                sleepy[i].root.gameObject.SetActive(i >= earned); // earned stars fill left, middle, right
            }

            confettiOn = false;
            skipped = false;
            settled = false;
            waitingForAd = false;
            SeedConfetti();

            t0 = Time.unscaledTime;
            Apply(0f);
            ApplyLife(0f);
            if (run != null) GameApp.I.StopCoroutine(run);
            run = GameApp.I.StartCoroutine(Run());
        }

        public override void OnHide()
        {
            MusicPlayer.Duck(1f);
            if (run != null) GameApp.I.StopCoroutine(run);
            run = null;
            content.anchoredPosition = Vector2.zero;
            foreach (var c in confetti) c.gameObject.SetActive(false);
            mrec.End();
            var game = GameApp.I.Get<ScreenGame>();
            if (game != null) game.RestoreBanner("PopupWin");
            else Ads.Banner(true, "PopupWin");
        }

        /// <summary>Closing with back would leave the finished (locked) board, so back goes Home (after the ad, like the button).</summary>
        public override bool OnBack()
        {
            AfterAd(GoHome);
            return true;
        }

        void OnNextTap()
        {
            if (waitingForAd) return;
            AnalyticsService.WinNextTap(GameApp.I.CurrentLevel, false); // no NEXT delay any more
            AfterAd(NextLevel);
        }

        /// <summary>
        /// Interstitial, then the action. The buttons stay locked from the tap until the ad is done. No interstitial
        /// after the very first level a player ever completes, nor once right after the bonus wheel's rewarded ad.
        /// </summary>
        void AfterAd(System.Action action)
        {
            if (waitingForAd) return;
            waitingForAd = true;
            LockButtons();
            mrec.End(); // Next / Home / Back: the MREC goes before the interstitial
            if (GameApp.I.SkipInterstitialOnce)
            {
                GameApp.I.SkipInterstitialOnce = false;
                Debug.Log("[Ads] interstitial skipped: the bonus wheel's rewarded ad was just watched");
                Done(action);
                return;
            }
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

        /// <summary>
        /// NEXT (after the interstitial, AfterAd): starts the next global level straight away. After level 150 (or if
        /// the next level is somehow still locked) it goes back to Home.
        /// </summary>
        static void NextLevel()
        {
            int next = GameApp.I.CurrentLevel + 1;
            GameApp.I.Close("PopupWin");
            var game = GameApp.I.Get<ScreenGame>();
            if (next > LevelPath.Count || !Save.IsUnlocked(next) || game == null)
            {
                GameApp.I.Show("ScreenHome");
                return;
            }
            GameApp.I.CurrentLevel = next;
            game.StartLevel(next); // ScreenGame is already visible, so Show would not reload it
        }

        /// <summary>Jump to the final state: stars in place, no landing rings or confetti, buttons visible.</summary>
        void Skip()
        {
            if (skipped || Time.unscaledTime - t0 >= SkipBefore) return;
            skipped = true;
            for (int i = 0; i < 3; i++)
            {
                popped[i] = landed[i] = true;
                landT[i] = -100f;
                popEnd[i] = ButtonsT1; // the wiggle eases in from now
            }
            confettiOn = false;
            foreach (var c in confetti) c.gameObject.SetActive(false);
            t0 = Time.unscaledTime - ButtonsT1;
            Apply(ButtonsT1);
        }

        IEnumerator Run()
        {
            while (true) // until OnHide: the stars keep their idle life
            {
                float t = Time.unscaledTime - t0;
                if (t <= End) Apply(t);
                else if (!settled) { Apply(End); settled = true; }
                ApplyLife(t);
                ApplyButtons(t);
                yield return null;
            }
        }

        // ------------------------------------------------------------------ the timeline

        void Apply(float t)
        {
            skipCatcher.SetActive(!skipped && t < SkipBefore);

            ApplySweep(t);
            ApplyBanner(t);
            card.anchoredPosition = new Vector2(0f, -BodyOffsetAt(t));
            ApplyStars(t);
            ApplyConfetti(t);
            ApplyCoinCard(t);
            if (t >= CardT1) mrec.Begin(); // body in place; once per show (skip jumps past CardT1 too)

            // shakes: banner impact + a tiny one per popped star
            float dx = 0f;
            if (t >= ShakeT0 && t < ShakeT1)
                dx += Mathf.Sin((t - ShakeT0) * ShakeFreq) * ShakeAmp * (1f - (t - ShakeT0) / (ShakeT1 - ShakeT0));
            for (int i = 0; i < 3; i++)
            {
                float s = t - landT[i];
                if (s >= 0f && s < LandShakeTime) dx += Mathf.Sin(s * ShakeFreq) * LandShake * (1f - s / LandShakeTime);
            }
            content.anchoredPosition = new Vector2(Theme.D(dx), 0f);
            ApplyButtons(t);
        }

        /// <summary>The buttons fade in with the opening and are tappable once visible, except while the interstitial runs.</summary>
        void ApplyButtons(float t)
        {
            float b = Mathf.Clamp01((t - ButtonsT0) / (ButtonsT1 - ButtonsT0));
            buttonsGroup.alpha = b;
            buttonsGroup.interactable = buttonsGroup.blocksRaycasts = b >= 0.5f && !waitingForAd;
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

        /// <summary>Stamps at the screen centre (big, tilted), then moves up to its place at the top.</summary>
        void ApplyBanner(float t)
        {
            float stamp = Mathf.Clamp01((t - StampT0) / (StampT1 - StampT0));
            float move = EaseInOutCubic(Mathf.Clamp01((t - MoveT0) / (MoveT1 - MoveT0)));

            bannerGroup.alpha = stamp;
            float scale = Mathf.LerpUnclamped(StampScale, 1f, EaseOutBack(stamp, StampBack)) * Mathf.Lerp(BannerBig, 1f, move);
            banner.localScale = Vector3.one * scale;
            banner.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(BannerTilt, 0f, move));
            banner.anchoredPosition = new Vector2(0f, -Mathf.Lerp(screenH * BannerYFrac, Theme.D(bannerCY), move));

            // gold ring from the banner centre (still at its stamp position)
            DrawRing(bigRing, (t - RingT0) / (RingT1 - RingT0), RingR0, RingR1, RingW0, RingW1);
            bigRing.rectTransform.anchoredPosition = new Vector2(0f, -screenH * BannerYFrac);
        }

        /// <summary>The coin card pops in (fade + a small scale overshoot) while the last stars land.</summary>
        void ApplyCoinCard(float t)
        {
            float k = Mathf.Clamp01((t - CoinCardT0) / (CoinCardT1 - CoinCardT0));
            coinCardGroup.alpha = k;
            coinCard.localScale = Vector3.one * Mathf.LerpUnclamped(0.85f, 1f, EaseOutBack(k, 1.8f));
        }

        /// <summary>How far the body is below its rest place (canvas units): slides up from below the screen.</summary>
        float BodyOffsetAt(float t)
        {
            float k = EaseOutCubic(Mathf.Clamp01((t - CardT0) / (CardT1 - CardT0)));
            return Mathf.Lerp(screenH + Theme.D(20f), 0f, k);
        }

        /// <summary>Earned stars pop in one by one: scale 0 -> 1.28 -> 1 (sound at the start, gold ring + shake at the peak).</summary>
        void ApplyStars(float t)
        {
            for (int i = 0; i < 3; i++)
            {
                if (i >= earned) { DrawRing(starRings[i], -1f, 0f, 0f, 0f, 0f); continue; }
                float s = PopT0 + i * PopEvery;
                float x = skipped ? 1f : (t - s) / PopTime;
                bool on = popped[i] || x >= 0f;
                if (happy[i].root.gameObject.activeSelf != on) happy[i].root.gameObject.SetActive(on);
                if (!on) continue;

                x = Mathf.Clamp01(x);
                popScale[i] = x < PopPeakAt
                    ? PopPeak * EaseOutCubic(x / PopPeakAt)
                    : Mathf.Lerp(PopPeak, 1f, EaseInOutCubic((x - PopPeakAt) / (1f - PopPeakAt)));
                if (!popped[i])
                {
                    popped[i] = true;
                    Sfx.Play(Sfx.Id.Star);
                }
                if (!landed[i] && x >= PopPeakAt)
                {
                    landed[i] = true;
                    landT[i] = s + PopPeakAt * PopTime;
                }
                DrawRing(starRings[i], (t - landT[i]) / StarRingTime, StarRingR0, StarRingR1, StarRingW0, StarRingW1);
            }
        }

        /// <summary>Every frame: popped stars wiggle, blink and twinkle; sleepy stars float their "z".</summary>
        void ApplyLife(float t)
        {
            for (int i = 0; i < 3; i++)
            {
                if (i < earned)
                {
                    var v = happy[i];
                    if (!v.root.gameObject.activeSelf) continue;
                    float w = Mathf.Clamp01((t - popEnd[i]) / WiggleRamp); // 0 while popping
                    float rot = v.tilt + w * WiggleDeg * Mathf.Sin(2.3f * t + i * 1.7f);
                    float sc = starScale * popScale[i] * (1f + w * WiggleScale * Mathf.Sin(3.3f * t + i));
                    v.root.localEulerAngles = new Vector3(0f, 0f, rot);
                    v.root.localScale = new Vector3(sc, sc, 1f);

                    bool blink = Mathf.Repeat(t + i * 0.9f, BlinkEvery) < BlinkTime;
                    var eye = new Vector3(1f, blink ? 0.15f : 1f, 1f);
                    v.eyeL.localScale = eye;
                    v.eyeR.localScale = eye;

                    for (int k = 0; k < v.sparkles.Count; k++)
                    {
                        float a = 0.5f + 0.5f * Mathf.Sin(4f * t + k * 2.1f + i * 1.3f);
                        v.sparkles[k].localScale = Vector3.one * (0.35f + 0.65f * a);
                        v.sparkleG[k].color = WithAlpha(SparkleColor, a);
                    }
                }
                else
                {
                    var v = sleepy[i];
                    float u = Mathf.Repeat(t + i * 0.8f, ZLoop) / ZLoop;
                    v.z.anchoredPosition = new Vector2(Theme.D(v.zBase.x + 3f * u), Theme.D(v.zBase.y + ZRise * u));
                    v.zText.color = WithAlpha(Theme.InkSoft, Mathf.Sin(Mathf.PI * u));
                }
            }
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
                // top of the stars row when they fire
                confettiOrigin = new Vector2(0f, -(BodyOffsetAt(ConfettiT0) + Theme.D(midY - MidD * 0.5f * starScale)));
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
    }
}
