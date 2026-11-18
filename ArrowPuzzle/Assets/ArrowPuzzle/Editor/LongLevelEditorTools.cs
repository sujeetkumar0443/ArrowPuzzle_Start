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
