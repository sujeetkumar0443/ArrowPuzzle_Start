using UnityEngine;
using UnityEngine.UI;

namespace ArrowPuzzle
{
    /// <summary>
    /// Soft shimmering bar in the banner space at the bottom of a screen, only while that space is still waiting for
    /// its first ad (the real banner is a native view drawn over Unity, so nothing is ever drawn under a loaded ad).
    /// Hidden the frame an ad has loaded, during refreshes (the old ad stays up), and faded out after MaxWait s or when
    /// there is no banner at all. Never takes touches, never changes the layout. Add one per screen with Create(),
    /// low in the screen's draw order so everything else (and the popups) draws over it.
    /// </summary>
    public class BannerPlaceholder : MonoBehaviour
    {
        const float MaxWait = 20f;      // s of waiting for the first ad, then it fades out for good
        const float FadeOut = 0.3f;     // s
        const float Radius = 12f, Border = 1f;

        // colours that only exist here (a light neutral on the cream screens, a barely visible edge)
        static readonly Color Fill = Theme.Hex("#E6DFD0");
        static readonly Color Edge = Theme.Hex("#DCD4C3");

        RectTransform bar;
        CanvasGroup group;
        bool on;

        /// <summary>Builds the bar as a child of parent (a screen Root), anchored to its bottom, full width.</summary>
        public static BannerPlaceholder Create(Transform parent)
        {
            var holder = Ui.New(parent, "BannerPlaceholder");
            Ui.Fill(holder);
            var p = holder.gameObject.AddComponent<BannerPlaceholder>();

            var edge = Ui.Img(holder, "Bar", Theme.Rounded, Edge, Radius);
            edge.raycastTarget = false;
            p.bar = edge.rectTransform;
            p.bar.anchorMin = Vector2.zero;
            p.bar.anchorMax = new Vector2(1f, 0f);
            p.bar.pivot = new Vector2(0.5f, 0f);
            p.bar.anchoredPosition = Vector2.zero;

            var fill = Ui.Img(edge.transform, "Fill", Theme.Rounded, Fill, Radius - Border);
            fill.raycastTarget = false;
            Ui.Fill(fill.rectTransform);
            fill.rectTransform.offsetMin = Vector2.one * Theme.D(Border);
            fill.rectTransform.offsetMax = -Vector2.one * Theme.D(Border);

            Ui.AddShimmer(p.bar, Radius); // same sweep as the Play buttons, unscaled time; stops while the bar is off

            p.group = edge.gameObject.AddComponent<CanvasGroup>();
            p.group.blocksRaycasts = false;
            p.group.interactable = false;
            edge.gameObject.SetActive(false);
            return p;
        }

        void OnEnable()
        {
            Ads.BannerHeightChanged += Resize;
            Resize();
            on = false;
            if (bar != null) bar.gameObject.SetActive(false);
            LateUpdate(); // right state on the first frame of the screen
        }

        void OnDisable()
        {
            Ads.BannerHeightChanged -= Resize;
        }

        void Resize()
        {
            if (bar != null) bar.sizeDelta = new Vector2(0f, Theme.D(Ads.BannerHeightDesign));
        }

        /// <summary>After the ad callbacks of this frame (they run in Update), so a loaded ad hides it the same frame.</summary>
        void LateUpdate()
        {
            if (bar == null) return;
            float h = Theme.D(Ads.BannerHeightDesign);
            if (Mathf.Abs(bar.sizeDelta.y - h) > 0.5f) Resize(); // current value, not only the event
            if (Ads.HasBannerAd || Ads.BannerHeightDesign <= 0f || !Ads.IsBannerWanted)
            {
                // an ad is (or can be) on screen, there is no banner, or this screen hides it: off at once
                if (bar.gameObject.activeSelf) bar.gameObject.SetActive(false);
                on = false;
                return;
            }

            if (Ads.BannerWaitSeconds < MaxWait)
            {
                if (!on)
                {
                    on = true;
                    group.alpha = 1f;
                    bar.gameObject.SetActive(true);
                }
                return;
            }

            // waited too long: fade out and stop (the layout keeps its space; a late ad still shows normally)
            on = false;
            if (!bar.gameObject.activeSelf) return;
            group.alpha = Mathf.MoveTowards(group.alpha, 0f, Time.unscaledDeltaTime / FadeOut);
            if (group.alpha <= 0f) bar.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Banner state text at the top-left of a screen (Ads.ShowBannerDebug), refreshed every Interval s while the
    /// screen is shown. Small dark text, wraps to the screen width, never takes touches.
    /// </summary>
    public class BannerDebug : MonoBehaviour
    {
        const float Interval = 0.5f;

        Text text;
        string screen;
        float next;

        static bool Enabled { get { return Ads.ShowBannerDebug; } } // property: no unreachable-code warning

        /// <summary>Adds the text to parent (a screen Root, call last in Build so it draws on top). Null when off.</summary>
        public static BannerDebug Create(Transform parent, string screenName)
        {
            if (!Enabled) return null;
            var t = Ui.Label(parent, "", 14f, Theme.Ink, Weight.Medium, TextAnchor.UpperLeft);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            Ui.PlaceD(t.rectTransform, Ui.TL, new Vector2(4f, -2f), new Vector2(382f, 60f));
            var d = t.gameObject.AddComponent<BannerDebug>();
            d.text = t;
            d.screen = screenName;
            return d;
        }

        void OnEnable()
        {
            next = 0f; // fresh line on the first frame of the screen
        }

        void Update()
        {
            if (Time.unscaledTime < next || text == null) return;
            next = Time.unscaledTime + Interval;
            text.text = Ads.BannerDebugLine(screen);
        }
    }
}
