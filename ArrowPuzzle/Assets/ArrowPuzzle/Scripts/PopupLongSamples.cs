using UnityEngine;
using UnityEngine.UI;

namespace ArrowPuzzle
{
    /// <summary>TEMPORARY dev list "Long samples 1..N" (LongSamplesDev). Tapping one plays it in ScreenGame.</summary>
    public class PopupLongSamples : AppScreen
    {
        public override bool IsPopup { get { return true; } }

        protected override void Build()
        {
            var cardImg = Ui.Img(Root, "Card", Theme.Rounded, Theme.Card, 32f);
            var card = cardImg.rectTransform;
            Ui.PlaceD(card, Ui.C, Vector2.zero, new Vector2(326f, 0f));
            Ui.Column(card, 24f, 28f, 24f, 10f);
            cardImg.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Ui.Label(card, "Long samples", 30f, Theme.Ink, Weight.Bold);

            int n = LongSamplesDev.Count;
            for (int i = 1; i <= n; i++)
            {
                int k = i;
                var d = ShapeLevels.GetFile(LongSamplesDev.File, k);
                string name = d != null ? k + ". " + d.shape + "  (" + d.pieces.Count + " arrows)" : k + ". missing";
                var b = Ui.Btn(card, name, Theme.Bg, Theme.Ink, 19f, 16f, Theme.CardShadow, 3f, () => Play(k));
                Ui.Size(b, 0f, 48f);
            }

            // dev entry points to the Themes / Shapes screens (Step 8 puts real buttons on Home)
            var navRow = Ui.New(card, "ScreensRow");
            Ui.Size(navRow, 0f, 44f);
            Ui.Row(navRow, 8f).childForceExpandWidth = true;
            var themesBtn = Ui.Btn(navRow, "Themes screen", Theme.Panel, Theme.PanelText, 16f, 14f, Theme.PanelEdge, 3f, ScreenThemes.Open);
            Ui.Size(themesBtn, 0f, 44f).flexibleWidth = 1f;
            var shapesBtn = Ui.Btn(navRow, "Shapes screen", Theme.Panel, Theme.PanelText, 16f, 14f, Theme.PanelEdge, 3f, ScreenShapes.Open);
            Ui.Size(shapesBtn, 0f, 44f).flexibleWidth = 1f;

            // dev theme switch: applies at once, no purchase check, does not change what the player owns
            Ui.Label(card, "Theme (dev)", 17f, Theme.InkSoft, Weight.Semi);
            for (int r = 0; r < 2; r++)
            {
                var rowRt = Ui.New(card, "ThemeRow" + r);
                Ui.Size(rowRt, 0f, 40f);
                Ui.Row(rowRt, 6f).childForceExpandWidth = true;
                for (int c = 0; c < 3; c++)
                {
                    var t = Themes.All[r * 3 + c];
                    string id = t.Id;
                    bool on = Theme.Current != null && Theme.Current.Id == id;
                    var tb = Ui.Btn(rowRt, t.Name, on ? Theme.Accent : Theme.Bg, on ? Theme.AccentText : Theme.Ink, 15f, 12f,
                                    Theme.CardShadow, 2f, () => Theme.Apply(id));
                    Ui.Size(tb, 0f, 40f).flexibleWidth = 1f;
                }
            }

            // dev theme tour: all themes, 2 s each, on the screen under this list (it closes); tap again to stop
            var tour = Ui.Btn(card, ThemeTour.Running ? "Stop theme tour" : "Theme tour", Theme.Panel, Theme.PanelText, 16f, 14f,
                              Theme.PanelEdge, 3f, () =>
                              {
                                  GameApp.I.Close("PopupLongSamples");
                                  ThemeTour.Toggle();
                              });
            Ui.Size(tour, 0f, 44f);

            var close = Ui.Btn(card, "Close", Color.clear, Theme.InkSoft, 19f, 16f, Theme.CardShadow, 0f,
                               () => GameApp.I.Close("PopupLongSamples"));
            Ui.Size(close, 0f, 44f);
        }

        void Play(int n)
        {
            LongSamplesDev.Start(n);
            GameApp.I.Close("PopupLongSamples");
            GameApp.I.Show("ScreenGame");
        }

        public override bool OnBack()
        {
            GameApp.I.Close("PopupLongSamples");
            return true;
        }
    }
}
