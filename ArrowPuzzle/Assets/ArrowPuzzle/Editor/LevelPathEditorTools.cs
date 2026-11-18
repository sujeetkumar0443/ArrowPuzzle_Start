using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ArrowPuzzle
{
    /// <summary>
    /// "Check level path": every prebuilt level loads through Levels.For, matches index.txt and its spec, plus the level
    /// plan of levels 1-1000 (no base shape twice within 20 levels, no shape key on two consecutive levels).
    /// "Sanity Check Campaign Levels": a few levels, prebuilt and generated (past the files), with the peel test.
    /// </summary>
    public static class LevelPathEditorTools
    {
        [MenuItem("ArrowPuzzle/Check level path")]
        static void CheckPath()
        {
            var list = new StringBuilder();
            int failed = 0, hard = 0, n = Campaign.PrebuiltCount;
            for (int g = 1; g <= n; g++)
            {
                var data = Levels.For(g);
                var sp = Campaign.Spec(g);
                bool ok = data != null && data.pieces.Count > 0 && data.number == g && System.Array.IndexOf(sp.Candidates, data.shape) >= 0
                          && data.pieces.Count == Campaign.ArrowCount(g);
                if (!ok) failed++;
                string difficulty = LevelPath.Difficulty(g);
                if (difficulty == "Hard") hard++;
                list.Append("\n  ").Append(g).Append(" -> ").Append(data != null ? data.shape : sp.Key).Append(", d ")
                    .Append(Campaign.D(g).ToString("0.###")).Append(", ").Append(difficulty);
                if (data != null) list.Append(", ").Append(data.cols).Append('x').Append(data.rows).Append(", ").Append(data.pieces.Count).Append(" arrows");
                if (!ok) list.Append("  FAILED");
            }
            var plan = LongCampaignBuild.CheckPlan(1000, 20);
            string head = "Level path: " + n + " prebuilt levels, " + (n - failed) + " load OK, " + (n - hard) + " Easy / " + hard
                          + " Hard; plan of levels 1-1000: " + (plan.Count == 0 ? "no repeats within 20 levels" : plan.Count + " problems:\n  "
                          + string.Join("\n  ", plan.ToArray()));
            if (failed == 0 && plan.Count == 0) Debug.Log(head + list);
            else Debug.LogError(head + ", " + failed + " FAILED" + list);
        }

        static readonly int[] SanityLevels = { 1, 2, 3, 5, 6, 50, 100, 101, 104, 250, 300, 301, 302, 304, 1000 };

        [MenuItem("Arrow/Long Levels/Sanity Check Campaign Levels")]
        static void Sanity()
        {
            var sb = new StringBuilder();
            bool all = true;
            foreach (int g in SanityLevels)
            {
                var data = Levels.For(g); // past the prebuilt files this generates on the main thread (slow, editor only)
                if (data == null) { sb.Append("\n  ").Append(g).Append(": MISSING"); all = false; continue; }
                int free;
                bool solvable = Peel(data, out free);
                all &= solvable && data.longArrows;
                sb.Append("\n  ").Append(g).Append(' ').Append(data.shape).Append(" d ").Append(Campaign.D(g).ToString("0.###"))
                  .Append(" tangle ").Append(Campaign.Tangle(g).ToString("0.##"))
                  .Append(": ").Append(data.cols).Append('x').Append(data.rows).Append(", ").Append(data.pieces.Count).Append(" arrows, free ")
                  .Append((100f * free / Mathf.Max(1, data.pieces.Count)).ToString("0")).Append("%, ")
                  .Append(solvable && data.longArrows ? "PASS" : "FAIL");
            }
            string msg = "Campaign sanity (Levels.For): " + (all ? "PASS" : "FAIL") + sb;
            if (all) Debug.Log(msg);
            else Debug.LogError(msg);
        }

        /// <summary>The game's rule: an arrow is free when every cell from its head to the grid edge is empty (its own body
        /// included). Removes free arrows until none move; free = arrows free at the start.</summary>
        static bool Peel(LevelData lv, out int freeAtStart)
        {
            var owner = new int[lv.cols, lv.rows];
            for (int x = 0; x < lv.cols; x++) for (int y = 0; y < lv.rows; y++) owner[x, y] = -1;
            for (int i = 0; i < lv.pieces.Count; i++) foreach (var c in lv.pieces[i].cells) owner[c.x, c.y] = i;
            var removed = new bool[lv.pieces.Count];
            int left = lv.pieces.Count;
            freeAtStart = -1;
            while (left > 0)
            {
                var round = new List<int>();
                for (int i = 0; i < lv.pieces.Count; i++)
                {
                    if (removed[i]) continue;
                    var p = lv.pieces[i];
                    bool free = true;
                    for (var c = p.Head + p.dir; c.x >= 0 && c.y >= 0 && c.x < lv.cols && c.y < lv.rows; c += p.dir)
                        if (owner[c.x, c.y] >= 0) { free = false; break; }
                    if (free) round.Add(i);
                }
                if (freeAtStart < 0) freeAtStart = round.Count;
                if (round.Count == 0) break;
                foreach (int i in round)
                {
                    removed[i] = true;
                    left--;
                    foreach (var c in lv.pieces[i].cells) owner[c.x, c.y] = -1;
                }
            }
            if (freeAtStart < 0) freeAtStart = 0;
            return left == 0;
        }
    }
}
