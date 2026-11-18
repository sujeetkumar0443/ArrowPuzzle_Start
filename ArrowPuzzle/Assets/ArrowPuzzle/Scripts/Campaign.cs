using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArrowPuzzle
{
    /// <summary>
    /// The level path, with no level cap in the code: level g (1, 2, ...) is built from its number alone.
    ///  - Levels 1..HandMadeLevels (100): each a different base shape (Shapes: 5 gentle ones first, then a fixed-seed
    ///    shuffle), so the gallery lists them in level order and shape i opens at level i + 1.
    ///  - Later levels: 3 of every 4 are a base shape plus a ShapeVariation, taken in blocks of 100 (every shape once
    ///    per block, a new fixed-seed order per block, and a different variation each block); every 4th level is a
    ///    procedural shape (petals, blob, mandala in turn). No base shape comes back within 20 levels.
    ///  - Difficulty D(g): bigger boards and longer arrows up to the size cap (d = 1 at level 200), then Tangle(g)
    ///    raises the arrow tangle instead of the board size.
    /// Levels 1..PrebuiltCount come from Resources/Levels/long (index.txt + pack_NNN.txt, built by "Arrow/Long
    /// Levels/Build Full Level Set"); later ones are generated on demand (LevelStream). ReleaseLevelCap is the only
    /// limit, and only on what the player may play.
    /// The table / spec / generate part has no Unity calls (worker thread, offline builder).
    /// </summary>
    public static class Campaign
    {
        // ------------------------------------------------------------------ release cap

        /// <summary>
        /// Last playable level in this build; past it the player sees "More levels coming soon" (ScreenComingSoon).
        /// 0 = no cap. The only switch: raising or removing it needs no other change.
        /// </summary>
        public const int ReleaseLevelCap = 100;

        public static bool IsPlayable(int level) { return level >= 1 && !PastCap(level); }

        public static bool PastCap(int level) { return ReleaseLevelCap > 0 && level > ReleaseLevelCap; }

        // ------------------------------------------------------------------ shapes in level order

        public const int HandMadeLevels = 100;

        /// <summary>Levels 1-5: small, compact, easy shapes (the tutorial stays gentle).</summary>
        public static readonly string[] EasyFirst = { "heart", "drop", "diamond", "cloud", "apple" };

        public const uint OrderSeed = 7919u;
        const int Tail = 20;      // the last 20 hand-made levels use group-B shapes only (block 0 starts with group A)
        const int BlockEdge = 16; // first / last 16 slots of a block: group A / group B (no repeat across a block edge)

        /// <summary>The 100 base shapes in level order: Shapes[i] is level i + 1 (the gallery lists them so).</summary>
        public static readonly string[] Shapes;

        static readonly string[] groupA, groupB; // fixed halves of the shape list (block edges)

        static Campaign()
        {
            var all = ShapeLibrary.All;
            if (all.Length != HandMadeLevels) throw new InvalidOperationException("ShapeLibrary has " + all.Length + " shapes, expected " + HandMadeLevels);
            var split = Shuffled(all, OrderSeed);
            groupA = Sub(split, 0, all.Length / 2);
            groupB = Sub(split, all.Length / 2, all.Length - all.Length / 2);

            var rest = new List<string>();
            foreach (var s in Shuffled(all, OrderSeed + 1)) if (Array.IndexOf(EasyFirst, s) < 0) rest.Add(s);
            var tail = new List<string>();
            foreach (var s in rest) if (tail.Count < Tail && Array.IndexOf(groupB, s) >= 0) tail.Add(s);
            var order = new List<string>(EasyFirst);
            foreach (var s in rest) if (!tail.Contains(s)) order.Add(s);
            order.AddRange(tail);
            Shapes = order.ToArray();
        }

        public static int ShapeCount { get { return Shapes.Length; } }

        public static int IndexOf(string shape) { return Array.IndexOf(Shapes, shape); }

        /// <summary>The level where shape index i first appears (and unlocks): i + 1.</summary>
        public static int FirstLevel(int shapeIndex) { return shapeIndex + 1; }

        public static int FirstLevel(string shape) { return FirstLevel(IndexOf(shape)); }

        /// <summary>Base shapes of block b (levels after the hand-made ones), each once: A head, mixed middle, B tail.</summary>
        static string[] Block(int b)
        {
            uint s = ShapeLibrary.Mix(OrderSeed ^ (uint)b * 0x9E3779B9u);
            var a = Shuffled(groupA, s + 1);
            var bb = Shuffled(groupB, s + 2);
            var mid = new List<string>();
            for (int i = BlockEdge; i < a.Length; i++) mid.Add(a[i]);
            for (int i = BlockEdge; i < bb.Length; i++) mid.Add(bb[i]);
            var res = new List<string>(Sub(a, 0, BlockEdge));
            res.AddRange(Shuffled(mid.ToArray(), s + 3));
            res.AddRange(Sub(bb, 0, BlockEdge));
            return res.ToArray();
        }

        static readonly ShapeVariation[] Cycle =
        {
            ShapeVariation.Mirror, ShapeVariation.Rot90, ShapeVariation.Scale, ShapeVariation.Hollow, ShapeVariation.Thick, ShapeVariation.Thin,
        };

        /// <summary>Variation of a base shape in block b: a different one each block (cycle offset by the name).</summary>
        static int VariationIndex(string baseName, int block)
        {
            return (int)((uint)(block % Cycle.Length) + Fnv(baseName) % (uint)Cycle.Length) % Cycle.Length;
        }

        /// <summary>Mirror of a left-right symmetric shape changes nothing: it is turned upside down instead.</summary>
        static ShapeVariation Real(ShapeVariation v, string baseName)
        {
            return v == ShapeVariation.Mirror && ShapeLibrary.IsSymmetric(baseName) ? ShapeVariation.Flip : v;
        }

        // ------------------------------------------------------------------ level spec

        /// <summary>Everything that decides level g, from g alone.</summary>
        public sealed class LevelSpec
        {
            public int Level, Seed;
            public double D, Tangle, LenD;   // LenD = arrow length mix d (-1 = D)
            public string Base;              // base shape, or the procedural kind ("mandala")
            public ShapeVariation Variation; // of the first candidate
            public bool Procedural;
            public string[] Candidates;      // shape keys, best first: the first whose mask passes the rules is used
            public string Key { get { return Candidates[0]; } }
        }

        public static LevelSpec Spec(int level)
        {
            if (level < 1) level = 1;
            var sp = new LevelSpec { Level = level, Seed = Seed(level), D = D(level), Tangle = Tangle(level), LenD = level <= EasyFirst.Length ? EasyLenD : -1 };
            if (level <= HandMadeLevels)
            {
                sp.Base = Shapes[level - 1];
                sp.Candidates = new[] { sp.Base };
                return sp;
            }
            int p = level - HandMadeLevels - 1;
            if (p % 4 == 3)
            {
                int q = p / 4;
                string kind = ShapeLibrary.ProcKinds[q % ShapeLibrary.ProcKinds.Length];
                sp.Base = kind;
                sp.Procedural = true;
                var c = new List<string>();
                uint s = ShapeLibrary.Mix((uint)level * 2654435761u);
                for (int k = 0; k < ProcTries; k++) c.Add(ShapeLibrary.ProcKey(kind, ShapeLibrary.Mix(s + (uint)k)));
                c.Add(SafeProc(kind));
                sp.Candidates = c.ToArray();
                return sp;
            }
            int slot = p / 4 * 3 + p % 4;
            int block = slot / HandMadeLevels;
            string name = Block(block)[slot % HandMadeLevels];
            int vi = VariationIndex(name, block);
            sp.Base = name;
            sp.Variation = Real(Cycle[vi], name);
            sp.Candidates = new[]
            {
                ShapeLibrary.Key(name, sp.Variation),
                ShapeLibrary.Key(name, Real(Cycle[(vi + 1) % Cycle.Length], name)),
                ShapeLibrary.Key(name, Real(Cycle[(vi + 2) % Cycle.Length], name)),
                name,
            };
            return sp;
        }

        public const double EasyLenD = 0.3; // levels 1-5: a few medium arrows instead of many short ones
        const int ProcTries = 6;

        /// <summary>A procedural key checked offline to pass the mask rules at every d (the last fallback).</summary>
        static string SafeProc(string kind)
        {
            switch (kind)
            {
                case ShapeLibrary.ProcPetals: return ShapeLibrary.ProcKey(kind, SafePetals);
                case ShapeLibrary.ProcBlob: return ShapeLibrary.ProcKey(kind, SafeBlob);
                default: return ShapeLibrary.ProcKey(kind, SafeMandala);
            }
        }

        public const uint SafePetals = 1, SafeBlob = 1, SafeMandala = 1;

        /// <summary>Base shape name ("heart") or procedural kind ("mandala") of a level.</summary>
        public static string ShapeOfLevel(int level) { return Spec(level).Base; }

        /// <summary>0-based gallery index of a level's base shape, -1 for a procedural level.</summary>
        public static int ShapeIndexOf(int level)
        {
            var sp = Spec(level);
            return sp.Procedural ? -1 : IndexOf(sp.Base);
        }

        public static string Title(string shape) { return ShapeLibrary.Title(shape); }

        /// <summary>Display "type" of a level: its base shape ("Heart") or procedural kind ("Mandala").</summary>
        public static string TypeTitle(int level) { return Title(ShapeOfLevel(level)); }

        /// <summary>Analytics / crash "type" of a level: the base shape or procedural kind (few distinct values).</summary>
        public static string TypeKey(int level) { return ShapeOfLevel(level); }

        // ------------------------------------------------------------------ difficulty

        // base(level): smooth monotone ramp through these points (piecewise cubic Hermite, Fritsch-Carlson slopes)
        static readonly double[] KnotL = { 1, 15, 50, 120, 200 };
        static readonly double[] KnotD = { 0, 0.3, 0.6, 0.85, 1.0 };
        static readonly double[] slopes = Slopes(KnotL, KnotD);

        public static double BaseD(int level)
        {
            if (level <= KnotL[0]) return KnotD[0];
            if (level >= KnotL[KnotL.Length - 1]) return KnotD[KnotD.Length - 1];
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
        /// Generator difficulty 0..1 (board size and arrow lengths): base ramp; every 5th level (not the 10th) a
        /// breather (-0.12), every 10th a boss (+0.1); rounded to 3 decimals. Reaches 1 (the size cap) at level 200.
        /// </summary>
        public static double D(int level)
        {
            double d = BaseD(level);
            if (level % 10 == 0) d += 0.1;
            else if (level % 5 == 0) d -= 0.12;
            d = Math.Max(0, Math.Min(1, d));
            return Math.Round(d, 3, MidpointRounding.AwayFromZero);
        }

        public const int TangleFrom = 200, TangleFull = 600;

        /// <summary>Past the size cap: 0 at level 200 rising to 1 at level 600 (more long arrows, tighter winding).</summary>
        public static double Tangle(int level)
        {
            return Math.Round(Math.Max(0, Math.Min(1, (level - TangleFrom) / (double)(TangleFull - TangleFrom))), 3);
        }

        /// <summary>Generator base seed of a level (the generator walks seed, seed + 1, ... until the level passes).</summary>
        public static int Seed(int level) { return (int)((long)Math.Max(1, level) * 1000 % int.MaxValue); }

        // ------------------------------------------------------------------ generate (any thread, no Unity calls)

        /// <summary>Seeds tried per level in all (deterministic: a count, never a time limit, so level g never changes).</summary>
        public const int TryBudget = 80;

        /// <summary>Seeds per candidate shape before the next candidate gets its turn (the last one gets what is left).</summary>
        public const int TriesPerCandidate = 30;

        /// <summary>
        /// Builds level g from its spec. Candidates in order: skipped when the mask breaks the rules (one region, at
        /// least 120 cells, 25-70 % of its box); otherwise the generator walks seed, seed + 1, ... (at most
        /// TriesPerCandidate, TryBudget over all) and the first level that is solvable and meets every quality gate wins.
        /// If none does, the best solvable one is used. log gets one line per rejected candidate.
        /// </summary>
        public static LongLevel Generate(int level, Action<string> log)
        {
            var sp = Spec(level);
            int budget = TryBudget;
            LongLevel best = null;
            double bestP = double.MaxValue;
            var usable = new List<string>();
            foreach (var key in sp.Candidates)
            {
                string problem = LongLevelGen.ProbeMask(key, sp.D);
                if (problem == null) usable.Add(key);
                else if (log != null) log("Level " + level + ": " + key + " skipped, mask " + problem);
            }
            for (int i = 0; i < usable.Count && budget > 0; i++)
            {
                int tries = i == usable.Count - 1 ? budget : Math.Min(budget, TriesPerCandidate);
                var l = LongLevelGen.Generate(usable[i], sp.D, sp.Seed, sp.Tangle, sp.LenD, tries);
                budget -= l.Tries;
                if (l.Solvable && l.InRange) return l;
                double p = LongLevelGen.QualityPenalty(l);
                if (l.Solvable && p < bestP) { best = l; bestP = p; }
                if (log != null && i < usable.Count - 1)
                    log("Level " + level + ": " + usable[i] + (l.Solvable ? " missed the quality gates" : " not solvable") + " in " + l.Tries + " seeds, next candidate");
            }
            if (best != null) return best;
            if (log != null) log("Level " + level + ": no candidate was solvable, using the heart");
            return LongLevelGen.Generate("heart", sp.D, sp.Seed, sp.Tangle, sp.LenD, TriesPerCandidate);
        }

        /// <summary>One level in the level-file format (number = g).</summary>
        public static string ToText(int level, LongLevel l)
        {
            return LongLevelGen.ToText(new[] { l }, new[] { level }, "# generated level " + level + ".", "0.###");
        }

        static uint Fnv(string s)
        {
            unchecked
            {
                uint h = 2166136261u;
                foreach (char c in s) { h ^= c; h *= 16777619u; }
                return h;
            }
        }

        static string[] Shuffled(string[] src, uint seed)
        {
            var a = (string[])src.Clone();
            uint s = seed;
            for (int i = a.Length - 1; i > 0; i--) // Fisher-Yates, own integer RNG (same on every platform)
            {
                s = ShapeLibrary.Mix(s + 0x6D2B79F5u);
                int j = (int)(s % (uint)(i + 1));
                var t = a[i]; a[i] = a[j]; a[j] = t;
            }
            return a;
        }

        static string[] Sub(string[] a, int start, int count)
        {
            var r = new string[count];
            Array.Copy(a, start, r, 0, count);
            return r;
        }

        // ------------------------------------------------------------------ files (main thread)

        public const string Folder = "Levels/long";   // under Resources
        public const string IndexFile = "index";
        public const string ThumbsFile = "thumbs"; // one small level per shape (level n = Shapes[n - 1]), for the gallery
        public const int PackSize = 25;            // prebuilt levels per pack file

        /// <summary>Pack file of a prebuilt level: "long/pack_001" holds levels 1-25.</summary>
        public static string PackOf(int level) { return "long/pack_" + ((level - 1) / PackSize + 1).ToString("000"); }

        /// <summary>The gallery thumbnail of shape index i (Resources/Levels/long/thumbs.txt, parsed once). Null if missing.</summary>
        public static LevelData Thumb(int shapeIndex)
        {
            var d = ShapeLevels.GetFile("long/" + ThumbsFile, shapeIndex + 1);
            if (d != null) d.longArrows = true;
            return d;
        }

        sealed class IndexEntry
        {
            public string File, Key;
            public int Arrows;
        }

        static Dictionary<int, IndexEntry> index;
        static int prebuilt = -1;

        /// <summary>Levels 1..PrebuiltCount ship as files (from index.txt; 0 if it is missing).</summary>
        public static int PrebuiltCount { get { LoadIndex(); return prebuilt; } }

        public static bool IsPrebuilt(int level) { return level >= 1 && level <= PrebuiltCount; }

        /// <summary>File of a prebuilt level ("long/pack_001"), null for a generated one.</summary>
        public static string FileOf(int level)
        {
            LoadIndex();
            IndexEntry e;
            return index.TryGetValue(level, out e) ? e.File : null;
        }

        /// <summary>Arrow count of a prebuilt level from index.txt, 0 for a generated one (not known before it is built).</summary>
        public static int ArrowCount(int level)
        {
            LoadIndex();
            IndexEntry e;
            return index.TryGetValue(level, out e) ? e.Arrows : 0;
        }

        /// <summary>
        /// index.txt lines: "level file indexInFile shapeKey cols rows arrowCount d" (# = comment). Any number of levels;
        /// PrebuiltCount = the last level of the unbroken run from 1. Loaded once.
        /// </summary>
        static void LoadIndex()
        {
            if (index != null) return;
            index = new Dictionary<int, IndexEntry>();
            var ta = Resources.Load<TextAsset>(Folder + "/" + IndexFile);
            if (ta == null)
            {
                Debug.LogError("[ArrowPuzzle] Missing Resources/" + Folder + "/" + IndexFile + ".txt: every level is generated");
                prebuilt = 0;
                return;
            }
            foreach (var raw in ta.text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var p = line.Split(' ');
                int g, n;
                if (p.Length < 8 || !int.TryParse(p[0], out g) || g < 1) continue;
                int.TryParse(p[6], out n);
                index[g] = new IndexEntry { File = p[1], Key = p[3], Arrows = n };
            }
            Resources.UnloadAsset(ta);
            int k = 0;
            while (index.ContainsKey(k + 1)) k++;
            prebuilt = k;
        }

        // ------------------------------------------------------------------ progress (Save)

        public static bool IsShapeUnlocked(int shapeIndex)
        {
            return shapeIndex >= 0 && shapeIndex < ShapeCount && Save.UnlockedGlobal >= FirstLevel(shapeIndex);
        }

        public static bool IsShapeUnlocked(string shape) { return IsShapeUnlocked(IndexOf(shape)); }

        /// <summary>The shape's first level is completed.</summary>
        public static bool ShapeDone(int shapeIndex)
        {
            return shapeIndex >= 0 && shapeIndex < ShapeCount && Save.IsCompleted(FirstLevel(shapeIndex));
        }

        public static bool ShapeDone(string shape) { return ShapeDone(IndexOf(shape)); }

        /// <summary>First shape (index) whose first level is above level `unlocked`; ShapeCount when all are reached.</summary>
        public static int NextShape(int unlocked)
        {
            return Math.Max(0, Math.Min(ShapeCount, unlocked));
        }
    }
}
