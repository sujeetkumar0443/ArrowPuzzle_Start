using UnityEngine;

namespace ArrowPuzzle
{
    /// <summary>
    /// The one long-arrow campaign: 37 shapes x 12 levels = 444 global levels, in this order. A shape's block starts at
    /// level 1 + 12 * index; the shape is unlocked once Save.UnlockedGlobal reaches that first level. Level data lives in
    /// Resources/Levels/long/&lt;shape&gt;.txt (built offline by "Arrow/Long Levels/Build Full Level Set"), and
    /// Resources/Levels/long/index.txt gives every level's arrow count and difficulty without parsing the level.
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

        public const int LevelsPerShape = 12;
        public const int ShapeCount = 37;
        public const int TotalLevels = ShapeCount * LevelsPerShape; // 444

        public const string Folder = "Levels/long";   // under Resources
        public const string IndexFile = "index";

        static int Clamp(int level) { return level < 1 ? 1 : level > TotalLevels ? TotalLevels : level; }

        /// <summary>0-based shape index of a global level.</summary>
        public static int ShapeIndexOf(int level) { return (Clamp(level) - 1) / LevelsPerShape; }

        /// <summary>Shape name of a global level, e.g. "heart".</summary>
        public static string ShapeOf(int level) { return Shapes[ShapeIndexOf(level)]; }

        /// <summary>0-based position of a global level inside its shape's block (0..11).</summary>
        public static int IndexInShape(int level) { return (Clamp(level) - 1) % LevelsPerShape; }

        /// <summary>First global level of a shape (its unlock level): 1 + 12 * index.</summary>
        public static int FirstLevel(int shapeIndex) { return 1 + LevelsPerShape * shapeIndex; }

        public static int FirstLevel(string shape) { return FirstLevel(IndexOf(shape)); }

        /// <summary>The 12 global levels of a shape, in order.</summary>
        public static int[] LevelsOf(int shapeIndex)
        {
            var res = new int[LevelsPerShape];
            for (int i = 0; i < LevelsPerShape; i++) res[i] = FirstLevel(shapeIndex) + i;
            return res;
        }

        public static int[] LevelsOf(string shape) { return LevelsOf(IndexOf(shape)); }

        /// <summary>Index of a shape name, -1 if it is not in the campaign.</summary>
        public static int IndexOf(string shape) { return System.Array.IndexOf(Shapes, shape); }

        /// <summary>Display name: "heart" -> "Heart", "icecream" -> "Ice cream".</summary>
        public static string Title(string shape)
        {
            if (shape == "icecream") return "Ice cream";
            return string.IsNullOrEmpty(shape) ? "" : char.ToUpperInvariant(shape[0]) + shape.Substring(1);
        }

        public static string ShapeTitle(int level) { return Title(ShapeOf(level)); }

        // ------------------------------------------------------------------ progress (Save)

        public static bool IsShapeUnlocked(int shapeIndex)
        {
            return shapeIndex >= 0 && shapeIndex < ShapeCount && Save.UnlockedGlobal >= FirstLevel(shapeIndex);
        }

        public static bool IsShapeUnlocked(string shape) { return IsShapeUnlocked(IndexOf(shape)); }

        /// <summary>Levels of this shape's block that are completed (0..12).</summary>
        public static int ShapeProgress(int shapeIndex)
        {
            if (shapeIndex < 0 || shapeIndex >= ShapeCount) return 0;
            int n = 0;
            foreach (int g in LevelsOf(shapeIndex)) if (Save.IsCompleted(g)) n++;
            return n;
        }

        public static int ShapeProgress(string shape) { return ShapeProgress(IndexOf(shape)); }

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
        /// index.txt lines: "level shape file indexInFile arrowCount d" (# = comment). Loaded once.
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
                if (p.Length < 6 || !int.TryParse(p[0], out g) || g < 1 || g > TotalLevels) continue;
                if (int.TryParse(p[4], out n)) a[g] = n;
                if (float.TryParse(p[5], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) d[g] = v;
            }
            arrows = a;
            diff = d;
            Resources.UnloadAsset(ta);
        }
    }
}
