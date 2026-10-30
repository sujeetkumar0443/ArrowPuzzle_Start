using UnityEngine;

namespace ArrowPuzzle
{
    /// <summary>Single entry point for level data of any mode.</summary>
    public static class Levels
    {
        public static LevelData For(GameMode mode, int n)
        {
            if (mode == GameMode.Classic) return LevelGen.Generate(Mathf.Clamp(n, 1, ModeInfo.LevelsPerMode));
            return ShapeLevels.Get(mode, n);
        }
    }
}
