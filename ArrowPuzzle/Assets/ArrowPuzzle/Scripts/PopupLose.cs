using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ArrowPuzzle
{
    /// <summary>
    /// Out of lives popup: lost hearts, watch an ad for +1 life, restart the level or go Home.
    /// Rises in (card, then message, then buttons); the Watch ad button pulses once visible. Unscaled time.
    /// </summary>
    public class PopupLose : AppScreen
    {
        public override bool IsPopup { get { return true; } }

        // rise animation (s since OnShow / design px)
        const float RiseFrom = 90f, CardTime = 0.6f;
        const float MsgT0 = 0.75f, MsgTime = 0.3f;
        const float ButtonsT0 = 0.85f, ButtonsTime = 0.4f;
        const float PulseAmp = 0.035f, PulseSpeed = 5f;
        const float WaitAlpha = 0.55f; // Watch ad button while the video loads / plays
        const string WatchText = "Watch ad · +1 life", WaitText = "Loading video...";

        RectTransform card, adBox;
        CanvasGroup cardGroup, msgGroup;
        CanvasGroup[] buttonGroups;
        Coroutine run;
        Text adLabel, status;
        bool waiting, used; // a video request is running / this loss was already revived
        int request;        // bumped on every show / request, so a late ad result can't touch a newer state

        protected override void Build()
        {
            // shared popup card: 326 wide, white, radius 32, centered, edge 10 of PopupShadow, height follows the content
            var cardImg = Ui.Img(Root, "Card", Theme.Rounded, Theme.Card, 32f);
            card = cardImg.rectTransform;
            Ui.PlaceD(card, Ui.C, Vector2.zero, new Vector2(326f, 0f));
            var edge = cardImg.gameObject.AddComponent<Shadow>();
            edge.effectColor = Theme.PopupShadow;
            edge.effectDistance = new Vector2(0f, -Theme.D(10f));
            edge.useGraphicAlpha = false;
            Ui.Column(card, 24f, 34f, 26f, 14f);
            cardImg.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            cardGroup = cardImg.gameObject.AddComponent<CanvasGroup>();

            // 1. title
            Ui.Label(card, "Out of lives", 34f, Theme.Ink, Weight.Bold);

            // 2. three lost hearts of 52, gap 8 (all grey)
            var hearts = Ui.New(card, "Hearts");
            Ui.Size(hearts, 0f, 52f);
            for (int i = 0; i < 3; i++)
            {
                var h = Icons.Make(hearts, Icon.Heart, Theme.HeartLost, 52f);
                Ui.PlaceD(h, Ui.C, new Vector2((i - 1) * 60f, 0f), new Vector2(52f, 52f));
            }

            // 3. message, wrapped to the card width
            var msg = Ui.Label(card, "Watch a short video to keep going with 1 extra life.", 19f, Theme.InkSoft, Weight.Medium);
            msg.horizontalOverflow = HorizontalWrapMode.Wrap;
            msg.lineSpacing = 1.35f / 1.2f; // CSS line-height 1.35; uGUI spacing is relative to the font's own ~1.2 line
            msgGroup = msg.gameObject.AddComponent<CanvasGroup>();

            // 4. Watch ad (margin-top 8), pulses inside its box so the press squash still works
            adBox = Ui.New(card, "AdBox");
            Ui.Column(adBox, 0f, 8f, 0f, 0f);
            var ad = Ui.Btn(adBox, WatchText, Theme.Teal, Theme.Ink, 22f, 24f, Theme.TealShadow, 5f,
                            WatchAd, Icon.Play, 26f);
            Ui.Size(ad, 0f, 72f);
            adLabel = ad.GetComponentInChildren<Text>();
            Ui.AddShimmer((RectTransform)ad.transform, 24f, ButtonsT0 + ButtonsTime); // first sweep once visible

            // "Video not ready", only after a failed / skipped video
            status = Ui.Label(card, "Video not ready, try again in a moment", 16f, Theme.Error, Weight.Medium);
            status.horizontalOverflow = HorizontalWrapMode.Wrap;
            status.gameObject.SetActive(false);

            // 5. Restart level
            var restart = Ui.Btn(card, "Restart level", Theme.Bg, Theme.Ink, 20f, 20f, Theme.CardShadow, 0f, () =>
            {
                GameApp.I.Close("PopupLose");
                var game = GameApp.I.Get<ScreenGame>();
                if (game != null) game.Restart();
            }, Icon.Restart, 24f, Weight.Semi);
            Ui.Size(restart, 0f, 60f);

            // 6. Home, text only (transparent button)
            var home = Ui.Btn(card, "Home", Color.clear, Theme.InkSoft, 19f, 20f, Theme.CardShadow, 0f,
                              () => GameApp.I.Show("ScreenHome"), null, 24f, Weight.Semi);
            Ui.Size(home, 0f, 52f);

            buttonGroups = new[]
            {
                adBox.gameObject.AddComponent<CanvasGroup>(),
                restart.gameObject.AddComponent<CanvasGroup>(),
                home.gameObject.AddComponent<CanvasGroup>(),
            };
        }

        public override void OnShow()
        {
            request++;
            used = false; // a new loss: the ad may revive once
            SetWaiting(false);
            status.gameObject.SetActive(false);
            Apply(0f);
            if (run != null) GameApp.I.StopCoroutine(run);
            run = GameApp.I.StartCoroutine(Run());
        }

        public override void OnHide()
        {
            request++;
            waiting = false;
            if (run != null) GameApp.I.StopCoroutine(run);
            run = null;
            Apply(CardTime + ButtonsT0 + ButtonsTime); // leave it at rest
            adBox.localScale = Vector3.one;
        }

        /// <summary>Closing with back would leave the board without lives, so back goes Home.</summary>
        public override bool OnBack()
        {
            if (waiting) return true; // the video request decides first
            GameApp.I.Show("ScreenHome");
            return true;
        }

        IEnumerator Run()
        {
            float t = 0f;
            while (true) // keeps pulsing while the popup is open
            {
                t += Time.unscaledDeltaTime;
                Apply(t);
                yield return null;
            }
        }

        void Apply(float t)
        {
            float k = EaseOutCubic(Mathf.Clamp01(t / CardTime));
            card.anchoredPosition = new Vector2(0f, -Theme.D(RiseFrom) * (1f - k));
            cardGroup.alpha = k;

            msgGroup.alpha = Mathf.Clamp01((t - MsgT0) / MsgTime);

            float b = Mathf.Clamp01((t - ButtonsT0) / ButtonsTime);
            foreach (var g in buttonGroups)
            {
                g.alpha = b;
                g.interactable = g.blocksRaycasts = b >= 1f && !waiting; // only tappable once visible, locked while a video runs
            }
            if (waiting) buttonGroups[0].alpha = b * WaitAlpha;
            if (used) buttonGroups[0].interactable = buttonGroups[0].blocksRaycasts = false;

            float visibleFor = t - (ButtonsT0 + ButtonsTime);
            float pulse = visibleFor > 0f ? 1f + PulseAmp * Mathf.Sin(visibleFor * PulseSpeed) : 1f;
            adBox.localScale = new Vector3(pulse, pulse, 1f);
        }

        void SetWaiting(bool on)
        {
            waiting = on;
            adLabel.text = on ? WaitText : WatchText;
        }

        void WatchAd()
        {
            if (waiting || used) return;
            int id = ++request;
            status.gameObject.SetActive(false);
            SetWaiting(true);
            Ads.ShowRewarded(() => OnRewarded(id), () => OnFailed(id));
        }

        /// <summary>Reward earned: revive this loss once (the same +1 life as before) and close.</summary>
        void OnRewarded(int id)
        {
            if (id != request || used || !GameApp.I.IsOpen<PopupLose>()) return;
            used = true;
            SetWaiting(false);
            GameApp.I.Close("PopupLose");
            var game = GameApp.I.Get<ScreenGame>();
            if (game != null) game.Board.ReviveLife(); // ReviveLife resets the collapse and scales the arrows in
        }

        void OnFailed(int id)
        {
            if (id != request) return;
            SetWaiting(false);
            status.gameObject.SetActive(true); // the popup stays: Restart / Home still work
        }

        static float EaseOutCubic(float x)
        {
            float a = 1f - x;
            return 1f - a * a * a;
        }
    }
}
