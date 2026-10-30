using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ArrowPuzzle
{
    /// <summary>
    /// Settings screen: back button + title, GAME (Sound and Vibration switches), MORE (Share / Rate us / Privacy
    /// Policy), version line. The content scrolls between the header and the banner, which this screen keeps on.
    /// Back (button or phone key) returns to the screen that opened it. Opened from Home only: ScreenGame.OnShow
    /// starts its level again, so a trip here from the game would restart the level.
    /// </summary>
    public class ScreenSettings : AppScreen
    {
        // header (design px): back button 56, 22 below the safe-area top, title next to it
        const float Side = 20f, HeaderTop = 22f, BackD = 56f, BackEdge = 3f, TitleGap = 14f;

        // scroll view: from under the header to BannerGap above the banner (BottomPad when there is no banner)
        const float ViewTop = HeaderTop + BackD + 14f, BannerGap = 8f, BottomPad = 16f;

        // lists (design px)
        const float ListRadius = 26f, ListEdgeD = 5f, RowH = 76f, DividerH = 2f, RowPad = 16f;
        const float IconBox = 46f, IconBoxRadius = 14f, IconD = 24f, LabelGap = 14f, LabelD = 23f, ChevronD = 20f;
        const float SwitchW = 52f, SwitchH = 30f, SwitchPad = 18f;
        const float ListW = 390f - Side * 2f;

        // toast (as on Home): above the bottom of the view
        const float ToastW = 330f, ToastH = 44f, ToastGap = 12f, ToastRise = 12f;
        const float ToastIn = 0.2f, ToastHold = 1.4f, ToastOut = 0.3f;

        SwitchView sound, haptics;
        RectTransform view;
        LayoutElement contentMin;
        ScrollRect scroll;
        CanvasGroup toast;
        Text toastText;
        Coroutine toastFx;
        float toastY;

        protected override void Build()
        {
            var bg = Ui.Img(Root, "Bg", null, Theme.Bg);
            bg.raycastTarget = false;
            Ui.Fill(bg.rectTransform);
            bg.rectTransform.offsetMin = new Vector2(0f, -Theme.D(400f)); // also under the insets
            bg.rectTransform.offsetMax = new Vector2(0f, Theme.D(400f));
            BannerPlaceholder.Create(Root); // just above the background, under everything else

            BuildHeader();
            var content = BuildScroll();

            // GAME: switch rows
            var game = Section(content, "Game");
            sound = SwitchRow(game, Icon.Sound, Theme.Teal, "Sound", () => Save.Sound, v => Save.Sound = v);
            Divider(game);
            haptics = SwitchRow(game, Icon.Vibrate, Theme.Accent, "Vibration", () => Save.Haptics, v => Save.Haptics = v);

            // MORE: link rows
            var more = Section(content, "More");
            LinkRow(more, Icon.Share, Theme.LinkBlue, "Share this game", AppLinks.Share);
            Divider(more);
            LinkRow(more, Icon.Star, Theme.ModeGold, "Rate us", AppLinks.Rate);
            Divider(more);
            LinkRow(more, Icon.Shield, Theme.LinkGreen, "Privacy Policy", OnPrivacy);

            // version, at the bottom of the view when everything fits (the spacer takes the free height)
            var spacer = Ui.New(content, "Spacer");
            spacer.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
            var version = Ui.Label(content, "Arrow Puzzle · Version " + Application.version, 17f, Theme.FootNote, Weight.Medium);
            Ui.Size(version, 0f, 24f);

            BuildToast();
            BannerDebug.Create(Root, "ScreenSettings"); // only when Ads.ShowBannerDebug
        }

        public override void OnShow()
        {
            sound.Refresh();
            haptics.Refresh();
            Ads.BannerHeightChanged -= Layout; // never twice
            Ads.BannerHeightChanged += Layout;
            Ads.Banner(true, "ScreenSettings"); // wins over the previous screen's hide in the same frame
            Layout();
        }

        public override void OnHide()
        {
            Ads.BannerHeightChanged -= Layout;
            Ads.Banner(false, "ScreenSettings"); // ignored when the next screen (Home) asks for it in the same frame
            if (toastFx != null) GameApp.I.StopCoroutine(toastFx);
            toastFx = null;
            toast.gameObject.SetActive(false);
        }

        public override bool OnBack()
        {
            GoBack();
            return true;
        }

        /// <summary>To the screen that opened Settings; Home when there is none.</summary>
        static void GoBack()
        {
            string prev = GameApp.I.PreviousScreen;
            if (string.IsNullOrEmpty(prev) || prev == "ScreenSettings" || !GameApp.I.Show(prev)) GameApp.I.Show("ScreenHome");
        }

        // ------------------------------------------------------------------ build

        void BuildHeader()
        {
            var back = Ui.Btn(Root, "", Theme.Card, Theme.Ink, 0f, BackD * 0.5f, Theme.CardShadow, BackEdge, GoBack, Icon.Back, 24f);
            Ui.PlaceD((RectTransform)back.transform, Ui.TL, new Vector2(Side, -HeaderTop), new Vector2(BackD, BackD));

            var title = Ui.Label(Root, "Settings", 30f, Theme.Ink, Weight.Bold, TextAnchor.MiddleLeft);
            float x = Side + BackD + TitleGap;
            Ui.PlaceD(title.rectTransform, Ui.TL, new Vector2(0f, 0.5f), new Vector2(x, -(HeaderTop + BackD * 0.5f)),
                      new Vector2(390f - x - Side, 40f));
        }

        /// <summary>View between the header and the banner; returns the content (full width, at least the view's height).</summary>
        RectTransform BuildScroll()
        {
            var area = Ui.Img(Root, "View", null, Color.clear); // catches drags around the cards
            view = area.rectTransform;
            Ui.Fill(view);
            scroll = area.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.inertia = true;

            var viewport = Ui.New(view, "Viewport");
            Ui.Fill(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll.viewport = viewport;

            var content = Ui.New(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            Ui.Column(content, Side, 4f, ListEdgeD + 4f, 24f);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentMin = content.gameObject.AddComponent<LayoutElement>();
            scroll.content = content;
            return content;
        }

        /// <summary>Fits the view between the header and the banner (or the safe-area bottom), scrolled to the top.</summary>
        void Layout()
        {
            float h = Root.rect.height;
            if (h <= 0f) return;
            float banner = Ads.BannerHeightDesign;
            float bottom = Theme.D(banner > 0f ? banner + BannerGap : BottomPad);
            float top = Theme.D(ViewTop);

            view.offsetMin = new Vector2(0f, bottom);
            view.offsetMax = new Vector2(0f, -top);
            contentMin.minHeight = Mathf.Max(0f, h - top - bottom);
            scroll.StopMovement();
            scroll.content.anchoredPosition = Vector2.zero;
            toastY = bottom / Theme.K + ToastGap;
        }

        // ------------------------------------------------------------------ lists

        /// <summary>Section label + white list card (radius 26, edge 5 ListEdge); returns the card the rows go into.</summary>
        static RectTransform Section(RectTransform content, string title)
        {
            var box = Ui.New(content, title);
            Ui.Column(box, 0f, 0f, 0f, 10f);
            SectionLabel(box, title);

            // the Mask clips the pressed-row highlight to the rounded corners
            var list = Ui.Img(box, "List", Theme.Rounded, Theme.Card, ListRadius);
            var edge = list.gameObject.AddComponent<Shadow>();
            edge.effectColor = Theme.ListEdge;
            edge.effectDistance = new Vector2(0f, -Theme.D(ListEdgeD));
            edge.useGraphicAlpha = false;
            list.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            Ui.Column(list.rectTransform, 0f, 0f, 0f, 0f);
            return list.rectTransform;
        }

        /// <summary>Semi 17 uppercase label in SectionLabel grey, letter-spaced (one Text per letter, 1.5 apart).</summary>
        static void SectionLabel(Transform parent, string text)
        {
            var box = Ui.New(parent, text + "Label");
            Ui.Row(box, 1.5f, TextAnchor.MiddleLeft);
            Ui.Size(box, 0f, 20f);
            foreach (char ch in text.ToUpperInvariant())
                Ui.Label(box, ch.ToString(), 17f, Theme.SectionLabel, Weight.Semi);
        }

        /// <summary>Row 76: icon square 46 (radius 14, white icon) and Semi label on the left, the switch on the right.</summary>
        static SwitchView SwitchRow(RectTransform list, Icon icon, Color iconBg, string text, Func<bool> get, Action<bool> set)
        {
            var row = Ui.New(list, text + "Row");
            Ui.Size(row, 0f, RowH);
            RowStart(row, icon, iconBg, text, SwitchW + SwitchPad);

            var sw = Ui.Switch(row, get, set);
            Ui.PlaceD((RectTransform)sw.transform, new Vector2(1f, 0.5f), new Vector2(-SwitchPad, 0f), new Vector2(SwitchW, SwitchH));
            return sw;
        }

        /// <summary>Row 76: icon square, Semi label, chevron on the right. Press = highlight + click.</summary>
        static void LinkRow(RectTransform list, Icon icon, Color iconBg, string text, Action onClick)
        {
            var row = Ui.Img(list, text + "Row", null, Color.clear);
            Ui.Size(row, 0f, RowH);
            var btn = row.gameObject.AddComponent<Button>();
            btn.targetGraphic = row;
            btn.transition = Selectable.Transition.None;
            row.gameObject.AddComponent<RowHighlight>().pressed = Theme.ListPress;
            btn.onClick.AddListener(() =>
            {
                Sfx.Play(Sfx.Id.Click);
                if (onClick != null) onClick();
            });
            RowStart(row.rectTransform, icon, iconBg, text, ChevronD);

            var chevron = Icons.Make(row.transform, Icon.Chevron, Theme.Chevron, ChevronD);
            Ui.PlaceD(chevron, new Vector2(1f, 0.5f), new Vector2(-RowPad + 4f, 0f), new Vector2(ChevronD, ChevronD)); // stroke sits left in its box
        }

        /// <summary>Icon square + label of a row; rightD = room kept free on the right for the switch / chevron.</summary>
        static void RowStart(RectTransform row, Icon icon, Color iconBg, string text, float rightD)
        {
            var box = Ui.Img(row, "IconBox", Theme.Rounded, iconBg, IconBoxRadius);
            box.raycastTarget = false;
            Ui.PlaceD(box.rectTransform, new Vector2(0f, 0.5f), new Vector2(RowPad, 0f), new Vector2(IconBox, IconBox));
            Icons.Make(box.transform, icon, Theme.Card, IconD); // centered in the square

            float labelX = RowPad + IconBox + LabelGap;
            var label = Ui.Label(row, text, LabelD, Theme.Ink, Weight.Semi, TextAnchor.MiddleLeft);
            Ui.PlaceD(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(labelX, 0f),
                      new Vector2(ListW - labelX - RowPad - rightD, RowH));
        }

        static void Divider(RectTransform list)
        {
            var line = Ui.Img(list, "Divider", null, Theme.ListDivider);
            line.raycastTarget = false;
            Ui.Size(line, 0f, DividerH);
        }

        void OnPrivacy()
        {
            if (AppLinks.OpenPrivacyPolicy()) return;
            if (toastFx != null) GameApp.I.StopCoroutine(toastFx);
            toastFx = GameApp.I.StartCoroutine(Toast("Privacy policy link is not set yet"));
        }

        // ------------------------------------------------------------------ toast

        /// <summary>Small ink pill above the bottom of the view, hidden until Toast() runs. Never takes taps.</summary>
        void BuildToast()
        {
            var pill = Ui.Img(Root, "Toast", Theme.Rounded, Theme.Ink, ToastH * 0.5f);
            pill.raycastTarget = false;
            Ui.PlaceD(pill.rectTransform, Ui.BC, Vector2.zero, new Vector2(ToastW, ToastH));
            toastText = Ui.Label(pill.transform, "", 17f, Theme.Card, Weight.Bold);
            Ui.Fill(toastText.rectTransform);
            toast = pill.gameObject.AddComponent<CanvasGroup>();
            toast.blocksRaycasts = false;
            toast.interactable = false;
            pill.gameObject.SetActive(false);
        }

        /// <summary>Fades and rises in, holds, fades out (unscaled time).</summary>
        IEnumerator Toast(string text)
        {
            toastText.text = text;
            toast.gameObject.SetActive(true);
            toast.transform.SetAsLastSibling();
            var trt = (RectTransform)toast.transform;
            float total = ToastIn + ToastHold + ToastOut;
            float t = 0f;
            while (t < total)
            {
                t += Time.unscaledDeltaTime;
                toast.alpha = t < ToastIn ? EaseOutCubic(t / ToastIn)
                            : t < ToastIn + ToastHold ? 1f
                            : 1f - Mathf.Clamp01((t - ToastIn - ToastHold) / ToastOut);
                float rise = EaseOutCubic(Mathf.Clamp01(t / ToastIn));
                trt.anchoredPosition = new Vector2(0f, Theme.D(toastY - ToastRise * (1f - rise)));
                yield return null;
            }
            toast.gameObject.SetActive(false);
            toastFx = null;
        }

        static float EaseOutCubic(float x)
        {
            float u = 1f - x;
            return 1f - u * u * u;
        }
    }

    /// <summary>List row press feedback: its Image takes the pressed color while held, clear otherwise.</summary>
    public class RowHighlight : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public Color pressed;
        Image img;

        void Awake() { img = GetComponent<Image>(); }
        public void OnPointerDown(PointerEventData e) { img.color = pressed; }
        public void OnPointerUp(PointerEventData e) { img.color = Color.clear; }
        public void OnPointerExit(PointerEventData e) { img.color = Color.clear; }
        void OnDisable() { if (img != null) img.color = Color.clear; }
    }
}
