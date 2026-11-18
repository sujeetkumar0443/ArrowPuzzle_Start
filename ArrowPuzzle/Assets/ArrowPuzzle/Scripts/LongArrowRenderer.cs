using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArrowPuzzle
{
    /// <summary>Colours and sizes of the long-arrow renderer, all in one place (the theme system replaces this later).</summary>
    public static class LongStyle
    {
        // ---- guide lines (Grid button: lines along the board cell edges), tune here
        public const float GuideLineCells = 0.14f;     // width, cells (scales with the zoom)
        public const float GuideLineAlpha = 0.26f;     // InkSoft at this alpha...
        public const float GuideLineAlphaDark = 0.32f; // ...and this on dark themes (Night)
        public const float GuideLineMinPx = 2f;        // never thinner than this on screen

        // ---- stroke shape, in cells (scales with the board and the zoom)
        public const float LineCells = 0.19f;      // line width
        public const float HeadCells = 0.62f;      // arrowhead length
        public const float HeadWideCells = 0.6f;   // arrowhead width (base of the triangle)
        public const float HeadRoundCells = 0.05f; // arrowhead corner rounding radius
        public const float ApexMaxCell = 0.4f;     // apex at most this far past the head cell centre
        public const float CornerCell = 0.5f;      // path corner radius
        public const float LineMinPx = 2f;         // dense boards: the line is never thinner than this on screen
        public const float FeatherPx = 1f;         // alpha ramp on each edge, screen px
        public const float TileCell = 0.92f;       // tile square size, cells
        public const float PressMul = 1.35f, HaloMul = 4f, HintGlowMul = 4.5f;
        public const float GlowMul = 3f, GlowAlpha = 0.22f; // theme Glow (night): soft copy under each static arrow
        public const bool BoardTiles = false;               // game board: no cell tiles (arrows on the screen background)
        public const float GridFadeIn = 0.15f, GridFadeOut = 0.25f; // s: guide lines on / off
        public const float DotCell = 0.11f, DotMinPx = 1.2f;        // board dot radius: cells, at least screen px
        public const float DotAlpha = 0.22f, DotAlphaDark = 0.28f;  // board dots: InkSoft at this alpha (dark themes: more)
        public const float DotFadeIn = 0.2f;                         // s: a dot appears once its cell is empty

        /// <summary>Board dot colour for the current theme: InkSoft at DotAlpha (DotAlphaDark on dark themes: Night).</summary>
        public static Color DotColor
        {
            get
            {
                var c = Theme.InkSoft;
                c.a = Theme.Current != null && Theme.Current.Dark ? DotAlphaDark : DotAlpha;
                return c;
            }
        }

        /// <summary>Guide line colour for the current theme: InkSoft at GuideLineAlpha (GuideLineAlphaDark on dark themes: Night).</summary>
        public static Color GridColor
        {
            get
            {
                var c = Theme.InkSoft;
                c.a = Theme.Current != null && Theme.Current.Dark ? GuideLineAlphaDark : GuideLineAlpha;
                return c;
            }
        }

        // ---- animation timings (tune here; BoardView's long-arrow routines read them)
        public const float ExitCellsPerSecond = 110f; // exit speed once the ease-in is done
        public const float ExitMaxSeconds = 0.5f;     // pull-back + slide never take longer: long arrows go faster
        public const float PullBackSeconds = 0.05f;   // short pull-back before the launch...
        public const float PullBackCells = 0.08f;     // ...over this distance
        public const float ExitStartSpeed = 0.4f;     // ease-in: the launch starts at 40 % of full speed...
        public const float ExitEaseSeconds = 0.08f;   // ...and reaches full speed after this
        public const float BumpCellsPerSecond = 80f;  // blocked tap: slide up to the blocker
        public const float RecoilSeconds = 0.31f;     // blocked tap: spring back to rest

        /// <summary>OLD: palette colour by index (arrow colours now come from the theme: Themes.ArrowColor).</summary>
        public static Color Arrow(int colorIndex) { return Theme.Palette[((colorIndex % Theme.Palette.Length) + Theme.Palette.Length) % Theme.Palette.Length]; }
        public static Color Tile { get { return Theme.Tile; } }
        public static Color Error { get { return Theme.Error; } }
        public static readonly Color Hint = Theme.Hex("#FFC400");
        public static readonly Color Grey = Theme.Hex("#9C9486"); // board collapse
        public static Color Pressed(Color c) { return Color.Lerp(c, Color.white, 0.45f); }
    }

    /// <summary>Sizes of one stroke in board-local units (BoardView's cell units), recomputed when the zoom changes.</summary>
    public struct StrokeStyle
    {
        public float hw, feather, corner, headLen, headHalf, apexOff, headRound;
        public bool head;

        /// <summary>
        /// Sizes for a cell size and a line width (both board-local), feather = 1 screen px, mul = width multiplier
        /// (press / halo / glow: the head grows with the line). Shared by the game board and LevelPreview.
        /// </summary>
        public static StrokeStyle Make(float cell, float line, float feather, float mul, bool head)
        {
            float w = line * mul, grow = (mul - 1f) * line;
            float headLen = LongStyle.HeadCells * cell + grow;
            return new StrokeStyle
            {
                hw = Mathf.Max(0.05f, w * 0.5f - feather * 0.5f),
                feather = feather,
                corner = cell * LongStyle.CornerCell,
                headLen = headLen,
                headHalf = LongStyle.HeadWideCells * 0.5f * cell + grow * 0.5f,
                apexOff = Mathf.Min(LongStyle.ApexMaxCell * cell, 0.5f * headLen),
                headRound = LongStyle.HeadRoundCells * cell,
                head = head,
            };
        }
    }

    /// <summary>
    /// One thin polyline as a mesh: collinear points merged, corners rounded (quadratic arcs), round start cap, filled
    /// triangle head. Every edge has an extra alpha-0 vertex ring FeatherPx outside it, so the default UI shader draws
    /// anti-aliased edges.
    /// </summary>
    public static class StrokeMesh
    {
        const int ArcSteps = 6, CapSteps = 8;
        static readonly List<Vector2> clean = new List<Vector2>();
        static readonly List<Vector2> line = new List<Vector2>();
        static readonly List<Vector2> dense = new List<Vector2>();

        /// <summary>Appends the stroke through pts (centre line, tail first) to vh.</summary>
        public static void Add(VertexHelper vh, List<Vector2> pts, StrokeStyle s, Color32 col)
        {
            Clean(pts, clean);
            if (clean.Count < 2) return;
            Vector2 tip = clean[clean.Count - 1];
            Vector2 dir = (tip - clean[clean.Count - 2]).normalized;

            line.Clear();
            line.AddRange(clean);
            Vector2 apex = tip;
            if (s.head)
            {
                apex = tip + dir * s.apexOff;
                // the line ends 30 % into the head, so no gap shows between them
                Vector2 lineEnd = apex - dir * (s.headLen * 0.7f);
                float trim = Vector2.Dot(tip - lineEnd, dir);
                if (trim > 0f) TrimEnd(line, trim);
                else line[line.Count - 1] = lineEnd;
            }

            if (Length(line) > 1e-3f)
            {
                Round(line, s.corner, dense);
                if (dense.Count >= 2)
                {
                    Strip(vh, dense, s.hw, s.feather, col);
                    Cap(vh, dense[0], (dense[0] - dense[1]).normalized, s.hw, s.feather, col);
                }
            }
            if (s.head) Head(vh, apex, dir, s.headLen, s.headHalf, s.headRound, s.feather, col);
        }

        /// <summary>The rounded centre line (no head trim), for hit tests.</summary>
        public static void Centerline(List<Vector2> pts, float corner, List<Vector2> result)
        {
            Clean(pts, clean);
            Round(clean, corner, result);
        }

        static void Clean(List<Vector2> src, List<Vector2> dst)
        {
            dst.Clear();
            for (int i = 0; i < src.Count; i++)
            {
                Vector2 q = src[i];
                if (dst.Count > 0 && (q - dst[dst.Count - 1]).sqrMagnitude < 1e-6f) continue;
                if (dst.Count >= 2)
                {
                    Vector2 a = dst[dst.Count - 1] - dst[dst.Count - 2], b = q - dst[dst.Count - 1];
                    float cross = a.x * b.y - a.y * b.x;
                    if (Mathf.Abs(cross) <= 1e-4f * a.magnitude * b.magnitude && Vector2.Dot(a, b) > 0f)
                    {
                        dst[dst.Count - 1] = q; // collinear: extend the last segment
                        continue;
                    }
                }
                dst.Add(q);
            }
        }

        static float Length(List<Vector2> l)
        {
            float s = 0f;
            for (int i = 1; i < l.Count; i++) s += Vector2.Distance(l[i - 1], l[i]);
            return s;
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

        /// <summary>Corners -> arcs of radius r, limited to half of an inner segment (all of an end segment).</summary>
        static void Round(List<Vector2> l, float r, List<Vector2> res)
        {
            res.Clear();
            int n = l.Count;
            if (n == 0) return;
            res.Add(l[0]);
            for (int k = 1; k < n - 1; k++)
            {
                Vector2 a = l[k - 1], v = l[k], b = l[k + 1];
                float la = Vector2.Distance(a, v), lb = Vector2.Distance(v, b);
                if (la < 1e-5f || lb < 1e-5f) continue;
                Vector2 da = (v - a) / la, db = (b - v) / lb;
                float rr = Mathf.Min(r, k - 1 == 0 ? la : la * 0.5f, k + 1 == n - 1 ? lb : lb * 0.5f);
                Vector2 p1 = v - da * rr, p2 = v + db * rr;
                AddPt(res, p1);
                for (int j = 1; j < ArcSteps; j++)
                {
                    float t = j / (float)ArcSteps, u = 1f - t;
                    AddPt(res, u * u * p1 + 2f * u * t * v + t * t * p2);
                }
                AddPt(res, p2);
            }
            AddPt(res, l[n - 1]);
        }

        static void AddPt(List<Vector2> res, Vector2 p)
        {
            if (res.Count == 0 || (p - res[res.Count - 1]).sqrMagnitude > 1e-6f) res.Add(p);
        }

        static void Strip(VertexHelper vh, List<Vector2> p, float hw, float f, Color32 col)
        {
            Color32 clear = col;
            clear.a = 0;
            int n = p.Count, start = vh.currentVertCount;
            for (int i = 0; i < n; i++)
            {
                Vector2 t = p[Mathf.Min(i + 1, n - 1)] - p[Mathf.Max(i - 1, 0)];
                t.Normalize();
                Vector2 nr = new Vector2(-t.y, t.x);
                vh.AddVert(p[i] - nr * (hw + f), clear, Vector4.zero);
                vh.AddVert(p[i] - nr * hw, col, Vector4.zero);
                vh.AddVert(p[i] + nr * hw, col, Vector4.zero);
                vh.AddVert(p[i] + nr * (hw + f), clear, Vector4.zero);
            }
            for (int i = 0; i < n - 1; i++)
            {
                int a = start + i * 4, b = a + 4;
                for (int j = 0; j < 3; j++)
                {
                    vh.AddTriangle(a + j, b + j, b + j + 1);
                    vh.AddTriangle(a + j, b + j + 1, a + j + 1);
                }
            }
        }

        /// <summary>Half disc behind c (back = direction away from the line), feathered rim.</summary>
        static void Cap(VertexHelper vh, Vector2 c, Vector2 back, float hw, float f, Color32 col)
        {
            Color32 clear = col;
            clear.a = 0;
            Vector2 side = new Vector2(-back.y, back.x);
            int center = vh.currentVertCount;
            vh.AddVert(c, col, Vector4.zero);
            for (int j = 0; j <= CapSteps; j++)
            {
                float th = -Mathf.PI * 0.5f + Mathf.PI * j / CapSteps;
                Vector2 d = back * Mathf.Cos(th) + side * Mathf.Sin(th);
                vh.AddVert(c + d * hw, col, Vector4.zero);
                vh.AddVert(c + d * (hw + f), clear, Vector4.zero);
            }
            for (int j = 0; j < CapSteps; j++)
            {
                int i0 = center + 1 + j * 2, i1 = i0 + 2;
                vh.AddTriangle(center, i0, i1);
                vh.AddTriangle(i0, i0 + 1, i1 + 1);
                vh.AddTriangle(i0, i1 + 1, i1);
            }
        }

        const int HeadArcSteps = 3;

        /// <summary>
        /// Filled triangle head (apex, base len behind it, half width half). round &gt; 0: corners rounded with that radius
        /// (the triangle shrunk around its incentre by round, then grown back by round with arcs), feathered rim.
        /// </summary>
        static void Head(VertexHelper vh, Vector2 apex, Vector2 dir, float len, float half, float round, float f, Color32 col)
        {
            Color32 clear = col;
            clear.a = 0;
            Vector2 perp = new Vector2(-dir.y, dir.x);
            Vector2 bc = apex - dir * len;
            Vector2 v0 = apex, v1 = bc + perp * half, v2 = bc - perp * half;
            if (round > 0f && RoundedHead(vh, v0, v1, v2, round, f, col, clear)) return;
            Vector2 mid = (v0 + v1 + v2) / 3f;
            int b = vh.currentVertCount;
            vh.AddVert(v0, col, Vector4.zero);
            vh.AddVert(v1, col, Vector4.zero);
            vh.AddVert(v2, col, Vector4.zero);
            vh.AddTriangle(b, b + 1, b + 2);
            Edge(vh, v0, v1, mid, f, col, clear);
            Edge(vh, v1, v2, mid, f, col, clear);
            Edge(vh, v2, v0, mid, f, col, clear);
        }

        static readonly List<Vector2> outline = new List<Vector2>();
        static readonly List<Vector2> outNormal = new List<Vector2>();

        static bool RoundedHead(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, float r, float f, Color32 col, Color32 clear)
        {
            float la = (b - c).magnitude, lb = (c - a).magnitude, lc = (a - b).magnitude, per = la + lb + lc;
            if (per < 1e-5f) return false;
            Vector2 inc = (a * la + b * lb + c * lc) / per;                      // incentre
            float area = Mathf.Abs((b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y)) * 0.5f;
            float rho = 2f * area / per;                                          // inradius
            r = Mathf.Min(r, rho * 0.6f);
            if (r <= 1e-5f) return false;
            float k = (rho - r) / rho;
            var p = new[] { inc + (a - inc) * k, inc + (b - inc) * k, inc + (c - inc) * k };
            // counter-clockwise order, so the outward normal of edge p[i] -> p[i+1] is (dy, -dx)
            float cross = (p[1].x - p[0].x) * (p[2].y - p[0].y) - (p[2].x - p[0].x) * (p[1].y - p[0].y);
            if (cross < 0f) { var t = p[1]; p[1] = p[2]; p[2] = t; }
            outline.Clear();
            outNormal.Clear();
            for (int i = 0; i < 3; i++)
            {
                Vector2 prev = p[(i + 2) % 3], cur = p[i], next = p[(i + 1) % 3];
                Vector2 e0 = (cur - prev).normalized, e1 = (next - cur).normalized;
                Vector2 n0 = new Vector2(e0.y, -e0.x), n1 = new Vector2(e1.y, -e1.x);
                float a0 = Mathf.Atan2(n0.y, n0.x), a1 = Mathf.Atan2(n1.y, n1.x);
                while (a1 < a0) a1 += Mathf.PI * 2f;
                for (int j = 0; j <= HeadArcSteps; j++)
                {
                    float ang = Mathf.Lerp(a0, a1, j / (float)HeadArcSteps);
                    var n = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                    outline.Add(cur + n * r);
                    outNormal.Add(n);
                }
            }
            int centre = vh.currentVertCount, m = outline.Count;
            vh.AddVert(inc, col, Vector4.zero);
            for (int i = 0; i < m; i++)
            {
                vh.AddVert(outline[i], col, Vector4.zero);
                vh.AddVert(outline[i] + outNormal[i] * f, clear, Vector4.zero);
            }
            for (int i = 0; i < m; i++)
            {
                int i0 = centre + 1 + 2 * i, i1 = centre + 1 + 2 * ((i + 1) % m);
                vh.AddTriangle(centre, i0, i1);
                vh.AddTriangle(i0, i0 + 1, i1 + 1);
                vh.AddTriangle(i0, i1 + 1, i1);
            }
            return true;
        }

        static void Edge(VertexHelper vh, Vector2 a, Vector2 b, Vector2 inside, float f, Color32 col, Color32 clear)
        {
            Vector2 e = (b - a).normalized;
            Vector2 n = new Vector2(-e.y, e.x);
            if (Vector2.Dot(n, inside - a) > 0f) n = -n;
            int i = vh.currentVertCount;
            vh.AddVert(a, col, Vector4.zero);
            vh.AddVert(b, col, Vector4.zero);
            vh.AddVert(b + n * f, clear, Vector4.zero);
            vh.AddVert(a + n * f, clear, Vector4.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }
    }

    /// <summary>Several strokes in one mesh (one draw batch). Points in board-local units, pivot = board origin.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class StrokeGraphic : MaskableGraphic
    {
        public struct Item
        {
            public List<Vector2> pts;
            public Color32 col;
            public float mul;  // width multiplier (press / halo / glow)
            public bool head;
        }

        public readonly List<Item> items = new List<Item>();
        public LongArrowRenderer owner;
        /// <summary>Sizes when there is no owner (static previews: LevelPreview). (width multiplier, head) -> style.</summary>
        public System.Func<float, bool, StrokeStyle> fixedStyle;
        public int Verts { get; private set; }

        public void Dirty() { SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            for (int i = 0; i < items.Count; i++)
            {
                if (owner != null) StrokeMesh.Add(vh, items[i].pts, owner.Style(items[i].mul, items[i].head), items[i].col);
                else if (fixedStyle != null) StrokeMesh.Add(vh, items[i].pts, fixedStyle(items[i].mul, items[i].head), items[i].col);
            }
            Verts = vh.currentVertCount;
        }
    }

    /// <summary>All mask cells as faint squares, one mesh.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class TileGraphic : MaskableGraphic
    {
        bool[,] mask;
        int cols, rows;
        float cell;
        public int Verts { get; private set; }

        public void Set(bool[,] m, int c, int r, float cellSize)
        {
            mask = m;
            cols = c;
            rows = r;
            cell = cellSize;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Verts = 0;
            if (mask == null) return;
            Color32 col = color;
            float h = cell * LongStyle.TileCell * 0.5f;
            for (int x = 0; x < cols; x++)
            {
                for (int y = 0; y < rows; y++)
                {
                    if (!mask[x, y]) continue;
                    Vector2 c = new Vector2((x + 0.5f) * cell, (y + 0.5f) * cell);
                    int i = vh.currentVertCount;
                    vh.AddVert(c + new Vector2(-h, -h), col, Vector4.zero);
                    vh.AddVert(c + new Vector2(-h, h), col, Vector4.zero);
                    vh.AddVert(c + new Vector2(h, h), col, Vector4.zero);
                    vh.AddVert(c + new Vector2(h, -h), col, Vector4.zero);
                    vh.AddTriangle(i, i + 1, i + 2);
                    vh.AddTriangle(i + 2, i + 3, i);
                }
            }
            Verts = vh.currentVertCount;
        }
    }

    /// <summary>
    /// The edges of the mask cells (null mask = every cell) as thin lines, one mesh. An edge shared by two cells is drawn
    /// once and neighbouring edges on the same grid line are merged into one run, so the mesh stays small on dense
    /// boards. width (board-local units) is set by LongArrowRenderer: GuideLineCells, at least GuideLineMinPx on screen.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class GridGraphic : MaskableGraphic
    {
        // runs in cell units: (fixed coordinate, from, to); horizontal = y fixed, vertical = x fixed
        readonly List<Vector3> hRuns = new List<Vector3>();
        readonly List<Vector3> vRuns = new List<Vector3>();
        float cell;
        public float width = 1f;
        public int Verts { get; private set; }

        public void Set(bool[,] m, int cols, int rows, float cellSize)
        {
            cell = cellSize;
            hRuns.Clear();
            vRuns.Clear();
            // horizontal line y (0..rows) between cells (x, y - 1) and (x, y): an edge if either is a board cell
            for (int y = 0; y <= rows; y++)
            {
                int start = -1;
                for (int x = 0; x <= cols; x++)
                {
                    bool on = x < cols && (In(m, cols, rows, x, y) || In(m, cols, rows, x, y - 1));
                    if (on && start < 0) start = x;
                    else if (!on && start >= 0) { hRuns.Add(new Vector3(y, start, x)); start = -1; }
                }
            }
            for (int x = 0; x <= cols; x++)
            {
                int start = -1;
                for (int y = 0; y <= rows; y++)
                {
                    bool on = y < rows && (In(m, cols, rows, x, y) || In(m, cols, rows, x - 1, y));
                    if (on && start < 0) start = y;
                    else if (!on && start >= 0) { vRuns.Add(new Vector3(x, start, y)); start = -1; }
                }
            }
            SetVerticesDirty();
        }

        static bool In(bool[,] m, int cols, int rows, int x, int y)
        {
            if (x < 0 || y < 0 || x >= cols || y >= rows) return false;
            return m == null || m[x, y];
        }

        public void Dirty() { SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Color32 col = color;
            float h = width * 0.5f;
            // horizontal runs reach h past their ends so the corners close; vertical runs stop between them (no overlap,
            // so crossings are not drawn twice at double alpha)
            // a vertical run crosses a horizontal line at every cell boundary, so it is cut into one piece per cell
            foreach (var r in hRuns)
                Quad(vh, col, r.y * cell - h, r.y * cell + h, r.x * cell - h, r.z * cell + h, true);
            foreach (var r in vRuns)
                for (int y = (int)r.y; y < (int)r.z; y++)
                    Quad(vh, col, r.x * cell - h, r.x * cell + h, y * cell + h, (y + 1) * cell - h, false);
            Verts = vh.currentVertCount;
        }

        /// <summary>A rectangle: across = (a0, a1) across the line, along = (b0, b1) along it.</summary>
        static void Quad(VertexHelper vh, Color32 col, float a0, float a1, float b0, float b1, bool horizontal)
        {
            if (b1 <= b0) return;
            int i = vh.currentVertCount;
            if (horizontal)
            {
                vh.AddVert(new Vector3(b0, a0), col, Vector4.zero);
                vh.AddVert(new Vector3(b0, a1), col, Vector4.zero);
                vh.AddVert(new Vector3(b1, a1), col, Vector4.zero);
                vh.AddVert(new Vector3(b1, a0), col, Vector4.zero);
            }
            else
            {
                vh.AddVert(new Vector3(a0, b0), col, Vector4.zero);
                vh.AddVert(new Vector3(a0, b1), col, Vector4.zero);
                vh.AddVert(new Vector3(a1, b1), col, Vector4.zero);
                vh.AddVert(new Vector3(a1, b0), col, Vector4.zero);
            }
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i + 2, i + 3, i);
        }
    }

    /// <summary>
    /// A dot at the centre of every mask cell (null mask = every cell), one mesh: a filled polygon with a 1 screen px
    /// alpha-0 rim for anti-aliasing. Each dot has its own alpha (0 = hidden: an arrow covers the cell). Showing / fading
    /// a dot only rewrites the vertex colours of the mesh (Flush), never its geometry; the geometry is rebuilt only when
    /// the size changes (LongArrowRenderer's throttled zoom rebuild) or on a new level.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class DotGraphic : MaskableGraphic
    {
        readonly List<Vector2> centres = new List<Vector2>();
        int[] dotOfCell;                 // x + y * cols -> dot index, -1 = not a board cell
        int cols;
        float[] alpha = new float[0];    // per dot, 0..1
        int[] firstVert = new int[0];    // per dot: index of its centre vertex
        int vertsPerDot;
        Mesh mesh;
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Color32> colors = new List<Color32>();
        readonly List<int> tris = new List<int>();
        public float radius = 1f, feather = 1f; // board-local units
        public int Verts { get { return verts.Count; } }
        public int Count { get { return centres.Count; } }

        public void Set(bool[,] m, int cols, int rows, float cell)
        {
            this.cols = cols;
            centres.Clear();
            dotOfCell = new int[cols * rows];
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                {
                    bool on = m == null || m[x, y];
                    dotOfCell[x + y * cols] = on ? centres.Count : -1;
                    if (on) centres.Add(new Vector2((x + 0.5f) * cell, (y + 0.5f) * cell));
                }
            alpha = new float[centres.Count];
            SetVerticesDirty();
        }

        /// <summary>Dot index of board cell (x, y), -1 when it is not a board cell.</summary>
        public int DotAt(int x, int y)
        {
            int i = x + y * cols;
            return dotOfCell == null || x < 0 || y < 0 || x >= cols || i >= dotOfCell.Length ? -1 : dotOfCell[i];
        }

        public float GetAlpha(int dot) { return alpha[dot]; }

        /// <summary>Sets one dot's alpha in the colour list (call Flush once after a batch of changes).</summary>
        public void SetAlpha(int dot, float a)
        {
            if (dot < 0 || dot >= alpha.Length) return;
            alpha[dot] = a;
            if (dot >= firstVert.Length || verts.Count == 0) return; // not built yet: applied when it is
            Color32 c = color;
            c.a = (byte)Mathf.RoundToInt(color.a * 255f * Mathf.Clamp01(a));
            int o = firstVert[dot];
            colors[o] = c;
            for (int k = 1; k < vertsPerDot; k += 2) colors[o + k] = c; // core ring (the rim stays at alpha 0)
        }

        /// <summary>Uploads the changed vertex colours (no geometry rebuild).</summary>
        public void Flush()
        {
            if (mesh == null || verts.Count == 0) return;
            mesh.SetColors(colors);
            canvasRenderer.SetMesh(mesh);
        }

        public void Dirty() { SetVerticesDirty(); }

        protected override void UpdateGeometry()
        {
            if (mesh == null)
            {
                mesh = new Mesh();
                mesh.MarkDynamic();
                mesh.hideFlags = HideFlags.HideAndDontSave;
            }
            Build();
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            canvasRenderer.SetMesh(mesh);
        }

        protected override void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
            base.OnDestroy();
        }

        void Build()
        {
            verts.Clear();
            colors.Clear();
            tris.Clear();
            Color32 rim = color;
            rim.a = 0;
            // few segments: the dots are a few px wide; 8 is round enough there, 12 once they get big (zoomed in)
            int seg = radius / Mathf.Max(1e-4f, feather) > 4f ? 12 : 8;
            vertsPerDot = 1 + 2 * seg;
            float rIn = Mathf.Max(radius - feather * 0.5f, radius * 0.5f), rOut = radius + feather * 0.5f;
            var dirs = new Vector2[seg];
            for (int k = 0; k < seg; k++)
            {
                float a = k * Mathf.PI * 2f / seg;
                dirs[k] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            }
            if (firstVert.Length != centres.Count) firstVert = new int[centres.Count];
            for (int d = 0; d < centres.Count; d++)
            {
                Vector2 c = centres[d];
                Color32 col = color;
                col.a = (byte)Mathf.RoundToInt(color.a * 255f * Mathf.Clamp01(alpha[d]));
                int o = verts.Count;
                firstVert[d] = o;
                verts.Add(c);
                colors.Add(col);
                for (int k = 0; k < seg; k++)
                {
                    verts.Add(c + dirs[k] * rIn);
                    colors.Add(col);
                    verts.Add(c + dirs[k] * rOut);
                    colors.Add(rim);
                }
                for (int k = 0; k < seg; k++)
                {
                    int i0 = o + 1 + 2 * k, i1 = o + 1 + 2 * ((k + 1) % seg);
                    tris.Add(o); tris.Add(i0); tris.Add(i1);                 // core
                    tris.Add(i0); tris.Add(i0 + 1); tris.Add(i1 + 1);        // rim
                    tris.Add(i1 + 1); tris.Add(i1); tris.Add(i0);
                }
            }
        }
    }

    /// <summary>
    /// Draws a long-arrow level for BoardView. Layers under the board (which has its own Canvas):
    ///  Tiles (one mesh) -> Dots (one mesh, a dot per board cell) -> Grid (own Canvas: grid lines along the cell edges,
    ///  shown by the Grid button)
    ///  -> Arrows (static arrows, PerBatch per mesh, CanvasGroup for the collapse) ->
    ///  Moving (own Canvas: one mesh per animating arrow, so its per-frame updates never re-batch the static arrows) ->
    ///  Fx (own Canvas: press / hint / halo overlays, impact ring).
    /// Line width is fixed on screen: every mesh is rebuilt when the board's screen scale changes (throttled).
    /// </summary>
    public class LongArrowRenderer
    {
        public const int PerBatch = 20;
        const float RebuildEvery = 0.1f; // s between zoom rebuilds while the scale keeps changing
        const float RebuildScale = 0.25f; // rebuild when the zoom moved this much (feather stays 0.8..1.25 px)

        class Arrow
        {
            public List<Vector2> rest, restLine;
            public Color baseColor, color;
            public bool inBatch = true, removed;
        }

        readonly RectTransform board;
        RectTransform root, arrowsRoot, movingRoot, fxRoot;
        CanvasGroup arrowsGroup;
        TileGraphic tiles;
        GridGraphic grid;
        DotGraphic dots;
        float dotR = -1f, dotF = -1f; // last built dot radius / feather (board-local)
        float gridAlpha, gridTarget; // touch grid fade, 0..1
        readonly List<StrokeGraphic> batches = new List<StrokeGraphic>();
        readonly List<Arrow> arrows = new List<Arrow>();
        readonly Dictionary<int, StrokeGraphic> moving = new Dictionary<int, StrokeGraphic>();
        readonly Dictionary<int, StrokeGraphic> halos = new Dictionary<int, StrokeGraphic>();
        readonly Stack<StrokeGraphic> pool = new Stack<StrokeGraphic>();
        StrokeGraphic press, hint;
        float cell, line, feather;     // board-local units
        float builtScale = -1f, lastBuild;
        bool pending;

        public LongArrowRenderer(RectTransform board)
        {
            this.board = board;
        }

        /// <summary>Line width in board-local units at the current zoom.</summary>
        public float LineWidth { get { return line; } }
        public RectTransform FxRoot { get { return fxRoot; } }

        public int TileVerts { get { return tiles != null ? tiles.Verts : 0; } }
        public int DotVerts { get { return dots != null ? dots.Verts : 0; } }

        public int ArrowVerts
        {
            get
            {
                int n = 0;
                foreach (var b in batches) if (b.gameObject.activeSelf) n += b.Verts;
                return n;
            }
        }

        public int BatchCount { get { return batches.Count; } }

        // ------------------------------------------------------------------ setup

        void Ensure()
        {
            if (root != null) return;
            root = Layer(board, "LongArrows", false);
            var tg = Layer(root, "Tiles", false);
            tiles = tg.gameObject.AddComponent<TileGraphic>();
            tiles.raycastTarget = false;
            dots = Layer(root, "Dots", false).gameObject.AddComponent<DotGraphic>();
            dots.raycastTarget = false;
            var gl = Layer(root, "Grid", true); // own canvas: its fade never re-batches the arrows
            grid = gl.gameObject.AddComponent<GridGraphic>();
            grid.raycastTarget = false;
            gl.gameObject.SetActive(false);
            arrowsRoot = Layer(root, "Static", false);
            arrowsRoot.pivot = new Vector2(0.5f, 0.5f); // collapse / revive scale around the board centre
            arrowsRoot.offsetMin = arrowsRoot.offsetMax = Vector2.zero;
            arrowsGroup = arrowsRoot.gameObject.AddComponent<CanvasGroup>();
            arrowsGroup.blocksRaycasts = false;
            arrowsGroup.interactable = false;
            movingRoot = Layer(root, "Moving", true);
            fxRoot = Layer(root, "Fx", true);
            press = NewStroke(fxRoot, "Press");
            hint = NewStroke(fxRoot, "Hint");
        }

        /// <summary>A child filling its parent, pivot at the origin; with its own Canvas when canvas is true.</summary>
        static RectTransform Layer(Transform parent, string name, bool canvas)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
            r.pivot = Vector2.zero;
            if (canvas) go.AddComponent<Canvas>();
            return r;
        }

        StrokeGraphic NewStroke(Transform parent, string name)
        {
            var r = Layer(parent, name, false);
            var g = r.gameObject.AddComponent<StrokeGraphic>();
            g.raycastTarget = false;
            g.owner = this;
            return g;
        }

        public void SetActive(bool on)
        {
            if (root != null) root.gameObject.SetActive(on);
        }

        /// <summary>New level: rest[i] = cell centres of arrow i (tail to head), colors[i] its colour.</summary>
        public void Build(LevelData data, float cellSize, List<List<Vector2>> rest, List<Color> colors)
        {
            Ensure();
            Clear();
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            cell = cellSize;
            tiles.color = LongStyle.Tile;
            tiles.Set(data.mask, data.cols, data.rows, cell);
            tiles.gameObject.SetActive(LongStyle.BoardTiles);
            dots.color = LongStyle.DotColor;
            dots.Set(data.mask, data.cols, data.rows, cell);
            dotR = dotF = -1f;
            grid.color = LongStyle.GridColor;
            grid.Set(data.mask, data.cols, data.rows, cell);
            gridAlpha = gridTarget = 0f;
            grid.gameObject.SetActive(false);

            for (int i = 0; i < rest.Count; i++)
            {
                var a = new Arrow { rest = rest[i], baseColor = colors[i], color = colors[i], restLine = new List<Vector2>() };
                StrokeMesh.Centerline(a.rest, cell * LongStyle.CornerCell, a.restLine);
                arrows.Add(a);
            }
            int nb = (rest.Count + PerBatch - 1) / PerBatch;
            while (batches.Count < nb) batches.Add(NewStroke(arrowsRoot, "Batch" + batches.Count));
            for (int b = 0; b < batches.Count; b++)
            {
                batches[b].gameObject.SetActive(b < nb);
                if (b < nb) Fill(b);
            }
            builtScale = -1f; // sizes follow on the first Tick
        }

        public void Clear()
        {
            if (root == null) return;
            arrows.Clear();
            foreach (var b in batches) { b.items.Clear(); b.Dirty(); }
            foreach (var kv in moving) Release(kv.Value);
            moving.Clear();
            foreach (var kv in halos) Release(kv.Value);
            halos.Clear();
            Overlay(press, -1, default(Color), 1f);
            Overlay(hint, -1, default(Color), 1f);
            ResetLayer(); // impact rings on the Fx canvas belong to BoardView (its extras), which destroys them
        }

        // ------------------------------------------------------------------ sizes

        /// <summary>
        /// Every frame from BoardView. rootPerLocal = root canvas units per board-local unit; screenPerLocal = screen
        /// px per board-local unit. The stroke shape is in cells, so it scales with the board transform by itself; only
        /// the screen-px parts (1 px feather, 2 px minimum line, 1 px grid lines, dot minimum) need a rebuild, so the
        /// meshes are rebuilt only when the scale moved by more than RebuildScale since the last build (at most every
        /// RebuildEvery s, and once more when it stops changing).
        /// </summary>
        public void Tick(float rootPerLocal, float screenPerLocal)
        {
            if (root == null || arrows.Count == 0 || rootPerLocal <= 0f) return;
            bool changed = builtScale < 0f || Mathf.Abs(rootPerLocal - builtScale) > builtScale * RebuildScale;
            if (!changed && !pending) return;
            float now = Time.unscaledTime;
            if (changed && builtScale > 0f && now - lastBuild < RebuildEvery) { pending = true; return; }
            if (!changed && pending && now - lastBuild < RebuildEvery) return;

            pending = false;
            lastBuild = now;
            builtScale = rootPerLocal;
            // the shape scales with the cell (and so with the zoom); only the 1 px feather and the 2 px minimum are screen px
            line = Mathf.Max(LongStyle.LineCells * cell, LongStyle.LineMinPx / Mathf.Max(1e-4f, screenPerLocal));
            feather = LongStyle.FeatherPx / Mathf.Max(1e-4f, screenPerLocal);
            grid.width = Mathf.Max(LongStyle.GuideLineCells * cell, LongStyle.GuideLineMinPx / Mathf.Max(1e-4f, screenPerLocal));
            if (grid.gameObject.activeSelf) grid.Dirty(); // hidden: built with this width when it shows (OnEnable)
            // dots: 0.11 cell, at least DotMinPx on screen; the mesh is rebuilt only when that size really changed
            float r = Mathf.Max(LongStyle.DotCell * cell, LongStyle.DotMinPx / Mathf.Max(1e-4f, screenPerLocal));
            if (Mathf.Abs(r - dotR) > dotR * 0.02f || Mathf.Abs(feather - dotF) > dotF * 0.25f)
            {
                dotR = dots.radius = r;
                dotF = dots.feather = feather;
                dots.Dirty();
            }
            foreach (var b in batches) if (b.gameObject.activeSelf) b.Dirty();
            foreach (var kv in moving) kv.Value.Dirty();
            foreach (var kv in halos) kv.Value.Dirty();
            press.Dirty();
            hint.Dirty();
        }

        // ------------------------------------------------------------------ dots on empty cells

        readonly List<int> dotFades = new List<int>(); // dots fading in (DotFadeIn)

        /// <summary>Every dot at once: shown (alpha 1) where empty[x, y], hidden where an arrow covers the cell. No fade.</summary>
        public void ResetDots(bool[,] empty)
        {
            if (dots == null) return;
            dotFades.Clear();
            int cols = empty.GetLength(0), rows = empty.GetLength(1);
            for (int x = 0; x < cols; x++)
                for (int y = 0; y < rows; y++)
                {
                    int d = dots.DotAt(x, y);
                    if (d >= 0) dots.SetAlpha(d, empty[x, y] ? 1f : 0f);
                }
            dots.Flush();
        }

        /// <summary>Cell (x, y) became empty: its dot fades in over DotFadeIn (TickDots).</summary>
        public void ShowDot(int x, int y)
        {
            if (dots == null) return;
            int d = dots.DotAt(x, y);
            if (d < 0 || dots.GetAlpha(d) >= 1f || dotFades.Contains(d)) return;
            dotFades.Add(d);
        }

        /// <summary>Every frame from BoardView: steps the dot fades. Nothing to do (no cost) when no dot is fading.</summary>
        public void TickDots(float dt)
        {
            if (dots == null || dotFades.Count == 0) return;
            float step = dt / LongStyle.DotFadeIn;
            for (int i = dotFades.Count - 1; i >= 0; i--)
            {
                int d = dotFades[i];
                float a = Mathf.Min(1f, dots.GetAlpha(d) + step);
                dots.SetAlpha(d, a);
                if (a >= 1f) dotFades.RemoveAt(i);
            }
            dots.Flush();
        }

        // ------------------------------------------------------------------ touch grid

        /// <summary>Grid lines on / off (BoardView, the Grid button): fade in / out, or at once with instant.</summary>
        public void SetGrid(bool on, bool instant)
        {
            gridTarget = on ? 1f : 0f;
            if (!instant || grid == null) return;
            gridAlpha = gridTarget;
            grid.gameObject.SetActive(on);
            if (on) grid.canvasRenderer.SetAlpha(1f);
        }

        public int GridVerts { get { return grid != null ? grid.Verts : 0; } }

        /// <summary>Every frame from BoardView: steps the grid fade. At alpha 0 the grid's GameObject is off (no cost).</summary>
        public void TickGrid(float dt)
        {
            if (grid == null || gridAlpha == gridTarget) return;
            float dur = gridTarget > gridAlpha ? LongStyle.GridFadeIn : LongStyle.GridFadeOut;
            gridAlpha = Mathf.MoveTowards(gridAlpha, gridTarget, dt / dur);
            bool show = gridAlpha > 0f;
            if (grid.gameObject.activeSelf != show) grid.gameObject.SetActive(show);
            if (show) grid.canvasRenderer.SetAlpha(gridAlpha); // the mesh keeps the theme alpha: no rebuild per frame
        }

        /// <summary>Stroke sizes for a width multiplier, in board-local units.</summary>
        public StrokeStyle Style(float mul, bool head)
        {
            return StrokeStyle.Make(cell, line, feather, mul, head);
        }

        // ------------------------------------------------------------------ static batches

        void Fill(int b)
        {
            var g = batches[b];
            g.items.Clear();
            int end = Mathf.Min(arrows.Count, (b + 1) * PerBatch);
            if (Theme.Glow)
            {
                // glows first, so no glow covers another arrow's line (moving arrows get none: frame time)
                for (int i = b * PerBatch; i < end; i++)
                {
                    var a = arrows[i];
                    if (a.removed || !a.inBatch) continue;
                    var gc = a.color;
                    gc.a *= LongStyle.GlowAlpha;
                    g.items.Add(new StrokeGraphic.Item { pts = a.rest, col = gc, mul = LongStyle.GlowMul, head = true });
                }
            }
            for (int i = b * PerBatch; i < end; i++)
            {
                var a = arrows[i];
                if (a.removed || !a.inBatch) continue;
                g.items.Add(new StrokeGraphic.Item { pts = a.rest, col = a.color, mul = 1f, head = true });
            }
            g.Dirty();
        }

        /// <summary>Arrow i leaves its batch and is drawn on its own mesh in the Moving canvas.</summary>
        public void BeginMove(int i)
        {
            var a = arrows[i];
            if (!a.inBatch) return;
            a.inBatch = false;
            Fill(i / PerBatch);
            StrokeGraphic g;
            if (!moving.TryGetValue(i, out g))
            {
                g = Take(movingRoot, "Moving" + i);
                moving[i] = g;
            }
            g.items.Clear();
            g.items.Add(new StrokeGraphic.Item { pts = new List<Vector2>(a.rest), col = a.color, mul = 1f, head = true });
            g.Dirty();
        }

        /// <summary>Moving arrow i is now drawn through pts (copied).</summary>
        public void Move(int i, List<Vector2> pts)
        {
            StrokeGraphic g;
            if (!moving.TryGetValue(i, out g) || g.items.Count == 0) return;
            var it = g.items[0];
            it.pts.Clear();
            it.pts.AddRange(pts);
            it.col = arrows[i].color;
            g.items[0] = it;
            g.Dirty();
        }

        /// <summary>Back into its batch at rest, or gone for good (removed).</summary>
        public void EndMove(int i, bool removed)
        {
            StrokeGraphic g;
            if (moving.TryGetValue(i, out g)) { Release(g); moving.Remove(i); }
            var a = arrows[i];
            a.inBatch = true;
            a.removed = removed;
            Fill(i / PerBatch);
        }

        public void SetColor(int i, Color c)
        {
            var a = arrows[i];
            if (a.color == c) return;
            a.color = c;
            if (a.inBatch && !a.removed) Fill(i / PerBatch);
            StrokeGraphic g;
            if (moving.TryGetValue(i, out g) && g.items.Count > 0)
            {
                var it = g.items[0];
                it.col = c;
                g.items[0] = it;
                g.Dirty();
            }
        }

        public Color BaseColor(int i) { return arrows[i].baseColor; }

        /// <summary>All remaining arrows lerped from their own colour towards c (k = 0: their own colour).</summary>
        public void TintAll(Color c, float k)
        {
            for (int i = 0; i < arrows.Count; i++) arrows[i].color = Color.Lerp(arrows[i].baseColor, c, k);
            for (int b = 0; b < batches.Count; b++) if (batches[b].gameObject.activeSelf) Fill(b);
        }

        StrokeGraphic Take(Transform parent, string name)
        {
            var g = pool.Count > 0 ? pool.Pop() : NewStroke(parent, name);
            g.transform.SetParent(parent, false);
            g.gameObject.name = name;
            g.gameObject.SetActive(true);
            g.transform.SetAsLastSibling();
            return g;
        }

        void Release(StrokeGraphic g)
        {
            g.items.Clear();
            g.Dirty();
            g.gameObject.SetActive(false);
            pool.Push(g);
        }

        // ------------------------------------------------------------------ overlays

        void Overlay(StrokeGraphic g, int i, Color c, float mul)
        {
            g.items.Clear();
            if (i >= 0 && i < arrows.Count && !arrows[i].removed)
                g.items.Add(new StrokeGraphic.Item { pts = arrows[i].rest, col = c, mul = mul, head = true });
            g.Dirty();
        }

        /// <summary>Lighter, slightly wider copy over arrow i while a finger is down on it; -1 clears.</summary>
        public void Press(int i)
        {
            if (press == null) return;
            if (i < 0) { if (press.items.Count > 0) Overlay(press, -1, default(Color), 1f); return; }
            Overlay(press, i, LongStyle.Pressed(arrows[i].baseColor), LongStyle.PressMul);
        }

        /// <summary>Gold arrow over arrow i with a pulsing gold glow (k 0..1); -1 clears.</summary>
        public void Hint(int i, float k)
        {
            if (hint == null) return;
            hint.items.Clear();
            if (i >= 0 && i < arrows.Count && !arrows[i].removed)
            {
                var glow = LongStyle.Hint;
                glow.a = 0.18f + 0.27f * k;
                hint.items.Add(new StrokeGraphic.Item { pts = arrows[i].rest, col = glow, mul = LongStyle.HintGlowMul, head = true });
                hint.items.Add(new StrokeGraphic.Item { pts = arrows[i].rest, col = LongStyle.Hint, mul = 1.2f, head = true });
            }
            hint.Dirty();
        }

        /// <summary>Red halo around arrow i at alpha a (a &lt;= 0 removes it).</summary>
        public void Halo(int i, float a)
        {
            StrokeGraphic g;
            halos.TryGetValue(i, out g);
            if (a <= 0f || arrows[i].removed)
            {
                if (g != null) { Release(g); halos.Remove(i); }
                return;
            }
            if (g == null)
            {
                g = Take(fxRoot, "Halo" + i);
                halos[i] = g;
            }
            var c = LongStyle.Error;
            c.a = a;
            Overlay(g, i, c, LongStyle.HaloMul);
        }

        // ------------------------------------------------------------------ whole layer (collapse / revive)

        /// <summary>Moves / scales / fades the static arrows (not the tiles).</summary>
        public void SetLayer(Vector2 offset, float scale, float alpha)
        {
            if (arrowsRoot == null) return;
            arrowsRoot.anchoredPosition = offset;
            arrowsRoot.localScale = new Vector3(scale, scale, 1f);
            arrowsGroup.alpha = alpha;
        }

        public void ResetLayer()
        {
            SetLayer(Vector2.zero, 1f, 1f);
            for (int i = 0; i < arrows.Count; i++) arrows[i].color = arrows[i].baseColor;
            for (int b = 0; b < batches.Count; b++) if (batches[b].gameObject.activeSelf && arrows.Count > 0) Fill(b);
        }

        // ------------------------------------------------------------------ hit test

        /// <summary>Distance (board-local) from lp to arrow i's rounded centre line at rest.</summary>
        public float Distance(int i, Vector2 lp)
        {
            var l = arrows[i].restLine;
            float best = Vector2.Distance(lp, l[0]);
            for (int k = 1; k < l.Count; k++)
            {
                Vector2 a = l[k - 1], ab = l[k] - a;
                float t = Mathf.Clamp01(Vector2.Dot(lp - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                best = Mathf.Min(best, Vector2.Distance(lp, a + ab * t));
            }
            return best;
        }
    }
}
