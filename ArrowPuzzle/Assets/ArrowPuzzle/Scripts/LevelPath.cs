namespace ArrowPuzzle
{
    /// <summary>
    /// The one path of levels: the long-arrow campaign (Campaign: 333 levels, every 3rd a shape). Global level g is the
    /// campaign level g; its shape, arrow count and difficulty come from Campaign / index.txt (no level is parsed).
    /// The old Classic / Shapes / Mosaic cycle (150 levels) is kept only for the one-time save migration and the music
    /// rotation (Old*, Music*).
    /// </summary>
    public static class LevelPath
    {
        public const int Count = Campaign.TotalLevels;

        /// <summary>"Hard" above this many arrows (analytics label).</summary>
        public const int EasyMaxArrows = 35;

        /// <summary>Display type of the level: its shape ("Heart") or "Board" for a normal level.</summary>
        public static string TypeName(int g)
        {
            return Campaign.TypeTitle(g);
        }

        /// <summary>"Easy" (at most EasyMaxArrows arrows) or "Hard", from index.txt.</summary>
        public static string Difficulty(int g)
        {
            return Campaign.ArrowCount(g) > EasyMaxArrows ? "Hard" : "Easy";
        }

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
