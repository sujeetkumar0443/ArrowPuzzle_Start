using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

// Generator for long, winding arrows that fill a shape. No UnityEngine dependency: it builds the prebuilt level files
// in the Editor (Arrow/Long Levels menus), runs outside Unity in the .NET harness, and generates levels past the
// prebuilt set on the phone's level worker thread (LevelStream). All static state is locked or immutable.
// Coordinates here are FILE coordinates: cell index = r * cols + c, r = 0 is the top row (like shapes.txt).
namespace ArrowPuzzle
{
    /// <summary>
    /// Vector shape registry: name -> inside(x, y), normalized coords, x right, y DOWN. The 9 built-in shapes are coded
    /// below; the rest come from ShapeDefs.Text (DSL, see ParseDefs). All lists every vector name: built-ins first, then
    /// the DSL's order. ShapeLibrary adds the bitmap, variation and procedural shapes on top.
    /// </summary>
    public static class LongShapes
    {
        public static readonly string[] BuiltIn = { "heart", "hourglass", "paw", "cat", "bunny", "star", "fish", "house", "tree" };

        sealed class Op
        {
            public bool add;
            public char kind;            // 'E' ellipse, 'R' rectangle, 'P' polygon
            public double cx, cy, a, b, cos, sin;
            public double[] poly;
        }

        static Dictionary<string, List<Op>> defs;
        static List<string> all;
        static readonly object defsLock = new object();

        /// <summary>Every vector shape name (built-ins + ShapeDefs.Text).</summary>
        public static List<string> All
        {
            get { Defs(); return all; }
        }

        /// <summary>Kept for the editor tools: the DSL is compiled in now (ShapeDefs.Text), projectRoot is not used.</summary>
        public static void LoadDefs(string projectRoot) { Defs(); }

        static Dictionary<string, List<Op>> Defs()
        {
            lock (defsLock)
            {
                if (defs != null) return defs;
                var order = new List<string>();
                var d = ParseDefs(ShapeDefs.Text, order);
                var a = new List<string>(BuiltIn);
                a.AddRange(order);
                all = a;
                defs = d; // last: other threads read defs without the lock only after this
                return defs;
            }
        }

        /// <summary>A built-in or ShapeDefs shape.</summary>
        public static bool IsVector(string name)
        {
            return Array.IndexOf(BuiltIn, name) >= 0 || Defs().ContainsKey(name);
        }

        /// <summary>
        /// "shape NAME" ... "end" blocks; each line is one operation applied in order to a boolean state:
        /// "+" adds (state OR inside), "-" subtracts (state AND NOT inside). Primitives:
        ///   E cx cy rx ry rot   ellipse, rot in degrees
        ///   R cx cy w h rot     rectangle
        ///   P x,y x,y ...       polygon
        /// Rotation: dx = x - cx, dy = y - cy, u = dx cos(a) + dy sin(a), w = -dx sin(a) + dy cos(a).
        /// Blank lines and lines starting with # are ignored.
        /// </summary>
        static Dictionary<string, List<Op>> ParseDefs(string text, List<string> defOrder)
        {
            var res = new Dictionary<string, List<Op>>();
            string cur = null;
            List<Op> ops = null;
            var lines = text.Replace("\r", "").Split('\n');
            for (int n = 0; n < lines.Length; n++)
            {
                string line = lines[n].Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                string where = "ShapeDefs line " + (n + 1) + ": ";
                if (line.StartsWith("shape "))
                {
                    if (cur != null) throw new FormatException(where + "'shape' inside '" + cur + "' (missing 'end')");
                    cur = line.Substring(6).Trim();
                    if (cur.Length == 0 || cur.Contains(" ")) throw new FormatException(where + "bad shape name");
                    if (res.ContainsKey(cur) || Array.IndexOf(BuiltIn, cur) >= 0) throw new FormatException(where + "duplicate shape " + cur);
                    ops = new List<Op>();
                    continue;
                }
                if (line == "end")
                {
                    if (cur == null) throw new FormatException(where + "'end' without 'shape'");
                    if (ops.Count == 0) throw new FormatException(where + "shape " + cur + " is empty");
                    res[cur] = ops;
                    defOrder.Add(cur);
                    cur = null;
                    continue;
                }
                if (cur == null) throw new FormatException(where + "operation outside a shape");
                if (line.Length < 3 || (line[0] != '+' && line[0] != '-')) throw new FormatException(where + "expected + or -");
                var op = new Op { add = line[0] == '+', kind = line[1] };
                var parts = line.Substring(2).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (op.kind == 'E' || op.kind == 'R')
                {
                    if (parts.Length != 5) throw new FormatException(where + op.kind + " needs cx cy a b rot");
                    var v = new double[5];
                    for (int i = 0; i < 5; i++) v[i] = Num(parts[i], where);
                    op.cx = v[0]; op.cy = v[1]; op.a = v[2]; op.b = v[3];
                    double rad = v[4] * Math.PI / 180.0;
                    op.cos = Math.Cos(rad);
                    op.sin = Math.Sin(rad);
                    if (op.a <= 0 || op.b <= 0) throw new FormatException(where + "sizes must be > 0");
                }
                else if (op.kind == 'P')
                {
                    if (parts.Length < 3) throw new FormatException(where + "a polygon needs at least 3 points");
                    op.poly = new double[parts.Length * 2];
                    for (int i = 0; i < parts.Length; i++)
                    {
                        var xy = parts[i].Split(',');
                        if (xy.Length != 2) throw new FormatException(where + "bad point '" + parts[i] + "'");
                        op.poly[2 * i] = Num(xy[0], where);
                        op.poly[2 * i + 1] = Num(xy[1], where);
                    }
                }
                else throw new FormatException(where + "unknown primitive '" + op.kind + "'");
                ops.Add(op);
            }
            if (cur != null) throw new FormatException("ShapeDefs: shape " + cur + " has no 'end'");
            return res;
        }

        static double Num(string s, string where)
        {
            double v;
            if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                throw new FormatException(where + "bad number '" + s + "'");
            return v;
        }

        static bool Eval(List<Op> ops, double x, double y)
        {
            bool state = false;
            foreach (var op in ops)
            {
                if (op.add == state) continue; // + on an inside point / - on an outside point: no change
                bool inside;
                if (op.kind == 'P') inside = Pip(op.poly, x, y);
                else
                {
                    double dx = x - op.cx, dy = y - op.cy;
                    double u = dx * op.cos + dy * op.sin, w = -dx * op.sin + dy * op.cos;
                    if (op.kind == 'E') { double p = u / op.a, q = w / op.b; inside = p * p + q * q <= 1; }
                    else inside = Math.Abs(u) <= op.a * 0.5 && Math.Abs(w) <= op.b * 0.5;
                }
                if (inside) state = op.add;
            }
            return state;
        }

        public static double Scale(string shape)
        {
            switch (shape)
            {
                case "heart": return 190;
                case "hourglass": return 270;
                case "paw": return 200;
                case "cat": return 235;
                case "bunny": return 232;
                case "star": return 255;
                case "fish": return 215;
                case "house": return 250;
                case "tree": return 250;
            }
            throw new ArgumentException("Unknown shape " + shape);
        }

        static bool Ell(double x, double y, double cx, double cy, double rx, double ry)
        {
            double a = (x - cx) / rx, b = (y - cy) / ry;
            return a * a + b * b <= 1;
        }

        /// <summary>Point in polygon (even-odd), poly = x0, y0, x1, y1, ...</summary>
        static bool Pip(double[] poly, double x, double y)
        {
            bool inside = false;
            int n = poly.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double xi = poly[2 * i], yi = poly[2 * i + 1], xj = poly[2 * j], yj = poly[2 * j + 1];
                if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) inside = !inside;
            }
            return inside;
        }

        static readonly double[] CatEarL = { -.58, -.05, -.64, -.98, -.12, -.36 }; // tall ears: the silhouette stays under 70 % of its box
        static readonly double[] CatEarR = { .58, -.05, .64, -.98, .12, -.36 };
        static readonly double[] FishFin = { -.36, -.3, -.12, -.64, .14, -.3 };    // dorsal fin
        static readonly double[] HouseChimney = { .3, -.35, .3, -.86, .48, -.86, .48, -.2 };
        static readonly double[] FishTail = { .4, 0, 1.0, -.46, .62, 0, 1.0, .46 }; // forked tail
        static readonly double[] HeartPoly = MakeHeart();

        /// <summary>
        /// The classic parametric heart (x = 16 sin^3 t, y = 13 cos t - 5 cos 2t - 2 cos 3t - cos 4t), scaled to about
        /// -1..1: about 61 % of its box (the old implicit heart filled 71-74 %, over the 70 % rule).
        /// </summary>
        static double[] MakeHeart()
        {
            const int n = 160;
            var p = new double[2 * n];
            for (int i = 0; i < n; i++)
            {
                double t = 2 * Math.PI * i / n, s = Math.Sin(t);
                p[2 * i] = 16 * s * s * s / 17.0;
                p[2 * i + 1] = -(13 * Math.Cos(t) - 5 * Math.Cos(2 * t) - 2 * Math.Cos(3 * t) - Math.Cos(4 * t)) / 17.0 + 0.1;
            }
            return p;
        }
        static readonly double[] Roof = { -.78, -.1, 0, -.78, .78, -.1 };
        static readonly double[] StarPoly = MakeStar();

        static double[] MakeStar()
        {
            var p = new double[20];
            for (int i = 0; i < 10; i++)
            {
                double a = 36.0 * i * Math.PI / 180.0, r = i % 2 == 0 ? 1.0 : 0.46;
                p[2 * i] = Math.Sin(a) * r;
                p[2 * i + 1] = -Math.Cos(a) * r;
            }
            return p;
        }

        public static bool Inside(string shape, double x, double y)
        {
            switch (shape)
            {
                case "heart":
                    return Pip(HeartPoly, x, y);
                case "hourglass":
                {
                    double ax = Math.Abs(x), ay = Math.Abs(y);
                    if (ay > 0.9) return ax <= 0.62 && ay <= 1.0;
                    if (ay < 0.08) return ax <= 0.09;
                    double t = Math.Min(1, (ay - 0.08) / 0.8);
                    double w = 0.09 + 0.47 * Math.Sin(Math.PI / 2 * Math.Pow(t, 0.85));
                    return ax <= w;
                }
                case "paw":
                    // toes touch the pad: one connected region (the mask rule)
                    return Ell(x, y, 0, .44, .6, .42) || Ell(x, y, -.6, .04, .18, .28) || Ell(x, y, -.24, -.24, .2, .3)
                           || Ell(x, y, .24, -.24, .2, .3) || Ell(x, y, .6, .04, .18, .28);
                case "cat":
                    // eyes and nose cut out: the plain silhouette filled 76 % of its box (rule: at most 70 %)
                    if (Ell(x, y, -.25, .1, .11, .15) || Ell(x, y, .25, .1, .11, .15) || Ell(x, y, 0, .36, .1, .07)) return false;
                    return Ell(x, y, 0, .18, .62, .56) || Pip(CatEarL, x, y) || Pip(CatEarR, x, y);
                case "bunny":
                    return Ell(x, y, 0, .46, .56, .5) || Ell(x, y, -.27, -.34, .15, .52) || Ell(x, y, .27, -.34, .15, .52);
                case "star":
                    return Pip(StarPoly, x, y);
                case "fish": // the neck ellipse joins the tail to the body (at least 3 cells wide)
                    if (Ell(x, y, -.56, -.08, .11, .11)) return false; // eye (with the forked tail: under 70 % of the box)
                    return Ell(x, y, -.18, 0, .66, .38) || Ell(x, y, .34, 0, .2, .2) || Pip(FishTail, x, y) || Pip(FishFin, x, y);
                case "house":
                    if (Math.Abs(x) <= .14 && y >= .34 && y <= .73) return false; // door
                    if (Math.Abs(x + .3) <= .1 && Math.Abs(y - .14) <= .1) return false; // window
                    return (Math.Abs(x) <= .55 && y >= -.12 && y <= .72) || Pip(Roof, x, y) || Pip(HouseChimney, x, y);
                case "tree":
                    return Ell(x, y, 0, -.42, .46, .4) || Ell(x, y, -.3, -.08, .4, .34) || Ell(x, y, .3, -.08, .4, .34)
                           || (Math.Abs(x) <= .1 && y >= .15 && y <= .78);
            }
            List<Op> ops;
            if (Defs().TryGetValue(shape, out ops)) return Eval(ops, x, y);
            throw new ArgumentException("Unknown shape " + shape);
        }
    }

    /// <summary>One generated level (file coordinates) and its quality stats.</summary>
    public sealed class LongLevel
    {
        public string Shape;
        public double D, Tangle, BoxFillPct;
        public int BaseSeed, Seed, Tries, Cols, Rows, Splits, Dropped, Snakes;
        public bool[] Mask;                                        // r * Cols + c
        public readonly List<List<int>> Arrows = new List<List<int>>(); // tail -> head, cell indices
        public readonly List<int> Colors = new List<int>();
        public bool[] FreeAtStart;
        public int FreeCount, Depth;
        public bool Solvable, InRange, Clipped;
        public double Ms;

        public int Count { get { return Arrows.Count; } }
        public double FreePct { get { return Count == 0 ? 0 : 100.0 * FreeCount / Count; } }
        public int MaxLen { get { int m = 0; foreach (var a in Arrows) m = Math.Max(m, a.Count); return m; } }
        public double AvgLen { get { if (Count == 0) return 0; double s = 0; foreach (var a in Arrows) s += a.Count; return s / Count; } }
        public int MaskCells { get { int n = 0; foreach (bool b in Mask) if (b) n++; return n; } }

        /// <summary>% of the arrow cells that belong to arrows of at least minLen cells.</summary>
        public double LongCellPct(int minLen)
        {
            int all = 0, lng = 0;
            foreach (var a in Arrows) { all += a.Count; if (a.Count >= minLen) lng += a.Count; }
            return all == 0 ? 0 : 100.0 * lng / all;
        }
    }

    /// <summary>
    /// Long snakes over the shape, cut into arrows, head choice by local search (annealing) and splits at cycle
    /// crossings so the in-game peel test passes, then a quality retry loop.
    /// Blocking rule = the game's: the ray from the head straight to the GRID edge (mask ignored); any arrow cell on it,
    /// the arrow's own body included, blocks.
    /// </summary>
    public static class LongLevelGen
    {
        public const int MaxTries = 200;     // seeds tried per level until the quality is in range (a passing level stops at its first good seed)
        public const int MaxPathLen = 70;    // a 1-cell leftover is only merged into a path that stays under this
        public const int MaxCols = 31, MaxRows = 50;
        const double Fill = 0.94;            // the shape's bbox fills this much of the max grid at d = 1
        const int SearchSteps = 2000;
        const int SearchRestarts = 3;        // extra annealing runs before a path is split
        const int MaxSplits = 60;
        const int LongArrow = 25;            // "long arrow" for the stats

        static readonly int[] DC = { 0, 1, 0, -1 };
        static readonly int[] DR = { -1, 0, 1, 0 };

        static int RoundHalfUp(double v) { return (int)Math.Floor(v + 0.5); }
        static double Lerp(double a, double b, double t) { return a + (b - a) * t; }

        // ------------------------------------------------------------------ shape fit and mask

        static readonly Dictionary<string, double[]> boxes = new Dictionary<string, double[]>();

        /// <summary>Bounding box (x0, y0, x1, y1) of the shape, sampled every 0.004 over [-1.6, 1.6], padded by one step.</summary>
        public static double[] BBox(string shape)
        {
            lock (boxes) return BBoxLocked(shape); // the full-set build generates levels in parallel
        }

        static double[] BBoxLocked(string shape)
        {
            double[] b;
            if (boxes.TryGetValue(shape, out b)) return b;
            if (boxes.Count > 512) boxes.Clear(); // endless levels: procedural keys never come back
            var inside = ShapeLibrary.Resolve(shape).Inside;
            const double lim = 1.6, step = 0.004;
            int n = (int)(2 * lim / step);
            double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
            for (int j = 0; j <= n; j++)
            {
                double y = -lim + j * step;
                for (int i = 0; i <= n; i++)
                {
                    double x = -lim + i * step;
                    if (!inside(x, y)) continue;
                    if (x < x0) x0 = x;
                    if (x > x1) x1 = x;
                    if (y < y0) y0 = y;
                    if (y > y1) y1 = y;
                }
            }
            if (x0 <= -lim + step || y0 <= -lim + step || x1 >= lim - step || y1 >= lim - step)
                throw new InvalidOperationException("Shape " + shape + " reaches the sampling limit");
            b = new[] { x0 - step, y0 - step, x1 + step, y1 + step };
            boxes[shape] = b;
            return b;
        }

        /// <summary>
        /// Scale so the bbox fills Fill of the max grid (MaxCols x MaxRows) at d = 1, times lerp(MinScale, 1, d) and the
        /// Scale variation's factor; grid = bbox rounded up + 1 cell margin per side (2 for Thick), capped at the max,
        /// min MinCols columns, bbox centred. Then the Hollow / Thick / Thin morph and the small-group drop. A mask under
        /// MinCells cells is redrawn larger (x1.12 per step, up to the d = 1 size), so even level 1 has MinCells cells.
        /// </summary>
        public static bool[] BuildMask(string shape, double d, out int cols, out int rows, out double boxFillPct, out bool clipped)
        {
            var src = ShapeLibrary.Resolve(shape);
            var b = BBox(shape);
            double bw = b[2] - b[0], bh = b[3] - b[1], cx = (b[0] + b[2]) / 2, cy = (b[1] + b[3]) / 2;
            double full = Math.Min(MaxCols * Fill / bw, MaxRows * Fill / bh) * src.SizeMul;
            double sc = full * Lerp(MinScale, 1.0, Math.Max(0, Math.Min(1, d)));
            int pad = src.Morph == ShapeVariation.Thick ? 2 : 1;
            bool[] mask;
            int dropped;
            int shrink = 0;
            while (true)
            {
                if (Math.Ceiling(bw * sc) + 2 * pad < MinCols) sc = (MinCols - 2 * pad - 0.001) / bw;
                cols = Math.Min(MaxCols, (int)Math.Ceiling(bw * sc) + 2 * pad);
                rows = Math.Min(MaxRows, (int)Math.Ceiling(bh * sc) + 2 * pad);
                mask = new bool[cols * rows];
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                        mask[r * cols + c] = src.Inside(cx + (c + 0.5 - cols / 2.0) / sc, cy + (r + 0.5 - rows / 2.0) / sc);
                Morph(mask, cols, rows, src.Morph);
                dropped = DropSmallGroups(mask, cols, rows, MinGroup);
                // the rules depend on the resolution (too few cells, a thin neck breaking apart, the fill wobbling
                // around 70 %): grow 6 % per step up to the full size, then try up to 4 slightly smaller sizes.
                // Deterministic (a fixed sequence of scales), so a level never changes.
                if (MaskProblem(mask, cols, rows) == null) break;
                if (shrink == 0 && sc < full - 1e-9) { sc = Math.Min(full, sc * 1.06); continue; }
                if (shrink >= 4) break;
                shrink++;
                sc = full * Math.Pow(0.97, shrink);
            }
            boxFillPct = 100.0 * (bw * sc) * (bh * sc) / (cols * rows);
            clipped = bw * sc > cols || bh * sc > rows;
            if (dropped > 0)
            {
                string key = shape + "@" + d.ToString(CultureInfo.InvariantCulture);
                bool first;
                lock (warned) first = warned.Add(key);
                if (first) Warn("Long levels: " + shape + " d=" + d.ToString(CultureInfo.InvariantCulture)
                                          + ": dropped " + dropped + " cell(s) in groups under " + MinGroup + " cells");
            }
            return mask;
        }

        public const int MinGroup = 6; // isolated mask groups smaller than this are dropped

        // ------------------------------------------------------------------ mask rules (every generated level)

        public const int MinCells = 120;                       // board cells
        public const double MinFillPct = 25, MaxFillPct = 70;  // board cells / their own bounding box

        /// <summary>All mask cells form one 4-connected region (true for an empty mask).</summary>
        public static bool OneRegion(bool[] mask, int cols, int rows)
        {
            int first = Array.IndexOf(mask, true);
            if (first < 0) return true;
            var seen = new bool[mask.Length];
            var stack = new Stack<int>();
            stack.Push(first);
            seen[first] = true;
            int reached = 0;
            while (stack.Count > 0)
            {
                int cell = stack.Pop();
                reached++;
                for (int k = 0; k < 4; k++)
                {
                    int nb = Step(cell, k, cols, rows);
                    if (nb >= 0 && mask[nb] && !seen[nb]) { seen[nb] = true; stack.Push(nb); }
                }
            }
            return reached == CountCells(mask);
        }

        public static int CountCells(bool[] mask)
        {
            int n = 0;
            foreach (bool b in mask) if (b) n++;
            return n;
        }

        /// <summary>
        /// Null when the mask is one 4-connected region of at least MinCells cells filling MinFillPct..MaxFillPct of its
        /// bounding box; otherwise the first rule it breaks.
        /// </summary>
        public static string MaskProblem(bool[] mask, int cols, int rows)
        {
            int n = 0, c0 = cols, c1 = -1, r0 = rows, r1 = -1, first = -1;
            for (int i = 0; i < mask.Length; i++)
            {
                if (!mask[i]) continue;
                n++;
                if (first < 0) first = i;
                int c = i % cols, r = i / cols;
                if (c < c0) c0 = c;
                if (c > c1) c1 = c;
                if (r < r0) r0 = r;
                if (r > r1) r1 = r;
            }
            if (n < MinCells) return n + " cells < " + MinCells;
            var seen = new bool[mask.Length];
            var stack = new Stack<int>();
            stack.Push(first);
            seen[first] = true;
            int reached = 0;
            while (stack.Count > 0)
            {
                int cell = stack.Pop();
                reached++;
                for (int k = 0; k < 4; k++)
                {
                    int nb = Step(cell, k, cols, rows);
                    if (nb >= 0 && mask[nb] && !seen[nb]) { seen[nb] = true; stack.Push(nb); }
                }
            }
            if (reached != n) return "not one region (" + reached + " of " + n + " cells connected)";
            double fill = 100.0 * n / ((c1 - c0 + 1) * (r1 - r0 + 1));
            if (fill < MinFillPct || fill > MaxFillPct)
                return string.Format(CultureInfo.InvariantCulture, "fills {0:0.#}% of its box (allowed {1:0}-{2:0}%)", fill, MinFillPct, MaxFillPct);
            return null;
        }

        /// <summary>Builds the mask of a shape key at difficulty d and checks it (MaskProblem); null = usable.</summary>
        public static string ProbeMask(string shape, double d)
        {
            int cols, rows;
            double fill;
            bool clipped;
            var mask = BuildMask(shape, d, out cols, out rows, out fill, out clipped);
            return MaskProblem(mask, cols, rows);
        }

        /// <summary>
        /// Grid variations: Hollow keeps a band (at least 3 cells, about a fifth of the shorter side) along the outline;
        /// Thick grows the shape by one cell (8 neighbours); Thin removes one cell from every edge (4 neighbours).
        /// </summary>
        static void Morph(bool[] mask, int cols, int rows, ShapeVariation v)
        {
            if (v == ShapeVariation.Thick)
            {
                var src = (bool[])mask.Clone();
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                    {
                        if (src[r * cols + c]) continue;
                        bool grow = false;
                        for (int dr = -1; dr <= 1 && !grow; dr++)
                            for (int dc = -1; dc <= 1 && !grow; dc++)
                            {
                                int rr = r + dr, cc = c + dc;
                                grow = rr >= 0 && cc >= 0 && rr < rows && cc < cols && src[rr * cols + cc];
                            }
                        mask[r * cols + c] = grow;
                    }
            }
            else if (v == ShapeVariation.Thin)
            {
                var src = (bool[])mask.Clone();
                for (int i = 0; i < mask.Length; i++)
                {
                    if (!src[i]) continue;
                    for (int k = 0; k < 4; k++)
                    {
                        int nb = Step(i, k, cols, rows);
                        if (nb < 0 || !src[nb]) { mask[i] = false; break; }
                    }
                }
            }
            else if (v == ShapeVariation.Hollow)
            {
                // distance (4 neighbours) from the outside: the grid border and every empty cell count as outside
                var dist = new int[mask.Length];
                var q = new Queue<int>();
                int c0 = cols, c1 = -1, r0 = rows, r1 = -1;
                for (int i = 0; i < mask.Length; i++)
                {
                    if (!mask[i]) continue;
                    int c = i % cols, r = i / cols;
                    c0 = Math.Min(c0, c); c1 = Math.Max(c1, c); r0 = Math.Min(r0, r); r1 = Math.Max(r1, r);
                    bool edge = false;
                    for (int k = 0; k < 4 && !edge; k++) { int nb = Step(i, k, cols, rows); edge = nb < 0 || !mask[nb]; }
                    dist[i] = edge ? 1 : int.MaxValue;
                    if (edge) q.Enqueue(i);
                }
                while (q.Count > 0)
                {
                    int cell = q.Dequeue();
                    for (int k = 0; k < 4; k++)
                    {
                        int nb = Step(cell, k, cols, rows);
                        if (nb >= 0 && dist[nb] == int.MaxValue) { dist[nb] = dist[cell] + 1; q.Enqueue(nb); }
                    }
                }
                if (c1 < 0) return;
                int band = Math.Max(3, RoundHalfUp(0.2 * Math.Min(c1 - c0 + 1, r1 - r0 + 1)));
                for (int i = 0; i < mask.Length; i++) if (mask[i] && dist[i] > band) mask[i] = false;
            }
        }
        public const double MinScale = 0.3; // grid scale at d = 0 (of the d = 1 size): level 1 is a tiny board
        public const int MinCols = 7;

        /// <summary>Where mask warnings go (Debug.LogWarning in the Editor; the console outside Unity).</summary>
        public static Action<string> Warn = s => Console.WriteLine("WARNING " + s);
        static readonly HashSet<string> warned = new HashSet<string>(); // once per shape and d (not per retry)

        /// <summary>Removes 4-connected mask groups of fewer than min cells. Returns the number of cells removed.</summary>
        public static int DropSmallGroups(bool[] mask, int cols, int rows, int min)
        {
            var seen = new bool[mask.Length];
            var group = new List<int>();
            var stack = new Stack<int>();
            int removed = 0;
            for (int s = 0; s < mask.Length; s++)
            {
                if (!mask[s] || seen[s]) continue;
                group.Clear();
                stack.Push(s);
                seen[s] = true;
                while (stack.Count > 0)
                {
                    int cell = stack.Pop();
                    group.Add(cell);
                    for (int k = 0; k < 4; k++)
                    {
                        int nb = Step(cell, k, cols, rows);
                        if (nb >= 0 && mask[nb] && !seen[nb]) { seen[nb] = true; stack.Push(nb); }
                    }
                }
                if (group.Count >= min) continue;
                foreach (int cell in group) mask[cell] = false;
                removed += group.Count;
            }
            return removed;
        }

        /// <summary>
        /// % of mask cells in parts only 1-2 cells wide: cells not covered by any 3 x 3 block that lies fully inside the
        /// mask (a morphological opening with a 3 x 3 square removes them).
        /// </summary>
        public static double ThinPct(bool[] mask, int cols, int rows)
        {
            var thick = new bool[mask.Length];
            for (int r = 0; r + 2 < rows; r++)
            {
                for (int c = 0; c + 2 < cols; c++)
                {
                    bool full = true;
                    for (int y = 0; y < 3 && full; y++)
                        for (int x = 0; x < 3 && full; x++)
                            full = mask[(r + y) * cols + c + x];
                    if (!full) continue;
                    for (int y = 0; y < 3; y++)
                        for (int x = 0; x < 3; x++)
                            thick[(r + y) * cols + c + x] = true;
                }
            }
            int n = 0, thin = 0;
            for (int i = 0; i < mask.Length; i++)
            {
                if (!mask[i]) continue;
                n++;
                if (!thick[i]) thin++;
            }
            return n == 0 ? 0 : 100.0 * thin / n;
        }

        // ------------------------------------------------------------------ quality gates

        public static int MinDepth(double d) { return RoundHalfUp(3 + 5 * d); }
        public static double MinFreePct(double d) { return d >= 0.5 ? 12 : 0; }
        public static double MaxFreePct(double d) { return d >= 0.5 ? 40 : 60; }
        public static double MinAvgLen(double d) { return d >= 0.5 ? Lerp(8, 15, (d - 0.5) / 0.5) : 0; }
        public static int MaxSplitsGate(double d) { return d >= 0.5 ? 3 : 99; }

        static bool Good(LongLevel l)
        {
            return l.Solvable && l.FreePct >= MinFreePct(l.D) && l.FreePct <= MaxFreePct(l.D) && l.Depth >= MinDepth(l.D)
                   && l.AvgLen >= MinAvgLen(l.D) && l.Splits <= MaxSplitsGate(l.D);
        }

        /// <summary>The quality gates the level misses, e.g. "free 44.1>40; avg 12.3<15", or "-" when it passes them all.</summary>
        public static string GateFailures(LongLevel l)
        {
            var f = new List<string>();
            var ic = CultureInfo.InvariantCulture;
            if (!l.Solvable) f.Add("unsolvable");
            if (l.FreePct < MinFreePct(l.D)) f.Add(string.Format(ic, "free {0:0.0}<{1:0}", l.FreePct, MinFreePct(l.D)));
            if (l.FreePct > MaxFreePct(l.D)) f.Add(string.Format(ic, "free {0:0.0}>{1:0}", l.FreePct, MaxFreePct(l.D)));
            if (l.Depth < MinDepth(l.D)) f.Add("depth " + l.Depth + "<" + MinDepth(l.D));
            if (l.AvgLen < MinAvgLen(l.D)) f.Add(string.Format(ic, "avg {0:0.0}<{1:0.#}", l.AvgLen, MinAvgLen(l.D)));
            if (l.Splits > MaxSplitsGate(l.D)) f.Add("splits " + l.Splits + ">" + MaxSplitsGate(l.D));
            return f.Count == 0 ? "-" : string.Join("; ", f.ToArray());
        }

        static double Penalty(LongLevel l)
        {
            if (!l.Solvable) return 1e9;
            double p = 0;
            if (l.FreePct < MinFreePct(l.D)) p += MinFreePct(l.D) - l.FreePct;
            if (l.FreePct > MaxFreePct(l.D)) p += l.FreePct - MaxFreePct(l.D);
            if (l.Depth < MinDepth(l.D)) p += 5 * (MinDepth(l.D) - l.Depth);
            if (l.AvgLen < MinAvgLen(l.D)) p += 5 * (MinAvgLen(l.D) - l.AvgLen);
            if (l.Splits > MaxSplitsGate(l.D)) p += 2 * (l.Splits - MaxSplitsGate(l.D));
            return p;
        }

        // ------------------------------------------------------------------ entry point

        /// <summary>Tries seeds baseSeed, baseSeed + 1, ... (max MaxTries) until the quality is in range; else the best one.</summary>
        public static LongLevel Generate(string shape, double d, int baseSeed)
        {
            return Run(shape, d, baseSeed, null, 0, -1, MaxTries);
        }

        /// <summary>
        /// Generate with the campaign's extra knobs: tangle 0..1 (past the size cap: more long arrows, fewer straight runs
        /// so they wind more tightly; 0 = the classic generator) and lenD (arrow length mix from this d instead of the
        /// level's; -1 = d). Same seed walk: baseSeed, baseSeed + 1, ... until the quality gates pass.
        /// </summary>
        public static LongLevel Generate(string shape, double d, int baseSeed, double tangle, double lenD)
        {
            return Run(shape, d, baseSeed, null, tangle, lenD, MaxTries);
        }

        /// <summary>The same with at most maxTries seeds (the campaign spreads a fixed budget over its candidate shapes).</summary>
        public static LongLevel Generate(string shape, double d, int baseSeed, double tangle, double lenD, int maxTries)
        {
            return Run(shape, d, baseSeed, null, tangle, lenD, Math.Max(1, maxTries));
        }

        /// <summary>How far a level misses the quality gates (0 = in range; 1e9 = not solvable).</summary>
        public static double QualityPenalty(LongLevel l) { return Penalty(l); }

        /// <summary>Name written for rectangle boards (normal levels): the whole grid is the mask.</summary>
        public const string BoardShape = "board";

        /// <summary>Extra rules for rectangle boards (normal levels).</summary>
        public sealed class RectOptions
        {
            public int Cols, Rows;
            public double LenD = -1;        // arrow length mix (TargetLen) from this d instead of the level's d; -1 = d
            public double MaxDropPct = 1;   // at most this % of the cells dropped by Cleanup
            public int MinArrows, MaxArrows; // 0 = no limit
            public int MaxTries = 400;      // full grids pass the free-share gate less often than shapes
            public int EdgeWin = 20;        // cut search window for arrow ends on the border pointing out (free heads)
        }

        /// <summary>
        /// A rectangle board cols x rows (every cell is a board cell), same snakes / cuts / head search / gates as the
        /// shapes, plus RectOptions (dropped cells, arrow count). Seeds baseSeed, baseSeed + 1, ... like Generate.
        /// </summary>
        public static LongLevel GenerateRect(RectOptions o, double d, int baseSeed)
        {
            return Run(BoardShape, d, baseSeed, o, 0, -1, o.MaxTries);
        }

        static bool GoodRect(LongLevel l, RectOptions o)
        {
            if (o == null) return true;
            if (100.0 * l.Dropped / (l.Cols * l.Rows) > o.MaxDropPct) return false;
            if (o.MinArrows > 0 && l.Count < o.MinArrows) return false;
            if (o.MaxArrows > 0 && l.Count > o.MaxArrows) return false;
            return true;
        }

        static double PenaltyRect(LongLevel l, RectOptions o)
        {
            if (o == null) return 0;
            double p = Math.Max(0, 100.0 * l.Dropped / (l.Cols * l.Rows) - o.MaxDropPct) * 10;
            if (o.MinArrows > 0 && l.Count < o.MinArrows) p += 3 * (o.MinArrows - l.Count);
            if (o.MaxArrows > 0 && l.Count > o.MaxArrows) p += 3 * (l.Count - o.MaxArrows);
            return p;
        }

        static LongLevel Run(string shape, double d, int baseSeed, RectOptions o, double tangle, double lenD, int maxTries)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            LongLevel best = null;
            double bestP = double.MaxValue;
            int tries = 0;
            // the shape's mask does not depend on the seed: built once, each try works on a copy (Cleanup edits it)
            Grid grid = o != null ? null : new Grid(shape, d);
            for (int t = 0; t < maxTries; t++)
            {
                tries++;
                var l = TryOnce(shape, d, baseSeed + t, o, grid, tangle, lenD);
                if (Good(l) && GoodRect(l, o)) { best = l; break; }
                double p = Penalty(l) + PenaltyRect(l, o);
                if (p < bestP) { bestP = p; best = l; }
            }
            best.BaseSeed = baseSeed;
            best.Tries = tries;
            best.InRange = Good(best) && GoodRect(best, o);
            best.Ms = sw.Elapsed.TotalMilliseconds;
            return best;
        }

        /// <summary>A shape's mask at one d (BuildMask), shared by all tries of a Run.</summary>
        sealed class Grid
        {
            public readonly bool[] Mask;
            public readonly int Cols, Rows;
            public readonly double Fill;
            public readonly bool Clipped;

            public Grid(string shape, double d)
            {
                Mask = BuildMask(shape, d, out Cols, out Rows, out Fill, out Clipped);
            }
        }

        static LongLevel TryOnce(string shape, double d, int seed, RectOptions o, Grid grid, double tangle, double lenOverride)
        {
            var rng = new Random(seed);
            int cols, rows;
            double fill;
            bool clipped;
            bool[] mask;
            if (o != null)
            {
                cols = o.Cols;
                rows = o.Rows;
                mask = new bool[cols * rows];
                for (int i = 0; i < mask.Length; i++) mask[i] = true;
                fill = 100;
                clipped = false;
            }
            else
            {
                if (grid == null) grid = new Grid(shape, d);
                mask = (bool[])grid.Mask.Clone();
                cols = grid.Cols;
                rows = grid.Rows;
                fill = grid.Fill;
                clipped = grid.Clipped;
            }
            double lenD = lenOverride >= 0 ? lenOverride : o != null && o.LenD >= 0 ? o.LenD : d;
            var paths = new List<List<int>>();
            var snakes = Snakes(mask, cols, rows, rng, tangle);
            int edgeWin = o != null ? o.EdgeWin : 0;
            foreach (var s in snakes) Cut(s, lenD, tangle, cols, rows, edgeWin, rng, paths);
            int dropped = Cleanup(paths, mask, cols, rows);

            var lvl = new LongLevel
            {
                Shape = shape, D = d, Tangle = tangle, Seed = seed, Cols = cols, Rows = rows, Mask = mask, Dropped = dropped,
                Snakes = snakes.Count, BoxFillPct = fill, Clipped = clipped
            };
            var sv = new Solver(paths, cols, rows, rng);
            lvl.Solvable = sv.Solve();
            lvl.Splits = sv.splits;
            for (int i = 0; i < sv.paths.Count; i++)
            {
                var a = new List<int>(sv.paths[i]);
                if (sv.pick[i] == 1) a.Reverse();
                lvl.Arrows.Add(a);
                lvl.Colors.Add(rng.Next(8));
            }
            Stats(lvl);
            if (o != null) EdgeFlips(lvl, rng);
            return lvl;
        }

        // ------------------------------------------------------------------ snakes and cuts

        static int Step(int cell, int k, int cols, int rows)
        {
            int c = cell % cols + DC[k], r = cell / cols + DR[k];
            return c < 0 || r < 0 || c >= cols || r >= rows ? -1 : r * cols + c;
        }

        static int FreeNbrs(bool[] free, int cell, int cols, int rows)
        {
            int n = 0;
            for (int k = 0; k < 4; k++)
            {
                int nb = Step(cell, k, cols, rows);
                if (nb >= 0 && free[nb]) n++;
            }
            return n;
        }

        /// <summary>Random walks until stuck (score = free neighbours + noise 0..1.4, -0.9 straight on), each from a free cell with the fewest free neighbours.</summary>
        static List<List<int>> Snakes(bool[] mask, int cols, int rows, Random rng, double tangle)
        {
            double straight = 0.9 * (1 - 0.8 * tangle); // bonus for going straight on: less of it = tighter winding
            int n = cols * rows;
            var free = (bool[])mask.Clone();
            int freeCount = 0;
            foreach (bool b in mask) if (b) freeCount++;
            var snakes = new List<List<int>>();
            var ties = new List<int>();

            while (freeCount > 0)
            {
                ties.Clear();
                int min = int.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    if (!free[i]) continue;
                    int f = FreeNbrs(free, i, cols, rows);
                    if (f < min) { min = f; ties.Clear(); }
                    if (f == min) ties.Add(i);
                }
                int start = ties[rng.Next(ties.Count)];

                var path = new List<int> { start };
                free[start] = false;
                freeCount--;
                int lastDir = -1;
                while (true)
                {
                    int cur = path[path.Count - 1], best = -1, bestDir = -1;
                    double bestScore = double.MaxValue;
                    for (int k = 0; k < 4; k++)
                    {
                        int nb = Step(cur, k, cols, rows);
                        if (nb < 0 || !free[nb]) continue;
                        double s = FreeNbrs(free, nb, cols, rows) + rng.NextDouble() * 1.4 - (k == lastDir ? straight : 0);
                        if (s < bestScore) { bestScore = s; best = nb; bestDir = k; }
                    }
                    if (best < 0) break;
                    path.Add(best);
                    free[best] = false;
                    freeCount--;
                    lastDir = bestDir;
                }
                snakes.Add(path);
            }
            return snakes;
        }

        /// <summary>
        /// Short 3-8 / medium 9-22 / long 23-60, mix by d: 0 -> 90/10/0, 0.5 -> 45/40/15, 1 -> 15/35/50. tangle moves up
        /// to 60 % of the short and 30 % of the medium share to long arrows (at d = 1, tangle 1: 6/25/69).
        /// </summary>
        static int TargetLen(double d, double tangle, Random rng)
        {
            double pS, pM;
            if (d <= 0.5) { pS = Lerp(0.90, 0.45, d / 0.5); pM = Lerp(0.10, 0.40, d / 0.5); }
            else { pS = Lerp(0.45, 0.15, (d - 0.5) / 0.5); pM = Lerp(0.40, 0.35, (d - 0.5) / 0.5); }
            if (tangle > 0)
            {
                pS *= 1 - 0.6 * tangle;
                pM *= 1 - 0.3 * tangle;
            }
            double u = rng.NextDouble();
            if (u < pS) return rng.Next(3, 9);
            if (u < pS + pM) return rng.Next(9, 23);
            return rng.Next(23, 61);
        }

        /// <summary>Cells in the straight run that contains the step s[j-1] -> s[j].</summary>
        static int RunLen(List<int> s, int j, int cols)
        {
            int dv = s[j] - s[j - 1];
            int a = j - 1, b = j;
            while (a - 1 >= 0 && s[a] - s[a - 1] == dv) a--;
            while (b + 1 < s.Count && s[b + 1] - s[b] == dv) b++;
            return b - a + 1;
        }

        /// <summary>Cuts a snake into arrows of sampled lengths; a leftover under 2 cells stays on the previous piece.</summary>
        static void Cut(List<int> s, double d, int cols, Random rng, List<List<int>> outPaths)
        {
            Cut(s, d, 0, cols, 0, 0, rng, outPaths);
        }

        /// <summary>
        /// edgeWin &gt; 0 (rectangle boards): a cut within edgeWin cells of the sampled one that leaves an arrow end on the
        /// border pointing out of the grid (that end can be a free head) is taken first.
        /// </summary>
        static void Cut(List<int> s, double d, double tangle, int cols, int rows, int edgeWin, Random rng, List<List<int>> outPaths)
        {
            int pos = 0, n = s.Count;
            while (pos < n)
            {
                int rem = n - pos;
                int len = TargetLen(d, tangle, rng);
                if (rem - len < 2)
                {
                    outPaths.Add(s.GetRange(pos, rem)); // 1-cell snakes are handled by Cleanup
                    break;
                }
                int k = pos + len, lo = pos + 2, hi = n - 2;
                int edgeK = -1;
                for (int off = 0; off <= edgeWin && edgeK < 0; off++)
                {
                    if (k - off >= lo && EdgeCut(s, k - off, cols, rows)) edgeK = k - off;
                    else if (off > 0 && k + off <= hi && EdgeCut(s, k + off, cols, rows)) edgeK = k + off;
                }
                if (edgeK >= 0) k = edgeK;
                else if (RunLen(s, k, cols) < 3)
                {
                    // a better cut (not inside a straight run under 3 cells) within 2 cells, nearest first
                    for (int off = 1; off <= 2; off++)
                    {
                        if (k - off >= lo && RunLen(s, k - off, cols) >= 3) { k -= off; break; }
                        if (k + off <= hi && RunLen(s, k + off, cols) >= 3) { k += off; break; }
                    }
                }
                outPaths.Add(s.GetRange(pos, k - pos));
                pos = k;
            }
        }

        /// <summary>A cut before s[k] leaves s[k-1] (coming from s[k-2]) or s[k] (left towards s[k+1]) pointing out of the grid.</summary>
        static bool EdgeCut(List<int> s, int k, int cols, int rows)
        {
            if (k >= 2 && OutEnd(s[k - 2], s[k - 1], cols, rows)) return true;
            return k + 1 < s.Count && OutEnd(s[k + 1], s[k], cols, rows);
        }

        /// <summary>One more step from b in the direction a -&gt; b leaves the grid.</summary>
        static bool OutEnd(int a, int b, int cols, int rows)
        {
            int c = b % cols + (b % cols - a % cols), r = b / cols + (b / cols - a / cols);
            return c < 0 || r < 0 || c >= cols || r >= rows;
        }

        /// <summary>
        /// 1-cell leftovers: appended to an adjacent path end (if it stays under MaxPathLen), else paired with an adjacent
        /// 1-cell leftover, else dropped from the mask. Returns the number of dropped cells.
        /// </summary>
        static int Cleanup(List<List<int>> paths, bool[] mask, int cols, int rows)
        {
            var owner = new int[cols * rows];
            for (int i = 0; i < owner.Length; i++) owner[i] = -1;
            for (int i = 0; i < paths.Count; i++) foreach (int c in paths[i]) owner[c] = i;

            int dropped = 0;
            for (int s = 0; s < paths.Count; s++)
            {
                if (paths[s].Count != 1) continue;
                int cell = paths[s][0];
                bool done = false;
                for (int k = 0; k < 4 && !done; k++)
                {
                    int nb = Step(cell, k, cols, rows);
                    if (nb < 0 || owner[nb] < 0 || owner[nb] == s) continue;
                    var q = paths[owner[nb]];
                    if (q.Count < 2 || q.Count + 1 >= MaxPathLen) continue;
                    if (q[q.Count - 1] == nb) q.Add(cell);
                    else if (q[0] == nb) q.Insert(0, cell);
                    else continue;
                    owner[cell] = owner[nb];
                    done = true;
                }
                for (int k = 0; k < 4 && !done; k++)
                {
                    int nb = Step(cell, k, cols, rows);
                    if (nb < 0 || owner[nb] < 0 || owner[nb] == s || paths[owner[nb]].Count != 1) continue;
                    paths[owner[nb]].Add(cell);
                    owner[cell] = owner[nb];
                    done = true;
                }
                if (!done)
                {
                    mask[cell] = false;
                    owner[cell] = -1;
                    dropped++;
                }
                paths[s].Clear();
            }
            paths.RemoveAll(p => p.Count == 0);
            return dropped;
        }

        // ------------------------------------------------------------------ heads and solvability

        /// <summary>
        /// Head choice per path (0 = head is the last cell, 1 = the first). Greedy start (end with fewer blockers), then
        /// simulated annealing over head flips of stuck arrows; if it fails, split one path where a dependency cycle
        /// crosses it and search again.
        /// </summary>
        sealed class Solver
        {
            public readonly List<List<int>> paths;
            public readonly List<int> pick = new List<int>();
            public int splits;
            readonly int cols, rows;
            readonly Random rng;
            // per path and option: distinct other arrows on the head ray, the first cell of each on the ray,
            // and the first own cell on the ray (-1 = none, the option is usable)
            readonly List<List<int>[]> blk = new List<List<int>[]>();
            readonly List<List<int>[]> hit = new List<List<int>[]>();
            readonly List<int[]> selfCell = new List<int[]>();
            int[] owner;

            public Solver(List<List<int>> paths, int cols, int rows, Random rng)
            {
                this.paths = paths;
                this.cols = cols;
                this.rows = rows;
                this.rng = rng;
            }

            bool Self(int i, int o) { return selfCell[i][o] >= 0; }

            public bool Solve()
            {
                Rebuild();
                for (int i = 0; i < paths.Count; i++) pick.Add(InitialPick(i));

                while (true)
                {
                    // a path that blocks itself from both ends can never leave: split it at its own crossing
                    int dead = -1;
                    for (int i = 0; i < paths.Count && dead < 0; i++)
                        if (Self(i, 0) && Self(i, 1) && paths[i].Count >= 4) dead = i;
                    if (dead >= 0)
                    {
                        if (splits >= MaxSplits) return false;
                        Split(dead, paths[dead].IndexOf(selfCell[dead][pick[dead]]));
                        continue;
                    }

                    // search, then up to SearchRestarts restarts with random heads on the stuck arrows, before any split
                    bool solved = Search();
                    for (int r = 0; r < SearchRestarts && !solved; r++)
                    {
                        var stuck = new List<int>();
                        Peel(stuck);
                        foreach (int i in stuck)
                        {
                            int o = rng.Next(2);
                            if (!Self(i, o)) pick[i] = o;
                        }
                        solved = Search();
                    }
                    if (solved) return true;
                    if (splits >= MaxSplits) return false;

                    int w, at;
                    if (!FindCycleSplit(out w, out at)) return false;
                    Split(w, at);
                }
            }

            int InitialPick(int i)
            {
                int b0 = Self(i, 0) ? int.MaxValue : blk[i][0].Count;
                int b1 = Self(i, 1) ? int.MaxValue : blk[i][1].Count;
                return b1 < b0 ? 1 : 0;
            }

            /// <summary>Simulated annealing on cost = 50 x stuck + total blockers; keeps the best state seen.</summary>
            bool Search()
            {
                var stuck = new List<int>();
                Peel(stuck);
                if (stuck.Count == 0) return true;

                int total = 0;
                for (int i = 0; i < paths.Count; i++) total += blk[i][pick[i]].Count;
                double cost = 50.0 * stuck.Count + total, bestCost = cost;
                var best = new List<int>(pick);
                var stuckSet = new bool[paths.Count];
                double temp = 20;

                for (int step = 0; step < SearchSteps; step++)
                {
                    // candidate: a stuck arrow whose other end is usable; half the time the one with most stuck blockers
                    foreach (int i in stuck) stuckSet[i] = true;
                    int cand = -1;
                    if (rng.NextDouble() < 0.5)
                    {
                        int most = -1;
                        foreach (int i in stuck)
                        {
                            if (Self(i, 1 - pick[i])) continue;
                            int m = 0;
                            foreach (int w in blk[i][pick[i]]) if (stuckSet[w]) m++;
                            if (m > most) { most = m; cand = i; }
                        }
                    }
                    else
                    {
                        for (int t = 0; t < 8 && cand < 0; t++)
                        {
                            int i = stuck[rng.Next(stuck.Count)];
                            if (!Self(i, 1 - pick[i])) cand = i;
                        }
                    }
                    foreach (int i in stuck) stuckSet[i] = false;
                    if (cand < 0) break;

                    int oldTotal = total;
                    total += blk[cand][1 - pick[cand]].Count - blk[cand][pick[cand]].Count;
                    pick[cand] = 1 - pick[cand];
                    var next = new List<int>();
                    Peel(next);
                    double c2 = 50.0 * next.Count + total;
                    if (c2 <= cost || rng.NextDouble() < Math.Exp((cost - c2) / temp))
                    {
                        cost = c2;
                        stuck = next;
                        if (cost < bestCost) { bestCost = cost; best = new List<int>(pick); }
                        if (stuck.Count == 0) return true;
                    }
                    else
                    {
                        pick[cand] = 1 - pick[cand];
                        total = oldTotal;
                    }
                    temp = Math.Max(0.5, temp * 0.997);
                }
                for (int i = 0; i < pick.Count; i++) pick[i] = best[i];
                Peel(stuck);
                return stuck.Count == 0;
            }

            /// <summary>
            /// Follows stuck arrows to a stuck blocker until one repeats (a dependency cycle). Splits the longest cycle
            /// member w at the first cell of w on the ray of the arrow it blocks.
            /// </summary>
            bool FindCycleSplit(out int w, out int at)
            {
                w = -1; at = -1;
                var stuck = new List<int>();
                Peel(stuck);
                if (stuck.Count == 0) return false;
                var isStuck = new bool[paths.Count];
                foreach (int i in stuck) isStuck[i] = true;

                // start from the stuck arrow with the most stuck blockers
                int cur = stuck[0], most = -1;
                foreach (int i in stuck)
                {
                    int m = 0;
                    foreach (int b in blk[i][pick[i]]) if (isStuck[b]) m++;
                    if (m > most) { most = m; cur = i; }
                }

                var order = new List<int>();
                var seenAt = new Dictionary<int, int>();
                while (!seenAt.ContainsKey(cur))
                {
                    seenAt[cur] = order.Count;
                    order.Add(cur);
                    if (Self(cur, pick[cur])) { w = cur; at = paths[cur].IndexOf(selfCell[cur][pick[cur]]); return paths[cur].Count >= 4; }
                    int nxt = -1;
                    foreach (int b in blk[cur][pick[cur]]) if (isStuck[b]) { nxt = b; break; }
                    if (nxt < 0) return false; // cannot happen: a stuck arrow always has a stuck blocker
                    cur = nxt;
                }

                // cycle = order[seenAt[cur]..], edge order[j] -> order[j + 1] means order[j] is blocked by order[j + 1]
                int startIdx = seenAt[cur], bestLen = 3;
                for (int j = startIdx; j < order.Count; j++)
                {
                    int i = order[j], b = j + 1 < order.Count ? order[j + 1] : cur;
                    if (paths[b].Count <= bestLen) continue;
                    var bl = blk[i][pick[i]];
                    int cell = hit[i][pick[i]][bl.IndexOf(b)];
                    bestLen = paths[b].Count;
                    w = b;
                    at = paths[b].IndexOf(cell);
                }
                return w >= 0;
            }

            /// <summary>
            /// Path i -> two pieces cut next to cell index at (pieces >= 6 cells where possible, always >= 2). The first
            /// piece stays at i, the second is appended; both get the greedy head.
            /// </summary>
            void Split(int i, int at)
            {
                var p = paths[i];
                int min = p.Count >= 12 ? 6 : 2;
                int k = Math.Max(min, Math.Min(p.Count - min, at));
                paths[i] = p.GetRange(0, k);
                paths.Add(p.GetRange(k, p.Count - k));
                splits++;
                Rebuild();
                pick[i] = InitialPick(i);
                pick.Add(InitialPick(paths.Count - 1));
            }

            void Rebuild()
            {
                owner = new int[cols * rows];
                for (int i = 0; i < owner.Length; i++) owner[i] = -1;
                for (int i = 0; i < paths.Count; i++) foreach (int c in paths[i]) owner[c] = i;
                blk.Clear();
                hit.Clear();
                selfCell.Clear();
                for (int i = 0; i < paths.Count; i++)
                {
                    var b = new[] { new List<int>(), new List<int>() };
                    var h = new[] { new List<int>(), new List<int>() };
                    var s = new int[2];
                    for (int o = 0; o < 2; o++) s[o] = Ray(i, o, b[o], h[o]);
                    blk.Add(b);
                    hit.Add(h);
                    selfCell.Add(s);
                }
            }

            /// <summary>Fills the distinct blockers on the head ray of option o (and their first cells); returns the first own cell or -1.</summary>
            int Ray(int i, int option, List<int> blockers, List<int> cells)
            {
                var p = paths[i];
                int head = option == 0 ? p[p.Count - 1] : p[0];
                int prev = option == 0 ? p[p.Count - 2] : p[1];
                int dc = head % cols - prev % cols, dr = head / cols - prev / cols;
                int self = -1;
                for (int c = head % cols + dc, r = head / cols + dr; c >= 0 && r >= 0 && c < cols && r < rows; c += dc, r += dr)
                {
                    int cell = r * cols + c, w = owner[cell];
                    if (w < 0) continue;
                    if (w == i) { if (self < 0) self = cell; }
                    else if (!blockers.Contains(w)) { blockers.Add(w); cells.Add(cell); }
                }
                return self;
            }

            /// <summary>Simultaneous peel rounds on the current heads. Fills stuck; returns the depth.</summary>
            int Peel(List<int> stuck)
            {
                int n = paths.Count;
                var indeg = new int[n];
                var rev = new List<int>[n];
                for (int i = 0; i < n; i++) rev[i] = new List<int>();
                for (int i = 0; i < n; i++)
                {
                    indeg[i] = Self(i, pick[i]) ? int.MaxValue / 2 : blk[i][pick[i]].Count;
                    foreach (int w in blk[i][pick[i]]) rev[w].Add(i);
                }
                var cur = new List<int>();
                var next = new List<int>();
                for (int i = 0; i < n; i++) if (indeg[i] == 0) cur.Add(i);
                int depth = 0;
                var gone = new bool[n];
                while (cur.Count > 0)
                {
                    depth++;
                    next.Clear();
                    foreach (int w in cur) gone[w] = true;
                    foreach (int w in cur)
                        foreach (int i in rev[w])
                            if (--indeg[i] == 0) next.Add(i);
                    var t = cur; cur = next; next = t;
                }
                stuck.Clear();
                for (int i = 0; i < n; i++) if (!gone[i]) stuck.Add(i);
                return depth;
            }
        }

        // ------------------------------------------------------------------ independent check + stats

        /// <summary>
        /// Re-checks a finished level from its arrows only (cover, neighbours, the game's peel rule) and fills
        /// Solvable, FreeAtStart, FreeCount, Depth (simultaneous rounds until the board is empty).
        /// </summary>
        /// <summary>
        /// Rectangle boards: in a full grid only an arrow whose head sits on the border pointing out is free at the start,
        /// so the head search alone leaves too few free arrows. Arrows whose TAIL is such a border cell (the step from its
        /// neighbour points out of the grid) get their head moved there, in random order, until the free share reaches
        /// MinFreePct + 3. Safe for solvability: a free arrow can always go first, and the rest peel as before.
        /// </summary>
        static void EdgeFlips(LongLevel l, Random rng)
        {
            double target = MinFreePct(l.D);
            if (target <= 0 || !l.Solvable || l.FreePct >= target + 3) return;
            int cols = l.Cols, rows = l.Rows;
            var cand = new List<int>();
            for (int i = 0; i < l.Count; i++)
            {
                var a = l.Arrows[i];
                if (l.FreeAtStart[i] || a.Count < 2) continue;
                int t = a[0], q = a[1];
                int c = t % cols + (t % cols - q % cols), r = t / cols + (t / cols - q / cols);
                if (c < 0 || r < 0 || c >= cols || r >= rows) cand.Add(i);
            }
            for (int i = cand.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); int tmp = cand[i]; cand[i] = cand[j]; cand[j] = tmp; }
            int need = (int)Math.Ceiling((target + 3) * l.Count / 100.0) - l.FreeCount;
            for (int k = 0; k < cand.Count && k < need; k++) l.Arrows[cand[k]].Reverse();
            if (need > 0 && cand.Count > 0) Stats(l);

            // still short: split an arrow at a border cell a[j] where it meets the border head-on, so one part gets a free
            // head there (each part keeps at least 3 cells):
            //  - arriving (a[j-1] -> a[j] points out): front a[0..j] heads out at a[j]; the back keeps the old head and ray,
            //    so the old peel order still works with the front part first (always safe);
            //  - leaving (a[j+1] -> a[j] points out): the back a[j..] is reversed to head out at a[j]; the front a[0..j-1]
            //    gets a new head, so this one is kept only if the peel test and the depth gate still pass.
            var tried = new HashSet<long>();
            for (int guard = 0; guard < 60 && l.FreePct < target + 3; guard++)
            {
                var moves = new List<long>(); // (arrow * 4096 + j) * 2 + kind
                for (int i = 0; i < l.Count; i++)
                {
                    if (l.FreeAtStart[i]) continue;
                    var a = l.Arrows[i];
                    for (int j = 2; j + 3 < a.Count; j++)
                    {
                        long key = ((long)i * 4096 + j) * 2;
                        if (OutEnd(a[j - 1], a[j], cols, rows) && !tried.Contains(key)) moves.Add(key);
                        if (j >= 3 && OutEnd(a[j + 1], a[j], cols, rows) && !tried.Contains(key + 1)) moves.Add(key + 1);
                    }
                }
                if (moves.Count == 0) break;
                long mv = moves[rng.Next(moves.Count)];
                tried.Add(mv);
                int kind = (int)(mv % 2), ai = (int)(mv / 2 / 4096), sj = (int)(mv / 2 % 4096);
                var arrow = l.Arrows[ai];
                var saved = new List<int>(arrow);
                List<int> extra;
                if (kind == 0)
                {
                    extra = arrow.GetRange(0, sj + 1);
                    arrow.RemoveRange(0, sj + 1);
                }
                else
                {
                    extra = arrow.GetRange(sj, arrow.Count - sj);
                    extra.Reverse();
                    arrow.RemoveRange(sj, arrow.Count - sj);
                }
                l.Arrows.Add(extra);
                l.Colors.Add(rng.Next(8));
                int freeBefore = l.FreeCount;
                Stats(l);
                if (kind == 1 && (!l.Solvable || l.Depth < MinDepth(l.D) || l.FreeCount <= freeBefore))
                {
                    l.Arrows.RemoveAt(l.Arrows.Count - 1);
                    l.Colors.RemoveAt(l.Colors.Count - 1);
                    arrow.Clear();
                    arrow.AddRange(saved);
                    Stats(l);
                }
                else tried.Clear(); // indices moved: start the candidate list over
            }
        }

        public static void Stats(LongLevel l)
        {
            int cols = l.Cols, rows = l.Rows, n = l.Count;
            var owner = new int[cols * rows];
            for (int i = 0; i < owner.Length; i++) owner[i] = -1;
            bool valid = true;
            for (int i = 0; i < n; i++)
            {
                var a = l.Arrows[i];
                if (a.Count < 2) valid = false;
                for (int j = 0; j < a.Count; j++)
                {
                    if (!l.Mask[a[j]] || owner[a[j]] >= 0) valid = false;
                    owner[a[j]] = i;
                    if (j > 0 && Math.Abs(a[j] % cols - a[j - 1] % cols) + Math.Abs(a[j] / cols - a[j - 1] / cols) != 1) valid = false;
                }
            }
            for (int i = 0; i < owner.Length; i++) if (l.Mask[i] && owner[i] < 0) valid = false;

            var gone = new bool[n];
            l.FreeAtStart = new bool[n];
            int left = n, depth = 0;
            var round = new List<int>();
            while (left > 0)
            {
                round.Clear();
                for (int i = 0; i < n; i++)
                {
                    if (gone[i]) continue;
                    var a = l.Arrows[i];
                    if (a.Count < 2) continue;
                    int h = a[a.Count - 1], p = a[a.Count - 2];
                    int dc = h % cols - p % cols, dr = h / cols - p / cols;
                    bool free = true;
                    for (int c = h % cols + dc, r = h / cols + dr; c >= 0 && r >= 0 && c < cols && r < rows; c += dc, r += dr)
                        if (owner[r * cols + c] >= 0) { free = false; break; }
                    if (free) round.Add(i);
                }
                if (round.Count == 0) break;
                depth++;
                foreach (int i in round)
                {
                    if (depth == 1) l.FreeAtStart[i] = true;
                    gone[i] = true;
                    left--;
                    foreach (int c in l.Arrows[i]) owner[c] = -1;
                }
            }
            l.FreeCount = 0;
            foreach (bool f in l.FreeAtStart) if (f) l.FreeCount++;
            l.Depth = depth;
            l.Solvable = valid && left == 0;
        }

        public static string StatLine(LongLevel l)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{0} d={1:0.##} grid {2}x{3} ({4} cells, {5} dropped, bbox fill {6:0}%{7}) | arrows {8}, avg len {9:0.0} (min {10:0.#}), " +
                "max {11}, cells in 25+ arrows {12:0}% | free at start {13:0.0}% (target {14:0}-{15:0}%) | depth {16} (min {17}) | {18} | " +
                "seed {19} (base {20}, try {21}/{22}), splits {23} (max {25}) | {24:0} ms",
                l.Shape, l.D, l.Cols, l.Rows, l.MaskCells, l.Dropped, l.BoxFillPct, l.Clipped ? ", CLIPPED" : "",
                l.Count, l.AvgLen, MinAvgLen(l.D), l.MaxLen, l.LongCellPct(LongArrow), l.FreePct,
                MinFreePct(l.D), MaxFreePct(l.D), l.Depth, MinDepth(l.D),
                !l.Solvable ? "NOT SOLVABLE" : l.InRange ? "in range" : "OUT OF RANGE",
                l.Seed, l.BaseSeed, l.Tries, MaxTries, l.Splits, l.Ms, MaxSplitsGate(l.D));
        }

        // ------------------------------------------------------------------ level file (shapes.txt format)

        /// <summary>
        /// Same format as shapes.txt. The L line gets extra "seed=" / "d=" tokens after the arrow count; the parser
        /// only reads the first six tokens, so old files and old parsers are unaffected.
        /// </summary>
        public static string ToText(IList<LongLevel> levels)
        {
            return ToText(levels, null, "# long samples (Arrow/Long Levels/Generate Samples).", "0.##");
        }

        /// <summary>numbers = level number per level (null: 1, 2, ...); header = first comment line; dFormat = d's format.</summary>
        public static string ToText(IList<LongLevel> levels, IList<int> numbers, string header, string dFormat)
        {
            var sb = new StringBuilder();
            sb.Append(header).Append(" L <n> <shape> <cols> <rows> <arrowCount> seed=<seed> d=<difficulty>; ")
              .Append("M = mask row (# is a board cell); A <colorIndex 0-7> <col,row ...> tail to head (last cell is the head)\n");
            for (int n = 0; n < levels.Count; n++)
            {
                var l = levels[n];
                sb.Append(string.Format(CultureInfo.InvariantCulture, "L {0} {1} {2} {3} {4} seed={5} d={6}\n",
                    numbers != null ? numbers[n] : n + 1, l.Shape, l.Cols, l.Rows, l.Count, l.Seed,
                    l.D.ToString(dFormat, CultureInfo.InvariantCulture)));
                for (int r = 0; r < l.Rows; r++)
                {
                    sb.Append("M ");
                    for (int c = 0; c < l.Cols; c++) sb.Append(l.Mask[r * l.Cols + c] ? '#' : '.');
                    sb.Append('\n');
                }
                for (int i = 0; i < l.Count; i++)
                {
                    sb.Append("A ").Append(l.Colors[i]);
                    foreach (int cell in l.Arrows[i]) sb.Append(' ').Append(cell % l.Cols).Append(',').Append(cell / l.Cols);
                    sb.Append('\n');
                }
            }
            return sb.ToString();
        }
    }

}
