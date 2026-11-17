using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArrowPuzzle
{
    /// <summary>
    /// The "ARROW / coral arrow / PUZZLE" wordmark. The splash plays its intro with Apply(t); Home shows the end state
    /// with Apply(Wordmark.End), so both are identical. Root is a 390 x 170 design px rect around the wordmark's
    /// centre (the arrow shaft sits on y = 0); the caller places it.
    /// </summary>
    public class Wordmark
    {
        public const float WidthD = 390f, HeightD = 170f;

        // layout (design px, y up from Root's centre)
        const float ArrowSizeD = 78f, ArrowCenterD = 44f, ArrowTrackD = 2f, ShadowD = 7f;
        const float ShaftThickD = 9f, HeadD = 28f;
        const float PuzzleSizeD = 40f, PuzzleCenterD = -42f, PuzzleTrackD = 4f, PuzzleWideD = 18f, PuzzleRiseD = 16f;

        // timeline (s): letters drop one by one, the shaft grows, the head pops, PUZZLE slides up
        const float DropStart = 0.1f, DropStep = 0.2f, DropTime = 0.5f, DropFromD = 90f;
        public const float ShaftStart = 1.3f;
        const float ShaftTime = 0.45f;
        public const float HeadStart = 1.7f;
        const float HeadTime = 0.35f;
        const float PuzzleStart = 1.9f, PuzzleTime = 0.6f;
        public const float End = PuzzleStart + PuzzleTime; // 2.5 s

        public RectTransform Root { get; private set; }

        Text[] arrow, puzzle;
        float[] arrowX, puzzleW;
        ArrowGraphic shaft, head;
        float shaftX0, tipX, lastShaft = -1f;
        readonly List<Vector2> pts = new List<Vector2>(2);

        public static Wordmark Build(Transform parent)
        {
            var w = new Wordmark();
            w.Root = Ui.New(parent, "Wordmark");
            w.Root.sizeDelta = new Vector2(Theme.D(WidthD), Theme.D(HeightD));

            float[] arrowW;
            w.arrow = w.Letters("ARROW", ArrowSizeD, Theme.Ink, out arrowW);
            foreach (var t in w.arrow)
            {
                var sh = t.gameObject.AddComponent<Shadow>();
                sh.effectColor = Theme.CardShadow;
                sh.effectDistance = new Vector2(0f, -Theme.D(ShadowD));
                sh.useGraphicAlpha = true;
            }
            w.arrowX = new float[w.arrow.Length];
            float arrowHalf = Centers(arrowW, Theme.D(ArrowTrackD), w.arrowX);

            w.puzzle = w.Letters("PUZZLE", PuzzleSizeD, Theme.Accent, out w.puzzleW);

            // coral arrow under ARROW: the shaft runs from the left edge of the A to the head, whose tip is at the
            // right edge of the W
            float half = Theme.D(HeadD) * 0.5f;
            w.tipX = arrowHalf - half;
            w.shaftX0 = -arrowHalf + Theme.D(ShaftThickD) * 0.5f;

            var srt = Ui.New(w.Root, "Shaft");
            srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(0.5f, 0.5f);
            srt.sizeDelta = Vector2.zero;
            w.shaft = srt.gameObject.AddComponent<ArrowGraphic>();
            w.shaft.raycastTarget = false;
            w.shaft.drawHead = false;
            w.shaft.thickness = Theme.D(ShaftThickD);
            w.shaft.color = Theme.Accent;

            var hrt = Ui.New(w.Root, "Head");
            hrt.anchorMin = hrt.anchorMax = hrt.pivot = new Vector2(0.5f, 0.5f);
            hrt.sizeDelta = Vector2.zero;
            hrt.anchoredPosition = new Vector2(w.tipX, 0f);
            w.head = hrt.gameObject.AddComponent<ArrowGraphic>();
            w.head.raycastTarget = false;
            w.head.thickness = 0f; // only the triangle; its centre is this rect's pivot, so it pops in place
            w.head.headSize = Theme.D(HeadD);
            w.head.color = Theme.Accent;
            w.head.SetPoints(new List<Vector2> { new Vector2(-1f, 0f), Vector2.zero });

            w.Apply(End);
            return w;
        }

        Text[] Letters(string s, float sizeD, Color c, out float[] widths)
        {
            var res = new Text[s.Length];
            widths = new float[s.Length];
            for (int i = 0; i < s.Length; i++)
            {
                var t = Ui.Label(Root, s[i].ToString(), sizeD, c, Weight.Bold);
                var rt = t.rectTransform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                widths[i] = t.preferredWidth;
                rt.sizeDelta = new Vector2(widths[i] + Theme.D(12f), Theme.D(sizeD * 1.3f));
                res[i] = t;
            }
            return res;
        }

        /// <summary>Letter centres for widths w with gap track (canvas units), centred on 0. Returns half the total width.</summary>
        static float Centers(float[] w, float track, float[] res)
        {
            float total = track * (w.Length - 1);
            for (int i = 0; i < w.Length; i++) total += w[i];
            float x = -total * 0.5f;
            for (int i = 0; i < w.Length; i++)
            {
                res[i] = x + w[i] * 0.5f;
                x += w[i] + track;
            }
            return total * 0.5f;
        }

        /// <summary>Pose at t seconds into the intro; t >= End is the final wordmark.</summary>
        public void Apply(float t)
        {
            // ARROW: each letter drops from 90 px above, scale 0.7 -> 1.06 -> 1, small bounce
            for (int i = 0; i < arrow.Length; i++)
            {
                float x = (t - (DropStart + i * DropStep)) / DropTime;
                var l = arrow[i];
                l.enabled = x > 0f;
                if (x <= 0f) continue;
                float k = Mathf.Clamp01(x);
                float y = ArrowCenterD + DropFromD * (1f - BackOut(k));
                l.rectTransform.anchoredPosition = new Vector2(arrowX[i], Theme.D(y));
                l.rectTransform.localScale = Vector3.one * Overshoot(k, 0.7f, 1.06f);
                SetAlpha(l, Mathf.Clamp01(k / 0.2f));
            }

            // shaft grows left to right
            float s = Mathf.Clamp01((t - ShaftStart) / ShaftTime);
            shaft.enabled = s > 0f;
            if (s > 0f && s != lastShaft)
            {
                lastShaft = s;
                pts.Clear();
                pts.Add(new Vector2(shaftX0, 0f));
                pts.Add(new Vector2(Mathf.Lerp(shaftX0, tipX, EaseOutCubic(s)), 0f));
                shaft.SetPoints(pts);
            }

            // head pops: scale 0 -> 1.3 -> 1
            float h = (t - HeadStart) / HeadTime;
            head.enabled = h > 0f;
            if (h > 0f) head.rectTransform.localScale = Vector3.one * Overshoot(Mathf.Clamp01(h), 0f, 1.3f);

            // PUZZLE fades in, slides up 16 px, letter spacing closes from wide to normal
            float p = Mathf.Clamp01((t - PuzzleStart) / PuzzleTime);
            float e = EaseOutCubic(p);
            float track = Theme.D(Mathf.Lerp(PuzzleWideD, PuzzleTrackD, e));
            float py = Theme.D(PuzzleCenterD - PuzzleRiseD * (1f - e));
            float total = track * (puzzle.Length - 1);
            for (int i = 0; i < puzzleW.Length; i++) total += puzzleW[i];
            float px = -total * 0.5f;
            for (int i = 0; i < puzzle.Length; i++)
            {
                var l = puzzle[i];
                l.enabled = p > 0f;
                l.rectTransform.anchoredPosition = new Vector2(px + puzzleW[i] * 0.5f, py);
                px += puzzleW[i] + track;
                SetAlpha(l, Mathf.SmoothStep(0f, 1f, p));
            }
        }

        static void SetAlpha(Graphic g, float a)
        {
            var c = g.color;
            if (Mathf.Approximately(c.a, a)) return;
            c.a = a;
            g.color = c;
        }

        /// <summary>from -> peak over the first 60 %, then settles to 1.</summary>
        static float Overshoot(float k, float from, float peak)
        {
            if (k < 0.6f)
            {
                float u = k / 0.6f;
                return Mathf.Lerp(from, peak, 1f - (1f - u) * (1f - u));
            }
            return Mathf.Lerp(peak, 1f, Mathf.SmoothStep(0f, 1f, (k - 0.6f) / 0.4f));
        }

        static float EaseOutCubic(float x) { float a = 1f - x; return 1f - a * a * a; }

        /// <summary>Ease-out with a small overshoot (about 6 % past the end), used for the letter drop bounce.</summary>
        static float BackOut(float x)
        {
            const float c1 = 1.2f, c3 = c1 + 1f;
            float a = x - 1f;
            return 1f + c3 * a * a * a + c1 * a * a;
        }
    }

    /// <summary>
    /// Faint arrows drifting left to right behind the content (splash and Home). Colours are pre-blended with the
    /// cream background at 20 % so overlapping parts of an arrow do not darken. Unscaled time.
    /// </summary>
    public class DriftingArrows : MonoBehaviour
    {
        const int Count = 8;
        const float Opacity = 0.2f;

        class Item
        {
            public RectTransform rt;
            public float x, xFrac, yFrac, speed, half;
        }

        readonly List<Item> items = new List<Item>(Count);
        RectTransform area;
        bool placed;

        /// <summary>Adds a full-size layer of drifting arrows under parent (put it right after the background).</summary>
        public static DriftingArrows Add(Transform parent, int seed)
        {
            var layer = Ui.New(parent, "DriftArrows");
            Ui.Fill(layer);
            var d = layer.gameObject.AddComponent<DriftingArrows>();
            d.area = layer;

            Color[] colors = { Theme.Teal, Theme.Gold, Theme.Palette[4], Theme.Accent, Theme.Palette[7] }; // teal, gold, purple, coral, blue
            var rng = new System.Random(seed);
            for (int i = 0; i < Count; i++)
            {
                float lenD = 36f + (float)rng.NextDouble() * 30f;
                var rt = Ui.New(layer, "Arrow");
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = Vector2.zero;
                var g = rt.gameObject.AddComponent<ArrowGraphic>();
                g.raycastTarget = false;
                g.color = Color.Lerp(Theme.Bg, colors[i % colors.Length], Opacity);
                g.thickness = Theme.D(8f);
                g.headSize = Theme.D(20f);
                g.SetPoints(new List<Vector2> { new Vector2(-Theme.D(lenD * 0.5f), 0f), new Vector2(Theme.D(lenD * 0.5f), 0f) });

                d.items.Add(new Item
                {
                    rt = rt,
                    xFrac = (float)rng.NextDouble() - 0.5f,
                    yFrac = (i + 0.2f + 0.6f * (float)rng.NextDouble()) / Count - 0.5f, // one per horizontal band
                    speed = Theme.D(14f + (float)rng.NextDouble() * 22f),
                    half = Theme.D(lenD * 0.5f + 14f),
                });
            }
            // bands top to bottom would pair colours with heights; shuffle the heights
            for (int i = d.items.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                float y = d.items[i].yFrac;
                d.items[i].yFrac = d.items[j].yFrac;
                d.items[j].yFrac = y;
            }
            return d;
        }

        void Update()
        {
            Rect r = area.rect;
            if (r.width <= 0f) return;
            float halfW = r.width * 0.5f, dt = Time.unscaledDeltaTime;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (!placed) it.x = it.xFrac * r.width;
                else it.x += it.speed * dt;
                if (it.x - it.half > halfW) it.x = -halfW - it.half;
                it.rt.anchoredPosition = new Vector2(it.x, it.yFrac * r.height);
            }
            placed = true;
        }
    }
}
