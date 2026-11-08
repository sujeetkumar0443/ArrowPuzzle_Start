using System;
using UnityEngine;
using UnityEngine.UI;

namespace ArrowPuzzle
{
    /// <summary>
    /// The screen side of the 300x250 dp MREC on a result popup (Bonus Spin, Level complete, Level failed). A white
    /// frame (3 px edge, radius 16) sized to the real ad: a header strip on top with the "Ad" tag and "ADVERTISEMENT",
    /// 7 px around the ad, horizontally centred, its ad rectangle at the popup's design y (MrecFit).
    ///
    /// Use: Create(safe, kind, who, planner, relayout) in Build (safe = the popup's safe-area rect; planner = the
    /// popup's MrecFit result for the current screen; relayout(t) = the popup's layout at compact amount t). Plan() in
    /// OnShow: returns the t the popup lays itself out at, and starts the load if nothing is loaded yet. Begin() once
    /// the content has settled: the ready ad shows at once, else a shimmering skeleton waits up to 8 s. A failed load,
    /// the timeout or a failed show fades the frame out (0.15 s), and the popup's layout eases back to normal
    /// (relayout, 0.3 s) to close the gap. End() on every exit: the ad is hidden and destroyed. Never takes touches.
    /// Preload(kind) loads a popup's MREC ahead (level start, or while the wheel spins), at the same place.
    /// </summary>
    public class MrecSlot : MonoBehaviour
    {
        const float Timeout = 8f;      // s of waiting for the ad
        const float FadeTime = 0.15f;  // s
        const float RelaxTime = 0.3f;  // s for the layout to ease back once the ad is gone
        const float Radius = 16f, EdgeW = 3f;
        const float TagW = 22f, TagH = 11f, TagTop = 3.5f, TagFont = 8.5f, LabelFont = 10f;

        static readonly Func<MrecFit.Result>[] planners = new Func<MrecFit.Result>[3];

        enum Phase { Idle, Waiting, Shown, Fading, Done }

        MrecKind kind;
        string who;
        RectTransform frame, skeleton;
        CanvasGroup group;
        Action<float> relayout;
        MrecFit.Result plan;
        Phase phase;
        bool begun, reserved, resultSent;
        float waitStart, plannedT, relaxStart = -1f, topD;

        string ScreenName { get { return kind == MrecKind.Bonus ? "bonus" : kind == MrecKind.Win ? "win" : "lose"; } }

        /// <summary>Builds the (hidden) frame under safe and registers the popup's planner for Preload.</summary>
        public static MrecSlot Create(RectTransform safe, MrecKind kind, string who, Func<MrecFit.Result> planner, Action<float> relayout)
        {
            planners[(int)kind] = planner;

            var edge = Ui.Img(safe, "MrecFrame", Theme.Rounded, Theme.ListEdge, Radius);
            edge.raycastTarget = false;
            var s = edge.gameObject.AddComponent<MrecSlot>();
            s.kind = kind;
            s.who = who;
            s.relayout = relayout;
            s.frame = edge.rectTransform;
            s.group = edge.gameObject.AddComponent<CanvasGroup>();
            s.group.blocksRaycasts = false;
            s.group.interactable = false;

            var fill = Ui.Img(edge.transform, "Fill", Theme.Rounded, Theme.Card, Radius - EdgeW);
            fill.raycastTarget = false;
            Ui.Fill(fill.rectTransform);
            fill.rectTransform.offsetMin = Vector2.one * Theme.D(EdgeW);
            fill.rectTransform.offsetMax = -Vector2.one * Theme.D(EdgeW);

            // header: "Ad" tag on the left, ADVERTISEMENT centred
            var tag = Ui.Img(edge.transform, "AdTag", Theme.Rounded, Theme.Gold, TagH * 0.5f);
            tag.raycastTarget = false;
            Ui.PlaceD(tag.rectTransform, Ui.TL, new Vector2(MrecFit.PadD + 3f, -TagTop), new Vector2(TagW, TagH));
            var tagText = Ui.Label(tag.transform, "Ad", TagFont, Theme.Ink, Weight.Bold);
            tagText.raycastTarget = false;
            Ui.Fill(tagText.rectTransform);
            var label = Ui.Label(edge.transform, "ADVERTISEMENT", LabelFont, Theme.InkSoft, Weight.Semi);
            label.raycastTarget = false;
            Ui.PlaceD(label.rectTransform, Ui.TC, new Vector2(0f, -EdgeW), new Vector2(200f, MrecFit.HeaderD - EdgeW));

            // the ad rectangle: the skeleton lives here until the ad (a native view on top) covers it
            var adRect = Ui.New(edge.transform, "AdRect");
            Ui.Fill(adRect);
            adRect.offsetMin = new Vector2(Theme.D(MrecFit.PadD), Theme.D(MrecFit.PadD));
            adRect.offsetMax = new Vector2(-Theme.D(MrecFit.PadD), -Theme.D(MrecFit.HeaderD));
            s.skeleton = Ui.New(adRect, "Skeleton");
            Ui.Fill(s.skeleton);
            BuildSkeleton(s.skeleton);
            Ui.AddShimmer(s.skeleton, 4f); // the banner placeholder's sweep, unscaled time
            s.skeleton.gameObject.SetActive(false);

            edge.gameObject.SetActive(false);
            return s;
        }

        /// <summary>A typical MREC: big media block, two text lines and a call-to-action bar under it.</summary>
        static void BuildSkeleton(RectTransform p)
        {
            Vector2 bl = Vector2.zero, tr = Vector2.one, br = new Vector2(1f, 0f);
            Bar(p, "Media", bl, tr, 0f, 78f, 0f, 0f, 6f);
            Bar(p, "Line1", bl, br, 10f, 56f, 90f, -68f, 6f);   // 12 high
            Bar(p, "Line2", bl, br, 10f, 38f, 150f, -48f, 5f);  // 10 high
            Bar(p, "Cta", bl, br, 10f, 6f, 10f, -32f, 13f);     // 26 high
        }

        /// <summary>Grey bar. Anchors 0..1 in the ad rect; offsets in design px (left, bottom, right, top insets).</summary>
        static void Bar(RectTransform parent, string name, Vector2 aMin, Vector2 aMax, float l, float b, float r, float t, float radius)
        {
            var img = Ui.Img(parent, name, Theme.Rounded, Theme.RulerTrack, radius);
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = new Vector2(Theme.D(l), Theme.D(b));
            rt.offsetMax = new Vector2(-Theme.D(r), -Theme.D(t));
        }

        /// <summary>Loads a popup's MREC ahead, where that popup will put it (no-op when its popup is missing or it does not fit).</summary>
        public static void Preload(MrecKind kind)
        {
            var p = planners[(int)kind];
            if (p == null) return;
            var r = p();
            if (!r.Fits) { Debug.Log("[Ads] MREC " + kind + " not preloaded: no room (" + r.Why + ")"); return; }
            Ads.PreloadMrec(kind, r.AdTopD);
        }

        /// <summary>
        /// Every show (OnShow, after the safe area is set). Returns the compact amount the popup lays itself out at:
        /// the plan's t when the ad gets its place, 0 when there is no ad (off, no room, last load failed).
        /// </summary>
        public float Plan()
        {
            begun = false;
            resultSent = false;
            reserved = false;
            phase = Phase.Idle;
            relaxStart = -1f;
            group.alpha = 1f;
            skeleton.gameObject.SetActive(false);
            frame.gameObject.SetActive(false);

            var p = planners[(int)kind];
            plan = p != null ? p() : new MrecFit.Result();
            var state = Ads.GetMrecState(kind);
            if (state == MrecState.Off) { Result("off", "no ad unit ID or LevelPlay off"); return 0f; }
            if (!plan.Fits)
            {
                Debug.LogWarning("[Ads] " + who + " MREC DOES NOT FIT: " + plan.Why + " -> no ad, normal layout");
                Result("no_fit", plan.Why);
                return 0f;
            }
            if (state == MrecState.Failed) { Result("load_failed", "last load failed, a retry is pending"); return 0f; }

            topD = Ads.PreloadMrec(kind, plan.AdTopD); // starts the load if nothing is loaded yet; whole-dp top
            reserved = true;
            plannedT = plan.T;
            SetFrame();
            Debug.Log("[Ads] " + who + " MREC plan: ad top " + topD.ToString("0.#") + " design px (design " + plan.AdTopD.ToString("0.#") +
                      "), " + plan.AdWD.ToString("0.#") + "x" + plan.AdHD.ToString("0.#") + " design px, layout t " + plan.T.ToString("0.##") +
                      ", available " + plan.AvailD.ToString("0") + ", state " + state);
            return plan.T;
        }

        /// <summary>Frame = the ad's real size (design px) + header + padding; its ad rectangle's top at topD.</summary>
        void SetFrame()
        {
            Vector2 s = Ads.MrecSizeDp(kind);
            float wDp = Ads.ScreenWidthDp;
            float w = MrecFit.DpToDesign(s.x, wDp), h = MrecFit.DpToDesign(s.y, wDp);
            Ui.PlaceD(frame, Ui.TC, new Vector2(0f, -(topD - MrecFit.HeaderD)),
                      new Vector2(w + 2f * MrecFit.PadD, MrecFit.HeaderD + h + MrecFit.PadD));
        }

        /// <summary>Once per show, after the content has settled. Later calls (e.g. tap to skip) do nothing.</summary>
        public void Begin()
        {
            if (begun) return;
            begun = true;
            phase = Phase.Done;
            if (!reserved) return;

            frame.gameObject.SetActive(true);
            var state = Ads.GetMrecState(kind);
            if (state == MrecState.Ready) { TryShow("was ready"); return; }
            if (state == MrecState.Off || state == MrecState.Failed) { Collapse(state == MrecState.Off ? "off" : "load_failed"); return; }
            skeleton.gameObject.SetActive(true);
            waitStart = Time.unscaledTime;
            phase = Phase.Waiting;
            Debug.Log("[Ads] " + who + " MREC " + state + ": shimmer, waiting up to " + Timeout + " s");
        }

        void TryShow(string how)
        {
            if (!Ads.ShowMrec(kind)) { Collapse("show_failed"); return; }
            phase = Phase.Shown;
            SetFrame(); // the real ad's size
            skeleton.gameObject.SetActive(false); // the ad covers the ad rectangle
            Result("shown", how);
        }

        /// <summary>No ad after all: the frame fades out, then the layout eases back to normal.</summary>
        void Collapse(string result)
        {
            Result(result, null);
            Ads.DestroyMrec(kind); // a late load must not pop up later
            phase = Phase.Fading;
        }

        /// <summary>Every exit path: the ad is hidden and destroyed, the frame removed.</summary>
        public void End()
        {
            if (phase == Phase.Waiting) Result("closed_early", null); // left before the ad arrived
            if (reserved || phase == Phase.Shown) Ads.DestroyMrec(kind);
            phase = Phase.Done;
            begun = true; // nothing more until the next Plan
            reserved = false;
            relaxStart = -1f;
            skeleton.gameObject.SetActive(false);
            frame.gameObject.SetActive(false);
        }

        /// <summary>Analytics mrec_ad_result, once per popup show.</summary>
        void Result(string result, string detail)
        {
            if (resultSent) return;
            resultSent = true;
            int ms = Ads.MrecLoadMs(kind);
            Debug.Log("[Ads] " + who + " mrec_ad_result: " + result + (detail != null ? " (" + detail + ")" : "") + ", load " + ms + " ms" +
                      (phase == Phase.Waiting ? ", waited " + (Time.unscaledTime - waitStart).ToString("0.0") + " s" : ""));
            AnalyticsService.MrecAdResult(ScreenName, result, ms);
        }

        void Update()
        {
            if (phase == Phase.Waiting)
            {
                var state = Ads.GetMrecState(kind);
                if (state == MrecState.Ready) TryShow("loaded after " + (Time.unscaledTime - waitStart).ToString("0.0") + " s");
                else if (state == MrecState.Failed || state == MrecState.Off) Collapse(state == MrecState.Off ? "off" : "load_failed");
                else if (Time.unscaledTime - waitStart >= Timeout) Collapse("timeout");
            }
            else if (phase == Phase.Fading)
            {
                group.alpha = Mathf.MoveTowards(group.alpha, 0f, Time.unscaledDeltaTime / FadeTime);
                if (group.alpha <= 0f)
                {
                    frame.gameObject.SetActive(false); // stops Update; the relax runs in the popup's own frame (RelaxStep)
                    phase = Phase.Done;
                    reserved = false;
                    if (plannedT > 0f && relayout != null) relaxStart = Time.unscaledTime;
                    RelaxHost.Run(this);
                }
            }
        }

        /// <summary>One relax frame (RelaxHost): layout t from the planned value back to 0, eased. False when done.</summary>
        bool RelaxStep()
        {
            if (relaxStart < 0f || relayout == null) return false;
            float k = Mathf.Clamp01((Time.unscaledTime - relaxStart) / RelaxTime);
            float e = k * k * (3f - 2f * k);
            relayout(Mathf.Lerp(plannedT, 0f, e));
            if (k >= 1f) { relaxStart = -1f; return false; }
            return true;
        }

        void OnDisable()
        {
            // the popup closed without End (should not happen): never leave an ad up without its screen
            if (phase == Phase.Shown || phase == Phase.Waiting) { Ads.DestroyMrec(kind); phase = Phase.Done; }
        }

        /// <summary>Runs RelaxStep every frame on a hidden host (the frame itself is inactive by then).</summary>
        sealed class RelaxHost : MonoBehaviour
        {
            static RelaxHost inst;
            MrecSlot slot;

            public static void Run(MrecSlot s)
            {
                if (s.relaxStart < 0f) return;
                if (inst == null)
                {
                    var go = new GameObject("MrecRelax");
                    DontDestroyOnLoad(go);
                    inst = go.AddComponent<RelaxHost>();
                }
                inst.slot = s;
                inst.enabled = true;
            }

            void Update()
            {
                if (slot == null || !slot.RelaxStep()) { slot = null; enabled = false; }
            }
        }
    }
}
