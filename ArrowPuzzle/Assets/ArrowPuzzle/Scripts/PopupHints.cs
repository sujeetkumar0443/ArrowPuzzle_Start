using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ArrowPuzzle
{
    /// <summary>
    /// Out of hints: watch a rewarded video for +3 hints, or "No thanks". Opened by ScreenGame's hint button when hints
    /// are 0, and by the Home hints pill (pill or +) at any count (title "Get more hints" while hints are left). The reward adds
    /// the hints (in the game: badge + toast) and closes; it never shows a hint by itself. A failed or
    /// skipped video keeps the popup open with a short message. Rises in like the other popups. Unscaled time.
    /// </summary>
    public class PopupHints : AppScreen
    {
        public override bool IsPopup { get { return true; } }

        const int Reward = 3;
        const string WaitText = "Loading video...";
        static readonly string WatchText = "Watch video  +" + Reward;

        // rise animation (s since OnShow / design px)
        const float RiseFrom = 90f, CardTime = 0.5f;
        const float ButtonsT0 = 0.3f, ButtonsTime = 0.3f;
        const float WaitAlpha = 0.55f;   // Watch button while the video loads / plays

        RectTransform card;
        CanvasGroup cardGroup, watchGroup, noGroup;
        Button watch;
        Text status, watchLabel, title;
        Coroutine run;
        bool waiting;
        int request; // bumped on every show / request, so a late ad result can't touch a newer state

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
            title = Ui.Label(card, "Out of hints", 34f, Theme.Ink, Weight.Bold); // "Get more hints" with hints left (OnShow)

            // 2. message, wrapped to the card width
            var msg = Ui.Label(card, "Watch a short video to get " + Reward + " more hints.", 19f, Theme.InkSoft, Weight.Medium);
            msg.horizontalOverflow = HorizontalWrapMode.Wrap;
            msg.lineSpacing = 1.35f / 1.2f; // CSS line-height 1.35; uGUI spacing is relative to the font's own ~1.2 line

            // 3. Watch video (margin-top 8)
            var watchBox = Ui.New(card, "WatchBox");
            Ui.Column(watchBox, 0f, 8f, 0f, 0f);
            watch = Ui.Btn(watchBox, WatchText, Theme.Teal, Theme.Ink, 22f, 24f, Theme.TealShadow, 5f,
                           WatchVideo, Icon.Play, 22f);
            Ui.Size(watch, 0f, 72f);
            watchLabel = watch.GetComponentInChildren<Text>();
            watchGroup = watchBox.gameObject.AddComponent<CanvasGroup>();

            // 4. "Video not ready" message, only after a failed / skipped video
            status = Ui.Label(card, "Video not ready, try again in a moment", 16f, Theme.Error, Weight.Medium);
            status.horizontalOverflow = HorizontalWrapMode.Wrap;
            status.gameObject.SetActive(false);

            // 5. No thanks, text only (transparent button)
            var no = Ui.Btn(card, "No thanks", Color.clear, Theme.InkSoft, 19f, 20f, Theme.CardShadow, 0f,
                            () => GameApp.I.Close("PopupHints"), null, 24f, Weight.Semi);
            Ui.Size(no, 0f, 52f);
            noGroup = no.gameObject.AddComponent<CanvasGroup>();
        }

        public override void OnShow()
        {
            request++;
            // the game opens this only at 0 hints ("Out of hints"); Home's hints pill opens it at any count
            title.text = Save.Hints > 0 ? "Get more hints" : "Out of hints";
            SetWaiting(false);
            status.gameObject.SetActive(false);
            Apply(0f);
            if (run != null) GameApp.I.StopCoroutine(run);
            run = GameApp.I.StartCoroutine(Run());
        }

        public override void OnHide()
        {
            request++; // a video still loading may reward later (GrantHints), but no longer touches this popup
            if (run != null) GameApp.I.StopCoroutine(run);
            run = null;
            waiting = false;
            Apply(CardTime + ButtonsT0 + ButtonsTime); // leave it at rest
        }

        IEnumerator Run()
        {
            float t = 0f;
            float end = CardTime + ButtonsT0 + ButtonsTime;
            while (t < end)
            {
                t += Time.unscaledDeltaTime;
                Apply(t);
                yield return null;
            }
            Apply(end);
            run = null;
        }

        void Apply(float t)
        {
            float k = EaseOutCubic(Mathf.Clamp01(t / CardTime));
            card.anchoredPosition = new Vector2(0f, -Theme.D(RiseFrom) * (1f - k));
            cardGroup.alpha = k;

            float b = Mathf.Clamp01((t - ButtonsT0) / ButtonsTime);
            bool ready = b >= 1f; // only tappable once visible
            watchGroup.alpha = b * (waiting ? WaitAlpha : 1f);
            watchGroup.blocksRaycasts = ready && !waiting;
            noGroup.alpha = b;
            noGroup.blocksRaycasts = ready;
        }

        void SetWaiting(bool on)
        {
            waiting = on;
            watch.interactable = !on;
            watchGroup.blocksRaycasts = !on && run == null;
            watchGroup.alpha = on ? WaitAlpha : 1f;
            watchLabel.text = on ? WaitText : WatchText;
        }

        void WatchVideo()
        {
            if (waiting) return; // already loading / playing
            if (Ads.IsRewardedBusy)
            {
                // ShowRewarded would ignore this call and neither callback would run: never start waiting for it
                status.gameObject.SetActive(true);
                return;
            }
            int id = ++request;
            status.gameObject.SetActive(false);
            SetWaiting(true);
            Ads.ShowRewarded(() => OnRewarded(id), () => OnFailed(id));
        }

        /// <summary>Completed video: always pays out, closes the popup if it is still this request's.</summary>
        void OnRewarded(int id)
        {
            var game = GameApp.I.Get<ScreenGame>();
            if (game != null && game.Root.gameObject.activeInHierarchy) game.GrantHints(Reward); // badge + "+3 hints" toast
            else Save.Hints = Save.Hints + Reward; // opened from Home: its pill follows Save.HintsChanged
            if (id == request && GameApp.I.IsOpen<PopupHints>()) GameApp.I.Close("PopupHints");
        }

        void OnFailed(int id)
        {
            if (id != request) return;
            SetWaiting(false);
            status.gameObject.SetActive(true);
        }

        static float EaseOutCubic(float x)
        {
            float a = 1f - x;
            return 1f - a * a * a;
        }
    }
}
