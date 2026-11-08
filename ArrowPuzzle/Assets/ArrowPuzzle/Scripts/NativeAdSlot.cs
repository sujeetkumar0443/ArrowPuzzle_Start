using UnityEngine;
using UnityEngine.UI;

namespace ArrowPuzzle
{
    /// <summary>
    /// The native ad frame at the bottom of a result screen (Level complete, Out of lives). Plan(contentBottom) in
    /// OnShow picks Medium / Small / None from the free space (Ads.PlanNative) and sizes the permanent white frame
    /// (radius 22, 3 px edge, 7 px padding, 342 wide). Begin() once the content has settled: shows the ready ad at once,
    /// or a shimmering skeleton of that template in the frame's inner rectangle until the ad is ready (then the ad
    /// replaces it in place), it fails, or Timeout s pass (then the skeleton fades out and the frame stays). Never
    /// moves anything else, never takes touches. End() on every exit path. Unscaled time.
    /// </summary>
    public class NativeAdSlot : MonoBehaviour
    {
        const float Timeout = 8f;      // s of waiting for the ad
        const float FadeTime = 0.15f;  // s
        const float Radius = 22f, EdgeW = 3f;

        // colours that only exist here: the frame edge and the skeleton bars (as BannerPlaceholder)
        static readonly Color Edge = Theme.Hex("#D9D1C1");
        static readonly Color Bars = Theme.Hex("#E6DFD0");

        enum Phase { Idle, Waiting, Shown, Fading, Done }

        NativeKind kind;
        string who;
        RectTransform frame, skeleton, medium, small;
        Image edgeImg;           // the frame's outer image: its colour is the 3 px edge around the white fill
        Rect placedCard;         // Unity screen px of the card the frame was last fitted to (for the placement log)
        NativeSize placedSize;
        bool placedMeasured;
        CanvasGroup skeletonGroup;
        Phase phase;
        bool begun;
        float waitStart;
        NativePlan plan;
        NativeSize size;
        bool resultLogged; // native_ad_result: once per screen show

        /// <summary>Builds the (hidden) frame and both skeletons under body: a rect whose top is the safe-area top.</summary>
        public static NativeAdSlot Create(RectTransform body, NativeKind kind, string who)
        {
            var edge = Ui.Img(body, "NativeAdFrame", Theme.Rounded, Edge, Radius);
            edge.raycastTarget = false;
            var s = edge.gameObject.AddComponent<NativeAdSlot>();
            s.kind = kind;
            s.who = who;
            s.frame = edge.rectTransform;
            s.edgeImg = edge;

            var fill = Ui.Img(edge.transform, "Fill", Theme.Rounded, Theme.Card, Radius - EdgeW);
            fill.raycastTarget = false;
            Inset(fill.rectTransform, EdgeW);

            // inner rectangle = the ad's rectangle (7 px padding)
            s.skeleton = Ui.New(edge.transform, "Skeleton");
            Inset(s.skeleton, Ads.NativeFramePad);
            s.medium = Ui.New(s.skeleton, "Medium");
            Ui.Fill(s.medium);
            s.small = Ui.New(s.skeleton, "Small");
            Ui.Fill(s.small);
            BuildMedium(s.medium);
            BuildSmall(s.small);
            Ui.AddShimmer(s.skeleton, Radius - Ads.NativeFramePad); // the banner placeholder's sweep, unscaled time

            s.skeletonGroup = s.skeleton.gameObject.AddComponent<CanvasGroup>();
            s.skeletonGroup.blocksRaycasts = false;
            s.skeletonGroup.interactable = false;
            s.skeleton.gameObject.SetActive(false);
            edge.gameObject.SetActive(false);
            return s;
        }

        static void Inset(RectTransform rt, float d)
        {
            Ui.Fill(rt);
            rt.offsetMin = Vector2.one * Theme.D(d);
            rt.offsetMax = -Vector2.one * Theme.D(d);
        }

        /// <summary>Grey bar. Anchors 0..1 in the inner rect; offsets in design px (left, bottom, right, top insets).</summary>
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

        /// <summary>Medium template: icon + two text lines on top, media block, full-width CTA at the bottom.</summary>
        static void BuildMedium(RectTransform p)
        {
            Vector2 tl = new Vector2(0f, 1f), tr = new Vector2(1f, 1f), bl = Vector2.zero, br = new Vector2(1f, 0f);
            // icon 44 at the top-left
            Bar(p, "Icon", tl, tl, 12f, -56f, -56f, 12f, 12f);
            // text lines right of the icon (the right end leaves room for AdChoices)
            Bar(p, "Line1", tl, tr, 68f, -28f, 40f, 16f, 6f);
            Bar(p, "Line2", tl, tr, 68f, -48f, 120f, 38f, 5f);
            // media block between the header and the CTA
            Bar(p, "Media", bl, tr, 12f, 64f, 12f, 68f, 12f);
            // CTA 40 high at the bottom
            Bar(p, "Cta", bl, br, 12f, 12f, 12f, -52f, 20f);
        }

        /// <summary>Small template: icon block left, two text lines, CTA bar right.</summary>
        static void BuildSmall(RectTransform p)
        {
            Vector2 ml = new Vector2(0f, 0.5f), mr = new Vector2(1f, 0.5f);
            Bar(p, "Icon", ml, ml, 12f, -26f, -64f, -26f, 12f);              // 52 x 52, centred vertically
            Bar(p, "Line1", ml, mr, 76f, 5f, 108f, -17f, 6f);                // 12 high, 11 above the middle
            Bar(p, "Line2", ml, mr, 76f, -16f, 164f, 6f, 5f);                // 10 high, 11 below the middle
            Bar(p, "Cta", mr, mr, -96f, -17f, 12f, -17f, 17f);               // 84 x 34 at the right
        }

        /// <summary>Compact steps: 0 = normal spacing, CompactSteps = the tightest (80 %).</summary>
        public const int CompactSteps = 4;

        /// <summary>
        /// Every screen show (OnShow, after the safe area is set). Medium first: tries the normal layout, then each
        /// compact step (rowBottomAt(t) = bottom of the last button row at compact amount t, 0..1), re-planning each
        /// time, until the medium ad fits. Returns the t the screen must lay itself out at. Small only as the last resort.
        /// </summary>
        public float Plan(System.Func<float, float> rowBottomAt)
        {
            begun = false;
            phase = Phase.Idle;
            skeleton.gameObject.SetActive(false);

            float t = 0f;
            for (int i = 0; i <= CompactSteps; i++)
            {
                t = i / (float)CompactSteps;
                plan = Ads.PlanNative(rowBottomAt(t), who, " (compact step " + i + "/" + CompactSteps + ", spacing " +
                                      Mathf.RoundToInt(100f - 20f * t) + " %)");
                if (plan.Size == NativeSize.Medium) break;
            }
            if (plan.Size != NativeSize.Medium)
                Debug.LogWarning("[Ads] " + who + " NATIVE MEDIUM DOES NOT FIT even at 80 % spacing (free " +
                                 plan.FreeDp.ToString("0") + " dp, needs " + plan.NeedMediumDp.ToString("0") +
                                 ") -> last resort: " + (plan.Size == NativeSize.Small ? "SMALL template" : "no ad"));
            ApplyPlan();
            return t;
        }

        void ApplyPlan()
        {
            resultLogged = false;
            size = plan.Size;
            var state = Ads.GetNativeState(kind);
            if (size == NativeSize.None) Result("no_fit", NativeSize.None);
            else if (state == NativeState.Off || state == NativeState.Failed)
            {
                Debug.Log("[Ads] " + who + " native frame hidden: ad " + state +
                          (state == NativeState.Off ? " (Editor / not Android, init failed or no ID)" : " (last load failed)"));
                Result(state == NativeState.Off ? "off" : "last_failed", size);
                size = NativeSize.None;
            }
            SetFrame(size);
        }

        /// <summary>Analytics native_ad_result, once per screen show: kind, planned / shown size, why.</summary>
        void Result(string reason, NativeSize s)
        {
            if (resultLogged) return;
            resultLogged = true;
            AnalyticsService.NativeAdResult(kind == NativeKind.Win ? "win" : "lose", s.ToString().ToLowerInvariant(), reason);
        }

        /// <summary>Frame for a template size (None = hidden). Its top stays put; only its height follows the size.</summary>
        void SetFrame(NativeSize s)
        {
            edgeImg.color = Edge; // the planned frame (shimmer / no ad) keeps its edge; FitToCard hides it under the ad
            placedSize = NativeSize.None;
            if (s == NativeSize.None)
            {
                frame.gameObject.SetActive(false);
                return;
            }
            float inner = Ads.NativeInnerHeight(plan, s);
            float pad = Ads.NativeFramePad;
            Ui.PlaceD(frame, Ui.TC, new Vector2(0f, -(plan.InnerTop - pad)), new Vector2(Ads.NativeFrameWidth, inner + 2f * pad));
            medium.gameObject.SetActive(s == NativeSize.Medium);
            small.gameObject.SetActive(s == NativeSize.Small);
            frame.gameObject.SetActive(true);
            Debug.Log("[Ads] " + who + " native frame " + s + ": top " + (plan.InnerTop - pad).ToString("0.#") +
                      " design px from the safe-area top, " + Ads.NativeFrameWidth + "x" + (inner + 2f * pad).ToString("0.#") +
                      " (inner " + plan.InnerWidth.ToString("0.#") + "x" + inner.ToString("0.#") + ")");
        }

        /// <summary>Once per show, after the content has settled. Later calls (e.g. tap to skip) do nothing.</summary>
        public void Begin()
        {
            if (begun) return;
            begun = true;
            phase = Phase.Done;
            if (size == NativeSize.None) return;

            var state = Ads.GetNativeState(kind);
            string m = "[Ads] " + who + " native " + size + ": ad " + state;
            if (state == NativeState.Off || state == NativeState.Failed)
            {
                Debug.Log(m + " -> no ad (frame stays)");
                Result(state == NativeState.Off ? "off" : "load_failed", size);
                return;
            }
            if (state == NativeState.Ready)
            {
                var got = Ads.ShowNative(kind, plan, size);
                if (got != NativeSize.None)
                {
                    if (got != size) { size = got; SetFrame(got); }
                    phase = Phase.Shown;
                    // a medium is measured off screen for a few frames first: shimmer until the ad is in place
                    skeletonGroup.alpha = 1f;
                    skeleton.gameObject.SetActive(true);
                    waitStart = Time.unscaledTime;
                }
                Debug.Log(m + (got != NativeSize.None ? " -> shown " + got + " (was ready, no shimmer)" : " -> show failed (frame stays)"));
                Result(got != NativeSize.None ? "shown" : "show_failed", got != NativeSize.None ? got : size);
                return;
            }

            if (state == NativeState.Idle) Ads.PreloadNative(kind);
            Debug.Log(m + " -> shimmer, waiting up to " + Timeout + " s");
            skeletonGroup.alpha = 1f;
            skeleton.gameObject.SetActive(true);
            waitStart = Time.unscaledTime;
            phase = Phase.Waiting;
        }

        /// <summary>Every exit path: the ad is hidden and destroyed (the next one preloads), the shimmer removed.</summary>
        public void End()
        {
            if (phase == Phase.Waiting) Result("closed_early", size); // left before the ad arrived
            if (phase == Phase.Shown) Ads.HideNative();
            phase = Phase.Done;
            begun = true; // nothing more until the next Plan
            skeleton.gameObject.SetActive(false);
            edgeImg.color = Edge;
        }

        /// <summary>
        /// While the ad is on screen the frame is fitted to the ad's real card every frame (LateUpdate, after the
        /// screen's own animation): inner rect = the card exactly, frame = card + 7 px padding. This also cancels any
        /// movement of the parent (PopupWin's star-landing shakes) since the ad itself never moves.
        /// </summary>
        void LateUpdate()
        {
            if (phase != Phase.Shown) return;
            Rect card;
            NativeSize s;
            bool measured;
            if (!Ads.TryGetNativeCard(out card, out s, out measured)) return;
            FitToCard(card, s, measured);
        }

        void FitToCard(Rect card, NativeSize s, bool measured)
        {
            var parent = (RectTransform)frame.parent;
            float padPx = Ads.DesignToUnityPx(Ads.NativeFramePad);
            Vector2 a, b;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, new Vector2(card.xMin - padPx, card.yMin - padPx), null, out a);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, new Vector2(card.xMax + padPx, card.yMax + padPx), null, out b);
            // anchor at the parent's pivot = the local origin, so the local points are anchored positions as they are
            frame.anchorMin = frame.anchorMax = parent.pivot;
            frame.pivot = new Vector2(0.5f, 0.5f);
            frame.anchoredPosition = (a + b) * 0.5f;
            frame.sizeDelta = b - a;
            medium.gameObject.SetActive(s == NativeSize.Medium);
            small.gameObject.SetActive(s == NativeSize.Small);
            // the ad's corners are square (the plugin's templates; no radius API) and a square card 7 px inside a
            // radius 22 frame always covers part of the 3 px edge at the corners: the edge is hidden under the ad
            edgeImg.color = Theme.Card;
            frame.gameObject.SetActive(true);
            skeleton.gameObject.SetActive(false); // the ad is in place: it covers the inner rectangle

            if (s == placedSize && measured == placedMeasured &&
                Mathf.Abs(card.x - placedCard.x) < 0.5f && Mathf.Abs(card.y - placedCard.y) < 0.5f &&
                Mathf.Abs(card.width - placedCard.width) < 0.5f && Mathf.Abs(card.height - placedCard.height) < 0.5f) return;
            placedCard = card;
            placedSize = s;
            placedMeasured = measured;

            float safeTop = Screen.height - Screen.safeArea.yMax;
            float fTop = Ads.UnityPxToDesign(Screen.height - (card.yMax + padPx) - safeTop);
            float fLeft = Ads.UnityPxToDesign(card.xMin - padPx);
            float inset = plan.BottomInsetPx;                         // Unity px kept free at the bottom
            float marginDp = Ads.UnityPxToDp(card.yMin - padPx - inset); // frame bottom -> top of the bottom inset
            Debug.Log("[Ads] " + who + " native placed: kind " + kind + ", template " + s + (measured ? " (measured)" : " (predicted)") +
                      ", visible height " + Ads.UnityPxToDp(card.height).ToString("0.#") + " dp" +
                      ", frame x " + fLeft.ToString("0.#") + " y " + fTop.ToString("0.#") + " w " +
                      Ads.UnityPxToDesign(card.width + 2f * padPx).ToString("0.#") + " h " +
                      Ads.UnityPxToDesign(card.height + 2f * padPx).ToString("0.#") + " design px (y from the safe-area top), ad " +
                      Ads.NativeCardDescription() + ", bottom inset " + Ads.UnityPxToDp(inset).ToString("0.#") +
                      " dp, bottom margin " + marginDp.ToString("0.#") + " dp, density " + Ads.DisplayDensity.ToString("0.###"));
            if (card.yMin < 0f)
                Debug.LogWarning("[Ads] " + who + " NATIVE AD BOTTOM IS BELOW THE SCREEN by " + Ads.UnityPxToDp(-card.yMin).ToString("0.#") + " dp");
            else if (card.yMin < inset)
                Debug.LogWarning("[Ads] " + who + " NATIVE AD BOTTOM IS INSIDE THE BOTTOM INSET by " +
                                 Ads.UnityPxToDp(inset - card.yMin).ToString("0.#") + " dp (nav bar / gesture area)");
        }

        void OnDisable()
        {
            // hidden without End (should not happen): never leave an ad up without its screen
            if (phase == Phase.Shown) Ads.HideNative();
            if (phase != Phase.Idle) phase = Phase.Done;
            if (skeleton != null) skeleton.gameObject.SetActive(false);
        }

        void FadeOut(string why)
        {
            Debug.Log("[Ads] " + who + " native shimmer fades out: " + why + " (frame stays)");
            phase = Phase.Fading;
        }

        void Update()
        {
            if (phase == Phase.Shown)
            {
                // the layout check a few frames after rendering may switch a too-tall medium to small; LateUpdate fits
                // the frame to whatever card is really on screen
                var now = Ads.ShownNativeSize;
                if (now == NativeSize.None) { FadeOut("the ad could not be placed"); return; } // e.g. the small fallback failed
                if (now != size) size = now;
                return;
            }
            if (phase == Phase.Waiting)
            {
                var state = Ads.GetNativeState(kind);
                if (state == NativeState.Ready)
                {
                    var got = Ads.ShowNative(kind, plan, size);
                    if (got != NativeSize.None)
                    {
                        // the shimmer stays until the ad is really in place (FitToCard): a medium is measured first
                        if (got != size) { size = got; SetFrame(got); }
                        phase = Phase.Shown;
                        Result("shown", got);
                        Debug.Log("[Ads] " + who + " native " + got + " replaced the shimmer after "
                                  + (Time.unscaledTime - waitStart).ToString("0.0") + " s");
                    }
                    else { Result("show_failed", size); FadeOut("show failed"); }
                }
                else if (state == NativeState.Failed || state == NativeState.Off) { Result("load_failed", size); FadeOut("load failed"); }
                else if (Time.unscaledTime - waitStart >= Timeout) { Result("timeout", size); FadeOut("no ad in " + Timeout + " s"); }
            }
            else if (phase == Phase.Fading)
            {
                skeletonGroup.alpha = Mathf.MoveTowards(skeletonGroup.alpha, 0f, Time.unscaledDeltaTime / FadeTime);
                if (skeletonGroup.alpha <= 0f)
                {
                    skeleton.gameObject.SetActive(false);
                    phase = Phase.Done;
                }
            }
        }
    }
}
