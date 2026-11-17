using System;
using UnityEngine;
using UnityEngine.UI;

namespace ArrowPuzzle
{
    /// <summary>
    /// Home: cream background with dots and faint drifting arrows (like the splash). Top bar: settings (left), coin pill
    /// (right). The splash's wordmark in its end state at the top third. A five-slot level ruler: the selected level big
    /// and coral with a pulsing ring, two levels left, two right (locked ones beige with a lock), prev / next buttons at
    /// the edges. PLAY ("LEVEL N" over "PLAY", breathing) and the "SKIPS TODAY" row sit above the banner. No layout
    /// groups: every position is fixed in design px (y from the top of the safe area) and button rects are snapped to
    /// whole pixels. Unscaled time.
    /// </summary>
    public class ScreenHome : AppScreen
    {
        // ---- top bar
        const float Side = 20f, TopY = 16f;
        const float SettingsD = 52f, SettingsR = 15f, GearD = 26f;
        const float PillH = 40f, CoinD = 24f, PillPadL = 8f, PillGap = 8f, PillPadR = 16f, CoinFont = 20f;

        // ---- wordmark: shaft line at the top third, clamped
        const float MarkFrac = 0.26f, MarkMinY = 150f, MarkMaxY = 220f, MarkInkBelow = 56f;

        // ---- ruler (x from the centre)
        const float BigD = 78f, SmallD = 40f, NearX = 68f, FarX = 115f, TrackH = 6f;
        const float BigFont = 34f, SmallFont = 16f, LockD = 16f, SmallEdge = 3f;
        const float StepD = 40f, StepR = 12f, StepX = 163f, StepIconD = 22f, StepDisabledAlpha = 0.35f;
        const float RingPeriod = 1.4f, RingGrow = 0.4f, RingAlpha = 0.35f;
        const float PopTime = 0.2f, PopScale = 1.12f;

        // ---- PLAY: 280 x 100 face + 8 px edge
        const float PlayW = 280f, PlayH = 100f, PlayEdge = 8f, PlayR = 26f;
        const float LevelFont = 17f, LevelY = 25f, PlayFont = 46f, PlayY = -12f; // label centres, y up from the face centre
        const float BreathPeriod = 1.8f, BreathAmp = 0.03f;

        // ---- SKIPS TODAY row
        const float SkipsH = 40f, SkipsPad = 18f, SkipsTextGap = 12f, SkipDotD = 12f, SkipDotGap = 8f, SkipsFont = 14f;
        const int MaxSkipDots = 8;

        // ---- vertical spacing
        const float BottomPad = 16f, BannerGap = 8f, PlayToSkips = 24f;

        static readonly Color SkipUsed = Theme.InkSoft;
        static readonly Color CoinEdge = Theme.Hex("#D98E00");
        static readonly Vector3[] corners = new Vector3[4];
        static readonly float[] SlotX = { -FarX, -NearX, 0f, NearX, FarX };

        class Slot
        {
            public RectTransform rt;
            public Image face;
            public Text number;
            public GameObject lockIcon;
            public int level;
        }

        RectTransform backdrop, settings, coinPill, ruler, rulerTrack, ring, prev, next, playRoot, skipsRow;
        Wordmark mark;
        Text coins, levelLabel, skipsText;
        Image ringImg;
        CanvasGroup prevGroup, nextGroup;
        readonly Slot[] slots = new Slot[5];
        readonly Image[] skipDots = new Image[MaxSkipDots];

        int selected = 1, unlocked = 1;
        float popT = -1f;
        float laidOutH = -1f, laidOutUnder = -1f, laidOutScale = -1f;
        Rect laidOutSafe;

        protected override void Build()
        {
            BuildBackdrop();
            BannerPlaceholder.Create(Root); // just above the background, under everything else
            BuildTopBar();
            mark = Wordmark.Build(Root); // end state, identical to the splash's last frame
            BuildRuler();
            BuildPlay();
            BuildSkips();

            Root.gameObject.AddComponent<HomeTicker>().tick = Tick;
            BannerDebug.Create(Root, "ScreenHome"); // only when Ads.ShowBannerDebug
            Refresh();
        }

        // ------------------------------------------------------------------ build

        /// <summary>Cream, dots and drifting arrows over the whole canvas (Layout matches it to the canvas), so the dot
        /// grid lines up with the splash's.</summary>
        void BuildBackdrop()
        {
            backdrop = Ui.New(Root, "Backdrop");
            var bg = Ui.Img(backdrop, "Bg", null, Theme.Bg);
            bg.raycastTarget = false;
            Ui.Fill(bg.rectTransform);

            var dots = Ui.Img(backdrop, "Dots", Theme.DotTile, Theme.CardShadow);
            dots.type = Image.Type.Tiled;
            dots.pixelsPerUnitMultiplier = 64f / (34f * Theme.K);
            dots.raycastTarget = false;
            Ui.Fill(dots.rectTransform);

            DriftingArrows.Add(backdrop, 11);
        }

        void BuildTopBar()
        {
            // settings: ink rounded square, white gear
            var sImg = Rounded(Root, "Settings", Theme.Ink, SettingsR);
            settings = sImg.rectTransform;
            MakeButton(sImg, () => GameApp.I.Show("ScreenSettings"));
            var gear = Icons.Make(settings, Icon.Gear, Theme.Card, GearD);
            Ui.PlaceD(gear, Ui.C, Vector2.zero, new Vector2(GearD, GearD));

            // coin pill: ink, gold coin, count (width follows the count, see SetCoins)
            var pill = Rounded(Root, "Coins", Theme.Ink, PillH * 0.5f);
            pill.raycastTarget = false;
            coinPill = pill.rectTransform;
            var coin = Coin(coinPill, CoinD);
            Ui.PlaceD(coin, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(PillPadL, 0f), new Vector2(CoinD, CoinD));
            coins = Ui.Label(coinPill, "0", CoinFont, Theme.Card, Weight.Bold, TextAnchor.MiddleLeft);
            Ui.PlaceD(coins.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(PillPadL + CoinD + PillGap, 0f), new Vector2(120f, PillH));
        }

        void BuildRuler()
        {
            ruler = Ui.New(Root, "Ruler");

            var trackImg = Rounded(ruler, "Track", Theme.RulerTrack, TrackH * 0.5f);
            trackImg.raycastTarget = false;
            rulerTrack = trackImg.rectTransform;

            // pulsing ring behind the selected circle
            ringImg = Ui.Img(ruler, "Ring", Theme.CircleHD, Theme.Accent);
            ringImg.raycastTarget = false;
            ring = ringImg.rectTransform;
            Ui.PlaceD(ring, Ui.C, Vector2.zero, new Vector2(BigD, BigD));

            for (int i = 0; i < slots.Length; i++)
            {
                bool big = i == 2;
                float d = big ? BigD : SmallD;
                var s = new Slot();
                s.face = Ui.Img(ruler, "Level_" + (i - 2), Theme.CircleHD, big ? Theme.Accent : Theme.Card);
                s.rt = s.face.rectTransform;
                Ui.PlaceD(s.rt, Ui.C, new Vector2(SlotX[i], 0f), new Vector2(d, d));
                if (!big)
                {
                    var edge = s.face.gameObject.AddComponent<Shadow>();
                    edge.effectColor = Theme.ListEdge;
                    edge.effectDistance = new Vector2(0f, -Theme.D(SmallEdge));
                    edge.useGraphicAlpha = false;
                }
                s.number = Ui.Label(s.rt, "", big ? BigFont : SmallFont, big ? Theme.Card : Theme.Ink, Weight.Bold);
                Ui.PlaceD(s.number.rectTransform, Ui.C, Vector2.zero, new Vector2(d, d));
                var lockRt = Icons.Make(s.rt, Icon.Lock, Theme.LockedLevelIcon, LockD);
                Ui.PlaceD(lockRt, Ui.C, Vector2.zero, new Vector2(LockD, LockD));
                s.lockIcon = lockRt.gameObject;

                var btn = s.face.gameObject.AddComponent<Button>();
                btn.targetGraphic = s.face;
                btn.transition = Selectable.Transition.None;
                var slot = s;
                btn.onClick.AddListener(() =>
                {
                    if (slot.level < 1 || slot.level > unlocked || slot.level == selected) return;
                    Sfx.Play(Sfx.Id.Click);
                    Pick(slot.level);
                });
                slots[i] = s;
            }

            prev = StepButton("Prev", Icon.Back, -1, out prevGroup);
            next = StepButton("Next", Icon.Chevron, 1, out nextGroup);
        }

        RectTransform StepButton(string name, Icon icon, int dir, out CanvasGroup group)
        {
            var img = Rounded(ruler, name, Theme.Card, StepR);
            var edge = img.gameObject.AddComponent<Shadow>();
            edge.effectColor = Theme.ListEdge;
            edge.effectDistance = new Vector2(0f, -Theme.D(SmallEdge));
            edge.useGraphicAlpha = false;
            MakeButton(img, () => Step(dir));
            var ic = Icons.Make(img.rectTransform, icon, Theme.Ink, StepIconD);
            Ui.PlaceD(ic, Ui.C, Vector2.zero, new Vector2(StepIconD, StepIconD));
            Ui.PlaceD(img.rectTransform, Ui.C, new Vector2(dir * StepX, 0f), new Vector2(StepD, StepD));
            group = img.gameObject.AddComponent<CanvasGroup>();
            return img.rectTransform;
        }

        void BuildPlay()
        {
            // playRoot (snapped, breathes) > press (PressScale, Button) > edge + face + labels
            playRoot = Ui.New(Root, "Play");
            var press = Ui.New(playRoot, "Press");
            Ui.Fill(press);

            var edge = Rounded(press, "Edge", Theme.AccentShadow, PlayR);
            edge.raycastTarget = false;
            Ui.PlaceD(edge.rectTransform, Ui.BC, Vector2.zero, new Vector2(PlayW, PlayH));

            var face = Rounded(press, "Face", Theme.Accent, PlayR);
            Ui.PlaceD(face.rectTransform, Ui.TC, Vector2.zero, new Vector2(PlayW, PlayH));

            levelLabel = Ui.Label(face.rectTransform, "LEVEL 1", LevelFont, Theme.Card, Weight.Semi);
            Ui.PlaceD(levelLabel.rectTransform, Ui.C, new Vector2(0f, LevelY), new Vector2(PlayW, LevelFont * 1.4f));
            var play = Ui.Label(face.rectTransform, "PLAY", PlayFont, Theme.Card, Weight.Bold);
            Ui.PlaceD(play.rectTransform, Ui.C, new Vector2(0f, PlayY), new Vector2(PlayW, PlayFont * 1.3f));

            var btn = press.gameObject.AddComponent<Button>();
            btn.targetGraphic = face;
            btn.transition = Selectable.Transition.None;
            press.gameObject.AddComponent<PressScale>();
            btn.onClick.AddListener(() =>
            {
                Sfx.Play(Sfx.Id.Click);
                Play();
            });
        }

        void BuildSkips()
        {
            var row = Rounded(Root, "SkipsToday", Theme.Ink, SkipsH * 0.5f);
            row.raycastTarget = false;
            skipsRow = row.rectTransform;

            skipsText = Ui.Label(skipsRow, "SKIPS TODAY", SkipsFont, Theme.Card, Weight.Semi, TextAnchor.MiddleLeft);
            Ui.PlaceD(skipsText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(SkipsPad, 0f), new Vector2(140f, SkipsH));

            for (int i = 0; i < MaxSkipDots; i++)
            {
                var dot = Ui.Img(skipsRow, "Dot" + i, Theme.CircleHD, Theme.Teal);
                dot.raycastTarget = false;
                skipDots[i] = dot;
            }
        }

        /// <summary>RoundedHD image with a radiusD design px corner (sharp at any size).</summary>
        static Image Rounded(Transform parent, string name, Color color, float radiusD)
        {
            var img = Ui.Img(parent, name, Theme.RoundedHD, color);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = Theme.RoundedHDRadius / Theme.D(radiusD);
            return img;
        }

        static void MakeButton(Image img, Action onClick)
        {
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            img.gameObject.AddComponent<PressScale>();
            btn.onClick.AddListener(() =>
            {
                Sfx.Play(Sfx.Id.Click);
                onClick();
            });
        }

        /// <summary>Gold coin (like the Level complete card): amber rim, gold face, small shine.</summary>
        static RectTransform Coin(Transform parent, float d)
        {
            var rim = Ui.Img(parent, "Coin", Theme.CircleHD, CoinEdge);
            rim.raycastTarget = false;
            var rt = rim.rectTransform;
            Dot(rt, "Face", Theme.Gold, new Vector2(0f, 0.04f * d), 0.76f * d);
            Dot(rt, "Shine", new Color(1f, 1f, 1f, 0.55f), new Vector2(-0.16f * d, 0.18f * d), 0.2f * d);
            return rt;
        }

        static void Dot(RectTransform parent, string name, Color c, Vector2 posD, float d)
        {
            var img = Ui.Img(parent, name, Theme.CircleHD, c);
            img.raycastTarget = false;
            Ui.PlaceD(img.rectTransform, Ui.C, posD, new Vector2(d, d));
        }

        // ------------------------------------------------------------------ show / refresh

        public override void OnShow()
        {
            laidOutH = -1f;
            Ads.BannerHeightChanged -= OnBannerHeight; // never twice
            Ads.BannerHeightChanged += OnBannerHeight;
            Ads.Banner(true, "ScreenHome"); // popups on top keep it; only leaving Home hides it
            Save.CoinsChanged -= SetCoins;
            Save.CoinsChanged += SetCoins;
            Save.SkipsChanged -= SetSkips;
            Save.SkipsChanged += SetSkips;
            MusicPlayer.PlayHome();
            mark.Apply(Wordmark.End);
            Refresh();
            Layout();
            Debug.Log("[Coins] Home shows " + coins.text + " (saved " + Save.Coins + ")");
        }

        public override void OnHide()
        {
            Ads.BannerHeightChanged -= OnBannerHeight;
            Save.CoinsChanged -= SetCoins;
            Save.SkipsChanged -= SetSkips;
            Ads.Banner(false, "ScreenHome"); // ignored when the next screen (game) asks for it in the same frame
            popT = -1f;
            slots[2].rt.localScale = Vector3.one;
        }

        /// <summary>Coins, skips, and the ruler back on the highest unlocked level.</summary>
        void Refresh()
        {
            unlocked = Save.IsUnlocked(LevelPath.Count) ? LevelPath.Count : Mathf.Clamp(Save.UnlockedGlobal, 1, LevelPath.Count);
            popT = -1f;
            slots[2].rt.localScale = Vector3.one;
            SetSelected(Save.UnlockedGlobal);
            SetCoins();
            SetSkips();
        }

        void SetCoins()
        {
            coins.text = Save.Coins.ToString();
            float w = PillPadL + CoinD + PillGap + coins.preferredWidth / Theme.K + PillPadR;
            Ui.PlaceD(coinPill, Ui.TR, new Vector2(-Side, -(TopY + (SettingsD - PillH) * 0.5f)), new Vector2(Mathf.Ceil(w), PillH));
            Snap(coinPill);
        }

        /// <summary>One dot per skip of today (4, more with a bonus): teal = left, grey = used. The row fits its content.</summary>
        void SetSkips()
        {
            int left = Save.SkipsLeft;
            int total = Mathf.Clamp(Save.SkipsPerDay + Save.SkipsBonusToday, 1, MaxSkipDots);
            left = Mathf.Min(left, total);
            float x = SkipsPad + skipsText.preferredWidth / Theme.K + SkipsTextGap;
            for (int i = 0; i < MaxSkipDots; i++)
            {
                var dot = skipDots[i];
                bool on = i < total;
                dot.gameObject.SetActive(on);
                if (!on) continue;
                dot.color = i < left ? Theme.Teal : SkipUsed;
                Ui.PlaceD(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(SkipDotD, SkipDotD));
                x += SkipDotD + SkipDotGap;
            }
            float w = Mathf.Ceil(x - SkipDotGap + SkipsPad);
            skipsRow.sizeDelta = new Vector2(Theme.D(w), Theme.D(SkipsH));
            Snap(skipsRow);
        }

        /// <summary>Kept within the unlocked levels; updates the ruler slots, prev / next and the PLAY label.</summary>
        void SetSelected(int level)
        {
            selected = Mathf.Clamp(level, 1, unlocked);
            levelLabel.text = "LEVEL " + selected;

            float minX = 0f, maxX = 0f;
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                int l = selected + i - 2;
                bool on = l >= 1 && l <= LevelPath.Count;
                s.rt.gameObject.SetActive(on);
                s.level = on ? l : 0;
                if (!on) continue;
                minX = Mathf.Min(minX, SlotX[i]);
                maxX = Mathf.Max(maxX, SlotX[i]);
                bool locked = l > unlocked;
                if (i != 2) s.face.color = locked ? Theme.Locked : Theme.Card;
                s.number.text = l.ToString();
                s.number.gameObject.SetActive(!locked);
                s.lockIcon.SetActive(locked);
            }

            // the track joins the visible circles
            rulerTrack.gameObject.SetActive(maxX > minX);
            Ui.PlaceD(rulerTrack, Ui.C, new Vector2((minX + maxX) * 0.5f, 0f), new Vector2(maxX - minX, TrackH));

            SetStep(prevGroup, selected > 1);
            SetStep(nextGroup, selected < unlocked);
        }

        static void SetStep(CanvasGroup g, bool on)
        {
            g.alpha = on ? 1f : StepDisabledAlpha;
            g.interactable = g.blocksRaycasts = on;
        }

        void Step(int dir)
        {
            int level = Mathf.Clamp(selected + dir, 1, unlocked);
            if (level != selected) Pick(level);
        }

        void Pick(int level)
        {
            SetSelected(level);
            popT = 0f;
        }

        void Play()
        {
            GameApp.I.CurrentLevel = selected;
            GameApp.I.Show("ScreenGame");
        }

        // ------------------------------------------------------------------ layout

        /// <summary>
        /// Top bar and wordmark from the top; SKIPS TODAY and PLAY from the bottom, above the banner (or BottomPad
        /// without one); the ruler centred between the wordmark and PLAY. Re-run when the height, the banner, the
        /// canvas scale or the safe area change. Button rects end on whole pixels.
        /// </summary>
        void Layout()
        {
            float h = Root.rect.height / Theme.K;
            float banner = Ads.BannerHeightDesign;
            float under = banner > 0f ? banner + BannerGap : BottomPad;
            float scale = Root.lossyScale.x;
            Rect safe = Screen.safeArea;
            if (h <= 0f || (Mathf.Abs(h - laidOutH) < 0.5f && Mathf.Abs(under - laidOutUnder) < 0.5f &&
                            Mathf.Approximately(scale, laidOutScale) && safe == laidOutSafe)) return;
            laidOutH = h;
            laidOutUnder = under;
            laidOutScale = scale;
            laidOutSafe = safe;

            FitBackdrop();

            Ui.PlaceD(settings, Ui.TL, new Vector2(Side, -TopY), new Vector2(SettingsD, SettingsD));
            Snap(settings);
            SetCoins();

            float markY = Mathf.Clamp(h * MarkFrac, MarkMinY, MarkMaxY);
            Ui.PlaceD(mark.Root, Ui.TC, Ui.C, new Vector2(0f, -markY), new Vector2(Wordmark.WidthD, Wordmark.HeightD));

            float skipsCY = h - under - SkipsH * 0.5f;
            Ui.PlaceD(skipsRow, Ui.TC, Ui.C, new Vector2(0f, -skipsCY), new Vector2(skipsRow.sizeDelta.x / Theme.K, SkipsH));
            Snap(skipsRow);

            float playTop = skipsCY - SkipsH * 0.5f - PlayToSkips - (PlayH + PlayEdge);
            playRoot.localScale = Vector3.one;
            Ui.PlaceD(playRoot, Ui.TC, new Vector2(0f, -playTop), new Vector2(PlayW, PlayH + PlayEdge));
            Snap(playRoot);

            float rulerY = (markY + MarkInkBelow + playTop) * 0.5f;
            Ui.PlaceD(ruler, Ui.TC, Ui.C, new Vector2(0f, -rulerY), new Vector2(390f, BigD));
            Snap(ruler);
            // children back on their design positions first, so repeated snaps never add up
            Ui.PlaceD(prev, Ui.C, new Vector2(-StepX, 0f), new Vector2(StepD, StepD));
            Ui.PlaceD(next, Ui.C, new Vector2(StepX, 0f), new Vector2(StepD, StepD));
            Snap(prev);
            Snap(next);
            for (int i = 0; i < slots.Length; i++)
            {
                float d = i == 2 ? BigD : SmallD;
                Ui.PlaceD(slots[i].rt, Ui.C, new Vector2(SlotX[i], 0f), new Vector2(d, d));
                Snap(slots[i].rt);
            }
        }

        /// <summary>The backdrop covers the whole canvas (Root is the safe area), like the splash's background.</summary>
        void FitBackdrop()
        {
            var canvas = GameApp.I.CanvasRoot;
            canvas.GetWorldCorners(corners);
            Vector2 bl = Root.InverseTransformPoint(corners[0]);
            Vector2 tr = Root.InverseTransformPoint(corners[2]);
            Vector2 min = Root.rect.min;
            backdrop.anchorMin = backdrop.anchorMax = backdrop.pivot = Vector2.zero;
            backdrop.anchoredPosition = bl - min;
            backdrop.sizeDelta = tr - bl;
        }

        /// <summary>Moves and resizes rt (fixed anchors, unscaled) so its screen rect starts and ends on whole pixels.</summary>
        static void Snap(RectTransform rt)
        {
            float s = rt.lossyScale.x;
            if (s <= 0f) return;
            rt.GetWorldCorners(corners); // screen space overlay: world units are pixels
            Vector2 bl = corners[0], size = (Vector2)corners[2] - bl;
            var blR = new Vector2(Mathf.Round(bl.x), Mathf.Round(bl.y));
            var sizeR = new Vector2(Mathf.Round(size.x), Mathf.Round(size.y));
            Vector2 pivotOld = bl + Vector2.Scale(rt.pivot, size);
            Vector2 pivotNew = blR + Vector2.Scale(rt.pivot, sizeR);
            rt.sizeDelta += (sizeR - size) / s;
            rt.position += (Vector3)(pivotNew - pivotOld);
        }

        void OnBannerHeight()
        {
            Layout();
        }

        // ------------------------------------------------------------------ per frame

        /// <summary>From HomeTicker.Update: layout check, PLAY breathing, the pulsing ring and the pick pop.</summary>
        void Tick()
        {
            Layout();
            float t = Time.unscaledTime, dt = Time.unscaledDeltaTime;

            float breath = 1f + BreathAmp * (0.5f - 0.5f * Mathf.Cos(t * Mathf.PI * 2f / BreathPeriod));
            playRoot.localScale = new Vector3(breath, breath, 1f);

            float k = Mathf.Repeat(t, RingPeriod) / RingPeriod;
            float rs = 1f + RingGrow * EaseOutCubic(k);
            ring.localScale = new Vector3(rs, rs, 1f);
            var c = Theme.Accent;
            c.a = RingAlpha * (1f - k);
            ringImg.color = c;

            if (popT >= 0f)
            {
                popT += dt;
                float p = Mathf.Clamp01(popT / PopTime);
                float s = 1f + (PopScale - 1f) * Mathf.Sin(Mathf.PI * p); // 1 -> 1.12 -> 1
                slots[2].rt.localScale = new Vector3(s, s, 1f);
                if (p >= 1f) popT = -1f;
            }
        }

        static float EaseOutCubic(float x)
        {
            float a = 1f - x;
            return 1f - a * a * a;
        }
    }

    /// <summary>Per-frame tick for ScreenHome (lives on the screen's Root, so it only runs while Home is shown).</summary>
    public class HomeTicker : MonoBehaviour
    {
        public Action tick;

        void Update() { if (tick != null) tick(); }
    }
}
