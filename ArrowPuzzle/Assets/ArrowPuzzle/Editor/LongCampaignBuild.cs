using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// Offline build of the prebuilt part of the level path (levels 1..PrebuildLevels; later levels are generated on the
// phone from the same code, Campaign.Generate). Editor only, no UnityEngine calls, so the .NET harness produces the
// same files. Menu: Arrow/Long Levels/Build Full Level Set (LongLevelEditorTools).
namespace ArrowPuzzle
{
    public static class LongCampaignBuild
    {
        public const string OutDir = "Assets/ArrowPuzzle/Resources/Levels/long"; // = Resources/ + Campaign.Folder
        public const string SummaryFile = "LongPreview/fullset_summary.txt";       // outside Assets (gitignored)

        /// <summary>Levels shipped as files, so phones never generate them (the release cap is lower).</summary>
        public const int PrebuildLevels = 300;

        /// <summary>
        /// Generates levels 1..count (in parallel; each depends only on its number), deletes the old level files in
        /// OutDir (shape_*, normal_*, pack_*), writes pack_NNN.txt (Campaign.PackSize each), index.txt and the summary.
        /// verify = the game's parser + peel test on level text (ShapeLevels.VerifyText). progress runs on the calling
        /// thread; log gets the generator's notes (rejected candidate shapes).
        /// </summary>
        public static LongLevel[] Run(string projectRoot, Action<string, float> progress,
                                      Func<string, IDictionary<int, string>> verify, out string summary, int count = PrebuildLevels,
                                      Action<string> log = null)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var notes = new List<string>();
            int n = count, done = 0;
            var levels = new LongLevel[n];
            // late (big) levels first, so the slow ones do not end up alone at the end
            var order = new List<int>();
            for (int g = n; g >= 1; g--) order.Add(g);
            var task = Task.Run(() => Parallel.ForEach(order, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, g =>
            {
                levels[g - 1] = Campaign.Generate(g, s => { lock (notes) notes.Add(s); });
                Interlocked.Increment(ref done);
            }));
            while (!task.Wait(250))
                if (progress != null) progress("Generating levels " + Volatile.Read(ref done) + " / " + n, Volatile.Read(ref done) / (float)n);
            if (task.IsFaulted) throw task.Exception;
            long genMs = sw.ElapsedMilliseconds;
            if (log != null) foreach (var s in notes) log(s);

            string dir = Path.Combine(projectRoot, OutDir);
            Directory.CreateDirectory(dir);
            foreach (var pattern in new[] { "shape_*.txt", "normal_*.txt", "pack_*.txt" })
                foreach (var f in Directory.GetFiles(dir, pattern))
                {
                    File.Delete(f);
                    if (File.Exists(f + ".meta")) File.Delete(f + ".meta");
                }

            var all = new StringBuilder();
            var ic = CultureInfo.InvariantCulture;
            var index = new StringBuilder();
            index.Append("# Level index (Arrow/Long Levels/Build Full Level Set): level file indexInFile shapeKey cols rows arrowCount d\n");
            for (int first = 1; first <= n; first += Campaign.PackSize)
            {
                var list = new List<LongLevel>();
                var nums = new List<int>();
                string file = Campaign.PackOf(first);
                for (int g = first; g < first + Campaign.PackSize && g <= n; g++)
                {
                    var l = levels[g - 1];
                    index.Append(g).Append(' ').Append(file).Append(' ').Append(list.Count).Append(' ').Append(l.Shape).Append(' ')
                         .Append(l.Cols).Append(' ').Append(l.Rows).Append(' ').Append(l.Count).Append(' ').Append(l.D.ToString("0.###", ic)).Append('\n');
                    list.Add(l);
                    nums.Add(g);
                }
                string name = file.Substring(file.IndexOf('/') + 1);
                string text = LongLevelGen.ToText(list, nums, "# " + name + ": levels " + nums[0] + "-" + nums[nums.Count - 1]
                                                  + " (Arrow/Long Levels/Build Full Level Set).", "0.###");
                File.WriteAllText(Path.Combine(dir, name + ".txt"), text);
                all.Append(text);
            }
            File.WriteAllText(Path.Combine(dir, Campaign.IndexFile + ".txt"), index.ToString());

            if (progress != null) progress("Peel test", 1f);
            var check = verify != null ? verify(all.ToString()) : null;
            summary = Summary(levels, check, genMs, DirBytes(dir)) + (notes.Count > 0 ? "Generator notes:\n  " + string.Join("\n  ", notes.ToArray()) + "\n" : "");
            string sumPath = Path.Combine(projectRoot, SummaryFile);
            Directory.CreateDirectory(Path.GetDirectoryName(sumPath));
            File.WriteAllText(sumPath, summary);
            return levels;
        }

        // ------------------------------------------------------------------ gallery thumbnails

        public const double ThumbD = 0.6;

        /// <summary>Seed base of shape index s's thumbnail (outside the levels' level * 1000 range).</summary>
        public static int ThumbSeed(int s) { return 1900000000 + s * 1000; }

        /// <summary>
        /// One mid-density level per base shape (d = ThumbD), in gallery order -> Resources/Levels/long/thumbs.txt, level
        /// number = shape index + 1, same text format. Returns the text; verify = the game's parser + peel test.
        /// </summary>
        public static string BuildThumbs(string projectRoot, Func<string, IDictionary<int, string>> verify, out string report)
        {
            var levels = new LongLevel[Campaign.ShapeCount];
            Parallel.For(0, Campaign.ShapeCount, i => levels[i] = LongLevelGen.Generate(Campaign.Shapes[i], ThumbD, ThumbSeed(i)));
            var nums = new List<int>();
            for (int i = 0; i < levels.Length; i++) nums.Add(i + 1);
            string text = LongLevelGen.ToText(levels, nums, "# Shape gallery thumbnails (Arrow/Long Levels/Build Shape Thumbnails): level n = Campaign.Shapes[n-1], d="
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

        // ------------------------------------------------------------------ checks

        /// <summary>
        /// Every base shape's mask at d = 0, 0.05, ... 1 through the mask rules (LongLevelGen.MaskProblem). One line
        /// per failure; empty when all pass.
        /// </summary>
        public static List<string> CheckBaseShapes()
        {
            var bad = new List<string>();
            foreach (var s in ShapeLibrary.All)
                for (int k = 0; k <= 20; k++)
                {
                    double d = k / 20.0;
                    string p = LongLevelGen.ProbeMask(s, d);
                    if (p != null) bad.Add(s + " d=" + d.ToString("0.##", CultureInfo.InvariantCulture) + ": " + p);
                }
            return bad;
        }

        /// <summary>
        /// The level plan for levels 1..count (no generation): base shapes repeating within `window` levels, and the same
        /// shape key on two consecutive levels. One line per problem.
        /// </summary>
        public static List<string> CheckPlan(int count, int window)
        {
            var bad = new List<string>();
            var last = new Dictionary<string, int>();
            string prevKey = null;
            for (int g = 1; g <= count; g++)
            {
                var sp = Campaign.Spec(g);
                if (sp.Key == prevKey) bad.Add("levels " + (g - 1) + " and " + g + " are both " + sp.Key);
                prevKey = sp.Key;
                if (sp.Procedural) continue;
                int at;
                if (last.TryGetValue(sp.Base, out at) && g - at <= window) bad.Add(sp.Base + " at levels " + at + " and " + g);
                last[sp.Base] = g;
            }
            return bad;
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
            sb.Append(string.Format(ic, "{0,5} {1,-22} {2,5} {3,5} {4,-6} {5,5} {6,6} {7,9} {8,6} {9,5} {10,6} {11,11} {12,7} {13,-5} {14}\n",
                "level", "shape", "d", "tngl", "grid", "cells", "arrows", "avg/max", "free%", "depth", "splits", "seed(try)", "ms", "peel", "gates failed"));
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
                sb.Append(string.Format(ic, "{0,5} {1,-22} {2,5} {3,5} {4,-6} {5,5} {6,6} {7,9} {8,6:0.0} {9,5} {10,6} {11,11} {12,7:0} {13,-5} {14}\n",
                    g, l.Shape, l.D.ToString("0.000", ic), l.Tangle.ToString("0.00", ic), l.Cols + "x" + l.Rows, l.MaskCells, l.Count,
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
