using System.Collections.Generic;
#if UNITY_EDITOR
using System.Text;
#endif
using UnityEngine;

namespace ArrowPuzzle
{
    /// <summary>
    /// Hand-made Shapes / Mosaic levels from Resources/Levels/shapes.txt and mosaic.txt (parsed once, cached).
    /// File format (rows top to bottom, columns left to right):
    ///   L n shape cols rows count   starts level n
    ///   M row                       one mask row, # = board cell, . = empty
    ///   A colorIndex c,r c,r ...    one arrow, cells from tail to head
    /// Converted to board coordinates on load: x = c, y = rows - 1 - r (y up, like BoardView).
    /// </summary>
    public static class ShapeLevels
    {
        class Entry
        {
            public LevelData data;
            public int expectedArrows, maskRows;
            public readonly List<string> errors = new List<string>(); // format problems found while parsing
        }

        static readonly Dictionary<GameMode, Dictionary<int, Entry>> cache = new Dictionary<GameMode, Dictionary<int, Entry>>();

        /// <summary>Level n (1..LevelsPerMode) of Shapes or Mosaic. Null for Classic or out-of-range n.</summary>
        public static LevelData Get(GameMode mode, int n)
        {
            if (mode == GameMode.Classic || n < 1 || n > ModeInfo.LevelsPerMode) return null;
            Entry e;
            return Table(mode).TryGetValue(n, out e) ? Copy(e.data) : null;
        }

        static Dictionary<int, Entry> Table(GameMode mode)
        {
            Dictionary<int, Entry> d;
            if (cache.TryGetValue(mode, out d)) return d;
            d = new Dictionary<int, Entry>();
            cache[mode] = d;

            string file = mode == GameMode.Shapes ? "shapes" : "mosaic";
            var ta = Resources.Load<TextAsset>("Levels/" + file);
            if (ta == null)
            {
                Debug.LogError("[ArrowPuzzle] Missing level file Resources/Levels/" + file + ".txt");
                return d;
            }
            Parse(ta.text, d);
            return d;
        }

        static void Parse(string text, Dictionary<int, Entry> d)
        {
            Entry cur = null;
            foreach (var raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var parts = line.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);

                if (parts[0] == "L" && parts.Length >= 6)
                {
                    int n, cols, rows, count;
                    if (!int.TryParse(parts[1], out n) || !int.TryParse(parts[3], out cols) ||
                        !int.TryParse(parts[4], out rows) || !int.TryParse(parts[5], out count) || cols < 1 || rows < 1)
                    {
                        Debug.LogError("[ArrowPuzzle] Bad level header: " + line);
                        cur = null;
                        continue;
                    }
                    cur = new Entry { expectedArrows = count };
                    cur.data = new LevelData { number = n, cols = cols, rows = rows, shape = parts[2], mask = new bool[cols, rows] };
                    d[n] = cur;
                }
                else if (cur == null)
                {
                    continue;
                }
                else if (parts[0] == "M" && parts.Length >= 2)
                {
                    var lv = cur.data;
                    string row = parts[1];
                    if (row.Length != lv.cols) cur.errors.Add("mask row " + cur.maskRows + " has " + row.Length + " columns, expected " + lv.cols);
                    int y = lv.rows - 1 - cur.maskRows;
                    if (y >= 0)
                        for (int x = 0; x < Mathf.Min(row.Length, lv.cols); x++) lv.mask[x, y] = row[x] == '#';
                    cur.maskRows++;
                }
                else if (parts[0] == "A" && parts.Length >= 2)
                {
                    var lv = cur.data;
                    var p = new PieceData();
                    int.TryParse(parts[1], out p.colorIndex);
                    for (int i = 2; i < parts.Length; i++)
                    {
                        var xy = parts[i].Split(',');
                        int c, r;
                        if (xy.Length != 2 || !int.TryParse(xy[0], out c) || !int.TryParse(xy[1], out r))
                        {
                            cur.errors.Add("bad cell '" + parts[i] + "'");
                            continue;
                        }
                        p.cells.Add(new Vector2Int(c, lv.rows - 1 - r));
                    }
                    if (p.cells.Count >= 2) p.dir = p.cells[p.cells.Count - 1] - p.cells[p.cells.Count - 2];
                    lv.pieces.Add(p);
                }
            }
        }

        /// <summary>Callers get their own copy, so nothing can change the cached level.</summary>
        static LevelData Copy(LevelData s)
        {
            var c = new LevelData
            {
                number = s.number, cols = s.cols, rows = s.rows, shape = s.shape,
                mask = s.mask != null ? (bool[,])s.mask.Clone() : null
            };
            foreach (var p in s.pieces)
                c.pieces.Add(new PieceData { cells = new List<Vector2Int>(p.cells), dir = p.dir, colorIndex = p.colorIndex });
            return c;
        }

#if UNITY_EDITOR
        // ------------------------------------------------------------------ editor self check

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void SelfCheck()
        {
            var report = new StringBuilder();
            int ok = 0, total = 0;
            foreach (var mode in new[] { GameMode.Shapes, GameMode.Mosaic })
            {
                var levels = Table(mode);
                for (int n = 1; n <= ModeInfo.LevelsPerMode; n++)
                {
                    total++;
                    Entry e;
                    string err = levels.TryGetValue(n, out e) ? Verify(e) : "missing";
                    if (err == null) ok++;
                    else report.Append("\n  ").Append(mode).Append(' ').Append(n).Append(": ").Append(err);
                }
            }
            if (ok == total) Debug.Log("ShapeLevels: " + total + " levels OK");
            else Debug.LogError("ShapeLevels: " + (total - ok) + " of " + total + " levels failed:" + report);
        }

        /// <summary>Null if the level is valid, otherwise the first problem found.</summary>
        static string Verify(Entry e)
        {
            var lv = e.data;
            if (e.errors.Count > 0) return e.errors[0];
            if (e.maskRows != lv.rows) return "mask has " + e.maskRows + " rows, expected " + lv.rows;
            if (lv.pieces.Count != e.expectedArrows) return lv.pieces.Count + " arrows, header says " + e.expectedArrows;

            var owner = new int[lv.cols, lv.rows];
            for (int x = 0; x < lv.cols; x++) for (int y = 0; y < lv.rows; y++) owner[x, y] = -1;

            for (int i = 0; i < lv.pieces.Count; i++)
            {
                var cells = lv.pieces[i].cells;
                if (cells.Count < 2) return "arrow " + i + " has " + cells.Count + " cell(s)";
                for (int j = 0; j < cells.Count; j++)
                {
                    var c = cells[j];
                    if (c.x < 0 || c.y < 0 || c.x >= lv.cols || c.y >= lv.rows || !lv.mask[c.x, c.y])
                        return "arrow " + i + " cell " + Fmt(lv, c) + " is outside the mask";
                    if (owner[c.x, c.y] >= 0) return "cell " + Fmt(lv, c) + " used by arrows " + owner[c.x, c.y] + " and " + i;
                    owner[c.x, c.y] = i;
                    if (j > 0)
                    {
                        var dd = c - cells[j - 1];
                        if (Mathf.Abs(dd.x) + Mathf.Abs(dd.y) != 1) return "arrow " + i + " cells " + Fmt(lv, cells[j - 1]) + " and " + Fmt(lv, c) + " are not neighbours";
                    }
                }
            }

            for (int x = 0; x < lv.cols; x++)
                for (int y = 0; y < lv.rows; y++)
                    if (lv.mask[x, y] && owner[x, y] < 0) return "mask cell " + Fmt(lv, new Vector2Int(x, y)) + " is not covered";

            // solvable: an arrow is free when every cell from its head to the board edge is empty,
            // its own body included; keep removing free arrows until none are left
            var removed = new bool[lv.pieces.Count];
            int left = lv.pieces.Count;
            bool progress = true;
            while (left > 0 && progress)
            {
                progress = false;
                for (int i = 0; i < lv.pieces.Count; i++)
                {
                    if (removed[i]) continue;
                    var p = lv.pieces[i];
                    bool free = true;
                    for (var c = p.Head + p.dir; c.x >= 0 && c.y >= 0 && c.x < lv.cols && c.y < lv.rows; c += p.dir)
                        if (owner[c.x, c.y] >= 0) { free = false; break; }
                    if (!free) continue;
                    foreach (var c in p.cells) owner[c.x, c.y] = -1;
                    removed[i] = true;
                    left--;
                    progress = true;
                }
            }
            return left == 0 ? null : "not solvable (" + left + " arrows stuck)";
        }

        /// <summary>Cell in file coordinates (col,row with row 0 at the top), so errors match the .txt.</summary>
        static string Fmt(LevelData lv, Vector2Int c)
        {
            return c.x + "," + (lv.rows - 1 - c.y);
        }
#endif
    }
}
