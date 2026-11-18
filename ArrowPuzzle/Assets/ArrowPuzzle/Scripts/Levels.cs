using UnityEngine;

namespace ArrowPuzzle
{
    /// <summary>Single entry point for level data.</summary>
    public static class Levels
    {
        static string cachedFile, cachedText;

        /// <summary>
        /// Campaign level g (1..Campaign.TotalLevels): its shape's file Resources/Levels/long/&lt;shape&gt;.txt is loaded on
        /// demand (the last file's text is cached), only level g is parsed, and it is flagged for the long-arrow renderer.
        /// Null (with an error log) if the file or the level is missing.
        /// </summary>
        public static LevelData For(int g)
        {
            g = Mathf.Clamp(g, 1, Campaign.TotalLevels);
            string file = Campaign.Folder + "/" + Campaign.ShapeOf(g);
            if (file != cachedFile)
            {
                var ta = Resources.Load<TextAsset>(file);
                if (ta == null)
                {
                    Debug.LogError("[ArrowPuzzle] Missing level file Resources/" + file + ".txt");
                    return null;
                }
                cachedText = ta.text;
                cachedFile = file;
                Resources.UnloadAsset(ta);
            }
            var data = ShapeLevels.ParseLevel(cachedText, g);
            if (data == null)
            {
                Debug.LogError("[ArrowPuzzle] Level " + g + " not found in Resources/" + file + ".txt");
                return null;
            }
            data.longArrows = true;
            return data;
        }

        /// <summary>OLD (before the campaign): Classic / Shapes / Mosaic level n. Unused now; kept until the cleanup step.</summary>
        public static LevelData For(GameMode mode, int n)
        {
            if (mode == GameMode.Classic) return LevelGen.Generate(Mathf.Clamp(n, 1, ModeInfo.LevelsPerMode));
            return ShapeLevels.Get(mode, n);
        }
    }
}
