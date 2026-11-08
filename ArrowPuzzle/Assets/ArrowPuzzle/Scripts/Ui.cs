using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ArrowPuzzle
{
    /// <summary>Helpers for building uGUI in code. Sizes named "D" are design pixels (see Theme.K).</summary>
    public static class Ui
    {
        // shimmer (AddShimmer): one sweep every ShimmerPeriod s, taking ShimmerSweep s, stripes tilted ShimmerAngle
        public const float ShimmerPeriod = 2.7f;
        public const float ShimmerSweep = 0.7f;
        public const float ShimmerAngle = -20f;   // Unity z: negative = clockwise, the stripe leans forward
        public const float ShimmerPeak = 0.5f;    // peak alpha of the main stripe

        public static readonly Vector2 TL = new Vector2(0f, 1f);
        public static readonly Vector2 TC = new Vector2(0.5f, 1f);
        public static readonly Vector2 TR = new Vector2(1f, 1f);
        public static readonly Vector2 C = new Vector2(0.5f, 0.5f);
        public static readonly Vector2 BL = new Vector2(0f, 0f);
        public static readonly Vector2 BC = new Vector2(0.5f, 0f);
        public static readonly Vector2 BR = new Vector2(1f, 0f);

        public static RectTransform New(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Fill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>Stretches rt (a child of a full-screen rect) over Screen.safeArea: below the notch, above the gesture bar.</summary>
        public static void FitSafeArea(RectTransform rt)
        {
            if (Screen.width <= 0 || Screen.height <= 0) { Fill(rt); return; }
            Rect s = Screen.safeArea;
            rt.anchorMin = new Vector2(s.xMin / Screen.width, s.yMin / Screen.height);
            rt.anchorMax = new Vector2(s.xMax / Screen.width, s.yMax / Screen.height);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>Fixed-size rect in design px. Pivot equals anchor, so pos is the offset of that point from the same point of the parent.</summary>
        public static void PlaceD(RectTransform rt, Vector2 anchor, Vector2 posD, Vector2 sizeD)
        {
            PlaceD(rt, anchor, anchor, posD, sizeD);
        }

        public static void PlaceD(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 posD, Vector2 sizeD)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = posD * Theme.K;
            rt.sizeDelta = sizeD * Theme.K;
        }

        public static Image Img(Transform parent, string name, Sprite sprite, Color color, float radiusD = 0f)
        {
            var rt = New(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            if (sprite != null && sprite.border.sqrMagnitude > 0f)
            {
                img.type = Image.Type.Sliced;
                if (radiusD > 0f) img.pixelsPerUnitMultiplier = 24f / (radiusD * Theme.K);
            }
            return img;
        }

        public static Text Label(Transform parent, string text, float sizeD, Color color, Weight weight = Weight.Bold, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var rt = New(parent, "Label");
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Theme.GetFont(weight);
            t.text = text;
            t.fontSize = Mathf.RoundToInt(sizeD * Theme.K);
            t.fontStyle = FontStyle.Normal;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Rounded button with a raised bottom edge (like the design). Optional icon on the left of the label.</summary>
        public static Button Btn(Transform parent, string label, Color bg, Color fg, float fontD, float radiusD,
                                 Color shadow, float shadowD, Action onClick, Icon? icon = null, float iconD = 24f,
                                 Weight weight = Weight.Bold)
        {
            var img = Img(parent, "Btn_" + label, Theme.Rounded, bg, radiusD);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            img.gameObject.AddComponent<PressScale>();

            if (shadowD > 0f)
            {
                var sh = img.gameObject.AddComponent<Shadow>();
                sh.effectColor = shadow;
                sh.effectDistance = new Vector2(0f, -shadowD * Theme.K);
                sh.useGraphicAlpha = false;
            }

            var row = img.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter;
            row.spacing = 10f * Theme.K;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;

            if (icon.HasValue)
            {
                var ic = Icons.Make(img.transform, icon.Value, fg, iconD);
                var le = ic.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = iconD * Theme.K;
                le.preferredHeight = iconD * Theme.K;
            }
            if (!string.IsNullOrEmpty(label)) Label(img.transform, label, fontD, fg, weight);

            btn.onClick.AddListener(() =>
            {
                Sfx.Play(Sfx.Id.Click);
                if (onClick != null) onClick();
            });
            return btn;
        }

        public static LayoutElement Size(Component c, float widthD, float heightD)
        {
            var le = c.gameObject.AddComponent<LayoutElement>();
            if (widthD > 0f) { le.preferredWidth = widthD * Theme.K; le.minWidth = widthD * Theme.K; }
            if (heightD > 0f) { le.preferredHeight = heightD * Theme.K; le.minHeight = heightD * Theme.K; }
            return le;
        }

        public static VerticalLayoutGroup Column(RectTransform rt, float padSideD, float padTopD, float padBottomD, float spacingD,
                                                 TextAnchor align = TextAnchor.UpperCenter)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(Mathf.RoundToInt(padSideD * Theme.K), Mathf.RoundToInt(padSideD * Theme.K),
                                       Mathf.RoundToInt(padTopD * Theme.K), Mathf.RoundToInt(padBottomD * Theme.K));
            v.spacing = spacingD * Theme.K;
            v.childAlignment = align;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup Row(RectTransform rt, float spacingD, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacingD * Theme.K;
            h.childAlignment = align;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            return h;
        }

        public static SwitchView Switch(Transform parent, Func<bool> get, Action<bool> set)
        {
            var track = Img(parent, "Switch", Theme.Rounded, Theme.Teal, 15f);
            var sv = track.gameObject.AddComponent<SwitchView>();
            var btn = track.gameObject.AddComponent<Button>();
            btn.targetGraphic = track;
            btn.transition = Selectable.Transition.None;

            var knob = Img(track.transform, "Knob", Theme.Circle, Color.white);
            knob.raycastTarget = false;
            PlaceD(knob.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(3f, 0f), new Vector2(24f, 24f));

            sv.track = track;
            sv.knob = knob.rectTransform;
            sv.get = get;
            btn.onClick.AddListener(() =>
            {
                Sfx.Play(Sfx.Id.Click);
                set(!get());
                sv.Refresh();
            });
            sv.Refresh();
            return sv;
        }

        /// <summary>
        /// Gloss that sweeps across a button every ShimmerPeriod s (unscaled). A masked "ShimmerClip" child with the
        /// button's rounded sprite and radiusD clips two soft white stripes to the button shape. phaseSeconds delays
        /// the sweeps so several buttons don't flash together. Call it after the button's label and icon exist.
        /// </summary>
        public static Shimmer AddShimmer(RectTransform button, float radiusD, float phaseSeconds = 0f)
        {
            var clip = Img(button, "ShimmerClip", Theme.Rounded, Theme.Card, radiusD);
            clip.raycastTarget = false;
            Fill(clip.rectTransform);
            clip.gameObject.AddComponent<LayoutElement>().ignoreLayout = true; // buttons have a layout group
            clip.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            clip.transform.SetAsLastSibling(); // above label and icon

            var main = ShimmerStripe(clip.transform, ShimmerPeak);
            var trail = ShimmerStripe(clip.transform, 0.35f);

            var sh = clip.gameObject.AddComponent<Shimmer>();
            sh.main = main;
            sh.trail = trail;
            sh.phase = phaseSeconds;
            return sh;
        }

        static RectTransform ShimmerStripe(Transform parent, float peakAlpha)
        {
            var c = Theme.Card;
            c.a = peakAlpha;
            var img = Img(parent, "Stripe", ShimmerSprite, c);
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localEulerAngles = new Vector3(0f, 0f, ShimmerAngle);
            rt.anchoredPosition = new Vector2(-100000f, 0f); // parked until the first Update measures the button
            return rt;
        }

        static Sprite shimmerSprite;

        /// <summary>64 px wide white stripe whose alpha falls off smoothly towards both edges.</summary>
        static Sprite ShimmerSprite
        {
            get
            {
                if (shimmerSprite != null) return shimmerSprite;
                const int w = 64, h = 4;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;
                var px = new Color32[w * h];
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    float a = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Abs(u * 2f - 1f));
                    for (int y = 0; y < h; y++) px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
                tex.SetPixels32(px);
                tex.Apply();
                shimmerSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
                return shimmerSprite;
            }
        }
    }

    /// <summary>On/off switch (track + knob).</summary>
    public class SwitchView : MonoBehaviour
    {
        public Image track;
        public RectTransform knob;
        public Func<bool> get;

        public void Refresh()
        {
            bool on = get != null && get();
            track.color = on ? Theme.Teal : Theme.SwitchOff;
            knob.anchoredPosition = new Vector2((on ? 25f : 3f) * Theme.K, 0f);
        }
    }

    /// <summary>
    /// Gloss sweeping across a button (see Ui.AddShimmer). Lives on the "ShimmerClip" child, whose Mask clips the
    /// stripes to the button's rounded shape. Sizes follow the button's rect, so it also works under layout groups.
    /// </summary>
    public class Shimmer : MonoBehaviour
    {
        public RectTransform main, trail;
        public float phase;

        RectTransform rt;
        Vector2 lastSize = new Vector2(-1f, -1f);
        float t0, x1Start, x1End, trailOffset;

        void Awake()
        {
            rt = (RectTransform)transform;
        }

        void OnEnable()
        {
            t0 = Time.unscaledTime;
            if (rt != null) Resize(rt.rect.size); // re-measured every Update if the layout changes it
            Place(0f);
        }

        void OnDisable()
        {
            Place(0f);
        }

        void Update()
        {
            Vector2 size = rt.rect.size;
            if (size != lastSize) Resize(size);

            float u = Mathf.Repeat(Time.unscaledTime - t0 - phase, Ui.ShimmerPeriod);
            Place(u < Ui.ShimmerSweep ? Mathf.SmoothStep(0f, 1f, u / Ui.ShimmerSweep) : 0f); // rest = parked off the left edge
        }

        void Resize(Vector2 size)
        {
            if (main == null || trail == null) return; // AddComponent runs OnEnable before AddShimmer sets them
            lastSize = size;
            float w1 = Mathf.Max(size.x * 0.13f, Theme.D(14f));
            float w2 = w1 * 0.4f;
            float h = size.y * 1.8f;
            main.sizeDelta = new Vector2(w1, h);
            trail.sizeDelta = new Vector2(w2, h);

            // half the horizontal footprint of a tilted stripe
            float a = Mathf.Abs(Ui.ShimmerAngle) * Mathf.Deg2Rad;
            float ext1 = (w1 * Mathf.Cos(a) + h * Mathf.Sin(a)) * 0.5f;
            float ext2 = (w2 * Mathf.Cos(a) + h * Mathf.Sin(a)) * 0.5f;

            trailOffset = w1 * 0.5f + w1 * 0.25f + w2 * 0.5f; // trail sits 25 % of w1 behind the main stripe
            x1Start = -size.x * 0.5f - ext1;                   // both stripes left of the button
            x1End = size.x * 0.5f + ext2 + trailOffset;        // both stripes right of the button
        }

        void Place(float k)
        {
            if (main == null || trail == null) return;
            float x1 = Mathf.Lerp(x1Start, x1End, k);
            main.anchoredPosition = new Vector2(x1, 0f);
            trail.anchoredPosition = new Vector2(x1 - trailOffset, 0f);
        }
    }

    /// <summary>Small squash effect while a button is pressed.</summary>
    public class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public void OnPointerDown(PointerEventData e) { transform.localScale = Vector3.one * 0.95f; }
        public void OnPointerUp(PointerEventData e) { transform.localScale = Vector3.one; }
        public void OnPointerExit(PointerEventData e) { transform.localScale = Vector3.one; }
        void OnDisable() { transform.localScale = Vector3.one; }
    }
}
