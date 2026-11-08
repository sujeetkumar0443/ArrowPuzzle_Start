namespace ArrowPuzzle
{
    /// <summary>
    /// The one path of 150 levels. Global level g (1..Count) cycles through the types: 1 = Classic 1, 2 = Shapes 1,
    /// 3 = Mosaic 1, 4 = Classic 2, ... Level data, stars and best times stay per type (Levels.For, Save keys).
    /// </summary>
    public static class LevelPath
    {
        const int Types = 3;
        public const int Count = ModeInfo.LevelsPerMode * Types;
        public const int EasyMaxArrows = 50;

        static readonly string[] Names = { "Arrow maze", "Picture", "Mosaic" };
        static readonly bool[] hard = new bool[Count];
        static readonly bool[] known = new bool[Count];

        public static GameMode TypeOf(int g)
        {
            return (GameMode)((Clamp(g) - 1) % Types);
        }

        public static int TypeLevelOf(int g)
        {
            return (Clamp(g) - 1) / Types + 1;
        }

        public static int Global(GameMode type, int typeLevel)
        {
            return (typeLevel - 1) * Types + (int)type + 1;
        }

        public static string TypeName(int g)
        {
            return Names[(int)TypeOf(g)];
        }

        /// <summary>"Easy" (at most EasyMaxArrows arrows) or "Hard", from the real level data. Loaded once per level.</summary>
        public static string Difficulty(int g)
        {
            int i = Clamp(g) - 1;
            if (!known[i])
            {
                var data = Levels.For(TypeOf(g), TypeLevelOf(g));
                hard[i] = data != null && data.pieces.Count > EasyMaxArrows;
                known[i] = true;
            }
            return hard[i] ? "Hard" : "Easy";
        }

        static int Clamp(int g)
        {
            return g < 1 ? 1 : g > Count ? Count : g;
        }
    }
}
