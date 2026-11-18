using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ArrowPuzzle
{
    /// <summary>
    /// "Check level path": every campaign level loads through Levels.For and matches index.txt.
    /// "Sanity Check Campaign Levels": a few levels through Levels.For with size, arrows, free % and the peel test.
    /// </summary>
    public static class LevelPathEditorTools
    {
        [MenuItem("ArrowPuzzle/Check level path")]
        static void CheckPath()
        {
            var list = new StringBuilder();
            int failed = 0, hard = 0;
            for (int g = 1; g <= LevelPath.Count; g++)
            {
                var data = Levels.For(g);
                bool ok = data != null && data.pieces.Count > 0 && data.number == g && data.shape == Campaign.ShapeOf(g)
                          && data.pieces.Count == Campaign.ArrowCount(g);
                if (!ok) failed++;
                string difficulty = LevelPath.Difficulty(g);
                if (difficulty == "Hard") hard++;
                list.Append("\n  ").Append(g).Append(" -> ").Append(Campaign.ShapeOf(g)).Append(' ')
                    .Append(Campaign.IndexInShape(g) + 1).Append("/").Append(Campaign.LevelsPerShape).Append(", d ")
                    .Append(Campaign.Difficulty(g).ToString("0.###")).Append(", ").Append(difficulty);
                if (data != null) list.Append(", ").Append(data.pieces.Count).Append(" arrows");
                if (!ok) list.Append("  FAILED");
            }
            string head = "Level path: " + LevelPath.Count + " levels, " + (LevelPath.Count - failed) + " load OK, "
                          + (LevelPath.Count - hard) + " Easy / " + hard + " Hard";
            if (failed == 0) Debug.Log(head + list);
            else Debug.LogError(head + ", " + failed + " FAILED" + list);
        }

        static readonly int[] SanityLevels = { 1, 2, 5, 10, 12, 13, 50, 100, 200, 444 };

        [MenuItem("Arrow/Long Levels/Sanity Check Campaign Levels")]
        static void Sanity()
        {
            var sb = new StringBuilder();
            bool all = true;
            foreach (int g in SanityLevels)
            {
                var data = Levels.For(g);
                if (data == null) { sb.Append("\n  ").Append(g).Append(": MISSING"); all = false; continue; }
                int free;
                bool solvable = Peel(data, out free);
                all &= solvable && data.longArrows;
                sb.Append("\n  ").Append(g).Append(' ').Append(data.shape).Append(" d ").Append(Campaign.Difficulty(g).ToString("0.###"))
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
