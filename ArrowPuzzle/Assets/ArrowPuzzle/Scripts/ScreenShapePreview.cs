using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArrowPuzzle
{
    /// <summary>
    /// DEV ONLY (Editor / development builds): every base shape in level order with its thumbnail ("12. Owl"; tap =
    /// play that level), and a "jump to level N" box (any number: 250, 1000, ...) with quick buttons. Both start the
    /// game with ScreenGame.DevJump, so locks and the release cap are ignored for that visit. Opened from the dev list
    /// (PopupLongSamples: long-press the coin pill on Home). A release build goes straight back to Home.
    /// </summary>
    public class ScreenShapePreview : AppScreen
    {
        const float CardW = 110f, CardH = 138f, Gap = 10f, CardR = 16f, PreviewD = 90f, PanelH = 120f, PanelGap = 10f;
        static readonly int[] Quick = { 101, 250, 500, 1000 };

        ScrollRect scroll;
        RectTransform view, content, panel;
        InputField input;
        readonly List<RectTransform> slots = new List<RectTransform>();
        readonly List<RectTransform> holders = new List<RectTransform>();
        readonly List<GameObject> previews = new List<GameObject>();

        public static void Open() { GameApp.I.Show("ScreenShapePreview"); }

        protected override void Build()
        {
            ScreenKit.Background(Root);
            ScreenKit.Header(Root, "SHAPE PREVIEW", "Dev: tap a shape to play its level", () => GameApp.I.Show("ScreenHome"));
            scroll = ScreenKit.Scroll(Root, out view, out content);
            ScreenKit.Grid(content, 3, new Vector2(CardW, CardH), Gap, 6f, 12f);
            scroll.onValueChanged.AddListener(_ => EnsurePreviews());

            for (int i = 0; i < Campaign.ShapeCount; i++)
            {
                int level = Campaign.FirstLevel(i);
                var slot = Ui.New(content, "Shape_" + Campaign.Shapes[i]);
                var card = ScreenKit.Card(slot, CardR, 4f);
                ScreenKit.Tap(card, () => Jump(level));
                var holder = Ui.New(card.rectTransform, "Holder");
                Ui.PlaceD(holder, Ui.TC, new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(PreviewD, PreviewD));
                var name = Ui.Label(card.rectTransform, level + ". " + Campaign.Title(Campaign.Shapes[i]), 14f, Theme.Ink, Weight.Bold);
                Ui.PlaceD(name.rectTransform, Ui.TC, new Vector2(0.5f, 1f), new Vector2(0f, -(PreviewD + 12f)), new Vector2(CardW - 6f, 20f));
                slots.Add(slot);
                holders.Add(holder);
                previews.Add(null);
            }
            BuildPanel();
        }

        /// <summary>Bottom panel: number box + GO, then quick jumps.</summary>
        void BuildPanel()
        {
            var img = ScreenKit.Rounded(Root, "Jump", Theme.Panel, 20f);
            panel = img.rectTransform;

            var row = Ui.New(panel, "Row");
            Ui.PlaceD(row, Ui.TC, new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(390f - ScreenKit.Side * 2f - 24f, 48f));
            Ui.Row(row, 8f).childForceExpandWidth = false;

            var field = ScreenKit.Rounded(row, "Field", Theme.Card, 12f);
            Ui.Size(field, 0f, 48f).flexibleWidth = 1f;
            var text = Ui.Label(field.transform, "", 22f, Theme.Ink, Weight.Bold);
            Ui.Fill(text.rectTransform);
            text.supportRichText = false;
            var hint = Ui.Label(field.transform, "Jump to level...", 18f, Theme.InkSoft, Weight.Medium);
            Ui.Fill(hint.rectTransform);
            input = field.gameObject.AddComponent<InputField>();
            input.textComponent = text;
            input.placeholder = hint;
            input.targetGraphic = field;
            input.contentType = InputField.ContentType.IntegerNumber;
            input.characterLimit = 7;

            var go = Ui.Btn(row, "GO", Theme.Accent, Theme.AccentText, 20f, 12f, Theme.AccentShadow, 3f, Go);
            Ui.Size(go, 84f, 48f);

            var quick = Ui.New(panel, "Quick");
            Ui.PlaceD(quick, Ui.TC, new Vector2(0.5f, 1f), new Vector2(0f, -68f), new Vector2(390f - ScreenKit.Side * 2f - 24f, 40f));
            Ui.Row(quick, 6f).childForceExpandWidth = true;
            foreach (int q in Quick)
            {
                int lv = q;
                var b = Ui.Btn(quick, lv.ToString(), Theme.Bg, Theme.Ink, 16f, 12f, Theme.CardShadow, 2f, () => Jump(lv));
                Ui.Size(b, 0f, 40f).flexibleWidth = 1f;
            }
        }

        void Go()
        {
            int n;
            if (input == null || !int.TryParse(input.text, out n) || n < 1) return;
            Jump(n);
        }

        static void Jump(int level)
        {
            ScreenGame.DevJump = true;
            GameApp.I.CurrentLevel = level;
            GameApp.I.Show("ScreenGame");
        }

        void EnsurePreviews()
        {
            if (scroll == null || scroll.viewport == null) return;
            var theme = Theme.Current ?? Themes.Get(Themes.Default);
            for (int i = 0; i < slots.Count; i++)
            {
                if (previews[i] != null || !ScreenKit.InView(slots[i], scroll.viewport, CardH)) continue;
                previews[i] = LevelPreview.Create(holders[i], Campaign.Thumb(i), theme, new Vector2(PreviewD, PreviewD), false).gameObject;
                previews[i].transform.SetAsFirstSibling();
            }
        }

        System.Collections.IEnumerator PreviewsNextFrame()
        {
            yield return null; // the grid has laid out the cards
            EnsurePreviews();
        }

        public override void OnShow()
        {
            if (!Debug.isDebugBuild)
            {
                GameApp.I.Show("ScreenHome");
                return;
            }
            float bottom = ScreenKit.BottomD();
            Ui.PlaceD(panel, Ui.BC, new Vector2(0.5f, 0f), new Vector2(0f, bottom + 4f), new Vector2(390f - ScreenKit.Side * 2f, PanelH));
            ScreenKit.FitView(view, scroll, PanelH + PanelGap + 4f);
            GameApp.I.StartCoroutine(PreviewsNextFrame());
        }

        public override void OnHide()
        {
            for (int i = 0; i < previews.Count; i++)
            {
                if (previews[i] != null) Object.Destroy(previews[i]); // frees the thumbnail meshes
                previews[i] = null;
            }
        }

        public override bool OnBack()
        {
            GameApp.I.Show("ScreenHome");
            return true;
        }
    }
}
