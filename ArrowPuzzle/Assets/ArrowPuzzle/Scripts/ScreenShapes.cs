using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArrowPuzzle
{
    /// <summary>
    /// SHAPES gallery: header (back, title, live coin pill), subtitle, a scrolling 3-column grid of the 37 campaign shapes
    /// and a dark NEXT SHAPE panel under it. Cards: COMPLETE (green check, its round-0 level done) / Next (the next shape
    /// level coming: teal ring, "Level N") / locked (faded preview, lock badge, grey "Level N"). Thumbnails come from
    /// thumbs.txt, are created as cards scroll into view and freed on hide. Tap: unlocked = play its round-0 level
    /// (Campaign.FirstLevel, a replay once done); locked = toast.
    /// Open with ScreenShapes.Open(); back returns to the screen that opened it.
    /// </summary>
    public class ScreenShapes : AppScreen
    {
        const float CardW = 110f, CardH = 160f, Gap = 10f, CardR = 18f, EdgeD = 4f, Pad = 8f;
        const float PreviewD = 90f, RingW = 3f, BarH = 6f;
        const float PanelH = 92f, PanelGap = 10f, PanelR = 20f;
        static readonly Color Teal = Theme.Hex("#2EC4B6");

        class Card
        {
            public int shape;
            public RectTransform slot, holder;
            public GameObject preview;
            public bool locked;
        }

        ScreenKit.CoinPill coins;
        ScreenKit.Toast toast;
        ScrollRect scroll;
        RectTransform view, content, panel;
        Text panelTitle, panelLevel, panelHint;
        RectTransform panelFill;
        readonly List<Card> cards = new List<Card>();

        public static void Open() { GameApp.I.Show("ScreenShapes"); }

        protected override void Build()
        {
            ScreenKit.Background(Root);
            BannerPlaceholder.Create(Root);
            coins = ScreenKit.Header(Root, "SHAPES", "New shapes unlock as you play", () => ScreenKit.GoBack("ScreenShapes"));
            scroll = ScreenKit.Scroll(Root, out view, out content);
            ScreenKit.Grid(content, 3, new Vector2(CardW, CardH), Gap, 6f, 12f);
            scroll.onValueChanged.AddListener(_ => EnsurePreviews());
            BuildPanel();
            coins.Rt.SetAsLastSibling();
            toast = new ScreenKit.Toast(Root);
        }

        /// <summary>Cards are (re)built on every show, so progress and locks are always current.</summary>
        void BuildCards()
        {
            foreach (var c in cards) if (c.slot != null) Object.Destroy(c.slot.gameObject);
            cards.Clear();
            // the next shape coming = the first one whose round-0 level is not completed yet
            int next = -1;
            for (int i = 0; i < Campaign.ShapeCount && next < 0; i++) if (!Campaign.ShapeDone(i)) next = i;
            for (int i = 0; i < Campaign.ShapeCount; i++) cards.Add(BuildCard(i, next));
        }

        Card BuildCard(int i, int next)
        {
            var c = new Card { shape = i };
            c.slot = Ui.New(content, "Shape_" + Campaign.Shapes[i]);
            bool unlocked = Campaign.IsShapeUnlocked(i);
            bool complete = Campaign.ShapeDone(i);
            bool isNext = i == next && !complete;
            c.locked = !unlocked;

            if (isNext) ScreenKit.Ring(c.slot, Teal, CardR, RingW);
            var card = ScreenKit.Card(c.slot, CardR, EdgeD);
            var crt = card.rectTransform;
            int shape = i;
            ScreenKit.Tap(card, () => OnTap(shape));

            c.holder = Ui.New(crt, "Holder");
            Ui.PlaceD(c.holder, Ui.TC, new Vector2(0.5f, 1f), new Vector2(0f, -Pad), new Vector2(PreviewD, PreviewD));

            if (c.locked)
            {
                var badge = Ui.Img(crt, "Lock", Theme.CircleHD, Theme.Panel);
                badge.raycastTarget = false;
                Ui.PlaceD(badge.rectTransform, Ui.TC, new Vector2(0.5f, 0.5f), new Vector2(0f, -(Pad + PreviewD * 0.5f)), new Vector2(36f, 36f));
                var li = Icons.Make(badge.rectTransform, Icon.Lock, Theme.PanelText, 18f);
                Ui.PlaceD(li, Ui.C, Vector2.zero, new Vector2(18f, 18f));
            }

            var name = Ui.Label(crt, Campaign.Title(Campaign.Shapes[i]), 16f, c.locked ? Theme.InkSoft : Theme.Ink, Weight.Bold);
            float nameY = Pad + PreviewD + 4f;
            Ui.PlaceD(name.rectTransform, Ui.TC, new Vector2(0.5f, 1f), new Vector2(0f, -nameY), new Vector2(CardW - 8f, 22f));

            float stateY = nameY + 24f;
            if (complete)
            {
                var row = Ui.New(crt, "Complete");
                Ui.PlaceD(row, Ui.TC, new Vector2(0.5f, 1f), new Vector2(0f, -stateY), new Vector2(CardW - 8f, 20f));
                Ui.Row(row, 4f);
                var ic = Icons.Make(row, Icon.Check, Theme.Success, 16f, 3f);
                var le = ic.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = le.preferredHeight = Theme.D(16f);
                Ui.Label(row, "COMPLETE", 13f, Theme.Success, Weight.Bold);
            }
            else
            {
                // Next: teal ring, "Level N" in Ink; locked: grey; reached but not done (a skip): InkSoft
                Color col = isNext ? Theme.Ink : c.locked ? Theme.LockedLevelIcon : Theme.InkSoft;
                var l = Ui.Label(crt, "Level " + Campaign.FirstLevel(i), 14f, col, isNext ? Weight.Bold : Weight.Semi);
                Ui.PlaceD(l.rectTransform, Ui.TC, new Vector2(0.5f, 1f), new Vector2(0f, -stateY), new Vector2(CardW - 8f, 20f));
            }
            return c;
        }

        /// <summary>Dark panel under the grid: NEXT SHAPE, level progress towards it, levels left.</summary>
        void BuildPanel()
        {
            var img = ScreenKit.Rounded(Root, "NextShape", Theme.Panel, PanelR);
            img.raycastTarget = false;
            panel = img.rectTransform;
            var edge = img.gameObject.AddComponent<Shadow>();
            edge.effectColor = Theme.PanelEdge;
            edge.effectDistance = new Vector2(0f, -Theme.D(4f));
            edge.useGraphicAlpha = false;

            panelTitle = Ui.Label(panel, "", 17f, Theme.PanelText, Weight.Bold, TextAnchor.MiddleLeft);
            Ui.PlaceD(panelTitle.rectTransform, Ui.TL, new Vector2(0f, 0.5f), new Vector2(16f, -24f), new Vector2(200f, 24f));
            panelLevel = Ui.Label(panel, "", 15f, Theme.PanelText, Weight.Semi, TextAnchor.MiddleRight);
            Ui.PlaceD(panelLevel.rectTransform, Ui.TR, new Vector2(1f, 0.5f), new Vector2(-16f, -24f), new Vector2(140f, 24f));

            var track = ScreenKit.Rounded(panel, "Track", Theme.PanelEdge, 4f);
            track.raycastTarget = false;
            Ui.PlaceD(track.rectTransform, Ui.TL, new Vector2(0f, 0.5f), new Vector2(16f, -48f), new Vector2(390f - ScreenKit.Side * 2f - 32f, 8f));
            var fill = ScreenKit.Rounded(track.rectTransform, "Fill", Teal, 4f);
            fill.raycastTarget = false;
            panelFill = fill.rectTransform;

            var hint = Ui.Label(panel, "", 14f, Theme.PanelText, Weight.Medium, TextAnchor.MiddleLeft);
            hint.color = new Color(Theme.PanelText.r, Theme.PanelText.g, Theme.PanelText.b, 0.75f);
            panelHint = hint;
            Ui.PlaceD(hint.rectTransform, Ui.TL, new Vector2(0f, 0.5f), new Vector2(16f, -70f), new Vector2(300f, 22f));
        }

        void RefreshPanel()
        {
            int unlocked = Save.UnlockedGlobal;
            int next = Campaign.NextShape(unlocked); // the next shape not yet reached
            float trackW = 390f - ScreenKit.Side * 2f - 32f;
            float k;
            if (next >= Campaign.ShapeCount)
            {
                panelTitle.text = "ALL SHAPES UNLOCKED";
                panelLevel.text = "Level " + unlocked + " / " + Campaign.TotalLevels;
                panelHint.text = Campaign.TotalLevels - unlocked > 0 ? (Campaign.TotalLevels - unlocked) + " levels to the end" : "You reached the last level";
                k = unlocked / (float)Campaign.TotalLevels;
            }
            else
            {
                // bar: from the previous shape level (or level 0) to this shape's level
                int first = Campaign.FirstLevel(next), from = next > 0 ? Campaign.FirstLevel(next - 1) : 0;
                int left = Mathf.Max(0, first - unlocked);
                panelTitle.text = "NEXT SHAPE: " + Campaign.Title(Campaign.Shapes[next]).ToUpperInvariant();
                panelLevel.text = "Level " + unlocked + " / " + first;
                panelHint.text = left + (left == 1 ? " more level" : " more levels");
                k = (unlocked - from) / (float)(first - from);
            }
            panelFill.anchorMin = panelFill.anchorMax = panelFill.pivot = new Vector2(0f, 0.5f);
            panelFill.anchoredPosition = Vector2.zero;
            panelFill.sizeDelta = new Vector2(Theme.D(Mathf.Max(8f, trackW * Mathf.Clamp01(k))), Theme.D(8f));
        }

        // ------------------------------------------------------------------ thumbnails (lazy)

        /// <summary>Creates the previews of the cards that are in (or near) the view; each one once per visit.</summary>
        void EnsurePreviews()
        {
            if (scroll == null || scroll.viewport == null) return;
            var theme = Theme.Current ?? Themes.Get(Themes.Default);
            foreach (var c in cards)
            {
                if (c.preview != null || c.holder == null) continue;
                if (!ScreenKit.InView(c.slot, scroll.viewport, CardH)) continue;
                var lv = Campaign.Thumb(c.shape);
                c.preview = LevelPreview.Create(c.holder, lv, theme, new Vector2(PreviewD, PreviewD), c.locked).gameObject;
                c.preview.transform.SetAsFirstSibling();
            }
        }

        System.Collections.IEnumerator PreviewsNextFrame()
        {
            yield return null; // the grid has laid out the cards
            EnsurePreviews();
        }

        // ------------------------------------------------------------------ tap

        void OnTap(int shape)
        {
            if (!Campaign.IsShapeUnlocked(shape))
            {
                toast.Show("Reach level " + Campaign.FirstLevel(shape) + " to unlock", ScreenKit.BottomD() + PanelH + PanelGap + 14f);
                return;
            }
            GameApp.I.CurrentLevel = Campaign.FirstLevel(shape); // its round-0 level (a replay once it is done)
            GameApp.I.Show("ScreenGame");
        }

        // ------------------------------------------------------------------ show / hide

        public override void OnShow()
        {
            Save.CoinsChanged -= OnCoins;
            Save.CoinsChanged += OnCoins;
            Ads.BannerHeightChanged -= Layout;
            Ads.BannerHeightChanged += Layout;
            Ads.Banner(true, "ScreenShapes");
            coins.Refresh();
            BuildCards();
            RefreshPanel();
            Layout();
            GameApp.I.StartCoroutine(PreviewsNextFrame());
        }

        public override void OnHide()
        {
            Save.CoinsChanged -= OnCoins;
            Ads.BannerHeightChanged -= Layout;
            Ads.Banner(false, "ScreenShapes");
            toast.Hide();
            foreach (var c in cards) if (c.slot != null) Object.Destroy(c.slot.gameObject); // frees the thumbnail meshes
            cards.Clear();
        }

        public override bool OnBack()
        {
            ScreenKit.GoBack("ScreenShapes");
            return true;
        }

        void OnCoins() { coins.Refresh(); }

        void Layout()
        {
            float bottom = ScreenKit.BottomD();
            Ui.PlaceD(panel, Ui.BC, new Vector2(0.5f, 0f), new Vector2(0f, bottom + 4f), new Vector2(390f - ScreenKit.Side * 2f, PanelH));
            ScreenKit.FitView(view, scroll, PanelH + PanelGap + 4f);
        }
    }
}
