using UnityEngine;
using UnityEngine.UI;

namespace ArrowPuzzle
{
    /// <summary>Pause popup: Resume, Restart, Sound switch, Home. Freezes the game (timeScale 0) while open.</summary>
    public class PopupPause : AppScreen
    {
        public override bool IsPopup { get { return true; } }

        SwitchView sound;

        protected override void Build()
        {
            // shared popup card: 326 wide, white, radius 32, centered, edge 10 of PopupShadow, height follows the content
            var card = Ui.Img(Root, "Card", Theme.Rounded, Theme.Card, 32f);
            Ui.PlaceD(card.rectTransform, Ui.C, Vector2.zero, new Vector2(326f, 0f));
            var edge = card.gameObject.AddComponent<Shadow>();
            edge.effectColor = Theme.PopupShadow;
            edge.effectDistance = new Vector2(0f, -Theme.D(10f));
            edge.useGraphicAlpha = false;
            Ui.Column(card.rectTransform, 24f, 30f, 26f, 14f);
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 1. title, with 4 extra below
            var titleBox = Ui.New(card.transform, "Title");
            Ui.Column(titleBox, 0f, 0f, 4f, 0f);
            Ui.Label(titleBox, "Paused", 36f, Theme.Ink, Weight.Bold);

            // 2. RESUME
            var resume = Ui.Btn(card.transform, "RESUME", Theme.Accent, Theme.Card, 24f, 24f, Theme.AccentShadow, 5f,
                                () => GameApp.I.Close("PopupPause"), Icon.Play, 24f);
            Ui.Size(resume, 0f, 72f);

            // 3. Restart
            var restart = Ui.Btn(card.transform, "Restart", Theme.Bg, Theme.Ink, 20f, 20f, Theme.CardShadow, 0f, () =>
            {
                GameApp.I.Close("PopupPause");
                var game = GameApp.I.Get<ScreenGame>();
                if (game != null) game.Restart();
            }, Icon.Restart, 24f, Weight.Semi);
            Ui.Size(restart, 0f, 60f);

            // 4. Sound row: icon + label left, switch right
            var row = Ui.Img(card.transform, "SoundRow", Theme.Rounded, Theme.Bg, 20f);
            row.raycastTarget = false;
            Ui.Size(row, 0f, 60f);

            var icon = Icons.Make(row.transform, Icon.Sound, Theme.Ink, 24f);
            Ui.PlaceD(icon, new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(24f, 24f));

            var label = Ui.Label(row.transform, "Sound", 20f, Theme.Ink, Weight.Semi, TextAnchor.MiddleLeft);
            Ui.PlaceD(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(20f + 24f + 10f, 0f), new Vector2(160f, 60f));

            sound = Ui.Switch(row.transform, () => Save.Sound, v => Save.Sound = v);
            Ui.PlaceD((RectTransform)sound.transform, new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(52f, 30f));

            // 5. Home
            var home = Ui.Btn(card.transform, "Home", Theme.Bg, Theme.Ink, 20f, 20f, Theme.CardShadow, 0f,
                              () => GameApp.I.Show("ScreenHome"), Icon.Home, 24f, Weight.Semi);
            Ui.Size(home, 0f, 60f);
        }

        public override void OnShow()
        {
            Time.timeScale = 0f;
            sound.Refresh();
        }

        /// <summary>Every way of closing (Resume, Restart, Home, back button) ends here.</summary>
        public override void OnHide()
        {
            Time.timeScale = 1f;
        }
    }
}
