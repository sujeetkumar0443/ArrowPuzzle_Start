using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// Offline build of the whole long-arrow campaign (Campaign: 333 levels, every 3rd a shape, the rest rectangle boards). Editor only, no UnityEngine calls,
// so the .NET harness produces the same files. Menu: Arrow/Long Levels/Build Full Level Set (LongLevelEditorTools).
namespace ArrowPuzzle
{
    public static class LongCampaignBuild
    {
        public const string OutDir = "Assets/ArrowPuzzle/Resources/Levels/long"; // = Resources/ + Campaign.Folder
        public const string SummaryFile = "LongPreview/fullset_summary.txt";       // outside Assets (gitignored)

        // ------------------------------------------------------------------ difficulty curve

        // base(level): smooth monotone ramp through these points (piecewise cubic Hermite, Fritsch-Carlson slopes)
        static readonly double[] KnotL = { 1, 15, 50, 120, 200 };
        static readonly double[] KnotD = { 0, 0.3, 0.6, 0.85, 1.0 };
        static double[] slopes;

        public static double BaseD(int level)
        {
            if (level <= KnotL[0]) return KnotD[0];
            if (level >= KnotL[KnotL.Length - 1]) return KnotD[KnotD.Length - 1];
            if (slopes == null) slopes = Slopes(KnotL, KnotD);
            int k = 0;
            while (level > KnotL[k + 1]) k++;
            double h = KnotL[k + 1] - KnotL[k], t = (level - KnotL[k]) / h, t2 = t * t, t3 = t2 * t;
            return (2 * t3 - 3 * t2 + 1) * KnotD[k] + (t3 - 2 * t2 + t) * h * slopes[k]
                   + (-2 * t3 + 3 * t2) * KnotD[k + 1] + (t3 - t2) * h * slopes[k + 1];
        }

        static double[] Slopes(double[] x, double[] y)
        {
            int n = x.Length;
            var delta = new double[n - 1];
            for (int k = 0; k < n - 1; k++) delta[k] = (y[k + 1] - y[k]) / (x[k + 1] - x[k]);
            var m = new double[n];
            m[0] = delta[0];
            m[n - 1] = delta[n - 2];
            for (int k = 1; k < n - 1; k++)
            {
                if (delta[k - 1] * delta[k] <= 0) { m[k] = 0; continue; }
                double h0 = x[k] - x[k - 1], h1 = x[k + 1] - x[k];
                double w1 = 2 * h1 + h0, w2 = h1 + 2 * h0;
                m[k] = (w1 + w2) / (w1 / delta[k - 1] + w2 / delta[k]);
            }
            return m;
        }

        /// <summary>
        /// Generator difficulty of a global level: base ramp; every 5th level (not the 10th) a breather (-0.12), every 10th
        /// a boss (+0.1); clamped to 0..1 and rounded to 3 decimals (the value written to the files is the value used).
        /// </summary>
        public static double D(int level)
        {
            double d = BaseD(level);
            if (level % 10 == 0) d += 0.1;
            else if (level % 5 == 0) d -= 0.12;
            d = Math.Max(0, Math.Min(1, d));
            return Math.Round(d, 3, MidpointRounding.AwayFromZero);
        }

        public static int Seed(int level) { return level * 1000; } // + try (LongLevelGen.Generate)

        // ------------------------------------------------------------------ normal boards (rectangles)

        public const int NormalRowsMin = 11, NormalRowsMax = 46, NormalColsMin = 7;
        public const double AspectMin = 0.6, AspectMax = 0.8;
        public const int TinyCols = 8, TinyRows = 11, TinyMinArrows = 8, TinyMaxArrows = 12; // levels 1 and 2
        public const double TinyLenD = 0.3; // levels 1 and 2: a few medium arrows so 88 cells make 8..12 arrows

        /// <summary>Deterministic aspect cols / rows of a normal level, AspectMin..AspectMax (integer hash of the level).</summary>
        public static double Aspect(int level)
        {
            return AspectMin + (AspectMax - AspectMin) * Unit(level, 0u);
        }

        /// <summary>Deterministic 0..1 value of (level, salt); salt 0 = the original Aspect hash.</summary>
        static double Unit(int level, uint salt)
        {
            unchecked
            {
                uint h = (uint)level * 2654435761u + salt * 0x9E3779B9u;
                h ^= h >> 13;
                h *= 2246822519u;
                h ^= h >> 16;
                return (h % 10001) / 10000.0;
            }
        }

        // Variety: without it, every normal level from ~180 on had d = 1 -> the same 31 x 46 board and length mix.
        public const double SizeScaleMin = 0.8;   // rows = lerp(11, 46, d) * (SizeScaleMin..1) per level
        public const double LenJitter = 0.15;     // arrow length mix from d +- this (per level), clamped to 0..1
        public const int NeighbourStep = 3;       // a size equal to the previous normal level's moves this many rows

        static int[,] sizes; // [level, 0 = cols / 1 = rows], normal levels only, filled in level order

        /// <summary>
        /// Size and extra rules of normal level `level` at difficulty d: rows = round(lerp(11, 46, d) * scale) with a
        /// per-level scale SizeScaleMin..1 (at least 11), cols = round(rows * Aspect) (at least 7, at most
        /// LongLevelGen.MaxCols); a size equal to the previous normal level's is moved NeighbourStep rows; the arrow
        /// length mix uses d +- LenJitter. Levels 1 and 2: 8 x 11, 8..12 arrows.
        /// </summary>
        public static LongLevelGen.RectOptions NormalSize(int level, double d)
        {
            if (level <= 2)
                return new LongLevelGen.RectOptions
                {
                    Cols = TinyCols, Rows = TinyRows, LenD = TinyLenD, MinArrows = TinyMinArrows, MaxArrows = TinyMaxArrows
                };
            var s = Sizes();
            double lenD = Math.Max(0, Math.Min(1, d + (Unit(level, 2u) * 2 - 1) * LenJitter));
            return new LongLevelGen.RectOptions { Cols = s[level, 0], Rows = s[level, 1], LenD = lenD };
        }

        static int[,] Sizes()
        {
            lock (typeof(LongCampaignBuild))
            {
                if (sizes != null) return sizes;
                var s = new int[Campaign.TotalLevels + 1, 2];
                int prevC = TinyCols, prevR = TinyRows;
                for (int g = 3; g <= Campaign.TotalLevels; g++)
                {
                    if (Campaign.IsShape(g)) continue;
                    double scale = SizeScaleMin + (1 - SizeScaleMin) * Unit(g, 1u);
                    int rows = (int)Math.Floor((NormalRowsMin + (NormalRowsMax - NormalRowsMin) * D(g)) * scale + 0.5);
                    rows = Math.Max(NormalRowsMin, rows);
                    int cols = ColsFor(g, rows);
                    if (cols == prevC && rows == prevR)
                    {
                        rows = rows - NeighbourStep >= NormalRowsMin ? rows - NeighbourStep : rows + NeighbourStep;
                        cols = ColsFor(g, rows);
                        if (cols == prevC) cols += cols > NormalColsMin ? -1 : 1; // rows differ anyway; cols too
                    }
                    s[g, 0] = cols;
                    s[g, 1] = rows;
                    prevC = cols;
                    prevR = rows;
                }
                sizes = s;
                return sizes;
            }
        }

        static int ColsFor(int level, int rows)
        {
            int cols = (int)Math.Floor(rows * Aspect(level) + 0.5);
            return Math.Max(NormalColsMin, Math.Min(LongLevelGen.MaxCols, cols));
        }

        /// <summary>One campaign level: a shape (Campaign.ShapeOfLevel) or a normal rectangle board.</summary>
        public static LongLevel GenerateLevel(int level)
        {
            double d = D(level);
            string shape = Campaign.ShapeOfLevel(level);
            return shape != null ? LongLevelGen.Generate(shape, d, Seed(level))
                                 : LongLevelGen.GenerateRect(NormalSize(level, d), d, Seed(level));
        }

        // ------------------------------------------------------------------ build

        /// <summary>
        /// Generates all Campaign.TotalLevels levels (in parallel; each level only depends on its own seed), writes
        /// long/shape_NAME.txt (its 3 rounds), long/normal_NN.txt (Campaign.NormalPack each), long/index.txt and the
        /// summary. verify = the game's parser + peel test on level text (ShapeLevels.VerifyText). progress is called on
        /// the calling thread.
        /// </summary>
        public static LongLevel[] Run(string projectRoot, Action<string, float> progress,
                                      Func<string, IDictionary<int, string>> verify, out string summary)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            LongShapes.LoadDefs(projectRoot);
            foreach (var s in Campaign.Shapes)
            {
                if (!LongShapes.All.Contains(s)) throw new InvalidOperationException("Campaign shape " + s + " is not registered");
                LongLevelGen.BBox(s); // warm the cache before the parallel part
            }

            int n = Campaign.TotalLevels, done = 0;
            var levels = new LongLevel[n];
            // biggest boards first, so the slow ones do not end up alone at the end
            var order = new List<int>();
            for (int g = n; g >= 1; g--) order.Add(g);
            var task = Task.Run(() => Parallel.ForEach(order, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, g =>
            {
                levels[g - 1] = GenerateLevel(g);
                Interlocked.Increment(ref done);
            }));
            while (!task.Wait(250))
                if (progress != null) progress("Generating levels " + Volatile.Read(ref done) + " / " + n, Volatile.Read(ref done) / (float)n);
            if (task.IsFaulted) throw task.Exception;
            long genMs = sw.ElapsedMilliseconds;

            // files: group the levels by Campaign.FileOf, in level order
            string dir = Path.Combine(projectRoot, OutDir);
            Directory.CreateDirectory(dir);
            var files = new SortedDictionary<string, List<int>>(StringComparer.Ordinal);
            for (int g = 1; g <= n; g++)
            {
                string f = Campaign.FileOf(g);
                List<int> list;
                if (!files.TryGetValue(f, out list)) files[f] = list = new List<int>();
                list.Add(g);
            }
            var all = new StringBuilder();
            var ic = CultureInfo.InvariantCulture;
            var where = new Dictionary<int, int>(); // level -> index in its file
            foreach (var kv in files)
            {
                var list = new List<LongLevel>();
                foreach (int g in kv.Value) { where[g] = list.Count; list.Add(levels[g - 1]); }
                string name = kv.Key.Substring(kv.Key.IndexOf('/') + 1);
                string text = LongLevelGen.ToText(list, kv.Value, "# " + name + ": campaign levels " + string.Join(",", kv.Value.ConvertAll(x => x.ToString(ic)).ToArray())
                                                  + " (Arrow/Long Levels/Build Full Level Set).", "0.###");
                File.WriteAllText(Path.Combine(dir, name + ".txt"), text);
                all.Append(text);
            }
            var index = new StringBuilder();
            index.Append("# Campaign index (Arrow/Long Levels/Build Full Level Set): level kind file indexInFile shape cols rows arrowCount d\n");
            for (int g = 1; g <= n; g++)
            {
                var l = levels[g - 1];
                string shape = Campaign.ShapeOfLevel(g);
                index.Append(g).Append(' ').Append(shape != null ? "shape" : "normal").Append(' ').Append(Campaign.FileOf(g)).Append(' ')
                     .Append(where[g]).Append(' ').Append(shape ?? "-").Append(' ').Append(l.Cols).Append(' ').Append(l.Rows).Append(' ')
                     .Append(l.Count).Append(' ').Append(l.D.ToString("0.###", ic)).Append('\n');
            }
            File.WriteAllText(Path.Combine(dir, Campaign.IndexFile + ".txt"), index.ToString());

            // check + summary
            if (progress != null) progress("Peel test", 1f);
            var check = verify != null ? verify(all.ToString()) : null;
            summary = Summary(levels, check, genMs, DirBytes(dir));
            string sumPath = Path.Combine(projectRoot, SummaryFile);
            Directory.CreateDirectory(Path.GetDirectoryName(sumPath));
            File.WriteAllText(sumPath, summary);
            return levels;
        }

        // ------------------------------------------------------------------ gallery thumbnails

        public const double ThumbD = 0.6;

        /// <summary>Seed base of shape index s's thumbnail (outside the campaign's level * 1000 range).</summary>
        public static int ThumbSeed(int s) { return 5000000 + s * 1000; }

        /// <summary>
        /// One mid-density level per campaign shape (d = ThumbD) -> Resources/Levels/long/thumbs.txt, level number =
        /// shape index + 1, same text format. The Shapes gallery draws these instead of parsing the shape files.
        /// Returns the text; verify = the game's parser + peel test.
        /// </summary>
        public static string BuildThumbs(string projectRoot, Func<string, IDictionary<int, string>> verify, out string report)
        {
            LongShapes.LoadDefs(projectRoot);
            var levels = new LongLevel[Campaign.ShapeCount];
            foreach (var s in Campaign.Shapes) LongLevelGen.BBox(s);
            Parallel.For(0, Campaign.ShapeCount, i => levels[i] = LongLevelGen.Generate(Campaign.Shapes[i], ThumbD, ThumbSeed(i)));
            var nums = new List<int>();
            for (int i = 0; i < levels.Length; i++) nums.Add(i + 1);
            string text = LongLevelGen.ToText(levels, nums, "# Shape gallery thumbnails (Arrow/Long Levels/Build Shape Thumbnails): level n = Campaign shape n-1, d="
                                              + ThumbD.ToString("0.##", CultureInfo.InvariantCulture) + ".", "0.###");
            string path = Path.Combine(projectRoot, OutDir, Campaign.ThumbsFile + ".txt");
            File.WriteAllText(path, text);

            var check = verify != null ? verify(text) : null;
            int pass = 0;
            var sb = new StringBuilder();
            for (int i = 0; i < levels.Length; i++)
            {
                string err = null;
                bool ok = check != null && check.TryGetValue(i + 1, out err) && err == null;
                if (ok) pass++;
                else sb.Append("\n  ").Append(i + 1).Append(' ').Append(levels[i].Shape).Append(": ").Append(err ?? "missing");
            }
            report = "Shape thumbnails: " + levels.Length + " levels, peel test " + pass + "/" + levels.Length + " PASS, "
                     + (new FileInfo(path).Length / 1024.0).ToString("0.0", CultureInfo.InvariantCulture) + " KB -> " + path + sb;
            return text;
        }

        static long DirBytes(string dir)
        {
            long b = 0;
            foreach (var f in Directory.GetFiles(dir, "*.txt")) b += new FileInfo(f).Length;
            return b;
        }

        static string Summary(LongLevel[] levels, IDictionary<int, string> check, long genMs, long bytes)
        {
            var ic = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            var slow = new List<string>();
            int pass = 0, gatesOk = 0;
            sb.Append(string.Format(ic, "{0,5} {1,-11} {2,5} {3,-6} {4,5} {5,6} {6,9} {7,6} {8,5} {9,6} {10,11} {11,7} {12,-5} {13}\n",
                "level", "shape", "d", "grid", "cells", "arrows", "avg/max", "free%", "depth", "splits", "seed(try)", "ms", "peel", "gates failed"));
            for (int i = 0; i < levels.Length; i++)
            {
                var l = levels[i];
                int g = i + 1;
                string err = null;
                bool ok = check != null && check.TryGetValue(g, out err) && err == null;
                if (ok) pass++;
                string gates = LongLevelGen.GateFailures(l);
                if (gates == "-") gatesOk++;
                if (l.Tries > 30) slow.Add(g + " " + l.Shape + " d=" + l.D.ToString("0.###", ic) + " (" + l.Tries + " tries)");
                sb.Append(string.Format(ic, "{0,5} {1,-11} {2,5} {3,-6} {4,5} {5,6} {6,9} {7,6:0.0} {8,5} {9,6} {10,11} {11,7:0} {12,-5} {13}\n",
                    g, l.Shape, l.D.ToString("0.000", ic), l.Cols + "x" + l.Rows, l.MaskCells, l.Count,
                    l.AvgLen.ToString("0.0", ic) + "/" + l.MaxLen, l.FreePct, l.Depth, l.Splits, l.Seed + "(" + l.Tries + ")", l.Ms,
                    check == null ? "?" : ok ? "PASS" : "FAIL", gates + (ok || check == null ? "" : " | peel: " + (err ?? "missing"))));
            }
            sb.Append(string.Format(ic, "\n{0} levels: peel test {1}/{0} PASS, quality gates met {2}/{0}. Generation {3:0.0} s. long/ folder {4:0.00} MB.\n",
                levels.Length, pass, gatesOk, genMs / 1000.0, bytes / (1024.0 * 1024.0)));
            sb.Append("Levels that needed more than 30 tries: " + (slow.Count == 0 ? "none" : slow.Count + "\n  " + string.Join("\n  ", slow.ToArray())) + "\n");
            return sb.ToString();
        }
    }
}
