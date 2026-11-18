using UnityEngine;

namespace ArrowPuzzle
{
    /// <summary>Kind of a campaign level: a rectangle board, or a shape (every 3rd level).</summary>
    public enum LevelKind { Normal, Shape }

    /// <summary>
    /// The one long-arrow campaign: 333 global levels. Level L is a SHAPE level when L % 3 == 0, else a NORMAL board (the
    /// whole rectangle is the mask). Shape level k = L / 3 - 1 (0..110) is shape k % 37 (Shapes order) in round k / 37
    /// (0..2): every shape once per round, harder each round. A shape is unlocked once Save.UnlockedGlobal reaches its
    /// round-0 level 3 * (index + 1). Level data: Resources/Levels/long/shape_NAME.txt (its 3 rounds) and
    /// long/normal_NN.txt (25 normal levels each), built offline by "Arrow/Long Levels/Build Full Level Set";
    /// long/index.txt gives every level's file, size, arrow count and difficulty without parsing the level.
    /// The table part has no Unity calls, so the offline generator uses the same table.
    /// </summary>
    public static class Campaign
    {
        public static readonly string[] Shapes =
        {
            "heart", "hourglass", "paw", "cat", "bunny", "star", "fish", "house", "tree", "apple", "cupcake", "icecream",
            "mushroom", "leaf", "flower", "moon", "cloud", "sailboat", "umbrella", "crown", "key", "lock", "rocket",
            "cherry", "pear", "strawberry", "bulb", "butterfly", "duck", "owl", "whale", "turtle", "snail", "cactus",
            "note", "diamond", "sun",
        };

        public const int ShapeCount = 37;
        public const int Rounds = 3;
        public const int TotalLevels = 333;
        public const int ShapeLevelCount = TotalLevels / 3;                // 111
        public const int NormalLevelCount = TotalLevels - ShapeLevelCount; // 222
        public const int NormalPack = 25;                                  // normal levels per file

        public const string Folder = "Levels/long";   // under Resources
        public const string IndexFile = "index";
        public const string ThumbsFile = "thumbs"; // one small level per shape (level n = shape n-1), for the gallery
        public const string BoardName = "Board";   // display "type" of a normal level

        /// <summary>The gallery thumbnail of a shape (Resources/Levels/long/thumbs.txt, parsed once). Null if missing.</summary>
        public static LevelData Thumb(int shapeIndex)
        {
            var d = ShapeLevels.GetFile("long/" + ThumbsFile, shapeIndex + 1);
            if (d != null) d.longArrows = true;
            return d;
        }

        public static int Clamp(int level) { return level < 1 ? 1 : level > TotalLevels ? TotalLevels : level; }

        public static LevelKind Kind(int level) { return Clamp(level) % 3 == 0 ? LevelKind.Shape : LevelKind.Normal; }

        public static bool IsShape(int level) { return Kind(level) == LevelKind.Shape; }

        /// <summary>0-based shape index of a shape level, -1 for a normal level.</summary>
        public static int ShapeIndexOf(int level)
        {
            level = Clamp(level);
            return level % 3 == 0 ? (level / 3 - 1) % ShapeCount : -1;
        }

        /// <summary>Shape name of a shape level ("heart"), null for a normal level.</summary>
        public static string ShapeOfLevel(int level)
        {
            int i = ShapeIndexOf(level);
            return i < 0 ? null : Shapes[i];
        }

        /// <summary>Round (0..2) of a shape level, -1 for a normal level.</summary>
        public static int RoundOf(int level)
        {
            level = Clamp(level);
            return level % 3 == 0 ? (level / 3 - 1) / ShapeCount : -1;
        }

        /// <summary>0-based position of a normal level among the normal levels (0..221), -1 for a shape level.</summary>
        public static int NormalIndexOf(int level)
        {
            level = Clamp(level);
            return level % 3 == 0 ? -1 : level - level / 3 - 1;
        }

        /// <summary>Global level of shape index s in round r: 3 * (r * 37 + s + 1).</summary>
        public static int ShapeLevel(int shapeIndex, int round) { return 3 * (round * ShapeCount + shapeIndex + 1); }

        /// <summary>The shape's round-0 level (its unlock level): 3 * (index + 1).</summary>
        public static int FirstLevel(int shapeIndex) { return ShapeLevel(shapeIndex, 0); }

        public static int FirstLevel(string shape) { return FirstLevel(IndexOf(shape)); }

        /// <summary>File of a level under Resources/Levels: "long/shape_cat" or "long/normal_03".</summary>
        public static string FileOf(int level)
        {
            level = Clamp(level);
            if (level % 3 == 0) return "long/shape_" + ShapeOfLevel(level);
            return "long/normal_" + (NormalIndexOf(level) / NormalPack + 1).ToString("00");
        }

        /// <summary>Index of a shape name, -1 if it is not in the campaign.</summary>
        public static int IndexOf(string shape) { return System.Array.IndexOf(Shapes, shape); }

        /// <summary>Display name: "heart" -> "Heart", "icecream" -> "Ice cream".</summary>
        public static string Title(string shape)
        {
            if (shape == "icecream") return "Ice cream";
            return string.IsNullOrEmpty(shape) ? "" : char.ToUpperInvariant(shape[0]) + shape.Substring(1);
        }

        /// <summary>Display "type" of a level: the shape title for a shape level, "Board" for a normal one.</summary>
        public static string TypeTitle(int level)
        {
            var s = ShapeOfLevel(level);
            return s == null ? BoardName : Title(s);
        }

        /// <summary>Analytics / crash "type" of a level: the shape name ("cat"), or "board" for a normal one.</summary>
        public static string TypeKey(int level)
        {
            return ShapeOfLevel(level) ?? "board";
        }

        // ------------------------------------------------------------------ progress (Save)

        public static bool IsShapeUnlocked(int shapeIndex)
        {
            return shapeIndex >= 0 && shapeIndex < ShapeCount && Save.UnlockedGlobal >= FirstLevel(shapeIndex);
        }

        public static bool IsShapeUnlocked(string shape) { return IsShapeUnlocked(IndexOf(shape)); }

        /// <summary>The shape's round-0 level is completed.</summary>
        public static bool ShapeDone(int shapeIndex)
        {
            return shapeIndex >= 0 && shapeIndex < ShapeCount && Save.IsCompleted(FirstLevel(shapeIndex));
        }

        public static bool ShapeDone(string shape) { return ShapeDone(IndexOf(shape)); }

        /// <summary>First shape (index) whose round-0 level is above level `unlocked`; ShapeCount when all are reached.</summary>
        public static int NextShape(int unlocked)
        {
            for (int i = 0; i < ShapeCount; i++) if (FirstLevel(i) > unlocked) return i;
            return ShapeCount;
        }

        // ------------------------------------------------------------------ index.txt (arrow count, difficulty)

        static int[] arrows;
        static float[] diff;

        /// <summary>Arrow count of a level from index.txt (0 if the index is missing).</summary>
        public static int ArrowCount(int level)
        {
            LoadIndex();
            return arrows != null ? arrows[Clamp(level)] : 0;
        }

        /// <summary>Generator difficulty d (0..1) of a level from index.txt (0 if the index is missing).</summary>
        public static float Difficulty(int level)
        {
            LoadIndex();
            return diff != null ? diff[Clamp(level)] : 0f;
        }

        /// <summary>
        /// index.txt lines: "level kind file indexInFile shape cols rows arrowCount d" (kind = normal | shape; shape is
        /// "-" for a normal level; # = comment). Loaded once.
        /// </summary>
        static void LoadIndex()
        {
            if (arrows != null) return;
            var ta = Resources.Load<TextAsset>(Folder + "/" + IndexFile);
            if (ta == null)
            {
                Debug.LogError("[ArrowPuzzle] Missing Resources/" + Folder + "/" + IndexFile + ".txt");
                return;
            }
            var a = new int[TotalLevels + 1];
            var d = new float[TotalLevels + 1];
            foreach (var raw in ta.text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var p = line.Split(' ');
                int g, n;
                float v;
                if (p.Length < 9 || !int.TryParse(p[0], out g) || g < 1 || g > TotalLevels) continue;
                if (int.TryParse(p[7], out n)) a[g] = n;
                if (float.TryParse(p[8], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) d[g] = v;
            }
            arrows = a;
            diff = d;
            Resources.UnloadAsset(ta);
        }
    }
}
