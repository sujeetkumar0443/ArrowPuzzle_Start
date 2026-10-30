using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArrowPuzzle
{
    /// <summary>Polyline helpers (arc-length parametrised paths).</summary>
    public static class Poly
    {
        public static float[] Cum(List<Vector2> p)
        {
            var c = new float[p.Count];
            for (int i = 1; i < p.Count; i++) c[i] = c[i - 1] + Vector2.Distance(p[i - 1], p[i]);
            return c;
        }

        public static float Length(List<Vector2> p)
        {
            float l = 0f;
            for (int i = 1; i < p.Count; i++) l += Vector2.Distance(p[i - 1], p[i]);
            return l;
        }

        public static Vector2 PointAt(List<Vector2> p, float[] cum, float s)
        {
            s = Mathf.Clamp(s, 0f, cum[cum.Length - 1]);
            for (int i = 1; i < p.Count; i++)
            {
                if (s <= cum[i])
                {
                    float seg = cum[i] - cum[i - 1];
                    float t = seg < 1e-4f ? 0f : (s - cum[i - 1]) / seg;
                    return Vector2.Lerp(p[i - 1], p[i], t);
                }
            }
            return p[p.Count - 1];
        }

        /// <summary>Writes the part of the polyline between arc positions s0 and s1 into <paramref name="res"/>.</summary>
        public static void Slice(List<Vector2> p, float[] cum, float s0, float s1, List<Vector2> res)
        {
            res.Clear();
            res.Add(PointAt(p, cum, s0));
            for (int i = 1; i < p.Count - 1; i++)
                if (cum[i] > s0 && cum[i] < s1) res.Add(p[i]);
            res.Add(PointAt(p, cum, s1));
        }
    }

    /// <summary>
    /// Draws a thick rounded polyline with an arrow head at the last point.
    /// Points are in the local space of this RectTransform (pivot = origin).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class ArrowGraphic : MaskableGraphic
    {
        public float thickness = 14f;
        public float headSize = 34f;
        public bool drawHead = true;
        public float widthFactor = 0.58f; // half width of the head relative to headSize

        readonly List<Vector2> pts = new List<Vector2>();
        readonly List<Vector2> clean = new List<Vector2>(); // reused every rebuild (no per-frame allocations)
        readonly List<Vector2> line = new List<Vector2>();

        public void SetPoints(List<Vector2> p)
        {
            pts.Clear();
            pts.AddRange(p);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (pts.Count < 2) return;

            // clean: drop zero-length segments
            clean.Clear();
            clean.Add(pts[0]);
            for (int i = 1; i < pts.Count; i++)
                if ((pts[i] - clean[clean.Count - 1]).sqrMagnitude > 0.01f) clean.Add(pts[i]);
            if (clean.Count < 2) return;

            Vector2 tip = clean[clean.Count - 1];
            Vector2 dir = (tip - clean[clean.Count - 2]).normalized;
            float half = headSize * 0.5f;

            line.Clear();
            line.AddRange(clean);
            if (drawHead)
            {
                // shorten the line so it ends inside the arrow head
                float trim = Mathf.Max(0f, half - 1f);
                TrimEnd(line, trim);
            }

            for (int i = 0; i + 1 < line.Count; i++) Quad(vh, line[i], line[i + 1], thickness);
            for (int i = 0; i < line.Count; i++) Disc(vh, line[i], thickness * 0.5f);

            if (drawHead)
            {
                Vector2 perp = new Vector2(-dir.y, dir.x);
                Vector2 apex = tip + dir * half;
                Vector2 bc = tip - dir * half;
                float w = headSize * widthFactor;
                int b = vh.currentVertCount;
                vh.AddVert(apex, color, Vector4.zero);
                vh.AddVert(bc + perp * w, color, Vector4.zero);
                vh.AddVert(bc - perp * w, color, Vector4.zero);
                vh.AddTriangle(b, b + 1, b + 2);
            }
        }

        static void TrimEnd(List<Vector2> l, float amount)
        {
            while (amount > 0f && l.Count >= 2)
            {
                Vector2 a = l[l.Count - 2], b = l[l.Count - 1];
                float len = Vector2.Distance(a, b);
                if (len > amount)
                {
                    l[l.Count - 1] = Vector2.Lerp(b, a, amount / len);
                    return;
                }
                amount -= len;
                if (l.Count > 2) l.RemoveAt(l.Count - 1);
                else { l[l.Count - 1] = a; return; }
            }
        }

        void Quad(VertexHelper vh, Vector2 a, Vector2 b, float w)
        {
            Vector2 d = b - a;
            if (d.sqrMagnitude < 1e-6f) return;
            d.Normalize();
            Vector2 n = new Vector2(-d.y, d.x) * (w * 0.5f);
            int i = vh.currentVertCount;
            vh.AddVert(a - n, color, Vector4.zero);
            vh.AddVert(a + n, color, Vector4.zero);
            vh.AddVert(b + n, color, Vector4.zero);
            vh.AddVert(b - n, color, Vector4.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i + 2, i + 3, i);
        }

        void Disc(VertexHelper vh, Vector2 c, float r)
        {
            const int seg = 12;
            int start = vh.currentVertCount;
            vh.AddVert(c, color, Vector4.zero);
            for (int i = 0; i < seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                vh.AddVert(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, color, Vector4.zero);
            }
            for (int i = 0; i < seg; i++)
                vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % seg);
        }
    }
}
