using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArrowPuzzle
{
    /// <summary>
    /// A thick polyline with round caps and joins in one mesh, or (dotSpacing > 0) a row of round dots along it.
    /// Points are in the local space of this RectTransform (pivot = origin), sizes in canvas units.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class PathGraphic : MaskableGraphic
    {
        public float width = 24f;      // line width, or dot diameter when dotted
        public float dotSpacing;       // > 0: dots every dotSpacing along the path instead of a line
        const int DiscSegments = 16;

        readonly List<Vector2> pts = new List<Vector2>();

        /// <summary>Uses the first count points of p (all when count &lt; 0).</summary>
        public void SetPath(List<Vector2> p, int count = -1)
        {
            pts.Clear();
            int n = count < 0 ? p.Count : Mathf.Min(count, p.Count);
            for (int i = 0; i < n; i++) pts.Add(p[i]);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (pts.Count == 0) return;
            float r = width * 0.5f;

            if (dotSpacing > 0f)
            {
                // a dot at the start, then every dotSpacing of arc length
                Disc(vh, pts[0], r);
                float next = dotSpacing;
                float walked = 0f;
                for (int i = 1; i < pts.Count; i++)
                {
                    Vector2 a = pts[i - 1], b = pts[i];
                    float len = Vector2.Distance(a, b);
                    while (next <= walked + len)
                    {
                        Disc(vh, Vector2.Lerp(a, b, (next - walked) / Mathf.Max(1e-4f, len)), r);
                        next += dotSpacing;
                    }
                    walked += len;
                }
                return;
            }

            for (int i = 1; i < pts.Count; i++) Quad(vh, pts[i - 1], pts[i], r);
            for (int i = 0; i < pts.Count; i++) Disc(vh, pts[i], r); // round caps and joins
        }

        void Quad(VertexHelper vh, Vector2 a, Vector2 b, float r)
        {
            Vector2 d = b - a;
            if (d.sqrMagnitude < 1e-6f) return;
            Vector2 n = new Vector2(-d.y, d.x).normalized * r;
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
            int start = vh.currentVertCount;
            vh.AddVert(c, color, Vector4.zero);
            for (int i = 0; i < DiscSegments; i++)
            {
                float a = i / (float)DiscSegments * Mathf.PI * 2f;
                vh.AddVert(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, color, Vector4.zero);
            }
            for (int i = 0; i < DiscSegments; i++)
                vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % DiscSegments);
        }
    }
}
