using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ArrowPuzzle
{
    /// <summary>
    /// "Level ruler" Home: top bar (logo, hints pill (opens PopupHints), star pill, settings), "LEVEL" and the big selected level number, its
    /// difficulty and type, a horizontal ruler of level circles (drag to scroll, tap to pick; snaps to unlocked levels
    /// only), earlier / next, the solved count and PLAY. The ruler reuses a pool of 9 circles. Everything under the
    /// top bar scales down to fit above the banner on short phones. Design px (y from the top of the safe area),
    /// unscaled time.
    /// </summary>
    public class ScreenHome : AppScreen
    {
        // ---- layout (design px, y from the top)
        const float Side = 20f, RowCY = 38f;                        // top bar: y 20, 36 high
        const float LogoD = 36f, LogoRadius = 11f;
        const float SettingsD = 46f, PillH = 40f, PillGap = 10f, CardEdge = 3f;
        const float PlusD = 26f, PlusBarW = 12f, PlusBarH = 3.5f, PlusHitD = 44f; // hints +: circle, bars, tap area
        const float BodyTop = 88f, BodyBottom = 683f;               // scaled block: "LEVEL" .. PLAY incl. its shadow
        const float BottomPad = 16f, BannerGap = 8f, MinScale = 0.6f;
        const float LevelLabelY = 100f, NumberY = 120f, NumberH = 170f, InfoY = 290f, InfoH = 28f, InfoPadX = 16f, InfoPadY = 4f;
        const float RulerY = 424f, RulerH = 110f, TrackH = 8f, Spacing = 80f;
        const float SelectedD = 80f, RingW = 8f, CircleD = 52f, BorderW = 5f, LockD = 22f;
        const float StepY = 480f, StepW = 110f, StepH = 36f;
        const float SolvedY = 530f, SolvedH = 26f;
        const float PlayX = 24f, PlayW = 342f, PlayY = 582f, PlayH = 94f, PlayEdge = 7f, PlayIconD = 34f;

        // ---- behaviour (s, levels)
        const float SnapTime = 0.25f, PopTime = 0.2f, PopScale = 1.12f;
        const float OverScroll = 0.6f;                              // how far past the ends a drag may pull
        const float StepDisabledAlpha = 0.4f;
        const int Pool = 9;

        // ---- colours: the app palette (Theme), like Settings
        static readonly Color Grey = Theme.SectionLabel;
        static readonly Color Ring = new Color(Theme.Teal.r, Theme.Teal.g, Theme.Teal.b, 0.2f); // selected circle glow

        enum Look { Hidden, Selected, Open, Locked }

        class Circle
        {
            public RectTransform rt;
            public Image ring, outer, inner;
            public Text number;
            public GameObject lockIcon;
            public Look look = Look.Hidden;
            public int shownLevel;
        }

        readonly Circle[] circles = new Circle[Pool];
        Text stars, hints, levelNumber, info, solved;
        RectTransform body, ruler, track, fill, numberRt;
        CanvasGroup earlier, next;

        int selected = 1, unlocked = 1;
        float pos = 1f;                  // level at the centre of the ruler (fractional while moving)
        float snapFrom, snapTo, snapT = -1f, popT = -1f;
        bool dragging;
        float laidOutH = -1f, laidOutUnder = -1f, scale = 1f;

        protected override void Build()
        {
            BuildBackground();
            BannerPlaceholder.Create(Root); // just above the background, under everything else
            BuildTopBar();
            BuildBody();

            Root.gameObject.AddComponent<HomeTicker>().tick = Tick;
            BannerDebug.Create(Root, "ScreenHome"); // only when Ads.ShowBannerDebug
            Refresh();
        }

        // ------------------------------------------------------------------ build

        /// <summary>Solid cream, like Settings; also under the insets.</summary>
        void BuildBackground()
        {
            var bg = Ui.Img(Root, "Bg", null, Theme.Bg);
            bg.raycastTarget = false;
            Ui.Fill(bg.rectTransform);
            bg.rectTransform.offsetMin = new Vector2(0f, -Theme.D(400f));
            bg.rectTransform.offsetMax = new Vector2(0f, Theme.D(400f));
        }

        void BuildTopBar()
        {
            var left = new Vector2(0f, 0.5f);
            var right = new Vector2(1f, 0.5f);

            // logo: teal rounded square with the white app arrow
            var logo = Ui.Img(Root, "Logo", Theme.Rounded, Theme.Teal, LogoRadius);
            logo.raycastTarget = false;
            Ui.PlaceD(logo.rectTransform, Ui.TL, left, new Vector2(Side, -RowCY), new Vector2(LogoD, LogoD));
            var mark = Ui.New(logo.transform, "Mark");
            Ui.Fill(mark);
            mark.pivot = Ui.C;
            var g = mark.gameObject.AddComponent<ArrowGraphic>();
            g.raycastTarget = false;
            g.color = Theme.Card;
            g.thickness = Theme.D(4.5f);
            g.headSize = Theme.D(12f);
            g.SetPoints(new List<Vector2>
            {
                new Vector2(-7.5f, -8.5f) * Theme.K, new Vector2(-7.5f, 2.5f) * Theme.K, new Vector2(7f, 2.5f) * Theme.K,
            });

            // no "Arrow Puzzle" title: the hints and stars pills need the room (the logo stays)

            // settings (round, right)
            var settings = Ui.Btn(Root, "", Theme.Card, Theme.Ink, 0f, SettingsD * 0.5f, Theme.ListEdge, CardEdge,
                                  () => GameApp.I.Show("ScreenSettings"), Icon.Gear, 24f);
            Ui.PlaceD((RectTransform)settings.transform, Ui.TR, right, new Vector2(-Side, -RowCY), new Vector2(SettingsD, SettingsD));

            // [hints][stars] left of Settings; the row grows to the left with the counts (it sizes widths, never heights)
            var pills = Ui.New(Root, "Pills");
            Ui.PlaceD(pills, Ui.TR, right, new Vector2(-(Side + SettingsD + PillGap), -RowCY), new Vector2(200f, PillH));
            var row = Ui.Row(pills, PillGap, TextAnchor.MiddleRight);
            row.childControlHeight = false;
            pills.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            BuildHintsPill(pills);
            HudPill.Make(pills, "Stars", Icon.Star, Theme.Gold, 10f, 14f, out stars);
        }

        /// <summary>
        /// Bulb, hint count and a teal + circle. Tapping the pill or the + (a separate Button with a PlusHitD square tap
        /// area around the 26 px circle) opens PopupHints at any count: the gameplay's rewarded video, +3 hints.
        /// </summary>
        void BuildHintsPill(RectTransform parent)
        {
            var pill = HudPill.Make(parent, "Hints", Icon.Bulb, Theme.Ink, 10f, (HudPill.H - PlusD) * 0.5f, out hints);
            pill.raycastTarget = true;
            var pillBtn = pill.gameObject.AddComponent<Button>();
            pillBtn.targetGraphic = pill;
            pillBtn.transition = Selectable.Transition.None;
            pill.gameObject.AddComponent<PressScale>();
            pillBtn.onClick.AddListener(OpenHints);

            var plus = Ui.Img(pill.transform, "Plus", Theme.Circle, Theme.Teal);
            plus.raycastTarget = false; // taps land on Hit below
            Ui.Size(plus, PlusD, PlusD);
            for (int i = 0; i < 2; i++)
            {
                var bar = Ui.Img(plus.transform, "Bar" + i, Theme.Rounded, Theme.Card, PlusBarH * 0.5f);
                bar.raycastTarget = false;
                Ui.PlaceD(bar.rectTransform, Ui.C, Vector2.zero, i == 0 ? new Vector2(PlusBarW, PlusBarH) : new Vector2(PlusBarH, PlusBarW));
            }
            // invisible tap area, centred on the circle and larger than it (spills a little past the pill)
            var hit = Ui.Img(plus.transform, "Hit", null, Color.clear);
            hit.raycastTarget = true;
            Ui.PlaceD(hit.rectTransform, Ui.C, Vector2.zero, new Vector2(PlusHitD, PlusHitD));
            var plusBtn = plus.gameObject.AddComponent<Button>();
            plusBtn.targetGraphic = hit;
            plusBtn.transition = Selectable.Transition.None;
            plus.gameObject.AddComponent<PressScale>();
            plusBtn.onClick.AddListener(OpenHints);
        }

        static void OpenHints()
        {
            Sfx.Play(Sfx.Id.Click);
            GameApp.I.Open("PopupHints");
        }

        void OnHintsChanged()
        {
            hints.text = Save.Hints.ToString();
        }

        void BuildBody()
        {
            body = Ui.New(Root, "Body");
            Ui.PlaceD(body, Ui.TC, new Vector2(0f, -BodyTop), new Vector2(390f, BodyBottom - BodyTop));

            var levelLabel = Ui.Label(body, "LEVEL", 22f, Grey, Weight.Semi);
            Center(levelLabel.rectTransform, LevelLabelY, 28f, 300f);

            levelNumber = Ui.Label(body, "1", 150f, Theme.Teal, Weight.Bold);
            numberRt = levelNumber.rectTransform;
            Center(numberRt, NumberY, NumberH, 390f);

            // "Easy · Picture" in a white pill that grows from the centre with the text
            float infoPillH = InfoH + InfoPadY * 2f;
            var infoPill = Ui.Img(body, "InfoPill", Theme.Rounded, Theme.Card, infoPillH * 0.5f);
            infoPill.raycastTarget = false;
            Center(infoPill.rectTransform, InfoY - InfoPadY, infoPillH, 200f);
            HudPill.AddEdge(infoPill);
            var infoRow = Ui.Row(infoPill.rectTransform, 0f);
            int padX = Mathf.RoundToInt(Theme.D(InfoPadX)), padY = Mathf.RoundToInt(Theme.D(InfoPadY));
            infoRow.padding = new RectOffset(padX, padX, padY, padY);
            infoPill.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            info = Ui.Label(infoPill.transform, "", 21f, Theme.Ink, Weight.Semi);

            BuildRuler();

            earlier = StepButton("‹ earlier", Ui.TL, Side, -1, TextAnchor.MiddleLeft);
            next = StepButton("next ›", Ui.TR, -Side, 1, TextAnchor.MiddleRight);

            solved = Ui.Label(body, "", 20f, Theme.Ink, Weight.Semi);
            Center(solved.rectTransform, SolvedY, SolvedH, 360f);

            var play = Ui.Btn(body, "PLAY", Theme.Teal, Theme.Card, 42f, PlayH * 0.5f, Theme.TealShadow, PlayEdge, Play, Icon.Play, PlayIconD);
            var playRt = (RectTransform)play.transform;
            Ui.PlaceD(playRt, Ui.TL, new Vector2(PlayX, -(PlayY - BodyTop)), new Vector2(PlayW, PlayH));
            Ui.AddShimmer(playRt, PlayH * 0.5f);
        }

        /// <summary>Centred horizontally, top edge at design y (from the top of the screen).</summary>
        static void Center(RectTransform rt, float y, float h, float w)
        {
            Ui.PlaceD(rt, Ui.TC, Ui.C, new Vector2(0f, -(y - BodyTop + h * 0.5f)), new Vector2(w, h));
        }

        void BuildRuler()
        {
            // input area: full width (Layout widens it when the body is scaled), centred on the ruler line
            var area = Ui.Img(body, "Ruler", null, Color.clear);
            ruler = area.rectTransform;
            Center(ruler, RulerY - RulerH * 0.5f, RulerH, 390f);
            var input = area.gameObject.AddComponent<RulerInput>();
            input.begin = OnDragBegin;
            input.drag = OnDragMove;
            input.end = OnDragEnd;
            input.click = OnRulerClick;

            var trackImg = Ui.Img(ruler, "Track", Theme.Rounded, Theme.RulerTrack, TrackH * 0.5f);
            trackImg.raycastTarget = false;
            track = trackImg.rectTransform;
            Ui.PlaceD(track, Ui.C, Vector2.zero, new Vector2(390f, TrackH));

            var fillImg = Ui.Img(track, "Fill", Theme.Rounded, Theme.Teal, TrackH * 0.5f);
            fillImg.raycastTarget = false;
            fill = fillImg.rectTransform;
            Ui.PlaceD(fill, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, TrackH));

            for (int i = 0; i < Pool; i++) circles[i] = BuildCircle(i);
        }

        Circle BuildCircle(int i)
        {
            var c = new Circle();
            c.rt = Ui.New(ruler, "Circle_" + i);
            Ui.PlaceD(c.rt, Ui.C, Vector2.zero, new Vector2(SelectedD + RingW * 2f, SelectedD + RingW * 2f));
            c.ring = Ui.Img(c.rt, "Ring", Theme.Circle, Ring);
            c.outer = Ui.Img(c.rt, "Outer", Theme.Circle, Theme.Teal);
            c.inner = Ui.Img(c.rt, "Inner", Theme.Circle, Theme.Card);
            c.ring.raycastTarget = c.outer.raycastTarget = c.inner.raycastTarget = false;
            c.number = Ui.Label(c.rt, "", 20f, Theme.TealShadow, Weight.Bold);
            Ui.PlaceD(c.number.rectTransform, Ui.C, Vector2.zero, new Vector2(SelectedD, SelectedD));
            var lockRt = Icons.Make(c.rt, Icon.Lock, Theme.LockedLevelIcon, LockD);
            Ui.PlaceD(lockRt, Ui.C, Vector2.zero, new Vector2(LockD, LockD));
            c.lockIcon = lockRt.gameObject;
            c.rt.gameObject.SetActive(false);
            return c;
        }

        CanvasGroup StepButton(string text, Vector2 corner, float x, int dir, TextAnchor align)
        {
            var btn = Ui.Btn(body, text, Color.clear, Grey, 18f, 0f, Color.clear, 0f, () => Step(dir), null, 24f, Weight.Medium);
            btn.GetComponent<HorizontalLayoutGroup>().childAlignment = align;
            Ui.PlaceD((RectTransform)btn.transform, corner, new Vector2(x, -(StepY - BodyTop)), new Vector2(StepW, StepH));
            return btn.gameObject.AddComponent<CanvasGroup>();
        }

        // ------------------------------------------------------------------ show / refresh

        public override void OnShow()
        {
            laidOutH = -1f;
            Ads.BannerHeightChanged -= OnBannerHeight; // never twice
            Ads.BannerHeightChanged += OnBannerHeight;
            Ads.Banner(true, "ScreenHome"); // popups on top keep it; only leaving Home hides it
            Save.HintsChanged -= OnHintsChanged;
            Save.HintsChanged += OnHintsChanged; // PopupHints' reward updates the pill, also after the popup closed
            MusicPlayer.PlayHome();
            Refresh();
            Layout();
        }

        public override void OnHide()
        {
            Ads.BannerHeightChanged -= OnBannerHeight;
            Save.HintsChanged -= OnHintsChanged;
            Ads.Banner(false, "ScreenHome"); // ignored when the next screen (game) asks for it in the same frame
            dragging = false;
            snapT = -1f;
            popT = -1f;
            numberRt.localScale = Vector3.one;
        }

        /// <summary>Stars, solved count, and the ruler back on the highest unlocked level.</summary>
        void Refresh()
        {
            stars.text = Save.TotalStars().ToString();
            OnHintsChanged();
            solved.text = Save.CompletedLevels() + " of " + LevelPath.Count + " levels solved";
            unlocked = Save.IsUnlocked(LevelPath.Count) ? LevelPath.Count : Save.UnlockedGlobal;
            dragging = false;
            snapT = -1f;
            popT = -1f;
            numberRt.localScale = Vector3.one;
            pos = Save.UnlockedGlobal;
            SetSelected(Save.UnlockedGlobal, true);
            UpdateRuler();
        }

        /// <summary>Kept within the unlocked levels; updates the number, its difficulty and type, and earlier / next.</summary>
        void SetSelected(int level, bool force = false)
        {
            level = Mathf.Clamp(level, 1, unlocked);
            if (level == selected && !force) return;
            selected = level;
            levelNumber.text = level.ToString();
            info.text = LevelPath.Difficulty(level) + " · " + LevelPath.TypeName(level);
            SetStep(earlier, selected > 1);
            SetStep(next, selected < unlocked);
        }

        static void SetStep(CanvasGroup g, bool on)
        {
            g.alpha = on ? 1f : StepDisabledAlpha;
            g.interactable = g.blocksRaycasts = on;
        }

        /// <summary>Places the pooled circles around pos and sets each one's look, and the filled part of the track.</summary>
        void UpdateRuler()
        {
            int centre = Mathf.RoundToInt(pos);
            for (int i = 0; i < Pool; i++)
            {
                var c = circles[i];
                int level = centre - Pool / 2 + i;
                if (level < 1 || level > LevelPath.Count)
                {
                    SetLook(c, Look.Hidden, 0);
                    continue;
                }
                c.rt.anchoredPosition = new Vector2(Theme.D((level - pos) * Spacing), 0f);
                SetLook(c, level == selected ? Look.Selected : level <= unlocked ? Look.Open : Look.Locked, level);
            }

            // filled from the left edge of the screen to the highest unlocked circle
            float width = track.rect.width / Theme.K;
            float w = Mathf.Clamp(width * 0.5f + (unlocked - pos) * Spacing, 0f, width);
            fill.gameObject.SetActive(w >= TrackH);
            fill.sizeDelta = new Vector2(Theme.D(w), Theme.D(TrackH));
        }

        static void SetLook(Circle c, Look look, int level)
        {
            if (look == c.look && level == c.shownLevel) return;
            c.shownLevel = level;
            if (look == Look.Hidden)
            {
                c.look = look;
                c.rt.gameObject.SetActive(false);
                return;
            }
            c.rt.gameObject.SetActive(true);
            if (look != Look.Locked) c.number.text = level.ToString();
            if (look == c.look) return;
            c.look = look;

            bool sel = look == Look.Selected;
            float d = sel ? SelectedD : CircleD;
            c.ring.gameObject.SetActive(sel);
            Size(c.ring.rectTransform, SelectedD + RingW * 2f);
            Size(c.outer.rectTransform, d);
            Size(c.inner.rectTransform, d - BorderW * 2f);
            c.outer.color = look == Look.Locked ? Theme.LockedLevelEdge : Theme.Teal;
            c.inner.color = look == Look.Locked ? Theme.LockedLevelFill : Theme.Card;
            c.inner.gameObject.SetActive(!sel);
            c.number.gameObject.SetActive(look != Look.Locked);
            c.number.color = sel ? Theme.Card : Theme.TealShadow;
            c.number.fontSize = Mathf.RoundToInt(Theme.D(sel ? 32f : 20f));
            c.lockIcon.SetActive(look == Look.Locked);
        }

        static void Size(RectTransform rt, float d)
        {
            Ui.PlaceD(rt, Ui.C, Vector2.zero, new Vector2(d, d));
        }

        /// <summary>
        /// Everything under the top bar keeps its design position and scales down (from its top) when it would
        /// reach the banner; the track and the drag area widen by 1 / scale so they still span the screen.
        /// </summary>
        void Layout()
        {
            float h = Root.rect.height / Theme.K;
            float banner = Ads.BannerHeightDesign;
            float under = banner > 0f ? banner + BannerGap : BottomPad;
            if (h <= 0f || (Mathf.Abs(h - laidOutH) < 0.5f && Mathf.Abs(under - laidOutUnder) < 0.5f)) return;
            laidOutH = h;
            laidOutUnder = under;

            float avail = h - BodyTop - under;
            scale = Mathf.Clamp(avail / (BodyBottom - BodyTop), MinScale, 1f);
            body.localScale = new Vector3(scale, scale, 1f);

            float wide = 390f / scale;
            ruler.sizeDelta = new Vector2(Theme.D(wide), Theme.D(RulerH));
            track.sizeDelta = new Vector2(Theme.D(wide), Theme.D(TrackH));
            UpdateRuler();
        }

        void OnBannerHeight()
        {
            Layout();
        }

        // ------------------------------------------------------------------ input

        void OnDragBegin()
        {
            dragging = true;
            snapT = -1f;
        }

        /// <summary>dx in the ruler's design px: the content follows the finger.</summary>
        void OnDragMove(float dx)
        {
            pos = Mathf.Clamp(pos - dx / Spacing, 1f - OverScroll, Mathf.Min(unlocked + OverScroll, LevelPath.Count + OverScroll));
            SetSelected(Mathf.RoundToInt(pos));
            UpdateRuler();
        }

        void OnDragEnd()
        {
            dragging = false;
            SetSelected(Mathf.RoundToInt(pos)); // clamped to unlocked levels: past the end snaps back
            SlideTo(selected);
        }

        /// <summary>Tap at (x, y) design px from the ruler centre: picks the unlocked circle under it.</summary>
        void OnRulerClick(float x, float y)
        {
            int level = Mathf.RoundToInt(pos + x / Spacing);
            if (level < 1 || level > unlocked) return;
            float r = (level == selected ? SelectedD : CircleD) * 0.5f;
            float cx = (level - pos) * Spacing;
            if ((x - cx) * (x - cx) + y * y > r * r) return;
            Sfx.Play(Sfx.Id.Click); // same click as the buttons
            Pick(level);
        }

        void Step(int dir)
        {
            int level = Mathf.Clamp(selected + dir, 1, unlocked);
            if (level != selected) Pick(level);
        }

        void Pick(int level)
        {
            SetSelected(level);
            SlideTo(selected);
            popT = 0f;
            UpdateRuler();
        }

        void SlideTo(int level)
        {
            snapFrom = pos;
            snapTo = level;
            snapT = Mathf.Approximately(snapFrom, snapTo) ? -1f : 0f;
        }

        void Play()
        {
            GameApp.I.CurrentLevel = selected;
            GameApp.I.Show("ScreenGame");
        }

        // ------------------------------------------------------------------ per frame

        /// <summary>From HomeTicker.Update: layout check, the ruler slide and the number pop.</summary>
        void Tick()
        {
            float dt = Time.unscaledDeltaTime;
            Layout();

            if (snapT >= 0f && !dragging)
            {
                snapT += dt;
                float k = Mathf.Clamp01(snapT / SnapTime);
                pos = Mathf.LerpUnclamped(snapFrom, snapTo, EaseOutCubic(k));
                if (k >= 1f) snapT = -1f;
                UpdateRuler();
            }

            if (popT >= 0f)
            {
                popT += dt;
                float k = Mathf.Clamp01(popT / PopTime);
                float s = 1f + (PopScale - 1f) * Mathf.Sin(Mathf.PI * k); // 1 -> 1.12 -> 1
                numberRt.localScale = new Vector3(s, s, 1f);
                if (k >= 1f) popT = -1f;
            }
        }

        static float EaseOutCubic(float x)
        {
            float a = 1f - x;
            return 1f - a * a * a;
        }
    }

    /// <summary>
    /// The white HUD pill on Home: radius H / 2, a 3 px Theme.ListEdge bottom edge, and a row of [icon 22][count,
    /// Bold 21] (callers may add more children to the row). Height H; the width follows the row, so the caller either
    /// adds a horizontal ContentSizeFitter or puts the pill in a row that controls widths. (Moved here from the removed
    /// ScreenMap.cs.)
    /// </summary>
    public static class HudPill
    {
        public const float H = 40f, Edge = 3f, IconD = 22f, CountD = 21f, Gap = 6f;

        public static Image Make(Transform parent, string name, Icon icon, Color iconColor, float padLeftD, float padRightD, out Text count)
        {
            var pill = Ui.Img(parent, name, Theme.Rounded, Theme.Card, H * 0.5f);
            pill.raycastTarget = false;
            pill.rectTransform.sizeDelta = new Vector2(Theme.D(80f), Theme.D(H));
            AddEdge(pill);
            var row = Ui.Row(pill.rectTransform, Gap);
            row.padding = new RectOffset(Mathf.RoundToInt(Theme.D(padLeftD)), Mathf.RoundToInt(Theme.D(padRightD)), 0, 0);
            var ic = Icons.Make(pill.transform, icon, iconColor, IconD);
            Ui.Size(ic, IconD, IconD);
            count = Ui.Label(pill.transform, "0", CountD, Theme.Ink, Weight.Bold);
            return pill;
        }

        /// <summary>The 3 px Theme.ListEdge edge under a white card.</summary>
        public static void AddEdge(Graphic g)
        {
            var edge = g.gameObject.AddComponent<Shadow>();
            edge.effectColor = Theme.ListEdge;
            edge.effectDistance = new Vector2(0f, -Theme.D(Edge));
            edge.useGraphicAlpha = false;
        }
    }

    /// <summary>Per-frame tick for ScreenHome (lives on the screen's Root, so it only runs while Home is shown).</summary>
    public class HomeTicker : MonoBehaviour
    {
        public Action tick;

        void Update() { if (tick != null) tick(); }
    }

    /// <summary>
    /// Drag and tap on the Home ruler. Positions are in the ruler's own design px (its scale included), so the
    /// content follows the finger exactly; a tap that turned into a drag is not a click.
    /// </summary>
    public class RulerInput : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        public Action begin, end;
        public Action<float> drag;
        public Action<float, float> click;

        float lastX;
        bool dragged;

        Vector2 Local(PointerEventData e)
        {
            Vector2 p;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, e.position, e.pressEventCamera, out p);
            return p / Theme.K;
        }

        public void OnPointerDown(PointerEventData e) { dragged = false; } // needed so this object gets the click

        public void OnBeginDrag(PointerEventData e)
        {
            dragged = true;
            lastX = Local(e).x;
            if (begin != null) begin();
        }

        public void OnDrag(PointerEventData e)
        {
            float x = Local(e).x;
            if (drag != null) drag(x - lastX);
            lastX = x;
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (end != null) end();
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (dragged || e.dragging) return;
            var p = Local(e);
            if (click != null) click(p.x, p.y);
        }
    }
}
