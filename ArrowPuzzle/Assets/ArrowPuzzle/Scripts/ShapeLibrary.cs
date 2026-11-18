using System;
using System.Collections.Generic;

namespace ArrowPuzzle
{
    /// <summary>Shape variation for levels after the hand-made ones (Campaign.Spec).</summary>
    public enum ShapeVariation { None, Mirror, Flip, Rot90, Scale, Hollow, Thick, Thin }

    /// <summary>
    /// Every shape the level generator can fill, as a key string:
    ///   "heart"            a base shape: the 37 vector outlines (LongShapes) or one of the 63 ASCII bitmaps below
    ///   "heart~hollow"     a base shape plus a ShapeVariation (mirror, flip, rot90, scale, hollow, thick, thin)
    ///   "petals-123"       a procedural shape from a seed: petals (superformula), blob (symmetric noise), mandala
    /// Resolve(key) gives inside(x, y) in normalized coords (x right, y DOWN, about -1..1) plus the grid morph
    /// (hollow / thick / thin) and size factor that LongLevelGen.BuildMask applies after rasterizing.
    /// No UnityEngine: runs on the level worker thread and in the offline builder. Everything is thread-safe.
    /// </summary>
    public static class ShapeLibrary
    {
        /// <summary>The 37 vector outlines (LongShapes built-ins + ShapeDefs), already tested in the campaign.</summary>
        public static readonly string[] VectorShapes =
        {
            "heart", "hourglass", "paw", "cat", "bunny", "star", "fish", "house", "tree", "apple", "cupcake", "icecream",
            "mushroom", "leaf", "flower", "moon", "cloud", "sailboat", "umbrella", "crown", "key", "lock", "rocket",
            "cherry", "pear", "strawberry", "bulb", "butterfly", "duck", "owl", "whale", "turtle", "snail", "cactus",
            "note", "diamond", "sun",
        };

        /// <summary>All 100 base shapes: the vector ones, then the bitmaps (in their table order).</summary>
        public static readonly string[] All;

        // explicit order: the table, then the parsed bitmaps, then the name list (field initializers would run too early)
        static ShapeLibrary()
        {
            BitmapTable = MakeTable();
            bitmaps = BuildBitmaps();
            All = BuildAll();
        }

        static string[] BuildAll()
        {
            var l = new List<string>(VectorShapes);
            foreach (var kv in BitmapTable) l.Add(kv.Key);
            return l.ToArray();
        }

        public static bool IsBase(string name) { return Array.IndexOf(All, name) >= 0; }

        public static bool IsBitmap(string name) { return name != null && bitmaps.ContainsKey(name); }

        // ------------------------------------------------------------------ titles

        static readonly Dictionary<string, string> titles = new Dictionary<string, string>
        {
            { "icecream", "Ice cream" }, { "moon", "Crescent moon" }, { "note", "Music note" }, { "bunny", "Rabbit" },
            { "paw", "Paw print" }, { "bulb", "Light bulb" }, { "lightning", "Lightning bolt" }, { "drop", "Water drop" },
            { "gift", "Gift box" }, { "tajmahal", "Taj Mahal" }, { "hotairballoon", "Hot air balloon" },
            { "petals", "Petals" }, { "blob", "Blob" }, { "mandala", "Mandala" },
        };

        /// <summary>Display name of a base shape or procedural kind: "heart" -> "Heart", "tajmahal" -> "Taj Mahal".</summary>
        public static string Title(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            string t;
            if (titles.TryGetValue(name, out t)) return t;
            return char.ToUpperInvariant(name[0]) + name.Substring(1);
        }

        /// <summary>Lower-case word for a variation ("hollow"), "" for None.</summary>
        public static string VariationName(ShapeVariation v)
        {
            return v == ShapeVariation.None ? "" : v.ToString().ToLowerInvariant();
        }

        // ------------------------------------------------------------------ keys

        public const string ProcPetals = "petals", ProcBlob = "blob", ProcMandala = "mandala";
        public static readonly string[] ProcKinds = { ProcPetals, ProcBlob, ProcMandala };

        public static string Key(string baseName, ShapeVariation v)
        {
            return v == ShapeVariation.None ? baseName : baseName + "~" + VariationName(v);
        }

        public static string ProcKey(string kind, uint seed) { return kind + "-" + seed; }

        /// <summary>Base shape name or procedural kind of a key: "heart~hollow" -> "heart", "mandala-12" -> "mandala".</summary>
        public static string BaseOf(string key)
        {
            if (string.IsNullOrEmpty(key)) return key;
            int t = key.IndexOf('~');
            if (t >= 0) return key.Substring(0, t);
            int d = key.IndexOf('-');
            return d >= 0 ? key.Substring(0, d) : key;
        }

        public static ShapeVariation VariationOf(string key)
        {
            int t = key == null ? -1 : key.IndexOf('~');
            if (t < 0) return ShapeVariation.None;
            string v = key.Substring(t + 1);
            foreach (ShapeVariation x in Enum.GetValues(typeof(ShapeVariation)))
                if (VariationName(x) == v) return x;
            throw new ArgumentException("Unknown variation in " + key);
        }

        // ------------------------------------------------------------------ resolve

        /// <summary>A resolved shape key: inside test plus what BuildMask does after rasterizing.</summary>
        public sealed class Source
        {
            public string Key;
            public Func<double, double, bool> Inside;
            public ShapeVariation Morph;   // Hollow / Thick / Thin act on the grid; the others are folded into Inside / SizeMul
            public double SizeMul = 1;     // Scale variation: the shape is drawn smaller in the grid
        }

        static readonly Dictionary<string, Source> sources = new Dictionary<string, Source>();

        /// <summary>Resolves (and caches) a shape key. Throws ArgumentException for an unknown key.</summary>
        public static Source Resolve(string key)
        {
            lock (sources)
            {
                Source s;
                if (sources.TryGetValue(key, out s)) return s;
                if (sources.Count > 512) sources.Clear(); // endless levels: procedural keys never come back
                s = Make(key);
                sources[key] = s;
                return s;
            }
        }

        static Source Make(string key)
        {
            var src = new Source { Key = key };
            int t = key.IndexOf('~');
            if (t >= 0)
            {
                var inner = Make(key.Substring(0, t)).Inside;
                var v = VariationOf(key);
                switch (v)
                {
                    case ShapeVariation.Mirror: src.Inside = (x, y) => inner(-x, y); break;
                    case ShapeVariation.Flip: src.Inside = (x, y) => inner(x, -y); break;
                    case ShapeVariation.Rot90: src.Inside = (x, y) => inner(y, -x); break;
                    case ShapeVariation.Scale: src.Inside = inner; src.SizeMul = ScaleVariation; break;
                    default: src.Inside = inner; src.Morph = v; break;
                }
                return src;
            }
            int dash = key.IndexOf('-');
            if (dash > 0)
            {
                string kind = key.Substring(0, dash);
                uint seed;
                if (!uint.TryParse(key.Substring(dash + 1), out seed)) throw new ArgumentException("Bad procedural key " + key);
                src.Inside = Procedural(kind, seed);
                return src;
            }
            if (IsBitmap(key))
            {
                var b = bitmaps[key];
                src.Inside = (x, y) => b.Inside(x, y);
                return src;
            }
            if (LongShapes.IsVector(key))
            {
                src.Inside = (x, y) => LongShapes.Inside(key, x, y);
                return src;
            }
            throw new ArgumentException("Unknown shape " + key);
        }

        /// <summary>Size of a Scale variation, relative to the level's normal size.</summary>
        public const double ScaleVariation = 0.78;

        // ------------------------------------------------------------------ symmetry (Mirror vs Flip)

        static readonly Dictionary<string, bool> symmetric = new Dictionary<string, bool>();

        /// <summary>
        /// The base shape looks the same mirrored left-right (sampled on a 64 x 64 grid, at most 1 % of the cells
        /// differ). A Mirror variation of such a shape would change nothing, so Campaign uses Flip (upside down) instead.
        /// </summary>
        public static bool IsSymmetric(string baseName)
        {
            lock (symmetric)
            {
                bool s;
                if (symmetric.TryGetValue(baseName, out s)) return s;
                var f = Resolve(baseName).Inside;
                int diff = 0, inside = 0;
                const int n = 64;
                for (int j = 0; j < n; j++)
                {
                    double y = -1.1 + 2.2 * (j + 0.5) / n;
                    for (int i = 0; i < n; i++)
                    {
                        double x = -1.1 + 2.2 * (i + 0.5) / n;
                        bool a = f(x, y);
                        if (a) inside++;
                        if (a != f(-x, y)) diff++;
                    }
                }
                s = diff <= Math.Max(2, inside / 100);
                symmetric[baseName] = s;
                return s;
            }
        }

        // ------------------------------------------------------------------ procedural shapes

        /// <summary>Deterministic 0..1 values from a seed (own integer hash: identical on every platform).</summary>
        sealed class Rand
        {
            uint s;
            public Rand(uint seed) { s = seed ^ 0x9E3779B9u; }
            public double Next()
            {
                s = Mix(s + 0x6D2B79F5u);
                return (s & 0xFFFFFF) / (double)0x1000000;
            }
            public double Range(double a, double b) { return a + (b - a) * Next(); }
            public int Int(int a, int bExclusive) { return a + (int)(Next() * (bExclusive - a)); }
        }

        public static uint Mix(uint h)
        {
            unchecked
            {
                h ^= h >> 16; h *= 0x7FEB352Du;
                h ^= h >> 15; h *= 0x846CA68Bu;
                h ^= h >> 16;
                return h;
            }
        }

        static Func<double, double, bool> Procedural(string kind, uint seed)
        {
            var r = new Rand(seed);
            switch (kind)
            {
                case ProcPetals: return Petals(r);
                case ProcBlob: return Blob(r);
                case ProcMandala: return Mandala(r);
            }
            throw new ArgumentException("Unknown procedural kind " + kind);
        }

        /// <summary>
        /// Superformula r(t) = (|cos(m t / 4)|^n2 + |sin(m t / 4)|^n3)^(-1 / n1), m = 3..8 lobes, normalized to a max of 1
        /// over a thick core (0.38 of the radius), so the petals / star points stay at least a few cells wide.
        /// </summary>
        static Func<double, double, bool> Petals(Rand r)
        {
            int m = r.Int(3, 9);
            double n1 = r.Range(0.6, 2.4), n2 = r.Range(0.8, 2.6), n3 = n2 * r.Range(0.85, 1.15);
            double rot = r.Next() < 0.5 ? 0 : Math.PI / m; // a lobe or a gap at the top
            Func<double, double> raw = t =>
            {
                double a = Math.Pow(Math.Abs(Math.Cos(m * t / 4)), n2) + Math.Pow(Math.Abs(Math.Sin(m * t / 4)), n3);
                return a <= 1e-9 ? 10 : Math.Pow(a, -1 / n1);
            };
            double max = 0, min = double.MaxValue;
            for (int i = 0; i < 720; i++)
            {
                double v = Math.Min(10, raw(i * Math.PI / 360));
                max = Math.Max(max, v);
                min = Math.Min(min, v);
            }
            const double core = 0.38;
            return (x, y) =>
            {
                double rho = Math.Sqrt(x * x + y * y);
                if (rho > 1) return false;
                double t = Math.Atan2(x, -y) + rot; // 0 = up, mirror symmetric
                double v = (Math.Min(10, raw(t)) - min) / Math.Max(1e-9, max - min);
                return rho <= core + (1 - core) * v;
            };
        }

        /// <summary>A smooth blob: radius 1 + sum of k-fold cosine bumps (2..6-fold symmetry), mirror symmetric, normalized to 1.</summary>
        static Func<double, double, bool> Blob(Rand r)
        {
            int fold = r.Int(2, 7);
            var amp = new double[3];
            var ph = new double[3];
            for (int k = 0; k < 3; k++)
            {
                amp[k] = r.Range(0.12, 0.3) / (k + 1);
                ph[k] = r.Next() < 0.5 ? 0 : Math.PI;
            }
            Func<double, double> rad = t =>
            {
                double v = 1;
                for (int k = 0; k < 3; k++) v += amp[k] * Math.Cos((k + 1) * fold * t + ph[k]);
                return v;
            };
            double max = 0;
            for (int i = 0; i < 720; i++) max = Math.Max(max, rad(i * Math.PI / 360));
            return (x, y) =>
            {
                double rho = Math.Sqrt(x * x + y * y);
                return rho <= 1 && rho <= rad(Math.Atan2(x, -y)) / max;
            };
        }

        /// <summary>
        /// Mandala: core disk, n spokes out to a ring, the ring, and n rounded petals outside it (offset by half a step or
        /// on the spokes). Every part at least ~0.2 wide, so 3+ cells on a phone-size board.
        /// </summary>
        static Func<double, double, bool> Mandala(Rand r)
        {
            int n = r.Int(5, 11);
            double core = r.Range(0.16, 0.24), ringIn = r.Range(0.42, 0.5), ringOut = ringIn + r.Range(0.12, 0.16);
            double spokeHalf = r.Range(0.1, 0.13);
            double petalOff = r.Next() < 0.5 ? 0 : Math.PI / n;
            double petalSharp = r.Range(0.6, 1.4);
            bool holes = r.Next() < 0.5;
            double step = 2 * Math.PI / n;
            return (x, y) =>
            {
                double rho = Math.Sqrt(x * x + y * y);
                if (rho > 1) return false;
                if (rho <= core) return true;
                double t = Math.Atan2(x, -y);
                if (rho >= ringIn && rho <= ringOut)
                {
                    if (!holes) return true;
                    double a0 = Wrap(t - petalOff, step);
                    return Math.Abs(a0) * rho > 0.06; // small notches in the ring, between the petals
                }
                if (rho < ringIn)
                {
                    double a = Wrap(t, step);
                    return Math.Abs(Math.Sin(a)) * rho <= spokeHalf; // distance to the nearest spoke
                }
                double p = Wrap(t - petalOff, step) / (step / 2); // -1..1 across a petal
                double reach = ringOut + (1 - ringOut) * Math.Pow(Math.Max(0, Math.Cos(p * Math.PI / 2)), petalSharp);
                return rho <= reach;
            };
        }

        /// <summary>Angle t wrapped to [-step / 2, step / 2).</summary>
        static double Wrap(double t, double step)
        {
            double a = t % step;
            if (a < 0) a += step;
            return a >= step / 2 ? a - step : a;
        }

        // ------------------------------------------------------------------ ASCII bitmaps

        /// <summary>A bitmap, sampled with bilinear smoothing (threshold 0.5) so it scales to any grid without jaggies.</summary>
        sealed class Bitmap
        {
            readonly int w, h;
            readonly bool[] px;
            readonly double s; // bitmap px per normalized unit / 2

            public Bitmap(string name, string[] rows)
            {
                h = rows.Length;
                w = rows[0].Length;
                px = new bool[w * h];
                for (int r = 0; r < h; r++)
                {
                    if (rows[r].Length != w) throw new FormatException("ShapeLibrary " + name + ": row " + r + " has " + rows[r].Length + " columns, expected " + w);
                    for (int c = 0; c < w; c++) px[r * w + c] = rows[r][c] == '#';
                }
                s = Math.Max(w, h) / 2.0; // the longer side spans -1..1
            }

            double At(int c, int r) { return c < 0 || r < 0 || c >= w || r >= h ? 0 : px[r * w + c] ? 1 : 0; }

            public bool Inside(double x, double y)
            {
                double u = x * s + w / 2.0 - 0.5, v = y * s + h / 2.0 - 0.5; // pixel centres at whole numbers
                int c = (int)Math.Floor(u), r = (int)Math.Floor(v);
                double fu = u - c, fv = v - r;
                double a = At(c, r) * (1 - fu) + At(c + 1, r) * fu;
                double b = At(c, r + 1) * (1 - fu) + At(c + 1, r + 1) * fu;
                return a * (1 - fv) + b * fv >= 0.5;
            }
        }

        static readonly Dictionary<string, Bitmap> bitmaps;

        static Dictionary<string, Bitmap> BuildBitmaps()
        {
            var d = new Dictionary<string, Bitmap>();
            foreach (var kv in BitmapTable)
            {
                if (d.ContainsKey(kv.Key) || Array.IndexOf(VectorShapes, kv.Key) >= 0) throw new FormatException("ShapeLibrary: duplicate shape " + kv.Key);
                d[kv.Key] = new Bitmap(kv.Key, kv.Value);
            }
            return d;
        }

        /// <summary>Rows as written (# = shape cell, . = empty), top row first.</summary>
        static string[] Rows(params string[] rows) { return rows; }

        /// <summary>
        /// Mirror-symmetric bitmap from its left half including the centre column: "..###" -> "..#####..".
        /// </summary>
        static string[] Sym(params string[] half)
        {
            var full = new string[half.Length];
            for (int i = 0; i < half.Length; i++)
            {
                var c = half[i].ToCharArray();
                var right = new char[c.Length - 1];
                for (int k = 0; k < right.Length; k++) right[k] = c[c.Length - 2 - k];
                full[i] = half[i] + new string(right);
            }
            return full;
        }

        /// <summary>
        /// The 63 bitmap shapes (21-31 columns): chunky silhouettes, strokes at least 3 cells, one connected region,
        /// 25-70 % of their bounding box. Drawn from primitives and checked offline, then pasted here as ASCII.
        /// </summary>
        static readonly List<KeyValuePair<string, string[]>> BitmapTable;

        /// <summary>Ordered name -> rows list with collection-initializer syntax.</summary>
        sealed class Table : List<KeyValuePair<string, string[]>>
        {
            public void Add(string name, string[] rows) { Add(new KeyValuePair<string, string[]>(name, rows)); }
        }

        static List<KeyValuePair<string, string[]>> MakeTable()
        {
            return new Table
            {
            { "circle", Sym(
                "........#####",
                "......#######",
                ".....########",
                "....#########",
                "...##########",
                "..###########",
                "..###########",
                ".#########...",
                ".########....",
                ".#######.....",
                "########.....",
                "#######......",
                "#######......",
                "########.....",
                ".#######.....",
                ".########....",
                ".#########...",
                "..###########",
                "..###########",
                "...##########",
                "....#########",
                ".....########",
                "......#######",
                "........#####") },
            { "lightning", Rows(
                "..............#####...",
                ".............#####....",
                "............######....",
                "...........######.....",
                "...........######.....",
                "..........######......",
                ".........#######......",
                "........#######.......",
                "........#######.......",
                ".......#######........",
                "......########........",
                "......#######.........",
                ".....########.........",
                "....########..........",
                "...###################",
                "...##################.",
                "..##################..",
                ".##################...",
                "##################....",
                "##################....",
                ".........########.....",
                ".........#######......",
                "........#######.......",
                "........######........",
                "........######........",
                "........#####.........",
                ".......#####..........",
                ".......####...........",
                ".......###............",
                "......####............",
                "......###.............",
                "......##..............",
                ".....##...............",
                ".....#................") },
            { "drop", Sym(
                "..........#",
                "..........#",
                "..........#",
                ".........##",
                ".........##",
                "........###",
                "........###",
                ".......####",
                ".......####",
                "......#####",
                "......#####",
                ".....######",
                ".....######",
                "....#######",
                "....#######",
                "...########",
                "...########",
                "..#########",
                "..#########",
                ".##########",
                ".##########",
                "###########",
                "###########",
                "###########",
                "###########",
                "###########",
                ".##########",
                ".##########",
                "..#########",
                "..#########",
                "...########",
                "....#######",
                "......#####") },
            { "clover", Rows(
                "..........#######..........",
                ".........#########.........",
                ".........#########.........",
                "........###########........",
                "........###########........",
                "........###########........",
                "........###########........",
                "...#####################...",
                "..#######################..",
                ".#########################.",
                ".#########################.",
                "###########################",
                "###########################",
                "###########################",
                ".#########################.",
                ".#########..##############.",
                "..########..####.########..",
                "...#####....####...#####...",
                "............####...........",
                "............####...........",
                ".............###...........",
                ".............####..........",
                ".............####..........",
                "..............####.........",
                "..............####.........",
                "..............####.........",
                "...............####........",
                "...............####........",
                "................###........") },
            { "bell", Sym(
                "...........##",
                "...........##",
                ".........####",
                "........#####",
                ".......######",
                "......#######",
                "......#######",
                "......#######",
                ".....########",
                ".....########",
                ".....########",
                ".....########",
                "....#########",
                "....#########",
                "...##########",
                "...##########",
                "...##########",
                "..###########",
                "..###########",
                ".############",
                "#############",
                "#############",
                "#############",
                "..........###",
                "..........###",
                "..........###",
                "............#") },
            { "anchor", Sym(
                "...........###",
                "...........###",
                "...........###",
                "............##",
                "............##",
                ".......#######",
                ".......#######",
                ".......#######",
                ".......#######",
                "............##",
                "............##",
                "............##",
                "..##........##",
                "..##........##",
                ".####.......##",
                "#####.......##",
                "..###.......##",
                "..####......##",
                "..####......##",
                "..####......##",
                "...####.....##",
                "...#####....##",
                "....#####...##",
                ".....#########",
                "......########",
                ".......#######",
                ".........#####") },
            { "balloon", Rows(
                "..........#####..........",
                ".......###########.......",
                "......#############......",
                "....#################....",
                "...##################....",
                "...###################...",
                "..#####################..",
                "..#####################..",
                ".#######################.",
                ".#######################.",
                ".#######################.",
                "#########################",
                "#########################",
                "#########################",
                "#########################",
                "#########################",
                "########################.",
                ".#######################.",
                ".#######################.",
                ".######################..",
                "..#####################..",
                "...###################...",
                "...###################...",
                "....#################....",
                ".....##############......",
                ".......###########.......",
                "..........#####..........",
                "...........###...........",
                "..........#####..........",
                "..........#####..........",
                "...........#####.........",
                "...........######........",
                "............#####........",
                "...........#####.........",
                "..........######.........",
                "..........#####..........",
                ".........#####...........",
                "..........####...........",
                "..........###............") },
            { "cup", Rows(
                "........###...##...........",
                ".......####..###...........",
                ".......###..####...........",
                "......###...###............",
                "......###...###............",
                ".......###..####...........",
                ".......###...###...........",
                "....#################......",
                "....##################.....",
                "....#####################..",
                "....######################.",
                "....######################.",
                "....################...####",
                "....################....###",
                ".....###############....###",
                ".....###############...####",
                ".....#####################.",
                ".....####################..",
                ".....###################...",
                ".....##############........",
                ".....##############........",
                ".....##############........",
                "......#############........",
                "#########################..",
                "#########################..",
                "#########################..",
                "#########################..") },
            { "gift", Rows(
                ".......##.........##.......",
                "....#######....########....",
                "...##########.##########...",
                "...##########.##########...",
                "..####....#######....####..",
                "..###.....######......###..",
                "..###......#####......###..",
                "..###......#####......###..",
                "..####....#######....####..",
                "..######################...",
                "...#####################...",
                "###########################",
                "###########################",
                "###########################",
                "###########################",
                "###########################",
                "..........#######..........",
                "...........#####...........",
                "..#######..#####..#######..",
                "..#######..#####..#######..",
                "..#######..#####..#######..",
                "..#######..#####..#######..",
                "..#######..#####..#######..",
                "..#######..#####..#######..",
                "..#######..#####..#######..",
                "..#######..#####..#######..",
                "..#######..#####..#######..",
                "..#######..#####..#######..",
                "..#######..#####..#######..",
                "..#######..#####..#######..",
                "..#######..#####..#######..",
                "..#######..#####..#######..",
                "..#######################..") },
            { "kite", Rows(
                "............#............",
                "............#............",
                "...........###...........",
                "..........#####..........",
                ".........#######.........",
                "........####.####........",
                ".......#####.#####.......",
                ".......#####.#####.......",
                "......######.######......",
                ".....#######.#######.....",
                "....########.########....",
                "...#########.#########...",
                "...#########.#########...",
                "..##########.##########..",
                ".###########.###########.",
                "####.................####",
                ".###########.###########.",
                "..##########.##########..",
                "...#########.#########...",
                "....########.########....",
                "....########.########....",
                ".....#######.#######.....",
                "......######.######......",
                ".......#####.#####.......",
                "........####.####........",
                ".........###.###.........",
                ".........###.###.........",
                "..........#####..........",
                "..........#####..........",
                "..........######.........",
                "..........#######........",
                "......#....#######.......",
                "......###...######.......",
                "......#####.######.......",
                "......############.......",
                "......##.########.##.....",
                "........############.....",
                "........############.....",
                ".........###########.....",
                "..........#######.##.....",
                "...........######........",
                "............#####........",
                ".............####........",
                "..............#..........") },
            { "diya", Rows(
                ".............#...............",
                ".............##..............",
                ".............##..............",
                "............###..............",
                "............####.............",
                "...........#####.............",
                "...........######............",
                "..........#######............",
                "..........#######............",
                "..........#######............",
                "...........######............",
                "...........#####.............",
                "............####.............",
                "............####.............",
                "............####.............",
                "............####...........##",
                "############################.",
                "############################.",
                "############################.",
                ".##########################..",
                ".#########################...",
                "..#######################....",
                "....####################.....",
                "......###############........",
                "..........#######............",
                "..........#######............") },
            { "car", Rows(
                "...........###########.........",
                "..........##...##....#.........",
                ".........###...##....##........",
                ".........##....##.....##.......",
                "........##.....##.....##.......",
                "........##.....##......##......",
                ".##############################",
                ".##############################",
                "###############################",
                "###############################",
                "###############################",
                "###############################",
                "###############################",
                ".##############################",
                "....########........########...",
                "....###..###........###...###..",
                "....###..###........###...###..",
                "....########........########...",
                "....########........########...",
                ".....######..........######....") },
            { "plane", Rows(
                "...............#...............",
                "..............###..............",
                "..............###..............",
                "..............###..............",
                "..............####.............",
                ".............#####.............",
                ".............#####.............",
                ".............#####.............",
                ".............#####.............",
                ".............#####.............",
                ".............#####.............",
                "...........#########...........",
                ".........#############.........",
                ".......#################.......",
                ".....#####################.....",
                "...#########################...",
                ".##########..#####..##########.",
                "########.....#####.....########",
                "####.........#####.........####",
                "#............#####............#",
                ".............#####.............",
                ".............#####.............",
                ".............#####.............",
                ".............#####.............",
                ".............#####.............",
                ".............#####.............",
                "............#######............",
                "..........###########..........",
                ".........#############.........",
                ".........#############.........") },
            { "bicycle", Rows(
                "....................######.....",
                "...........#.......#######.....",
                ".........#####.....#######.....",
                ".........###############.......",
                "..........#############........",
                "..........##############.......",
                "......##.#####....#######......",
                "...###########....##########...",
                "..#############..############..",
                ".##############.##############.",
                ".####..##################.####.",
                "####..###################..####",
                "####.############.###.####.####",
                "####.###########..###..###.####",
                "####..#########...####..#..####",
                ".####....####.....####....####.",
                ".###########.......###########.",
                "..##########.......##########..",
                "...########.........########...",
                ".....###...............###.....") },
            { "trophy", Sym(
                ".....########",
                "...##########",
                "..###########",
                ".############",
                "###..########",
                "###...#######",
                "###...#######",
                ".############",
                ".############",
                "...##########",
                ".......######",
                "........#####",
                ".........####",
                "...........##",
                "...........##",
                "...........##",
                "...........##",
                "...........##",
                "...........##",
                "...........##",
                ".......######",
                ".......######",
                ".......######",
                ".....########",
                ".....########",
                ".....########") },
            { "pineapple", Sym(
                "...........#",
                "...........#",
                "...........#",
                "..#.......##",
                "..##......##",
                "..###.....##",
                "..###.....##",
                "..####...###",
                "...####..###",
                "...#####.###",
                "...#########",
                "...#########",
                "...#########",
                "...#...#####",
                "......######",
                "....########",
                "...#########",
                "...#########",
                "..##########",
                ".###########",
                ".####...####",
                ".####...####",
                "######.#####",
                "############",
                "############",
                "###########.",
                "##########..",
                "##########..",
                "############",
                ".###########",
                ".###########",
                ".####...####",
                "..###...####",
                "...#########",
                "...#########",
                "....########",
                "......######",
                ".......#####") },
            { "carrot", Rows(
                "..........#..........",
                ".####...#####...####.",
                "######..#####..#####.",
                "######..#####..######",
                "#######.#####.#######",
                ".######.############.",
                ".###################.",
                "..#################..",
                "...###############...",
                "...###############...",
                "....#############....",
                ".....###########.....",
                ".....###########.....",
                "......#########......",
                "......#########......",
                "...###############...",
                "..#################..",
                "..#################..",
                "..#################..",
                "..#################..",
                "..#################..",
                "...###############...",
                "...###############...",
                "...###############...",
                "...###############...",
                "....#############....",
                "....#############....",
                "....#############....",
                "....#############....",
                "....############.....",
                ".....###########.....",
                ".....###########.....",
                ".....###########.....",
                ".....##########......",
                "......#########......",
                "......#########......",
                "......#########......",
                "......#########......",
                ".......#######.......",
                ".......#######.......",
                ".......#######.......",
                ".......#######.......",
                "........#####........",
                "........#####........",
                "........#####........",
                "........#####........",
                ".........###.........",
                ".........###.........",
                ".........###.........",
                ".........###.........",
                "..........#..........") },
            { "dog", Sym(
                ".........#####",
                "........######",
                ".......#######",
                "...###########",
                "...###########",
                "..#######..###",
                ".########..###",
                ".########..###",
                ".#############",
                "##############",
                "##############",
                "############..",
                "############..",
                "######.#######",
                ".####...######",
                ".###.....#####",
                "..........####",
                ".............#") },
            { "elephant", Rows(
                ".....########..................",
                "....#####......................",
                "...###...####...####...........",
                "...###..######.########........",
                "..#######################......",
                "...#######################.....",
                "..#########################....",
                "..##########################...",
                "..###########################..",
                ".############################..",
                ".####...###################.##.",
                ".####....##################.###",
                "#####....#################...##",
                "####......#################....",
                "#####.....#################....",
                ".####.....#################....",
                ".####.....####.############....",
                "..####....####.############....",
                "..####....####.############....",
                "..####....####.############....") },
            { "penguin", Sym(
                "..........##",
                "........####",
                ".......#####",
                "......##..##",
                "......##..##",
                ".....#######",
                ".....#####..",
                "....#######.",
                "....########",
                "...#########",
                "..##########",
                ".###########",
                ".###########",
                ".###########",
                "############",
                "############",
                "############",
                "####.#######",
                ".##..#######",
                "......######",
                "......######",
                ".......#####",
                "......######",
                ".....#######",
                ".....######.",
                ".......##...") },
            { "rooster", Rows(
                ".....#####...................",
                "....######...................",
                "...#######...................",
                "...######............###.....",
                "....###.##..........####.....",
                "..####..##.........#####.....",
                "##########.........#####.....",
                "...######.........######.###.",
                "....#####........###########.",
                "...######..#####.###########.",
                "...########################..",
                "....#######################..",
                "......####################...",
                "......######################.",
                "......#######################",
                "......######################.",
                "......#####################..",
                ".......###################...",
                "........###########..........",
                ".........#########...........",
                "..........#######............",
                "..........###.###............",
                "..........###.###............",
                "..........###.###............",
                "........#########............",
                "........#########............") },
            { "giraffe", Rows(
                "....###..................",
                "....###..................",
                ".######..................",
                "######...................",
                "###.###..................",
                ".#######.................",
                "...#####.................",
                "...#####.................",
                "....#####................",
                "....#####................",
                "....#####................",
                ".....#####...............",
                ".....#####...............",
                ".....#####...............",
                "......#####..............",
                "......#####..............",
                "......############.......",
                ".......#############.....",
                ".......####...#########..",
                ".......####...##..#####..",
                ".......#########...#####.",
                ".......#################.",
                ".......##################",
                ".......####.#############",
                ".......####.##########...",
                ".......####.##########...",
                ".......####.##########...",
                ".......####.##########...",
                ".......####.##########...",
                ".......####.##########...",
                ".......####.##########...") },
            { "crab", Sym(
                "...####.........",
                "..#####.........",
                "..####..##......",
                "..########......",
                "..########......",
                "..########......",
                "...######..###..",
                "....#####..###..",
                ".....####..###..",
                "......####.#####",
                ".......#########",
                ".......#########",
                ".......#########",
                "..##############",
                "..##############",
                ".###############",
                ".###...#########",
                "###..###########",
                "###.############",
                "...#####....####",
                "...###..........",
                "..###...........",
                "..###...........",
                "..###...........") },
            { "dolphin", Rows(
                "..................#...........",
                "..................#...........",
                ".................###.........#",
                ".................###.........#",
                "..............############..##",
                "...........###################",
                ".........#####################",
                "........#####################.",
                ".....##.######################",
                "..#######################.####",
                ".######################.....##",
                "#####################........#",
                ".###...##########.............",
                "..............##..............",
                "...............#..............") },
            { "shark", Rows(
                "...............#.............",
                "...............##............",
                "...............##............",
                "..............###............",
                "..............####...........",
                ".............#####..........#",
                ".............#####.........##",
                ".........##########........##",
                ".....##################...###",
                "..###.###################.##.",
                ".###..######################.",
                "############################.",
                ".############################",
                "....####################..###",
                "........############.......##",
                "...........####............##",
                "............###.............#",
                ".............#...............",
                ".............#...............") },
            { "jellyfish", Rows(
                "..........####...........",
                ".......###########.......",
                ".....###############.....",
                "...###################...",
                "..#####################..",
                ".######################..",
                ".######.########.#######.",
                ".######..#######..######.",
                "#######..#######..#######",
                "#########################",
                "#########################",
                "#########################",
                ".#######################.",
                ".#######################.",
                ".#####.#####.#####.#####.",
                "..###...###...###...###..",
                "..###...###...###...###..",
                "..###...###...###...###..",
                ".###....###...###....###.",
                ".###...###....####...###.",
                ".###...###.....###...###.",
                ".####..####...####..####.",
                "..###...###...###..####..",
                "...###..###..####..###...",
                "...###...###.###..####...",
                "...###...###.###...###...",
                "...###....##.##....###...",
                "..###.....##.##....####..",
                "..###...............###..",
                "..###...............###..",
                "...#.................#...") },
            { "bee", Rows(
                ".........####...#####........",
                "...#....######.#######.......",
                "..###..#######.#######.......",
                ".###...###############.......",
                "###....###############.......",
                "###....###############.......",
                ".##.....##############.......",
                ".###....#############........",
                "..##.....###########.........",
                "..#####..#############.......",
                "..#####################......",
                ".##.#######..####.######.....",
                ".##..######..####.######.....",
                ".##########..####.########...",
                ".##########..####.###########",
                "..#########..####.########...",
                "...########..####.######.....",
                ".......####..####.#####......",
                "........###..####.####.......",
                "..........##########.........") },
            { "dinosaur", Rows(
                ".#..###........................",
                "#######........................",
                ".#######.......................",
                "...#####.......................",
                "....#####......................",
                "....#####......................",
                ".....#####.....................",
                ".....#####.....................",
                "......#####..#...#.............",
                "......#####..##.###............",
                ".......############............",
                ".......#############...........",
                "........##############.........",
                "........###############.....###",
                "........#################..####",
                ".......#######################.",
                "........#####################..",
                "........####################...",
                ".........##############.####...",
                ".........##############........",
                ".........###.###.##.###........",
                ".........###.###.##.###........",
                ".........###.###.##.###........",
                ".........###.###.##.###........",
                ".........###.###.##............") },
            { "snake", Rows(
                "..........###########....",
                "........##########..##...",
                ".....####################",
                "....##################...",
                "....########.....####....",
                "....#####................",
                "....####.................",
                "....####.................",
                "...######................",
                "...########..............",
                "....###########..........",
                ".....#############.......",
                ".......############......",
                "..........#########......",
                "..............######.....",
                "................####.....",
                "................####.....",
                "...............#####.....",
                "..............######.....",
                ".....###############.....",
                "....###############......",
                "..###############........",
                ".##############..........",
                "#######..................",
                "#####....................",
                ".###.....................") },
            { "tajmahal", Sym(
                "...............#",
                "..............##",
                "..............##",
                "..............##",
                "..............##",
                "............####",
                "...........#####",
                ".###......######",
                ".####.....######",
                "#####....#######",
                ".####....#######",
                ".####....#######",
                ".####.....######",
                ".####..#########",
                ".####.##########",
                ".####.##########",
                ".####.##########",
                ".####.##########",
                ".####.#########.",
                ".####.#######...",
                ".####.##...##...",
                ".####.##...##...",
                ".####.##..###...",
                ".####.#######...",
                ".####.#######...",
                ".####.#######...",
                ".####.#######...",
                ".####.#######...",
                ".####.##########",
                "################",
                "################",
                "################") },
            { "lotus", Sym(
                "...............#",
                "..............##",
                ".............###",
                ".............###",
                "............####",
                "............####",
                ".....###...#####",
                ".....#####.#####",
                ".....###########",
                ".....###########",
                ".....###########",
                ".....###########",
                ".....###########",
                ".....###########",
                "...#############",
                "################",
                ".###############",
                "..##############",
                "..##############",
                "...#############",
                "....############",
                "......##########",
                "......##########",
                ".......#########",
                "....############",
                "....############") },
            { "peacock", Rows(
                "...#..........................",
                "..###.........................",
                ".#####........................",
                "######........................",
                "...###........................",
                "...###........................",
                "...###........................",
                "...###........................",
                "...###........................",
                "...###.......######...........",
                "...#######.###########........",
                "...#####################......",
                "..#######################.....",
                "..############...#########....",
                "..############...###..#####...",
                "...############..###...##..#..",
                "....################...##..##.",
                ".....####################..##.",
                ".....#########################",
                ".....#########################",
                ".....####.#####...###...######",
                "..........#####...###...######",
                "...........#####.#####..#####.",
                ".............################.",
                "..............##############..",
                "................###########...",
                "....................####......") },
            { "temple", Rows(
                "............#............",
                "............###..........",
                "............###..........",
                "...........###...........",
                ".........#######.........",
                ".........#######.........",
                ".........#######.........",
                ".........#######.........",
                "........#########........",
                "........#########........",
                "........#########........",
                ".......###########.......",
                ".......###########.......",
                ".......###########.......",
                "......#############......",
                ".....##...........##.....",
                ".....###############.....",
                ".....###############.....",
                ".....###############.....",
                ".....######...######.....",
                ".....#####.....#####.....",
                "..########.....########..",
                "..########.....########..",
                "..########.....########..",
                "..########.....########..",
                "..########.....########..",
                "##########.....##########",
                "#########################") },
            { "snowflake", Sym(
                "..............#",
                ".............##",
                ".............##",
                ".............##",
                "..........#####",
                "..........#####",
                "......#....####",
                ".....###....###",
                "####.###.....##",
                "########.....##",
                ".#######.....##",
                "..#######....##",
                "..#########..##",
                ".##############",
                "..####..#######",
                ".........######",
                "...........####",
                ".........######",
                "..####..#######",
                ".##############",
                "..#########..##",
                "..#######....##",
                ".#######.....##",
                "########.....##",
                "####.###.....##",
                ".....###....###",
                "......#....####",
                "..........#####",
                "..........#####",
                ".............##",
                ".............##",
                ".............##",
                "..............#") },
            { "shield", Sym(
                "........#####",
                "......#######",
                "#############",
                "#########..##",
                "####.......##",
                "####.......##",
                "####.......##",
                "####.......##",
                "####.......##",
                "#############",
                "#############",
                "#############",
                "#############",
                "####.......##",
                "####.......##",
                "####.......##",
                "####.......##",
                "####.......##",
                ".###.......##",
                ".###.......##",
                ".####......##",
                ".####......##",
                "..###......##",
                "..####.....##",
                "...####....##",
                "...#####...##",
                "....#####..##",
                ".....########",
                "......#######",
                "........#####",
                "...........##") },
            { "windmill", Sym(
                "..##..........",
                ".####.........",
                "######........",
                "#######.......",
                ".#######......",
                ".########.....",
                "..########....",
                "...########...",
                "....########..",
                ".....########.",
                "......########",
                ".......#######",
                "........######",
                ".........#####",
                ".........#####",
                "........######",
                ".......#######",
                "......########",
                ".....#########",
                "....##########",
                "...###########",
                "..############",
                ".########.####",
                "########..####",
                "#######...####",
                ".#####....####",
                "..###....#####",
                ".........#####",
                ".........#####",
                ".........#####",
                ".........#####",
                ".........#####",
                ".........####.",
                ".........###..",
                "........####..",
                "........####..",
                "........####..",
                "........####..",
                "........####..") },
            { "lighthouse", Rows(
                "..............#..............",
                "#............####...........#",
                "####.......#######.......####",
                "#######.....#####.....#######",
                "###########.#####.###########",
                "#############################",
                "#########...#####...#########",
                "######....#########....######",
                "###.......#########.......###",
                "............######...........",
                "...........#######...........",
                "...........#######...........",
                "...........#######...........",
                "...........##...##...........",
                "...........##...##...........",
                "...........#######...........",
                "...........#######...........",
                "...........#######...........",
                "...........########..........",
                "..........##.....##..........",
                "..........#########..........",
                "..........####.####..........",
                "..........###...###..........",
                "..........####.####..........",
                ".......###############.......",
                ".....###################.....",
                "....#####################....",
                "......#################......") },
            { "castle", Rows(
                "..##.....................##...",
                ".###.....................###..",
                ".####...................####..",
                "#####...................#####.",
                "######.................######.",
                "######.................#######",
                "######.................#######",
                "######.................#######",
                "######.................#######",
                "######.................#######",
                "######.................#######",
                "#######...###...###...########",
                "#######...###...###...########",
                "##..#####################..###",
                "##..#####################..###",
                "##..#####################..###",
                "##############################",
                "##############################",
                "##############.###############",
                "############.....#############",
                "###########.......############",
                "###########.......############",
                "###########.......############",
                "###########.......############",
                "###########.......############",
                "###########.......############",
                "###########.......############",
                "###########.......############") },
            { "tent", Rows(
                "...............#...............",
                "...............####............",
                "...............###.............",
                "...............#...............",
                "...............#...............",
                "...............#...............",
                "..............###..............",
                "..............###..............",
                ".............#####.............",
                "............#######............",
                "............#######............",
                "...........#########...........",
                "..........###########..........",
                "..........###########..........",
                ".........#############.........",
                "........###############........",
                "........###############........",
                ".......#################.......",
                "......#########.#########......",
                "......#########.#########......",
                ".....##########.##########.....",
                "....##########...##########....",
                "....##########...##########....",
                "...##########.....##########...",
                "...##########.....##########...",
                "..###########.....###########..",
                ".###########.......###########.",
                "###############################",
                "###############################") },
            { "train", Rows(
                "............####............",
                "........#########...........",
                ".......#########............",
                ".......######...............",
                "....########................",
                "...########......###########",
                "....#####........###########",
                ".....####.........#########.",
                ".....####.........#########.",
                ".....####.........#########.",
                ".....####.........##.....##.",
                ".....####.........##.....##.",
                "....################.....##.",
                "....################.....##.",
                "....#######################.",
                "....#######################.",
                "....#######################.",
                "....#######################.",
                "....#######################.",
                "....#######################.",
                ".##########################.",
                "##########################..",
                ".#########################..",
                "....######.#####...######...",
                "....############...######...",
                "....#####..#####...######...",
                ".....####..#####....####....") },
            { "bus", Rows(
                "..###########################..",
                ".#############################.",
                "###############################",
                "###.....###....###.....##...###",
                "###.....###....###.....##...###",
                "###.....###....###.....##...###",
                "###.....###....###.....##...###",
                "###.....###....###.....##...###",
                "#########################...###",
                "#########################...###",
                "##########################..###",
                "###############################",
                "###############################",
                "...########.........########...",
                "....#######.........#######....",
                "....######...........######....",
                ".....#####...........#####.....") },
            { "helicopter", Rows(
                ".#######################.......",
                "########################.......",
                ".#######################.......",
                "...........##..................",
                "...........##..................",
                "........#######.............##.",
                "......###########..........###.",
                ".....##...########..###########",
                "....##....#####################",
                "....#.....####################.",
                "....#################.......##.",
                "....###############............",
                "....###############............",
                ".....#############.............",
                ".....#############.............",
                ".......#########...............",
                "........######.#...............",
                "........#......#...............",
                "......##############...........",
                ".....################..........",
                ".....################..........") },
            { "hotairballoon", Rows(
                ".......###########.......",
                ".....##############......",
                "....#################....",
                "...###################...",
                "..#####.#########.#####..",
                "..#####.#########.#####..",
                ".######.#########.######.",
                ".######.#########.######.",
                "#######.#########.######.",
                "#######.#########.#######",
                "#######.#########.#######",
                "#######.#########.#######",
                "#######.#########.#######",
                ".######.#########.######.",
                ".######.#########.######.",
                ".######.#########.######.",
                "..#####.#########.#####..",
                "...###################...",
                "....#################....",
                ".....###############.....",
                ".....##############......",
                "......#############......",
                ".......###########.......",
                "........#########........",
                "........#########........",
                "........#########........",
                "........#########........",
                "........###...##.........",
                ".........#######.........",
                ".........#######.........",
                ".........#######.........",
                ".........#######.........",
                ".........#######.........") },
            { "submarine", Rows(
                "............######.............",
                "............######.............",
                "............##.................",
                "............##.................",
                "............##.................",
                "..........#######..............",
                "..........#######..............",
                "..........#######..............",
                "..........#######..............",
                "..........#######..............",
                "..........##########...........",
                ".....###################.......",
                "...########################...#",
                ".###########################.##",
                "######..###...###...###########",
                "#####...###...###...###########",
                ".#####..####.#####.############",
                "..##########################..#",
                "...#######################....#",
                "......#################........") },
            { "guitar", Sym(
                ".......####",
                ".......####",
                ".......####",
                ".......####",
                ".......####",
                "........###",
                "........###",
                "........###",
                "........###",
                "........###",
                "........###",
                "........###",
                "........###",
                "........###",
                "........###",
                "........###",
                "........###",
                "........###",
                "......#####",
                ".....######",
                "....#######",
                "...########",
                "...########",
                "...########",
                "...########",
                "...########",
                "...#######.",
                "...#####...",
                "...#####...",
                "..######...",
                ".#######...",
                ".########..",
                "###########",
                "###########",
                "###########",
                "###########",
                ".##########",
                ".##########",
                ".##########",
                "..#########",
                "...########",
                "....#######",
                "......#####",
                ".........##") },
            { "drum", Sym(
                ".#.............",
                "####...........",
                "####...........",
                ".#####.........",
                "...####........",
                ".....####......",
                "......####.....",
                "........####...",
                ".........#####.",
                ".........######",
                "....###########",
                "..#######...###",
                ".###........##.",
                ".###...........",
                ".########......",
                ".##############",
                ".##############",
                ".##############",
                ".####.......###",
                ".#####.....####",
                ".#####.....####",
                ".######...#####",
                ".######...#####",
                ".#######.######",
                ".#######.######",
                ".##############",
                ".##############",
                ".##############",
                ".##############",
                ".##############",
                "..#############",
                ".....##########",
                "............###") },
            { "headphones", Sym(
                "............###",
                ".........######",
                "........#######",
                "......#########",
                ".....######....",
                "....#####......",
                "....####.......",
                "...####........",
                "...####........",
                "...###.........",
                "..####.........",
                "..####.........",
                "..####.........",
                "######.........",
                "######.........",
                "######.........",
                "######.........",
                "######.........",
                "######.........",
                "..####.........",
                "..####.........") },
            { "camera", Rows(
                ".....##########.........####...",
                ".....##########.........####...",
                ".....##########.........####...",
                "###############################",
                "###...#######.....########..###",
                "###...#####.........######..###",
                "###...####...........#####..###",
                "#########....#####....#########",
                "#########...#######...#########",
                "########...#########...########",
                "########...###...###...########",
                "########...###...###...########",
                "########...###...###...########",
                "########...#########...########",
                "#########...#######...#########",
                ".########.....###.....########.",
                "..########...#####...########..",
                "...########..#####..########...",
                "....#######################....") },
            { "horse", Rows(
                "........#......................",
                "........#......................",
                "........##.....................",
                "#####..###.....................",
                "#####.####.....................",
                ".####.#####....................",
                ".##########....................",
                "..##########...................",
                "....########...................",
                "........#####..................",
                ".........####..................",
                ".........####.########.........",
                "..........###############......",
                "..........#################....",
                ".........#################.#...",
                ".........####################..",
                ".........######################",
                "..........################..###",
                "..........################..###",
                "..........################..###",
                "..........###.#####.##.###...##",
                "..........###.###...##.###...#.",
                "..........###.###...##.###.....",
                "..........###.###...##.###.....",
                "..........###.###...##.###.....",
                "..........###.###...##.###.....",
                "..........###.###...##.###.....",
                "..........###.###...##.###.....",
                "..........###.###...##.###.....") },
            { "cow", Rows(
                ".##.....###....................",
                ".###...####....................",
                "..###..###.....................",
                "..#######......................",
                "..########................##...",
                "..###########################..",
                "..##..#########..############..",
                "..############....############.",
                ".############......###########.",
                "##############....###...###.###",
                "###############..###.....##.###",
                ".####..#############....###.###",
                ".........#############.####.###",
                ".........##################.##.",
                "..........###.#########.###.##.",
                "..........###.#########.###.##.",
                "..........###.###...###.###....",
                "..........###.###...###.###....",
                "..........###.###...###.###....",
                "..........###.###...###.###....") },
            { "lion", Rows(
                ".............###.............",
                "............#####............",
                ".......###############.......",
                "......#################......",
                "......#################......",
                ".....###################.....",
                "...#########.....#########...",
                "..##########..#..##########..",
                "..######.###########.######..",
                "..#####..###########.######..",
                "..#####..###########..#####..",
                "..####..#############..####..",
                ".#####..###.####..###..#####.",
                "######.####..###..####.######",
                "######.###############.######",
                "######.###############.######",
                ".#####.######...######.#####.",
                "..####..######.######..####..",
                "..#####.#############.#####..",
                "..#####..###########..#####..",
                "..######..#########..######..",
                "..#######..#######..#######..",
                "...#######################...",
                ".....###################.....",
                "......#################......",
                "......#################......",
                ".......###############.......",
                "............#####............",
                ".............###.............") },
            { "bear", Sym(
                "...###........",
                ".######.......",
                ".#######......",
                "###...##......",
                "###...########",
                "###...########",
                ".#############",
                ".#############",
                "...###########",
                "...###########",
                "...###########",
                "..######..####",
                "..######..####",
                "..#######.####",
                "..############",
                "..############",
                "..##########..",
                "...########...",
                "...#########..",
                "....##########",
                "....##########",
                ".....#########",
                "......########",
                "........######",
                "...........###") },
            { "fox", Sym(
                "..#...........",
                "..##..........",
                "..###.........",
                "..####........",
                "..#####.......",
                ".#######......",
                ".########.....",
                ".#############",
                ".#############",
                ".#...#########",
                "....##########",
                "##############",
                "########.#####",
                ".#######..####",
                "..######..####",
                "..############",
                "...###########",
                "....##########",
                "....##########",
                ".....#########",
                "......########",
                "......########",
                ".......#######",
                ".......#######",
                "........######",
                ".........###..",
                ".........###..",
                "..........####",
                "...........###",
                "............##") },
            { "monkey", Sym(
                "...............#",
                "...............#",
                "..............##",
                "..............##",
                ".............###",
                "...........#####",
                "..........######",
                ".........#######",
                "........########",
                "...###..########",
                "..#####.####.###",
                ".##########...##",
                "###########...##",
                "############.###",
                "################",
                ".###############",
                ".###############",
                "..#####.########",
                "........########",
                ".........#######",
                ".........###..##",
                "..........###...",
                "...........#####",
                "..............##") },
            { "parrot", Rows(
                "..........###..........",
                "........#######........",
                ".......#########.......",
                "......###########......",
                ".....#####..######.....",
                "...#######...#####.....",
                ".##########.######.....",
                "##################.....",
                ".##################....",
                ".##################....",
                "..####.#############...",
                "..##.....###########...",
                "..#......###..#######..",
                "........####..#######..",
                "........#####..######..",
                ".......######..######..",
                ".......#######..#####..",
                ".......########..####..",
                ".......########..####..",
                ".......#########..##...",
                ".......#########..##...",
                ".......##########..#...",
                ".......###########.....",
                ".......###########.....",
                ".......###########.....",
                ".......###########.....",
                "........##########.....",
                "..#####################",
                "..#####################",
                "..#####################",
                "..............######...",
                "..............######...",
                "...............#####...",
                "...............#####...",
                "...............#####...",
                "...............#####...",
                "...............#####...",
                "...............#####...",
                "...............#####...",
                "...............####....") },
            { "frog", Rows(
                ".....######.........######.....",
                "....########.......########....",
                "....###..###.......###..###....",
                "....##....##.......##....##....",
                "....##....##.......##...###....",
                "....#######################....",
                ".....#####################.....",
                ".....#####################.....",
                "....#######################....",
                "...#########################...",
                "..###########################..",
                "..###########################..",
                ".#############################.",
                ".#############################.",
                "..###########################..",
                "..########.#########.########..",
                "...########...###...########...",
                "....##########...##########....",
                "......###################......",
                "..###########################..",
                "##########..#######..##########",
                "##########...........##########",
                ".########.............########.") },
            { "squirrel", Rows(
                "................##.........",
                "..............######.......",
                ".............########......",
                ".............#########.....",
                "............###########....",
                "............#.##########...",
                "............#.##########...",
                "......##....#.###########..",
                "......##....#..##########..",
                "....#####...#..###########.",
                "..#######...#..###########.",
                "..##.#####..#..###########.",
                ".###.#####..#..###########.",
                ".#########..#..############",
                "..########..#..############",
                "..#########....############",
                "....#######################",
                ".....######################",
                ".##.#######################",
                "##########################.",
                "##########################.",
                "###############.##########.",
                "..#############.#########..",
                "...############...#######..",
                "...############....#####...",
                "....##########.............",
                "....##########.............",
                "....#########..............",
                ".....########..............",
                "...#########...............",
                "..##########...............",
                "...#########...............",
                ".....####..................") },
            { "bat", Sym(
                ".............#..",
                ".............##.",
                "......##.....###",
                "....#####....#.#",
                "..#########..###",
                "################",
                ".###############",
                ".###############",
                ".###############",
                "..##############",
                "..##..##########",
                "..#...##########",
                ".......###.#####",
                ".......##...####",
                "..............##",
                "...............#") },
            { "spider", Sym(
                ".##.............",
                "####............",
                "####............",
                ".####...........",
                ".####...........",
                "..####..........",
                "...####.........",
                "..#####.........",
                "..######.....###",
                "..########..####",
                "...########.####",
                "...#############",
                "....############",
                ".....###########",
                ".....###########",
                "........########",
                ".....###########",
                "....############",
                "....############",
                "...#############",
                "..##############",
                "..##############",
                "..#######..#####",
                "...####.....####",
                "...####......###",
                "..####..........",
                ".####...........",
                ".####...........",
                "####............",
                "####............",
                ".##.............") },
            { "octopus", Sym(
                "...........####",
                ".........######",
                "........#######",
                ".......########",
                ".......########",
                "......#########",
                "......#####.###",
                "......####...##",
                "......#####..##",
                ".......########",
                ".......########",
                "........#######",
                "........#######",
                ".......########",
                "......#########",
                "###..####.#####",
                "########..###..",
                ".#######..###..",
                "..#####..####..",
                "...###...###...",
                "........####...",
                ".......####....",
                ".....######....",
                ".....####......",
                ".....###.......") },
            { "seahorse", Rows(
                "..................##.....",
                ".................###.....",
                "..............####.##....",
                "............########.....",
                "...........##########....",
                "........#######..####....",
                "....###########..#####...",
                ".#####################...",
                "######################...",
                "######################...",
                ".#####.....############..",
                ".............##########..",
                ".............##########..",
                "...........###########...",
                "...........###########...",
                "..........###########....",
                "..........############...",
                ".........##############..",
                ".........###############.",
                "........#################",
                "........################.",
                ".......################..",
                ".......###############...",
                ".......##############....",
                "......#############......",
                "......#############......",
                ".......############......",
                ".......############......",
                "........############.....",
                ".........###########.....",
                ".........###########.....",
                "..........##########.....",
                "..........##########.....",
                "........############.....",
                ".......##############....",
                "......###############....",
                "......#######.#######....",
                "......#####..########....",
                "......##############.....",
                "......#############......",
                ".......###########.......",
                ".......###########.......",
                "............#####........") },
            { "starfish", Rows(
                "..............###............",
                ".............#####...........",
                ".............######..........",
                "............#######..........",
                "............#######..........",
                "............#######..........",
                "............######...........",
                ".#####......##..##...........",
                ".#######....##..##...........",
                "##########..######...........",
                "##################....#####..",
                ".######.####################.",
                "..#####.#####################",
                "...#################..#######",
                ".....###############..######.",
                "........####################.",
                "........################.....",
                ".......############..........",
                "......#############..........",
                ".....###############.........",
                "....####..##########.........",
                "...#########..###.###........",
                "...########...########.......",
                "...#######.....#######.......",
                "...######......########......",
                "...#####........#######......",
                "....###.........#######......",
                ".................#####.......",
                "..................###........") },
            { "dragon", Rows(
                "....................#..........",
                "....................#..........",
                "............##.....##..........",
                "......#.....###...###..........",
                "......#.....####..####.........",
                ".######.....##########...#.....",
                "##..###.....##########..##.....",
                ".#######....#############......",
                "..#######...#############......",
                ".....####...#############......",
                ".....#####..############.......",
                "......#####.############.......",
                "......#####.############.......",
                ".......################........",
                "........#############........#.",
                "........##############.......#.",
                ".........#############.....###.",
                ".........##############...#####",
                "..........#####################",
                "..........###################..",
                "..........##########..######...",
                "..........###....###....###....",
                "..........###....###...........",
                "..........###....###...........",
                "..........###....###...........") },
            };
        }
    }
}
