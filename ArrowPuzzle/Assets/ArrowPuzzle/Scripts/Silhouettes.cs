using System.Collections.Generic;
using UnityEngine;

namespace ArrowPuzzle
{
    /// <summary>
    /// Cached board silhouettes: 1 pixel per board cell, white where the mask is set, transparent elsewhere,
    /// point filtered. Show them with preserveAspect and tint the Image. Texture y goes up like LevelData.mask,
    /// so shapes come out upright.
    /// </summary>
    public static class Silhouettes
    {
        static readonly Dictionary<int, Sprite> cache = new Dictionary<int, Sprite>();

        /// <summary>Thumbnail for a mode card: Classic = full 5 x 6 rectangle, Shapes = level 1, Mosaic = level 10.</summary>
        public static Sprite ForMode(GameMode mode)
        {
            if (mode == GameMode.Classic) return Cached(0, null, 5, 6);
            return ForLevel(mode, mode == GameMode.Shapes ? 1 : 10);
        }

        /// <summary>Mask silhouette of Shapes / Mosaic level n. Null for Classic or a missing level.</summary>
        public static Sprite ForLevel(GameMode mode, int n)
        {
            if (mode == GameMode.Classic) return null;
            int key = (int)mode * 1000 + n;
            Sprite s;
            if (cache.TryGetValue(key, out s) && s != null) return s;
            var lv = ShapeLevels.Get(mode, n);
            if (lv == null || lv.mask == null) return null;
            return Cached(key, lv.mask, lv.cols, lv.rows);
        }

        static Sprite Cached(int key, bool[,] mask, int cols, int rows)
        {
            Sprite s;
            if (cache.TryGetValue(key, out s) && s != null) return s;

            var tex = new Texture2D(cols, rows, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[cols * rows];
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                    px[y * cols + x] = mask == null || mask[x, y] ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            tex.SetPixels32(px);
            tex.Apply();

            s = Sprite.Create(tex, new Rect(0, 0, cols, rows), new Vector2(0.5f, 0.5f), 100f);
            cache[key] = s;
            return s;
        }
    }
}
