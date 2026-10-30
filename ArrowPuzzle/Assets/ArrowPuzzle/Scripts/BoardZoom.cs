using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ArrowPuzzle
{
    /// <summary>
    /// Pinch-to-zoom and pan for the game board. Lives on the board area (the view, clipped while zoomed) and only
    /// scales / moves the board card inside it; BoardView hit tests through that transform, so the tapped arrow is
    /// always the one under the finger. Owns all board input: a touch is a tap (sent to BoardView on release) only if
    /// it moved less than TapSlop and no second finger came down. Double-tap on empty space animates back to 1x.
    /// Editor / desktop: mouse wheel zooms around the cursor, right drag pans. Unscaled time.
    /// </summary>
    public class BoardZoom : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IInitializePotentialDragHandler,
                             IDragHandler, IScrollHandler
    {
        const float MinZoom = 1f, MaxZoom = 3f;
        const float SnapToMin = 1.02f;                          // a settle below this goes back to exactly 1x
        const float TapSlop = 12f;                              // design px
        const float DoubleTapTime = 0.35f, DoubleTapDist = 40f; // s between releases, design px
        const float PanMargin = 40f;                            // design px the board edge may leave the view by
        const float RubberZoom = 0.15f, RubberPan = 60f;        // overshoot scales (ln zoom, design px)
        const float SettleTime = 0.25f, ResetTime = 0.3f;
        const float WheelStep = 1.15f;
        const float ButtonShowAt = 1.05f, ButtonFadeTime = 0.15f, ButtonSize = 40f, ButtonInset = 8f;

        RectTransform view, content;
        BoardView board;
        RectMask2D mask;
        CanvasGroup button;
        Canvas canvas;

        float zoom = MinZoom;
        Vector2 pan;                                            // content anchoredPosition (anchored at the view centre)
        readonly List<int> fingers = new List<int>();           // in touch order: the first two pinch
        readonly Dictionary<int, Vector2> pos = new Dictionary<int, Vector2>();
        int tapId;
        bool tapLive, rightDown;
        Vector2 tapStart, pinchMid;
        float pinchDist;
        float lastTapTime = -10f;
        Vector2 lastTapPos;
        Coroutine anim;

        /// <summary>
        /// Adds zoom to view (the board area) for content (the card holding the board, centre anchored and pivoted).
        /// Builds a transparent hit area behind the card, the clip mask and the round "1x" reset button.
        /// </summary>
        public static BoardZoom Attach(RectTransform view, RectTransform content, BoardView board)
        {
            // gestures may start next to the board too (events from the board image bubble up to here as well)
            var hit = Ui.Img(view, "ZoomHit", null, Color.clear);
            Ui.Fill(hit.rectTransform);
            hit.transform.SetAsFirstSibling();

            var z = view.gameObject.AddComponent<BoardZoom>();
            z.view = view;
            z.content = content;
            z.board = board;
            z.mask = view.gameObject.AddComponent<RectMask2D>();
            z.mask.enabled = false; // at 1x nothing is clipped (exiting arrows still fly over the buttons)

            var btn = Ui.Btn(view, "1x", Theme.Card, Theme.Ink, 16f, ButtonSize * 0.5f, Theme.CardShadow, 3f,
                             () => z.ResetZoom(true));
            Ui.PlaceD((RectTransform)btn.transform, Ui.TR, new Vector2(-ButtonInset, -ButtonInset), new Vector2(ButtonSize, ButtonSize));
            z.button = btn.gameObject.AddComponent<CanvasGroup>();
            z.SetButton(0f);
            return z;
        }

        public float Zoom { get { return zoom; } }

        /// <summary>Back to 1x, centred. animate = false for a level start / restart (also drops a pending tap).</summary>
        public void ResetZoom(bool animate)
        {
            if (animate)
            {
                AnimateTo(MinZoom, Vector2.zero, ResetTime);
                return;
            }
            StopAnim();
            tapLive = false;
            lastTapTime = -10f;
            zoom = MinZoom;
            pan = Vector2.zero;
            Apply();
            SetButton(0f);
        }

        void OnDisable()
        {
            fingers.Clear();
            pos.Clear();
            tapLive = rightDown = false;
            anim = null; // coroutines stop with the object
        }

        void Update()
        {
            bool zoomed = zoom > MinZoom + 0.001f;
            if (mask.enabled != zoomed) mask.enabled = zoomed;

            float target = zoom > ButtonShowAt ? 1f : 0f;
            if (button.alpha != target) SetButton(Mathf.MoveTowards(button.alpha, target, Time.unscaledDeltaTime / ButtonFadeTime));
        }

        void SetButton(float alpha)
        {
            button.alpha = alpha;
            button.interactable = button.blocksRaycasts = alpha > 0.5f;
        }

        // ------------------------------------------------------------------ input

        public void OnPointerDown(PointerEventData e)
        {
            StopAnim(); // a touch grabs the board
            if (e.button == PointerEventData.InputButton.Right) { rightDown = true; return; }
            if (e.button != PointerEventData.InputButton.Left) return;

            if (!pos.ContainsKey(e.pointerId)) fingers.Add(e.pointerId);
            pos[e.pointerId] = e.position;
            if (fingers.Count == 1)
            {
                tapId = e.pointerId;
                tapLive = true;
                tapStart = e.position;
            }
            else
            {
                tapLive = false; // a second finger never lets the first one tap
                BeginPinch();
            }
        }

        public void OnInitializePotentialDrag(PointerEventData e)
        {
            e.useDragThreshold = false; // every move counts (our own slop decides what is a tap)
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Right)
            {
                if (rightDown && zoom > MinZoom) Pan(Local(e.position) - Local(e.position - e.delta));
                return;
            }

            Vector2 prev;
            if (!pos.TryGetValue(e.pointerId, out prev)) return; // pressed on something else (reset button)
            pos[e.pointerId] = e.position;

            if (fingers.Count >= 2)
            {
                if (e.pointerId == fingers[0] || e.pointerId == fingers[1]) Pinch();
                return;
            }
            if (tapLive)
            {
                if ((e.position - tapStart).magnitude <= TapSlop * ScreenPerD()) return;
                tapLive = false;
                prev = tapStart; // the board catches up with the finger
            }
            if (zoom > MinZoom) Pan(Local(e.position) - Local(prev));
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Right)
            {
                rightDown = false;
                if (fingers.Count == 0) Settle();
                return;
            }
            if (!pos.ContainsKey(e.pointerId)) return;

            bool tap = tapLive && e.pointerId == tapId;
            fingers.Remove(e.pointerId);
            pos.Remove(e.pointerId);
            if (fingers.Count >= 2) BeginPinch(); // a third finger lifted the pinch pair
            if (tap)
            {
                tapLive = false;
                Tap(tapStart);
            }
            if (fingers.Count == 0 && !rightDown) Settle();
        }

        public void OnScroll(PointerEventData e)
        {
            if (fingers.Count > 0) return;
            StopAnim();
            float z = Mathf.Clamp(zoom * Mathf.Pow(WheelStep, Mathf.Clamp(e.scrollDelta.y, -3f, 3f)), MinZoom, MaxZoom);
            if (z < SnapToMin) z = MinZoom;
            SetZoom(z, Local(e.position));
            pan = ClampPan(pan, zoom);
            Apply();
        }

        /// <summary>An arrow under the point gets the tap; two quick taps on empty space reset the zoom.</summary>
        void Tap(Vector2 screen)
        {
            if (board.TapAt(screen, Cam()))
            {
                lastTapTime = -10f; // a tap on an arrow is never half of a double-tap
                return;
            }
            float now = Time.unscaledTime;
            if (now - lastTapTime <= DoubleTapTime && (screen - lastTapPos).magnitude <= DoubleTapDist * ScreenPerD())
            {
                lastTapTime = -10f;
                ResetZoom(true);
            }
            else
            {
                lastTapTime = now;
                lastTapPos = screen;
            }
        }

        // ------------------------------------------------------------------ zoom / pan

        void BeginPinch()
        {
            Vector2 a = Local(pos[fingers[0]]), b = Local(pos[fingers[1]]);
            pinchMid = (a + b) * 0.5f;
            pinchDist = Mathf.Max(1f, (a - b).magnitude);
        }

        /// <summary>Zoom around the previous midpoint, then follow the midpoint (two-finger pan).</summary>
        void Pinch()
        {
            Vector2 a = Local(pos[fingers[0]]), b = Local(pos[fingers[1]]);
            Vector2 mid = (a + b) * 0.5f;
            float dist = Mathf.Max(1f, (a - b).magnitude);
            ZoomBy(dist / pinchDist, pinchMid);
            Pan(mid - pinchMid);
            pinchMid = mid;
            pinchDist = dist;
        }

        /// <summary>Zoom by ratio around q (view-centre local), with rubber-band resistance outside MinZoom..MaxZoom.</summary>
        void ZoomBy(float ratio, Vector2 q)
        {
            float ln = Mathf.Log(ratio);
            float over = zoom < MinZoom ? Mathf.Log(MinZoom / zoom) : zoom > MaxZoom ? Mathf.Log(zoom / MaxZoom) : 0f;
            if (over > 0f && (zoom < MinZoom) == (ln < 0f)) ln *= Resist(over, RubberZoom);
            SetZoom(zoom * Mathf.Exp(ln), q);
        }

        /// <summary>New zoom keeping the content point under q in place.</summary>
        void SetZoom(float z, Vector2 q)
        {
            pan = q - (z / zoom) * (q - pan);
            zoom = z;
            Apply();
        }

        /// <summary>Pan by d (canvas units), with rubber-band resistance past the pan limits.</summary>
        void Pan(Vector2 d)
        {
            Vector2 lim = PanLimit(zoom);
            pan.x += d.x * PanResist(pan.x, d.x, lim.x);
            pan.y += d.y * PanResist(pan.y, d.y, lim.y);
            Apply();
        }

        float PanResist(float p, float d, float lim)
        {
            float over = Mathf.Abs(p) - lim;
            if (over <= 0f || p * d <= 0f) return 1f;
            return Resist(over, Theme.D(RubberPan));
        }

        static float Resist(float over, float len)
        {
            return 0.5f * len / (len + over);
        }

        /// <summary>
        /// Max |pan| per axis: the part of the zoomed card that sticks out of the view, plus a small margin
        /// (grows in over the first 0.25x so 1x stays centred).
        /// </summary>
        Vector2 PanLimit(float z)
        {
            Vector2 slack = (content.rect.size * z - view.rect.size) * 0.5f;
            float m = Theme.D(PanMargin) * Mathf.Clamp01((z - MinZoom) / 0.25f);
            return new Vector2(Mathf.Max(0f, slack.x) + m, Mathf.Max(0f, slack.y) + m);
        }

        Vector2 ClampPan(Vector2 p, float z)
        {
            Vector2 lim = PanLimit(z);
            return new Vector2(Mathf.Clamp(p.x, -lim.x, lim.x), Mathf.Clamp(p.y, -lim.y, lim.y));
        }

        /// <summary>All fingers up: ease back inside the zoom range (around the last pinch point) and the pan limits.</summary>
        void Settle()
        {
            float z = Mathf.Clamp(zoom, MinZoom, MaxZoom);
            if (z < SnapToMin) z = MinZoom;
            Vector2 p = pinchMid - (z / zoom) * (pinchMid - pan);
            AnimateTo(z, ClampPan(p, z), SettleTime);
        }

        void AnimateTo(float z, Vector2 p, float time)
        {
            StopAnim();
            if (Mathf.Approximately(z, zoom) && (p - pan).sqrMagnitude < 0.01f)
            {
                zoom = z;
                pan = p;
                Apply();
                return;
            }
            anim = StartCoroutine(AnimRoutine(z, p, time));
        }

        IEnumerator AnimRoutine(float z1, Vector2 p1, float time)
        {
            float z0 = zoom;
            Vector2 p0 = pan;
            float t = 0f;
            while (t < time)
            {
                t += Time.unscaledDeltaTime;
                float k = EaseOutCubic(Mathf.Clamp01(t / time));
                zoom = Mathf.Lerp(z0, z1, k);
                pan = Vector2.Lerp(p0, p1, k);
                Apply();
                yield return null;
            }
            zoom = z1;
            pan = p1;
            Apply();
            anim = null;
        }

        void StopAnim()
        {
            if (anim != null) StopCoroutine(anim);
            anim = null;
        }

        void Apply()
        {
            content.localScale = new Vector3(zoom, zoom, 1f);
            content.anchoredPosition = pan;
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Screen point in view local units, relative to the view centre (the content's anchor).</summary>
        Vector2 Local(Vector2 screen)
        {
            Vector2 lp;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(view, screen, Cam(), out lp);
            return lp - view.rect.center;
        }

        Camera Cam()
        {
            if (canvas == null) canvas = view.GetComponentInParent<Canvas>().rootCanvas;
            return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }

        /// <summary>Screen pixels per design px.</summary>
        float ScreenPerD()
        {
            Cam();
            return Theme.D(1f) * canvas.scaleFactor;
        }

        static float EaseOutCubic(float x)
        {
            float a = 1f - x;
            return 1f - a * a * a;
        }
    }
}
