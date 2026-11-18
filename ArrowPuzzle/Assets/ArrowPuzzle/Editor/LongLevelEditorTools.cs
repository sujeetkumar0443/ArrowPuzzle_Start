using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ArrowPuzzle
{
    /// <summary>
    /// "Arrow/Long Levels/Generate Samples": long winding sample levels (LongLevelGen) -> Assets/Resources/Levels/
    /// long_samples.txt + PNG previews in LongPreview/ (project folder, outside Assets), then the game's own parser and
    /// peel test (ShapeLevels.VerifyText) as the final check. The game does not load this file yet.
    /// </summary>
    public static class LongLevelEditorTools
    {
        [MenuItem("Arrow/Long Levels/Build Full Level Set")]
        static void BuildFullSet()
        {
            if (!EditorUtility.DisplayDialog("Build Full Level Set",
                    "Generates all " + Campaign.TotalLevels + " campaign levels into " + LongCampaignBuild.OutDir +
                    " (overwrites). This takes several minutes.", "Build", "Cancel")) return;
            string root = Path.GetDirectoryName(Application.dataPath);
            LongLevelGen.Warn = s => Debug.LogWarning(s);
            string summary;
            try
            {
                LongCampaignBuild.Run(root, (what, p) => EditorUtility.DisplayProgressBar("Long levels: full set", what, p),
                                      text => ShapeLevels.VerifyText(text), out summary);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            AssetDatabase.Refresh();
            int tail = summary.IndexOf("\n" + Campaign.TotalLevels + " levels:");
            string head = "Long levels: full set built -> " + LongCampaignBuild.OutDir + "; summary " + Path.Combine(root, LongCampaignBuild.SummaryFile)
                          + (tail >= 0 ? summary.Substring(tail) : "");
            if (summary.Contains(" FAIL ")) Debug.LogError(head);
            else Debug.Log(head);
        }

        [MenuItem("Arrow/Long Levels/Build Shape Thumbnails")]
        static void BuildThumbs()
        {
            string root = Path.GetDirectoryName(Application.dataPath);
            LongLevelGen.Warn = s => Debug.LogWarning(s);
            string report;
            LongCampaignBuild.BuildThumbs(root, text => ShapeLevels.VerifyText(text), out report);
            AssetDatabase.Refresh();
            if (report.Contains(Campaign.ShapeCount + "/" + Campaign.ShapeCount + " PASS")) Debug.Log(report);
            else Debug.LogError(report);
        }

        /// <summary>Every campaign file through the game's parser + peel test, plus the index (arrow counts, all levels present).</summary>
        [MenuItem("Arrow/Long Levels/Verify Full Level Set")]
        static void VerifyFullSet()
        {
            string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), LongCampaignBuild.OutDir);
            var arrows = new System.Collections.Generic.Dictionary<int, int>();
            var problems = new StringBuilder();
            int pass = 0, seen = 0;
            foreach (var shape in Campaign.Shapes)
            {
                string file = Path.Combine(dir, shape + ".txt");
                if (!File.Exists(file)) { problems.Append("\n  missing ").Append(file); continue; }
                string text = File.ReadAllText(file);
                foreach (var kv in ShapeLevels.VerifyText(text))
                {
                    seen++;
                    if (Campaign.ShapeOf(kv.Key) != shape) problems.Append("\n  level ").Append(kv.Key).Append(" is in ").Append(shape).Append(".txt");
                    if (kv.Value == null) pass++;
                    else problems.Append("\n  level ").Append(kv.Key).Append(": ").Append(kv.Value);
                }
                foreach (var line in text.Split('\n'))
                {
                    var p = line.Trim().Split(' ');
                    int g, n;
                    if (p.Length >= 6 && p[0] == "L" && int.TryParse(p[1], out g) && int.TryParse(p[5], out n)) arrows[g] = n;
                }
            }
            int indexed = 0;
            string indexFile = Path.Combine(dir, Campaign.IndexFile + ".txt");
            if (!File.Exists(indexFile)) problems.Append("\n  missing index.txt");
            else
                foreach (var line in File.ReadAllLines(indexFile))
                {
                    var p = line.Trim().Split(' ');
                    int g, n;
                    if (p.Length < 6 || line.StartsWith("#") || !int.TryParse(p[0], out g) || !int.TryParse(p[4], out n)) continue;
                    indexed++;
                    int real;
                    if (!arrows.TryGetValue(g, out real) || real != n || p[1] != Campaign.ShapeOf(g))
                        problems.Append("\n  index line for level ").Append(g).Append(" does not match its file");
                }
            bool ok = pass == Campaign.TotalLevels && seen == Campaign.TotalLevels && indexed == Campaign.TotalLevels && problems.Length == 0;
            string msg = "Long levels: Verify Full Level Set " + (ok ? "PASS" : "FAIL") + ": " + pass + " / " + Campaign.TotalLevels
                         + " levels pass the peel test, " + indexed + " index lines" + problems;
            if (ok) Debug.Log(msg);
            else Debug.LogError(msg);
        }

        [MenuItem("Arrow/Long Levels/Generate All Shapes Preview")]
        static void GenerateAllShapes()
        {
            string root = Path.GetDirectoryName(Application.dataPath);
            LongLevelGen.Warn = s => Debug.LogWarning(s);
            string summary;
            System.Collections.Generic.List<LongLevel> levels;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                levels = LongSamples.RunAll(root, (what, p) => EditorUtility.DisplayProgressBar("Long levels: all shapes", what, p),
                                            text => ShapeLevels.VerifyText(text), out summary);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            string head = "Long levels: " + levels.Count + " previews in " + (sw.ElapsedMilliseconds / 1000f).ToString("F1") + " s -> "
                          + Path.Combine(root, LongSamples.AllDir) + " (sheet_d1.png, summary_all.txt)\n";
            if (summary.Contains(" FAIL ")) Debug.LogError(head + summary);
            else Debug.Log(head + summary);
        }

        [MenuItem("Arrow/Long Levels/Generate Samples")]
        static void GenerateSamples()
        {
            string root = Path.GetDirectoryName(Application.dataPath);
            LongLevelGen.Warn = s => Debug.LogWarning(s);
            LongShapes.LoadDefs(root);
            string text;
            System.Collections.Generic.List<LongLevel> levels;
            try
            {
                levels = LongSamples.Run(root, (shape, p) => EditorUtility.DisplayProgressBar("Long levels", "Generating " + shape, p), out text);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            AssetDatabase.ImportAsset(LongSamples.LevelFile);

            var check = ShapeLevels.VerifyText(text);
            var report = new StringBuilder();
            bool all = check.Count == levels.Count;
            for (int i = 0; i < levels.Count; i++)
            {
                string err;
                bool pass = check.TryGetValue(i + 1, out err) && err == null;
                all &= pass;
                report.Append("\n  ").Append(i + 1).Append(". ").Append(pass ? "PASS " : "FAIL (" + (err ?? "missing") + ") ")
                      .Append(LongLevelGen.StatLine(levels[i]));
            }
            string head = "Long levels: " + levels.Count + " samples, peel test " + (all ? "PASS" : "FAIL") + ". Previews: "
                          + Path.Combine(root, LongSamples.PreviewDir);
            if (all) Debug.Log(head + report);
            else Debug.LogError(head + report);
        }
    }
}
