using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

// Offline generator for long, winding arrows that fill a shape (Editor only, no UnityEngine dependency so the same
// code also runs outside Unity). Menu: Arrow/Long Levels/Generate Samples (LongLevelEditorTools).
// Coordinates here are FILE coordinates: cell index = r * cols + c, r = 0 is the top row (like shapes.txt).
namespace ArrowPuzzle
{
    /// <summary>Implicit shape functions, normalized coords, x right, y DOWN.</summary>
    public static class LongShapes
    {
        public static readonly string[] All = { "heart", "hourglass", "paw", "cat", "bunny", "star", "fish", "house", "tree" };

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

        static readonly double[] CatEarL = { -.58, -.05, -.62, -.78, -.1, -.36 };
        static readonly double[] CatEarR = { .58, -.05, .62, -.78, .1, -.36 };
        static readonly double[] FishTail = { .4, 0, .98, -.42, .98, .42 };
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
                {
                    double X = x * 1.12, Y = -y * 1.12 + 0.12;
                    double q = X * X + Y * Y - 1;
                    return q * q * q - X * X * Y * Y * Y <= 0;
                }
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
                    return Ell(x, y, 0, .44, .6, .42) || Ell(x, y, -.72, -.06, .17, .27) || Ell(x, y, -.26, -.4, .19, .29)
                           || Ell(x, y, .26, -.4, .19, .29) || Ell(x, y, .72, -.06, .17, .27);
                case "cat":
                    return Ell(x, y, 0, .18, .62, .56) || Pip(CatEarL, x, y) || Pip(CatEarR, x, y);
                case "bunny":
                    return Ell(x, y, 0, .46, .56, .5) || Ell(x, y, -.27, -.34, .15, .52) || Ell(x, y, .27, -.34, .15, .52);
                case "star":
                    return Pip(StarPoly, x, y);
                case "fish":
                    return Ell(x, y, -.18, 0, .66, .38) || Pip(FishTail, x, y);
                case "house":
                    return (Math.Abs(x) <= .55 && y >= -.12 && y <= .72) || Pip(Roof, x, y);
                case "tree":
                    return Ell(x, y, 0, -.42, .46, .4) || Ell(x, y, -.3, -.08, .4, .34) || Ell(x, y, .3, -.08, .4, .34)
                           || (Math.Abs(x) <= .1 && y >= .15 && y <= .78);
            }
            throw new ArgumentException("Unknown shape " + shape);
        }
    }

    /// <summary>One generated level (file coordinates) and its quality stats.</summary>
    public sealed class LongLevel
    {
        public string Shape;
        public double D, BoxFillPct;
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
        public const int MaxTries = 60;      // seeds tried per level until the quality is in range
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
            double[] b;
            if (boxes.TryGetValue(shape, out b)) return b;
            const double lim = 1.6, step = 0.004;
            int n = (int)(2 * lim / step);
            double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
            for (int j = 0; j <= n; j++)
            {
                double y = -lim + j * step;
                for (int i = 0; i <= n; i++)
                {
                    double x = -lim + i * step;
                    if (!LongShapes.Inside(shape, x, y)) continue;
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
        /// Scale so the bbox fills Fill of the max grid (MaxCols x MaxRows) at d = 1, times lerp(0.45, 1, d); grid = bbox
        /// rounded up + 1 cell margin per side (capped at the max, min 12 columns), bbox centred.
        /// </summary>
        public static bool[] BuildMask(string shape, double d, out int cols, out int rows, out double boxFillPct, out bool clipped)
        {
            var b = BBox(shape);
            double bw = b[2] - b[0], bh = b[3] - b[1], cx = (b[0] + b[2]) / 2, cy = (b[1] + b[3]) / 2;
            double sc = Math.Min(MaxCols * Fill / bw, MaxRows * Fill / bh) * Lerp(0.45, 1.0, d);
            if (Math.Ceiling(bw * sc) + 2 < 12) sc = 9.999 / bw;
            cols = Math.Min(MaxCols, (int)Math.Ceiling(bw * sc) + 2);
            rows = Math.Min(MaxRows, (int)Math.Ceiling(bh * sc) + 2);
            boxFillPct = 100.0 * (bw * sc) * (bh * sc) / (cols * rows);
            clipped = bw * sc > cols || bh * sc > rows;

            var mask = new bool[cols * rows];
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    mask[r * cols + c] = LongShapes.Inside(shape, cx + (c + 0.5 - cols / 2.0) / sc, cy + (r + 0.5 - rows / 2.0) / sc);
            return mask;
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
            var sw = System.Diagnostics.Stopwatch.StartNew();
            LongLevel best = null;
            double bestP = double.MaxValue;
            int tries = 0;
            for (int t = 0; t < MaxTries; t++)
            {
                tries++;
                var l = TryOnce(shape, d, baseSeed + t);
                if (Good(l)) { best = l; break; }
                double p = Penalty(l);
                if (p < bestP) { bestP = p; best = l; }
            }
            best.BaseSeed = baseSeed;
            best.Tries = tries;
            best.InRange = Good(best);
            best.Ms = sw.Elapsed.TotalMilliseconds;
            return best;
        }

        static LongLevel TryOnce(string shape, double d, int seed)
        {
            var rng = new Random(seed);
            int cols, rows;
            double fill;
            bool clipped;
            var mask = BuildMask(shape, d, out cols, out rows, out fill, out clipped);
            var snakes = Snakes(mask, cols, rows, rng);
            var paths = new List<List<int>>();
            foreach (var s in snakes) Cut(s, d, cols, rng, paths);
            int dropped = Cleanup(paths, mask, cols, rows);

            var lvl = new LongLevel
            {
                Shape = shape, D = d, Seed = seed, Cols = cols, Rows = rows, Mask = mask, Dropped = dropped,
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
        static List<List<int>> Snakes(bool[] mask, int cols, int rows, Random rng)
        {
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
                        double s = FreeNbrs(free, nb, cols, rows) + rng.NextDouble() * 1.4 - (k == lastDir ? 0.9 : 0);
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

        /// <summary>Short 3-8 / medium 9-22 / long 23-60, mix by d: 0 -> 90/10/0, 0.5 -> 45/40/15, 1 -> 15/35/50.</summary>
        static int TargetLen(double d, Random rng)
        {
            double pS, pM;
            if (d <= 0.5) { pS = Lerp(0.90, 0.45, d / 0.5); pM = Lerp(0.10, 0.40, d / 0.5); }
            else { pS = Lerp(0.45, 0.15, (d - 0.5) / 0.5); pM = Lerp(0.40, 0.35, (d - 0.5) / 0.5); }
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
            int pos = 0, n = s.Count;
            while (pos < n)
            {
                int rem = n - pos;
                int len = TargetLen(d, rng);
                if (rem - len < 2)
                {
                    outPaths.Add(s.GetRange(pos, rem)); // 1-cell snakes are handled by Cleanup
                    break;
                }
                int k = pos + len, lo = pos + 2, hi = n - 2;
                if (RunLen(s, k, cols) < 3)
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
            var sb = new StringBuilder();
            sb.Append("# long samples (Arrow/Long Levels/Generate Samples). L <n> <shape> <cols> <rows> <arrowCount> seed=<seed> d=<difficulty>; ")
              .Append("M = mask row (# is a board cell); A <colorIndex 0-7> <col,row ...> tail to head (last cell is the head)\n");
            for (int n = 0; n < levels.Count; n++)
            {
                var l = levels[n];
                sb.Append(string.Format(CultureInfo.InvariantCulture, "L {0} {1} {2} {3} {4} seed={5} d={6:0.##}\n",
                    n + 1, l.Shape, l.Cols, l.Rows, l.Count, l.Seed, l.D));
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

    /// <summary>The sample set: generates, writes long_samples.txt and the PNG previews.</summary>
    public static class LongSamples
    {
        public struct Sample { public string Shape; public double D; public int Seed; }

        public static readonly Sample[] List =
        {
            new Sample { Shape = "heart", D = 0.15, Seed = 1001 },
            new Sample { Shape = "hourglass", D = 0.5, Seed = 2001 },
            new Sample { Shape = "paw", D = 1.0, Seed = 3001 },
            new Sample { Shape = "star", D = 1.0, Seed = 4001 },
            new Sample { Shape = "cat", D = 1.0, Seed = 5001 },
            new Sample { Shape = "fish", D = 1.0, Seed = 6001 },
            new Sample { Shape = "heart", D = 1.0, Seed = 7001 },
        };

        public const string LevelFile = "Assets/ArrowPuzzle/Resources/Levels/long_samples.txt"; // next to shapes.txt
        public const string PreviewDir = "LongPreview";

        /// <summary>projectRoot = the Unity project folder (parent of Assets). Returns the generated levels.</summary>
        public static List<LongLevel> Run(string projectRoot, Action<string, float> progress, out string text)
        {
            var levels = new List<LongLevel>();
            for (int i = 0; i < List.Length; i++)
            {
                if (progress != null) progress(List[i].Shape, i / (float)List.Length);
                levels.Add(LongLevelGen.Generate(List[i].Shape, List[i].D, List[i].Seed));
            }

            text = LongLevelGen.ToText(levels);
            File.WriteAllText(Path.Combine(projectRoot, LevelFile), text.Replace("\r", ""));

            string dir = Path.Combine(projectRoot, PreviewDir);
            Directory.CreateDirectory(dir);
            foreach (var old in Directory.GetFiles(dir)) File.Delete(old); // no stale previews from an older sample list
            var summary = new StringBuilder();
            for (int i = 0; i < levels.Count; i++)
            {
                var l = levels[i];
                string name = (i + 1) + "_" + l.Shape;
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), LongPreview.Png(l, 20));
                File.WriteAllText(Path.Combine(dir, name + ".txt"), LongLevelGen.StatLine(l) + "\n");
                summary.Append(i + 1).Append(". ").Append(LongLevelGen.StatLine(l)).Append('\n');
            }
            File.WriteAllText(Path.Combine(dir, "summary.txt"), summary.ToString());
            return levels;
        }
    }

    /// <summary>
    /// PNG preview: white background, faint grey mask cells, each arrow a 2 px line through its cell centres with a small
    /// arrowhead. Free at the start = green, others black.
    /// </summary>
    public static class LongPreview
    {
        const int Margin = 12;

        public static byte[] Png(LongLevel l, int cellPx)
        {
            int w = l.Cols * cellPx + 2 * Margin, h = l.Rows * cellPx + 2 * Margin;
            var img = new float[w * h * 3];
            for (int i = 0; i < img.Length; i++) img[i] = 1f;

            for (int r = 0; r < l.Rows; r++)
                for (int c = 0; c < l.Cols; c++)
                    if (l.Mask[r * l.Cols + c])
                        for (int y = 1; y < cellPx - 1; y++)
                            for (int x = 1; x < cellPx - 1; x++)
                                Blend(img, w, h, Margin + c * cellPx + x, Margin + r * cellPx + y, 0.945f, 0.945f, 0.945f, 1f);

            for (int i = 0; i < l.Count; i++)
            {
                bool free = l.FreeAtStart != null && l.FreeAtStart[i];
                float cr = free ? 0.10f : 0f, cg = free ? 0.62f : 0f, cb = free ? 0.25f : 0f;
                var a = l.Arrows[i];
                var pts = new List<double[]>();
                foreach (int cell in a)
                    pts.Add(new[] { Margin + (cell % l.Cols + 0.5) * cellPx, Margin + (cell / l.Cols + 0.5) * cellPx });
                var hp = pts[pts.Count - 1];
                var pp = pts[pts.Count - 2];
                double dx = hp[0] - pp[0], dy = hp[1] - pp[1], len = Math.Sqrt(dx * dx + dy * dy);
                dx /= len; dy /= len;
                for (int j = 1; j < pts.Count; j++) Segment(img, w, h, pts[j - 1], pts[j], 1.0, cr, cg, cb);
                double hs = cellPx;
                var apex = new[] { hp[0] + dx * 0.42 * hs, hp[1] + dy * 0.42 * hs };
                var bc = new[] { hp[0] - dx * 0.06 * hs, hp[1] - dy * 0.06 * hs };
                double hw = 0.24 * hs;
                Triangle(img, w, h, apex, new[] { bc[0] - dy * hw, bc[1] + dx * hw }, new[] { bc[0] + dy * hw, bc[1] - dx * hw }, cr, cg, cb);
            }
            return Encode(w, h, img);
        }

        static void Blend(float[] img, int w, int h, int x, int y, float r, float g, float b, float a)
        {
            if (x < 0 || y < 0 || x >= w || y >= h || a <= 0f) return;
            int i = (y * w + x) * 3;
            img[i] += (r - img[i]) * a;
            img[i + 1] += (g - img[i + 1]) * a;
            img[i + 2] += (b - img[i + 2]) * a;
        }

        /// <summary>Anti-aliased line of half width hw (coverage from the distance to the segment).</summary>
        static void Segment(float[] img, int w, int h, double[] a, double[] b, double hw, float r, float g, float bl)
        {
            int x0 = (int)Math.Floor(Math.Min(a[0], b[0]) - hw - 2), x1 = (int)Math.Ceiling(Math.Max(a[0], b[0]) + hw + 2);
            int y0 = (int)Math.Floor(Math.Min(a[1], b[1]) - hw - 2), y1 = (int)Math.Ceiling(Math.Max(a[1], b[1]) + hw + 2);
            double vx = b[0] - a[0], vy = b[1] - a[1], vv = Math.Max(1e-9, vx * vx + vy * vy);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    double px = x + 0.5 - a[0], py = y + 0.5 - a[1];
                    double t = Math.Max(0, Math.Min(1, (px * vx + py * vy) / vv));
                    double ex = px - vx * t, ey = py - vy * t;
                    double cov = hw + 0.5 - Math.Sqrt(ex * ex + ey * ey);
                    if (cov > 0) Blend(img, w, h, x, y, r, g, bl, (float)Math.Min(1, cov));
                }
            }
        }

        /// <summary>Filled triangle, 4 x 4 supersampled.</summary>
        static void Triangle(float[] img, int w, int h, double[] p0, double[] p1, double[] p2, float r, float g, float b)
        {
            int x0 = (int)Math.Floor(Math.Min(p0[0], Math.Min(p1[0], p2[0]))), x1 = (int)Math.Ceiling(Math.Max(p0[0], Math.Max(p1[0], p2[0])));
            int y0 = (int)Math.Floor(Math.Min(p0[1], Math.Min(p1[1], p2[1]))), y1 = (int)Math.Ceiling(Math.Max(p0[1], Math.Max(p1[1], p2[1])));
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < 4; sy++)
                        for (int sx = 0; sx < 4; sx++)
                            if (InTri(x + (sx + 0.5) / 4, y + (sy + 0.5) / 4, p0, p1, p2)) hit++;
                    if (hit > 0) Blend(img, w, h, x, y, r, g, b, hit / 16f);
                }
            }
        }

        static bool InTri(double x, double y, double[] a, double[] b, double[] c)
        {
            double d1 = (x - b[0]) * (a[1] - b[1]) - (a[0] - b[0]) * (y - b[1]);
            double d2 = (x - c[0]) * (b[1] - c[1]) - (b[0] - c[0]) * (y - c[1]);
            double d3 = (x - a[0]) * (c[1] - a[1]) - (c[0] - a[0]) * (y - a[1]);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        // ------------------------------------------------------------------ minimal PNG encoder (RGB 8 bit, zlib)

        static byte[] Encode(int w, int h, float[] img)
        {
            var raw = new byte[h * (w * 3 + 1)];
            int k = 0;
            for (int y = 0; y < h; y++)
            {
                raw[k++] = 0; // filter: none
                for (int x = 0; x < w * 3; x++)
                    raw[k++] = (byte)Math.Max(0, Math.Min(255, (int)Math.Round(img[y * w * 3 + x] * 255f)));
            }

            byte[] deflated;
            using (var ms = new MemoryStream())
            {
                using (var ds = new DeflateStream(ms, CompressionLevel.Optimal, true)) ds.Write(raw, 0, raw.Length);
                deflated = ms.ToArray();
            }
            uint s1 = 1, s2 = 0;
            foreach (byte b in raw) { s1 = (s1 + b) % 65521; s2 = (s2 + s1) % 65521; }
            var z = new byte[deflated.Length + 6];
            z[0] = 0x78; z[1] = 0x9C;
            Buffer.BlockCopy(deflated, 0, z, 2, deflated.Length);
            uint adler = (s2 << 16) | s1;
            z[z.Length - 4] = (byte)(adler >> 24); z[z.Length - 3] = (byte)(adler >> 16);
            z[z.Length - 2] = (byte)(adler >> 8); z[z.Length - 1] = (byte)adler;

            using (var png = new MemoryStream())
            {
                png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
                var ihdr = new byte[13];
                Be(ihdr, 0, (uint)w); Be(ihdr, 4, (uint)h);
                ihdr[8] = 8; ihdr[9] = 2; // 8 bit, RGB
                Chunk(png, "IHDR", ihdr);
                Chunk(png, "IDAT", z);
                Chunk(png, "IEND", new byte[0]);
                return png.ToArray();
            }
        }

        static void Be(byte[] b, int o, uint v)
        {
            b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v;
        }

        static uint[] crcTable;

        static void Chunk(Stream s, string type, byte[] data)
        {
            var len = new byte[4];
            Be(len, 0, (uint)data.Length);
            s.Write(len, 0, 4);
            var td = new byte[4 + data.Length];
            for (int i = 0; i < 4; i++) td[i] = (byte)type[i];
            Buffer.BlockCopy(data, 0, td, 4, data.Length);
            s.Write(td, 0, td.Length);
            var crc = new byte[4];
            Be(crc, 0, Crc(td));
            s.Write(crc, 0, 4);
        }

        static uint Crc(byte[] d)
        {
            if (crcTable == null)
            {
                crcTable = new uint[256];
                for (uint n = 0; n < 256; n++)
                {
                    uint c = n;
                    for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                    crcTable[n] = c;
                }
            }
            uint x = 0xFFFFFFFFu;
            foreach (byte b in d) x = crcTable[(x ^ b) & 0xFF] ^ (x >> 8);
            return x ^ 0xFFFFFFFFu;
        }
    }
}
