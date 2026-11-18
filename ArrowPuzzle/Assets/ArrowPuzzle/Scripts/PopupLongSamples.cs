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
