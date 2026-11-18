using UnityEngine;
using UnityEngine.UI;

namespace ArrowPuzzle
{
    /// <summary>
    /// End of this release (Campaign.ReleaseLevelCap): a friendly "More levels coming soon" card with the stars
    /// collected, REPLAY LEVEL N (the last level) and HOME. Shown by NEXT / Skip past the cap and by Home's PLAY once
    /// every level is done. Raising the cap needs no change here.
    /// </summary>
    public class ScreenComingSoon : AppScreen
    {
        const float CardW = 342f, CardR = 28f;

        Text body, stars;
        Button replay;
        Text replayLabel;

        protected override void Build()
        {
            ScreenKit.Background(Root);

            var cardImg = Ui.Img(Root, "Card", Theme.Rounded, Theme.Card, CardR);
            var card = cardImg.rectTransform;
            Ui.PlaceD(card, Ui.C, Vector2.zero, new Vector2(CardW, 0f));
            Ui.Column(card, 24f, 30f, 24f, 14f);
            cardImg.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var edge = cardImg.gameObject.AddComponent<Shadow>();
            edge.effectColor = Theme.CardShadow;
            edge.effectDistance = new Vector2(0f, -Theme.D(4f));
            edge.useGraphicAlpha = false;

            var title = Ui.Label(card, "More levels\ncoming soon", 30f, Theme.Ink, Weight.Bold);
            Ui.Size(title, 0f, 84f);

            body = Ui.Label(card, "", 18f, Theme.InkSoft, Weight.Medium);
            Ui.Size(body, 0f, 52f);

            stars = Ui.Label(card, "", 20f, Theme.Ink, Weight.Semi);
            Ui.Size(stars, 0f, 30f);

            replay = Ui.Btn(card, "", Theme.Accent, Theme.AccentText, 20f, 18f, Theme.AccentShadow, 4f, Replay);
            Ui.Size(replay, 0f, 56f);
            replayLabel = replay.GetComponentInChildren<Text>();

            var home = Ui.Btn(card, "HOME", Theme.Bg, Theme.Ink, 20f, 18f, Theme.CardShadow, 3f, () => GameApp.I.Show("ScreenHome"));
            Ui.Size(home, 0f, 56f);
        }

        int LastLevel { get { return Mathf.Max(1, Campaign.ReleaseLevelCap); } }

        public override void OnShow()
        {
            body.text = "You finished all " + LastLevel + " levels!\nNew shapes are on the way.";
            stars.text = Save.TotalStars() + " stars collected";
            if (replayLabel != null) replayLabel.text = "REPLAY LEVEL " + LastLevel;
        }

        void Replay()
        {
            GameApp.I.CurrentLevel = LastLevel;
            GameApp.I.Show("ScreenGame");
        }

        public override bool OnBack()
        {
            GameApp.I.Show("ScreenHome");
            return true;
        }
    }
}
