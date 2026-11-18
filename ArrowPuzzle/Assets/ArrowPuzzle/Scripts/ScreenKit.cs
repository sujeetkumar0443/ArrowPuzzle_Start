using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ArrowPuzzle
{
    /// <summary>
    /// Shared pieces of the Themes and Shapes screens (design px, theme tokens): header with the round dark back button,
    /// title and live coin pill, subtitle, scroll view between the header and the banner, cards with a bottom edge,
    /// toast and pill shake.
    /// </summary>
    public static class ScreenKit
    {
        public const float Side = 20f, HeaderTop = 16f, BackD = 52f, TitleGap = 14f;
        public const float PillH = 40f, CoinD = 24f, PillPadL = 8f, PillGap = 8f, PillPadR = 16f, CoinFont = 20f;
        public const float SubtitleH = 24f, SubtitleGap = 8f;
        public const float ViewTop = HeaderTop + BackD + SubtitleGap + SubtitleH + 12f; // 112
        public const float BannerGap = 8f, BottomPad = 16f;
        static readonly Color CoinEdge = Theme.Hex("#D98E00");

        /// <summary>Full-screen background (solid or the theme's gradient), also under the insets.</summary>
        public static void Background(RectTransform root)
        {
            var bg = Ui.Img(root, "Bg", null, Theme.Bg);
            Theme.PaintBg(bg);
            bg.raycastTarget = false;
            Ui.Fill(bg.rectTransform);
            bg.rectTransform.offsetMin = new Vector2(0f, -Theme.D(400f));
            bg.rectTransform.offsetMax = new Vector2(0f, Theme.D(400f));
        }

        /// <summary>Round dark back button (Panel, as Home's Settings button), title, subtitle. Returns the coin pill.</summary>
        public static CoinPill Header(RectTransform root, string title, string subtitle, Action onBack)
        {
            var back = Ui.Btn(root, "", Theme.Panel, Theme.PanelText, 0f, BackD * 0.5f, Theme.PanelEdge, 3f, onBack, Icon.Back, 24f);
            Ui.PlaceD((RectTransform)back.transform, Ui.TL, new Vector2(Side, -HeaderTop), new Vector2(BackD, BackD));

            float x = Side + BackD + TitleGap;
            var t = Ui.Label(root, title, 30f, Theme.Ink, Weight.Bold, TextAnchor.MiddleLeft);
            Ui.PlaceD(t.rectTransform, Ui.TL, new Vector2(0f, 0.5f), new Vector2(x, -(HeaderTop + BackD * 0.5f)), new Vector2(170f, 40f));

            var s = Ui.Label(root, subtitle, 17f, Theme.InkSoft, Weight.Medium, TextAnchor.MiddleLeft);
            Ui.PlaceD(s.rectTransform, Ui.TL, new Vector2(0f, 1f), new Vector2(Side, -(HeaderTop + BackD + SubtitleGap)),
                      new Vector2(390f - Side * 2f, SubtitleH));
            return new CoinPill(root);
        }

        /// <summary>Gold coin (amber rim, gold face, shine), as Home's.</summary>
        public static RectTransform Coin(Transform parent, float d)
        {
            var rim = Ui.Img(parent, "Coin", Theme.CircleHD, CoinEdge);
            rim.raycastTarget = false;
            var rt = rim.rectTransform;
            Dot(rt, "Face", Theme.CoinGold, new Vector2(0f, 0.04f * d), 0.76f * d);
            Dot(rt, "Shine", new Color(1f, 1f, 1f, 0.55f), new Vector2(-0.16f * d, 0.18f * d), 0.2f * d);
            return rt;
        }

        static void Dot(RectTransform parent, string name, Color c, Vector2 posD, float d)
        {
            var img = Ui.Img(parent, name, Theme.CircleHD, c);
            img.raycastTarget = false;
            Ui.PlaceD(img.rectTransform, Ui.C, posD, new Vector2(d, d));
        }

        /// <summary>Rounded image with the high-resolution corner sprite (as Home's pills).</summary>
        public static Image Rounded(Transform parent, string name, Color color, float radiusD)
        {
            var img = Ui.Img(parent, name, Theme.RoundedHD, color);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = Theme.RoundedHDRadius / Theme.D(radiusD);
            return img;
        }

        /// <summary>White (Card) rounded card with a bottom edge (CardShadow), filling parent.</summary>
        public static Image Card(RectTransform parent, float radiusD, float edgeD)
        {
            var img = Rounded(parent, "Card", Theme.Card, radiusD);
            Ui.Fill(img.rectTransform);
            img.rectTransform.offsetMin = new Vector2(0f, Theme.D(edgeD));
            var edge = img.gameObject.AddComponent<Shadow>();
            edge.effectColor = Theme.CardShadow;
            edge.effectDistance = new Vector2(0f, -Theme.D(edgeD));
            edge.useGraphicAlpha = false;
            return img;
        }

        /// <summary>A rounded ring around a card (selected / current), drawn under it.</summary>
        public static Image Ring(RectTransform parent, Color c, float radiusD, float widthD)
        {
            var img = Rounded(parent, "Ring", c, radiusD + widthD);
            img.raycastTarget = false;
            Ui.Fill(img.rectTransform);
            img.rectTransform.offsetMin = new Vector2(-Theme.D(widthD), -Theme.D(widthD));
            img.rectTransform.offsetMax = new Vector2(Theme.D(widthD), Theme.D(widthD));
            img.transform.SetAsFirstSibling();
            return img;
        }

        /// <summary>Makes an image a tappable button with the usual squash.</summary>
        public static void Tap(Image img, Action onClick)
        {
            img.raycastTarget = true;
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

        /// <summary>Vertical scroll view; returns the content (top anchored, grows with its children).</summary>
        public static ScrollRect Scroll(RectTransform root, out RectTransform view, out RectTransform content)
        {
            var area = Ui.Img(root, "View", null, Color.clear); // catches drags between the cards
            view = area.rectTransform;
            Ui.Fill(view);
            var scroll = area.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.inertia = true;

            var viewport = Ui.New(view, "Viewport");
            Ui.Fill(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll.viewport = viewport;

            content = Ui.New(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            return scroll;
        }

        /// <summary>Grid of fixed cells (design px) in a scroll content.</summary>
        public static GridLayoutGroup Grid(RectTransform content, int columns, Vector2 cellD, float gapD, float padTopD, float padBottomD)
        {
            var g = content.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = cellD * Theme.K;
            g.spacing = new Vector2(gapD, gapD) * Theme.K;
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = columns;
            g.childAlignment = TextAnchor.UpperCenter;
            g.padding = new RectOffset(0, 0, Mathf.RoundToInt(Theme.D(padTopD)), Mathf.RoundToInt(Theme.D(padBottomD)));
            return g;
        }

        /// <summary>Places the view between ViewTop and the banner (or the safe-area bottom) + extraBottomD; scrolled to the top.</summary>
        public static void FitView(RectTransform view, ScrollRect scroll, float extraBottomD)
        {
            float banner = Ads.BannerHeightDesign;
            float bottom = (banner > 0f ? banner + BannerGap : BottomPad) + extraBottomD;
            view.offsetMin = new Vector2(0f, Theme.D(bottom));
            view.offsetMax = new Vector2(0f, -Theme.D(ViewTop));
            scroll.StopMovement();
            scroll.content.anchoredPosition = Vector2.zero;
        }

        /// <summary>Bottom of the content area above the banner (design px from the safe-area bottom).</summary>
        public static float BottomD()
        {
            float banner = Ads.BannerHeightDesign;
            return banner > 0f ? banner + BannerGap : BottomPad;
        }

        /// <summary>True when rt overlaps the viewport (grown by marginD at both ends).</summary>
        public static bool InView(RectTransform rt, RectTransform viewport, float marginD)
        {
            var a = new Vector3[4];
            var b = new Vector3[4];
            rt.GetWorldCorners(a);
            viewport.GetWorldCorners(b);
            float m = Theme.D(marginD) * viewport.lossyScale.y;
            return a[0].y <= b[1].y + m && a[1].y >= b[0].y - m;
        }

        /// <summary>Back to the screen that opened this one (Home when there is none or it is the same).</summary>
        public static void GoBack(string self)
        {
            string prev = GameApp.I.PreviousScreen;
            if (string.IsNullOrEmpty(prev) || prev == self || !GameApp.I.Show(prev)) GameApp.I.Show("ScreenHome");
        }

        // ------------------------------------------------------------------ coin pill

        /// <summary>Panel pill at the top right: gold coin + the coin count. Refresh on Save.CoinsChanged (OnShow / OnHide).</summary>
        public sealed class CoinPill
        {
            public readonly RectTransform Rt;
            readonly Text label;

            public CoinPill(RectTransform root)
            {
                var pill = Rounded(root, "Coins", Theme.Panel, PillH * 0.5f);
                pill.raycastTarget = false;
                Rt = pill.rectTransform;
                var coin = Coin(Rt, CoinD);
                Ui.PlaceD(coin, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(PillPadL, 0f), new Vector2(CoinD, CoinD));
                label = Ui.Label(Rt, "0", CoinFont, Theme.PanelText, Weight.Bold, TextAnchor.MiddleLeft);
                Ui.PlaceD(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(PillPadL + CoinD + PillGap, 0f), new Vector2(120f, PillH));
                Refresh();
            }

            public void Refresh()
            {
                if (Rt == null) return;
                label.text = Save.Coins.ToString();
                float w = PillPadL + CoinD + PillGap + label.preferredWidth / Theme.K + PillPadR;
                Ui.PlaceD(Rt, Ui.TR, new Vector2(1f, 0.5f), new Vector2(-Side, -(HeaderTop + BackD * 0.5f)), new Vector2(Mathf.Ceil(w), PillH));
            }

            /// <summary>Short horizontal shake (not enough coins).</summary>
            public void Shake()
            {
                if (Rt != null) GameApp.I.StartCoroutine(ShakeRoutine(Rt));
            }
        }

        static IEnumerator ShakeRoutine(RectTransform rt)
        {
            Vector2 p0 = rt.anchoredPosition;
            const float dur = 0.4f;
            float t = 0f;
            while (t < dur && rt != null)
            {
                t += Time.unscaledDeltaTime;
                float k = 1f - Mathf.Clamp01(t / dur);
                rt.anchoredPosition = p0 + new Vector2(Mathf.Sin(t * 50f) * Theme.D(7f) * k, 0f);
                yield return null;
            }
            if (rt != null) rt.anchoredPosition = p0;
        }

        // ------------------------------------------------------------------ toast

        /// <summary>Panel pill near the bottom that fades in, holds and fades out. Never takes taps.</summary>
        public sealed class Toast
        {
            readonly RectTransform rt;
            readonly CanvasGroup group;
            readonly Text text;
            Coroutine run;

            public Toast(RectTransform root)
            {
                var pill = Rounded(root, "Toast", Theme.Panel, 20f);
                pill.raycastTarget = false;
                rt = pill.rectTransform;
                text = Ui.Label(rt, "", 18f, Theme.PanelText, Weight.Bold);
                Ui.Fill(text.rectTransform);
                group = pill.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;
                pill.gameObject.SetActive(false);
            }

            public void Show(string msg, float bottomD)
            {
                if (rt == null) return;
                text.text = msg;
                float w = Mathf.Max(170f, text.preferredWidth / Theme.K + 36f);
                Ui.PlaceD(rt, Ui.BC, new Vector2(0.5f, 0f), new Vector2(0f, bottomD), new Vector2(w, 40f));
                rt.SetAsLastSibling();
                if (run != null) GameApp.I.StopCoroutine(run);
                run = GameApp.I.StartCoroutine(Run());
            }

            IEnumerator Run()
            {
                rt.gameObject.SetActive(true);
                const float tin = 0.2f, hold = 1.4f, tout = 0.3f;
                float t = 0f;
                while (t < tin + hold + tout)
                {
                    if (rt == null) yield break;
                    t += Time.unscaledDeltaTime;
                    group.alpha = t < tin ? t / tin : t < tin + hold ? 1f : 1f - Mathf.Clamp01((t - tin - hold) / tout);
                    yield return null;
                }
                if (rt != null) rt.gameObject.SetActive(false);
                run = null;
            }

            public void Hide()
            {
                if (run != null) GameApp.I.StopCoroutine(run);
                run = null;
                if (rt != null) rt.gameObject.SetActive(false);
            }
        }
    }
}
