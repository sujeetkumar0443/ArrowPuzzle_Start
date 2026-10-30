using System.Collections.Generic;
using UnityEngine;

namespace ArrowPuzzle
{
    public class PieceData
    {
        public List<Vector2Int> cells = new List<Vector2Int>(); // tail -> head
        public Vector2Int dir;                                   // direction the head points / slides
        public int colorIndex;
        public Vector2Int Head { get { return cells[cells.Count - 1]; } }
    }

    public class LevelData
    {
        public int number, cols, rows;
        public List<PieceData> pieces = new List<PieceData>();
        public bool[,] mask;   // [x, y] true = board cell; null = full rectangle (Classic)
        public string shape;   // shape name for Shapes / Mosaic levels, null for Classic
    }

    /// <summary>
    /// Deterministic level generator. Every level is guaranteed solvable:
    ///  1. tile the whole grid with random snake paths,
    ///  2. pick which end is the head of each snake,
    ///  3. check the "blocked-by" graph is acyclic (a removal order exists); repair or retry otherwise.
    /// </summary>
    public static class LevelGen
    {
        static readonly Vector2Int[] Dirs =
        {
            new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0)
        };

        class Opt
        {
            public List<Vector2Int> path;
            public Vector2Int dir;
        }

        public static void SizeFor(int level, out int cols, out int rows, out int maxLen)
        {
            int t = Mathf.Max(0, level - 1);
            cols = Mathf.Min(4 + t / 4, 10);
            rows = Mathf.Min(5 + t / 3, 14);
            maxLen = Mathf.Min(3 + t / 5, 9);
        }

        public static LevelData Generate(int level)
        {
            int cols, rows, maxLen;
            SizeFor(level, out cols, out rows, out maxLen);
            for (int attempt = 0; attempt < 80; attempt++)
            {
                var rng = new System.Random(level * 7919 + attempt * 104729 + 17);
                var d = TryDense(level, cols, rows, maxLen, rng);
                if (d != null) return d;
            }
            return Fallback(level, cols, rows);
        }

        static bool In(int x, int y, int cols, int rows) { return x >= 0 && y >= 0 && x < cols && y < rows; }

        static LevelData TryDense(int level, int cols, int rows, int maxLen, System.Random rng)
        {
            var paths = Tile(cols, rows, maxLen, rng);
            var opts = new List<List<Opt>>();
            foreach (var p in paths)
            {
                var o = Options(p, cols, rows);
                if (o.Count == 0) return null;
                opts.Add(o);
            }
            var pick = new int[paths.Count];
            for (int i = 0; i < pick.Length; i++) pick[i] = rng.Next(opts[i].Count);

            for (int it = 0; it < 3000; it++)
            {
                var stuck = Stuck(opts, pick, cols, rows);
                if (stuck.Count == 0) return Build(level, cols, rows, opts, pick, rng);
                int i = stuck[rng.Next(stuck.Count)];
                pick[i] = rng.Next(opts[i].Count);
            }
            return null;
        }

        static int FreeNbrs(bool[,] free, Vector2Int p, int cols, int rows)
        {
            int n = 0;
            foreach (var d in Dirs)
            {
                int x = p.x + d.x, y = p.y + d.y;
                if (In(x, y, cols, rows) && free[x, y]) n++;
            }
            return n;
        }

        static List<List<Vector2Int>> Tile(int cols, int rows, int maxLen, System.Random rng)
        {
            var free = new bool[cols, rows];
            for (int x = 0; x < cols; x++) for (int y = 0; y < rows; y++) free[x, y] = true;
            int freeCount = cols * rows;
            var result = new List<List<Vector2Int>>();

            while (freeCount > 0)
            {
                // start from the free cell with the fewest free neighbours (avoids stranded cells)
                var best = new Vector2Int(-1, -1);
                int bestScore = int.MaxValue;
                for (int x = 0; x < cols; x++)
                {
                    for (int y = 0; y < rows; y++)
                    {
                        if (!free[x, y]) continue;
                        int score = FreeNbrs(free, new Vector2Int(x, y), cols, rows) * 1000 + rng.Next(1000);
                        if (score < bestScore) { bestScore = score; best = new Vector2Int(x, y); }
                    }
                }

                var path = new List<Vector2Int> { best };
                free[best.x, best.y] = false;
                freeCount--;
                int target = rng.Next(2, maxLen + 1);

                while (path.Count < target)
                {
                    var cur = path[path.Count - 1];
                    var ns = new List<Vector2Int>();
                    foreach (var d in Dirs)
                    {
                        int nx = cur.x + d.x, ny = cur.y + d.y;
                        if (In(nx, ny, cols, rows) && free[nx, ny]) ns.Add(new Vector2Int(nx, ny));
                    }
                    if (ns.Count == 0) break;

                    Vector2Int chosen;
                    if (rng.NextDouble() < 0.6)
                    {
                        chosen = ns[0];
                        int bs = FreeNbrs(free, chosen, cols, rows) * 1000 + rng.Next(1000);
                        for (int k = 1; k < ns.Count; k++)
                        {
                            int s = FreeNbrs(free, ns[k], cols, rows) * 1000 + rng.Next(1000);
                            if (s < bs) { bs = s; chosen = ns[k]; }
                        }
                    }
                    else chosen = ns[rng.Next(ns.Count)];

                    path.Add(chosen);
                    free[chosen.x, chosen.y] = false;
                    freeCount--;
                }
                result.Add(path);
            }
            return result;
        }

        static List<Opt> Options(List<Vector2Int> path, int cols, int rows)
        {
            var cand = new List<Opt>();
            if (path.Count == 1)
            {
                foreach (var d in Dirs) cand.Add(new Opt { path = path, dir = d });
            }
            else
            {
                var rev = new List<Vector2Int>(path);
                rev.Reverse();
                foreach (var p in new[] { path, rev })
                {
                    var h = p[p.Count - 1];
                    var pr = p[p.Count - 2];
                    cand.Add(new Opt { path = p, dir = h - pr });
                }
            }

            var good = new List<Opt>();
            foreach (var o in cand)
            {
                var c = o.path[o.path.Count - 1] + o.dir;
                bool self = false;
                while (In(c.x, c.y, cols, rows))
                {
                    if (o.path.Contains(c)) { self = true; break; }
                    c += o.dir;
                }
                if (!self) good.Add(o);
            }
            return good;
        }

        // Returns indices of pieces that can never be removed with the current head choices.
        static List<int> Stuck(List<List<Opt>> opts, int[] pick, int cols, int rows)
        {
            int n = opts.Count;
            var owner = new int[cols, rows];
            for (int x = 0; x < cols; x++) for (int y = 0; y < rows; y++) owner[x, y] = -1;
            for (int i = 0; i < n; i++)
                foreach (var c in opts[i][pick[i]].path) owner[c.x, c.y] = i;

            var blockers = new List<int>[n];
            for (int i = 0; i < n; i++)
            {
                blockers[i] = new List<int>();
                var o = opts[i][pick[i]];
                var c = o.path[o.path.Count - 1] + o.dir;
                while (In(c.x, c.y, cols, rows))
                {
                    int w = owner[c.x, c.y];
                    if (w >= 0 && w != i && !blockers[i].Contains(w)) blockers[i].Add(w);
                    c += o.dir;
                }
            }

            var alive = new bool[n];
            for (int i = 0; i < n; i++) alive[i] = true;
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int i = 0; i < n; i++)
                {
                    if (!alive[i]) continue;
                    bool blocked = false;
                    foreach (int w in blockers[i]) if (alive[w]) { blocked = true; break; }
                    if (!blocked) { alive[i] = false; changed = true; }
                }
            }

            var res = new List<int>();
            for (int i = 0; i < n; i++) if (alive[i]) res.Add(i);
            return res;
        }

        static LevelData Build(int level, int cols, int rows, List<List<Opt>> opts, int[] pick, System.Random rng)
        {
            var data = new LevelData { number = level, cols = cols, rows = rows };
            for (int i = 0; i < opts.Count; i++)
            {
                var o = opts[i][pick[i]];
                var pd = new PieceData { dir = o.dir, colorIndex = rng.Next(1000) };
                pd.cells.AddRange(o.path);
                data.pieces.Add(pd);
            }
            return data;
        }

        // Always-solvable simple board, only used if the dense generator somehow fails.
        static LevelData Fallback(int level, int cols, int rows)
        {
            var data = new LevelData { number = level, cols = cols, rows = rows };
            int k = 0;
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x + 1 < cols; x += 2)
                {
                    var pd = new PieceData { dir = new Vector2Int(1, 0), colorIndex = k++ };
                    pd.cells.Add(new Vector2Int(x, y));
                    pd.cells.Add(new Vector2Int(x + 1, y));
                    data.pieces.Add(pd);
                }
            }
            return data;
        }
    }
}
