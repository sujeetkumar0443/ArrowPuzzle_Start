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
