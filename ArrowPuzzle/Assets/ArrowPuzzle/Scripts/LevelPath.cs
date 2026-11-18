namespace ArrowPuzzle
{
    /// <summary>
    /// The one path of levels (Campaign): level g is built from its number, with no cap in the code (only
    /// Campaign.ReleaseLevelCap limits play). The old Classic / Shapes / Mosaic cycle (150 levels) is kept only for the
    /// one-time save migration and the music rotation (Old*, Music*).
    /// </summary>
    public static class LevelPath
    {
        /// <summary>"Hard" above this many arrows (analytics label).</summary>
        public const int EasyMaxArrows = 35;

        /// <summary>Display type of the level: its base shape ("Heart") or procedural kind ("Mandala").</summary>
        public static string TypeName(int g)
        {
            return Campaign.TypeTitle(g);
        }

        /// <summary>
        /// "Easy" (at most EasyMaxArrows arrows) or "Hard": from index.txt for a prebuilt level; a generated level's count
        /// is not known before it is built, so there d >= HardD counts as Hard.
        /// </summary>
        public static string Difficulty(int g)
        {
            int n = Campaign.ArrowCount(g);
            if (n > 0) return n > EasyMaxArrows ? "Hard" : "Easy";
            return Campaign.D(g) >= HardD ? "Hard" : "Easy";
        }

        public const double HardD = 0.5;

        // ------------------------------------------------------------------ the old 150-level cycle

        /// <summary>Levels of the old cycle: 1 = Classic 1, 2 = Shapes 1, 3 = Mosaic 1, 4 = Classic 2, ...</summary>
        public const int OldCount = ModeInfo.LevelsPerMode * 3;

        public static GameMode OldTypeOf(int g) { return (GameMode)((g - 1) % 3); }

        public static int OldTypeLevelOf(int g) { return (g - 1) / 3 + 1; }

        /// <summary>Music rotation (same tracks and cuts as before for levels 1..150, then it wraps).</summary>
        public static GameMode MusicTypeOf(int g) { return OldTypeOf(g < 1 ? 1 : g); }

        public static int MusicLevelOf(int g) { return ((g < 1 ? 0 : g - 1) / 3) % ModeInfo.LevelsPerMode + 1; }
    }
}
