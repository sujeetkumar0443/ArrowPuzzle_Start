using UnityEngine;
using UnityEngine.UI;

namespace ArrowPuzzle
{
    /// <summary>Scrolling text from Resources/Music/credits.txt with a Back button. Settings hides its row when there is no text.</summary>
    public class PopupMusicCredits : AppScreen
    {
        public override bool IsPopup { get { return true; } }

        const float CardW = 326f, CardH = 520f, Side = 24f, TitleTop = 28f, TitleH = 40f;
        const float BackH = 60f, BackBottom = 26f, Gap = 14f;
        const int MaxChars = 15000; // one uGUI Text can't draw much more

        static string text;

        RectTransform content;
        Text body;
        ScrollRect scroll;

        /// <summary>The credits text, empty when the file is missing or blank. Read once.</summary>
        public static string CreditsText
        {
            get
            {
                if (text != null) return text;
                var asset = Resources.Load<TextAsset>("Music/credits");
                text = asset != null ? asset.text.Trim() : "";
                if (asset != null) Resources.UnloadAsset(asset);
                if (text.Length > MaxChars) text = text.Substring(0, MaxChars);
                return text;
            }
        }

        protected override void Build()
        {
            var card = Ui.Img(Root, "Card", Theme.Rounded, Theme.Card, 32f);
            Ui.PlaceD(card.rectTransform, Ui.C, Vector2.zero, new Vector2(CardW, CardH));
            var edge = card.gameObject.AddComponent<Shadow>();
            edge.effectColor = Theme.PopupShadow;
            edge.effectDistance = new Vector2(0f, -Theme.D(10f));
            edge.useGraphicAlpha = false;

            var title = Ui.Label(card.transform, "Music credits", 30f, Theme.Ink, Weight.Bold);
            Ui.PlaceD(title.rectTransform, Ui.TC, new Vector2(0f, -TitleTop), new Vector2(CardW - Side * 2f, TitleH));

            var view = Ui.Img(card.transform, "View", null, Color.clear); // catches drags
            var viewRt = view.rectTransform;
            Ui.Fill(viewRt);
            viewRt.offsetMin = new Vector2(Theme.D(Side), Theme.D(BackBottom + BackH + Gap));
            viewRt.offsetMax = new Vector2(-Theme.D(Side), -Theme.D(TitleTop + TitleH + Gap));
            scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;

            var viewport = Ui.New(viewRt, "Viewport");
            Ui.Fill(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll.viewport = viewport;

            body = Ui.Label(viewport, "", 17f, Theme.InkSoft, Weight.Medium, TextAnchor.UpperLeft);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            content = body.rectTransform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            body.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;

            var back = Ui.Btn(card.transform, "Back", Theme.Bg, Theme.Ink, 20f, 20f, Theme.CardShadow, 0f,
                              () => GameApp.I.Close("PopupMusicCredits"), Icon.Back, 24f, Weight.Semi);
            Ui.PlaceD((RectTransform)back.transform, Ui.BC, new Vector2(0f, BackBottom), new Vector2(CardW - Side * 2f, BackH));
        }

        public override void OnShow()
        {
            body.text = CreditsText;
            scroll.StopMovement();
            content.anchoredPosition = Vector2.zero;
        }
    }
}
