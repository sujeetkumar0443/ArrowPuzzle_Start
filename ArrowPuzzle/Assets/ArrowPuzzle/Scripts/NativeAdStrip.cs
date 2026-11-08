using UnityEngine;
using UnityEngine.UI;

namespace ArrowPuzzle
{
    /// <summary>
    /// The screen side of a full-width SMALL native ad at the bottom of a full-screen popup (the Bonus Spin screen,
    /// NativeKind.Bonus). Like NativeAdSlot, but without a frame: a white strip from the ad top down to the very bottom
    /// of the screen (it fills the gesture area under the ad, so the ad looks flush), with a grey skeleton of the small
    /// template and a soft shimmer while the ad loads. The ad itself (Ads.ShowNative, plugin template, its own "Ad"
    /// badge and AdChoices) sits above the bottom inset, so its CTA is never under the gesture bar.
    /// Use: Create(root, kind, who) in Build (root = the popup's full-screen Root); Plan() in OnShow (returns the ad
    /// top in design px from the safe-area top, for the screen's layout); Begin() once the content has settled;
    /// End() on every exit (destroys the ad, the next one preloads). A failed load / 8 s timeout fades the strip out;
    /// the screen's layout never depends on it.
    /// </summary>
    public class NativeAdStrip : MonoBehaviour
    {
        const float Timeout = 8f;      // s of waiting for the ad
        const float FadeTime = 0.15f;  // s
        static readonly Color Base = Theme.Hex("#EFEAE0");  // the placeholder strip (grey)
        static readonly Color Bars = Theme.Hex("#E6DFD0");  // skeleton bars (as NativeAdSlot / BannerPlaceholder)

        enum Phase { Idle, Waiting, Shown, Fading, Done }

        NativeKind kind;
        string who;
        RectTransform strip, skeleton;
        CanvasGroup stripGroup;
        NativePlan plan;
        Phase phase;
        bool begun, resultLogged;
        float waitStart;

        /// <summary>Builds the (hidden) strip under root: a full-screen rect (the popup's Root, not its safe area).</summary>
        public static NativeAdStrip Create(RectTransform root, NativeKind kind, string who)
        {
            var white = Ui.Img(root, "NativeAdStrip", null, Theme.Card);
            white.raycastTarget = false;
            var s = white.gameObject.AddComponent<NativeAdStrip>();
            s.kind = kind;
            s.who = who;
            s.strip = white.rectTransform;
            s.strip.anchorMin = Vector2.zero;
            s.strip.anchorMax = new Vector2(1f, 0f);
            s.strip.pivot = new Vector2(0.5f, 0f);
            s.strip.anchoredPosition = Vector2.zero;
            s.stripGroup = white.gameObject.AddComponent<CanvasGroup>();
            s.stripGroup.blocksRaycasts = false;
            s.stripGroup.interactable = false;

            // skeleton = the ad's rectangle at the top of the strip (Plan sizes it)
            var sk = Ui.Img(s.strip, "Skeleton", null, Base);
            sk.raycastTarget = false;
            s.skeleton = sk.rectTransform;
            s.skeleton.anchorMin = new Vector2(0f, 1f);
            s.skeleton.anchorMax = Vector2.one;
            s.skeleton.pivot = new Vector2(0.5f, 1f);
            s.skeleton.anchoredPosition = Vector2.zero;
            BuildSmall(s.skeleton);
            Ui.AddShimmer(s.skeleton, 2f); // the banner placeholder's sweep, unscaled time
            s.skeleton.gameObject.SetActive(false);
            white.gameObject.SetActive(false);
            return s;
        }

        /// <summary>Small template skeleton: icon block left, two text lines, CTA bar right (anchors in the ad rect).</summary>
        static void BuildSmall(RectTransform p)
        {
            Vector2 ml = new Vector2(0f, 0.5f), mr = new Vector2(1f, 0.5f);
            Bar(p, "Icon", ml, ml, 14f, -30f, -74f, -30f, 12f);   // 60 x 60, centred vertically
            Bar(p, "Line1", ml, mr, 88f, 6f, 150f, -20f, 7f);     // 14 high, above the middle
            Bar(p, "Line2", ml, mr, 88f, -18f, 210f, 6f, 6f);     // 12 high, below the middle
            Bar(p, "Cta", mr, mr, -110f, -20f, 14f, -20f, 20f);   // 96 x 40 at the right
        }

        /// <summary>Grey bar. Anchors 0..1 in the ad rect; offsets in design px (left, bottom, right, top insets).</summary>
        static void Bar(RectTransform parent, string name, Vector2 aMin, Vector2 aMax, float l, float b, float r, float t, float radius)
        {
            var img = Ui.Img(parent, name, Theme.Rounded, Bars, radius);
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = new Vector2(Theme.D(l), Theme.D(b));
            rt.offsetMax = new Vector2(-Theme.D(r), -Theme.D(t));
        }

        /// <summary>
        /// Every show (OnShow, after the safe area is set): plans the ad at the bottom (Ads.PlanNativeBottom) and sizes
        /// the strip to it (hidden until Begin). Returns the ad top in design px from the safe-area top.
        /// </summary>
        public float Plan()
        {
            begun = false;
            resultLogged = false;
            phase = Phase.Idle;
            plan = Ads.PlanNativeBottom(who);
            stripGroup.alpha = 1f;
            skeleton.gameObject.SetActive(false);
            strip.gameObject.SetActive(false);
            if (plan.Size == NativeSize.None)
            {
                float safeH = Ads.UnityPxToDesign(Screen.safeArea.height);
                return safeH; // nothing fits (should not happen): the whole safe area is the screen's
            }
            float safeTopPx = Screen.height - Screen.safeArea.yMax;
            float adTopFromBottomPx = Screen.height - (safeTopPx + Ads.DesignToUnityPx(plan.InnerTop));
            float adHPx = Ads.DesignToUnityPx(Ads.NativeInnerHeight(plan, NativeSize.Small));
            SetStrip(adTopFromBottomPx, adHPx);
            return plan.InnerTop;
        }

        /// <summary>Strip from the screen bottom to topPx (Unity px from the bottom); the skeleton is the top adHPx of it.</summary>
        void SetStrip(float topPx, float adHPx)
        {
            var parent = (RectTransform)strip.parent;
            float k = Screen.height > 0 ? parent.rect.height / Screen.height : 1f; // Unity px -> parent units
            strip.sizeDelta = new Vector2(0f, Mathf.Max(0f, topPx) * k);
            skeleton.sizeDelta = new Vector2(0f, Mathf.Max(0f, adHPx) * k);
        }

        /// <summary>Once the content has settled (once per show): the ready ad goes up, else the shimmer waits up to 8 s.</summary>
        public void Begin()
        {
            if (begun) return;
            begun = true;
            phase = Phase.Done;
            if (plan.Size == NativeSize.None) { Result("no_fit"); return; }

            var state = Ads.GetNativeState(kind);
            string m = "[Ads] " + who + " native strip: ad " + state;
            if (state == NativeState.Off || state == NativeState.Failed)
            {
                Debug.Log(m + " -> no ad (strip hidden)");
                Result(state == NativeState.Off ? "off" : "load_failed");
                return;
            }
            strip.gameObject.SetActive(true);
            skeleton.gameObject.SetActive(true);
            waitStart = Time.unscaledTime;
            if (state == NativeState.Ready)
            {
                TryShow(m + " -> shown (was ready)");
                return;
            }
            if (state == NativeState.Idle) Ads.PreloadNative(kind);
            Debug.Log(m + " -> shimmer, waiting up to " + Timeout + " s");
            phase = Phase.Waiting;
        }

        void TryShow(string log)
        {
            var got = Ads.ShowNative(kind, plan, NativeSize.Small);
            if (got == NativeSize.None) { Result("show_failed"); FadeOut("show failed"); return; }
            phase = Phase.Shown; // the shimmer stays until the ad's card is measured in place (LateUpdate)
            Result("shown");
            Debug.Log(log);
        }

        /// <summary>Every exit path: the ad is hidden and destroyed (the next one preloads), the strip removed.</summary>
        public void End()
        {
            if (phase == Phase.Waiting) Result("closed_early"); // left before the ad arrived
            if (phase == Phase.Shown) Ads.HideNative();
            phase = Phase.Done;
            begun = true; // nothing more until the next Plan
            skeleton.gameObject.SetActive(false);
            strip.gameObject.SetActive(false);
        }

        void Result(string reason)
        {
            if (resultLogged) return;
            resultLogged = true;
            AnalyticsService.NativeAdResult("bonus", reason == "shown" ? "small" : "none", reason);
        }

        void FadeOut(string why)
        {
            Debug.Log("[Ads] " + who + " native strip fades out: " + why);
            phase = Phase.Fading;
        }

        void Update()
        {
            if (phase == Phase.Shown)
            {
                if (Ads.ShownNativeSize == NativeSize.None) FadeOut("the ad could not be placed");
                return;
            }
            if (phase == Phase.Waiting)
            {
                var state = Ads.GetNativeState(kind);
                if (state == NativeState.Ready)
                    TryShow("[Ads] " + who + " native strip: the ad replaced the shimmer after " +
                            (Time.unscaledTime - waitStart).ToString("0.0") + " s");
                else if (state == NativeState.Failed || state == NativeState.Off) { Result("load_failed"); FadeOut("load failed"); }
                else if (Time.unscaledTime - waitStart >= Timeout) { Result("timeout"); FadeOut("no ad in " + Timeout + " s"); }
            }
            else if (phase == Phase.Fading)
            {
                stripGroup.alpha = Mathf.MoveTowards(stripGroup.alpha, 0f, Time.unscaledDeltaTime / FadeTime);
                if (stripGroup.alpha <= 0f)
                {
                    strip.gameObject.SetActive(false);
                    skeleton.gameObject.SetActive(false);
                    phase = Phase.Done;
                }
            }
        }

        /// <summary>While the ad is up: the strip's top follows the ad's measured card, and the skeleton goes once it is in place.</summary>
        void LateUpdate()
        {
            if (phase != Phase.Shown) return;
            Rect card;
            NativeSize s;
            bool measured;
            if (!Ads.TryGetNativeCard(out card, out s, out measured)) return;
            SetStrip(card.yMax, card.height);
            if (measured && skeleton.gameObject.activeSelf) skeleton.gameObject.SetActive(false);
        }

        void OnDisable()
        {
            // hidden without End (should not happen): never leave an ad up without its screen
            if (phase == Phase.Shown) Ads.HideNative();
            if (phase != Phase.Idle) phase = Phase.Done;
        }
    }
}
