using UnityEngine;

namespace ArrowPuzzle
{
    /// <summary>Tiny PlayerPrefs wrapper for progress and settings.</summary>
    public static class Save
    {
        const string UnlockedKey = "unlockedGlobal";

        /// <summary>Dev switch: all 150 levels are playable. Keep false for release.</summary>
        public const bool UnlockAllLevels = false;

        static bool AllLevels { get { return UnlockAllLevels; } } // property, so "UnlockAllLevels || x" gives no unreachable-code warning

        /// <summary>
        /// Highest unlocked global level (LevelPath). Without the key yet (first run, or progress from the mode
        /// version) it starts at the first level that is not completed, or the last level when all are done.
        /// </summary>
        public static int UnlockedGlobal
        {
            get
            {
                if (!PlayerPrefs.HasKey(UnlockedKey))
                {
                    int g = 1;
                    while (g < LevelPath.Count && IsCompleted(g)) g++;
                    PlayerPrefs.SetInt(UnlockedKey, g);
                    PlayerPrefs.Save();
                }
                return Mathf.Clamp(PlayerPrefs.GetInt(UnlockedKey, 1), 1, LevelPath.Count);
            }
            set { PlayerPrefs.SetInt(UnlockedKey, Mathf.Clamp(value, 1, LevelPath.Count)); PlayerPrefs.Save(); }
        }

        public static bool IsUnlocked(int g)
        {
            return AllLevels || g <= UnlockedGlobal;
        }

        /// <summary>Global level g was won (a win always saves 1 to 3 stars).</summary>
        public static bool IsCompleted(int g)
        {
            return Stars(LevelPath.TypeOf(g), LevelPath.TypeLevelOf(g)) > 0;
        }

        /// <summary>A win of global level g opens the next one and pays the base win reward (WinCoins, every win).</summary>
        public static void LevelWon(int g)
        {
            UnlockAfter(g);
            AddCoins(WinCoins);
        }

        /// <summary>The unlock rule shared by a win and a skip: finishing the highest unlocked level opens the next one.</summary>
        static void UnlockAfter(int g)
        {
            if (g == UnlockedGlobal && g < LevelPath.Count) UnlockedGlobal = g + 1;
        }

        // ------------------------------------------------------------------ skip level (rewarded, daily limit)

        const string SkipDayKey = "ap_skip_day", SkipUsedKey = "ap_skip_used", SkipBonusKey = "ap_skip_bonus";

        /// <summary>Free skips per day (each one still plays a rewarded ad); bonus ads add more for the same day.</summary>
        public const int SkipsPerDay = 4;

        /// <summary>Today as yyyyMMdd, device local date (the day turns at local midnight).</summary>
        static int Today
        {
            get { var d = System.DateTime.Now; return d.Year * 10000 + d.Month * 100 + d.Day; }
        }

        /// <summary>A stored day other than today (later, earlier or none) resets used and bonus to 0.</summary>
        static void CheckSkipDay()
        {
            int today = Today;
            if (PlayerPrefs.GetInt(SkipDayKey, 0) == today) return;
            PlayerPrefs.SetInt(SkipDayKey, today);
            PlayerPrefs.SetInt(SkipUsedKey, 0);
            PlayerPrefs.SetInt(SkipBonusKey, 0);
            PlayerPrefs.Save();
        }

        public static int SkipsUsedToday
        {
            get { CheckSkipDay(); return Mathf.Max(0, PlayerPrefs.GetInt(SkipUsedKey, 0)); }
        }

        public static int SkipsBonusToday
        {
            get { CheckSkipDay(); return Mathf.Max(0, PlayerPrefs.GetInt(SkipBonusKey, 0)); }
        }

        /// <summary>SkipsPerDay + today's bonus - today's used, never below 0.</summary>
        public static int SkipsLeft
        {
            get { return Mathf.Max(0, SkipsPerDay + SkipsBonusToday - SkipsUsedToday); }
        }

        /// <summary>One skip used today (call only after its rewarded ad completed).</summary>
        public static void UseSkip()
        {
            PlayerPrefs.SetInt(SkipUsedKey, SkipsUsedToday + 1); // the getter rolls the day over first
            PlayerPrefs.Save();
            if (SkipsChanged != null) SkipsChanged();
        }

        /// <summary>n more skips for today (the "Get +2 skips" rewarded ad).</summary>
        public static void AddSkipBonus(int n)
        {
            if (n <= 0) return;
            PlayerPrefs.SetInt(SkipBonusKey, SkipsBonusToday + n);
            PlayerPrefs.Save();
            if (SkipsChanged != null) SkipsChanged();
        }

        /// <summary>Raised after every UseSkip / AddSkipBonus (skip buttons and the "Skips today" dots listen).</summary>
        public static event System.Action SkipsChanged;

        /// <summary>
        /// Global level g was skipped: it counts as passed (1 star only if it had none; an existing count is never
        /// lowered) and the next level unlocks with the same rule as a win. No coins, no best time.
        /// </summary>
        public static void SkipLevel(int g)
        {
            g = Mathf.Clamp(g, 1, LevelPath.Count);
            var type = LevelPath.TypeOf(g);
            int typeLevel = LevelPath.TypeLevelOf(g);
            if (Stars(type, typeLevel) <= 0) SetStars(type, typeLevel, 1);
            UnlockAfter(g);
        }

        // ------------------------------------------------------------------ coins

        const string CoinsKey = "ap_coins";

        /// <summary>Base reward for every win, given in LevelWon (the result screen's wheel multiplies it).</summary>
        public const int WinCoins = 25;

        /// <summary>Coin balance (never below 0).</summary>
        public static int Coins
        {
            get { return Mathf.Max(0, PlayerPrefs.GetInt(CoinsKey, 0)); }
        }

        /// <summary>Adds n coins (negative spends; the balance stays within 0..int.MaxValue) and saves.</summary>
        public static void AddCoins(int n)
        {
            if (n == 0) return;
            long total = (long)Coins + n;
            PlayerPrefs.SetInt(CoinsKey, (int)System.Math.Max(0L, System.Math.Min(int.MaxValue, total)));
            PlayerPrefs.Save();
            if (CoinsChanged != null) CoinsChanged();
        }

        /// <summary>Raised after every coin change (the result screen's coin chip can listen).</summary>
        public static event System.Action CoinsChanged;

        public static bool Sound
        {
            get { return PlayerPrefs.GetInt("ap_sound", 1) == 1; }
            set { PlayerPrefs.SetInt("ap_sound", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool Music
        {
            get { return PlayerPrefs.GetInt("music", 1) == 1; }
            set { PlayerPrefs.SetInt("music", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool Haptics
        {
            get { return PlayerPrefs.GetInt("ap_haptics", 1) == 1; }
            set { PlayerPrefs.SetInt("ap_haptics", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static int Hints
        {
            get { return PlayerPrefs.GetInt("ap_hints", 3); }
            set
            {
                PlayerPrefs.SetInt("ap_hints", Mathf.Max(0, value));
                PlayerPrefs.Save();
                if (HintsChanged != null) HintsChanged();
            }
        }

        /// <summary>Raised after every Hints write (the map's hints pill listens while it is shown).</summary>
        public static event System.Action HintsChanged;

        public static int Stars(int level)
        {
            return PlayerPrefs.GetInt("ap_stars_" + level, 0);
        }

        public static void SetStars(int level, int stars)
        {
            if (stars > Stars(level)) PlayerPrefs.SetInt("ap_stars_" + level, stars);
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------------ per type (Classic = the keys above)

        static string Tag(GameMode mode)
        {
            return mode == GameMode.Shapes ? "shapes" : "mosaic";
        }

        public static int Stars(GameMode mode, int level)
        {
            if (mode == GameMode.Classic) return Stars(level);
            return PlayerPrefs.GetInt("ap_stars_" + Tag(mode) + "_" + level, 0);
        }

        public static void SetStars(GameMode mode, int level, int stars)
        {
            if (mode == GameMode.Classic) { SetStars(level, stars); return; }
            if (stars > Stars(mode, level)) PlayerPrefs.SetInt("ap_stars_" + Tag(mode) + "_" + level, stars);
            PlayerPrefs.Save();
        }

        /// <summary>Best solve time in seconds, 0 when the level has no best yet.</summary>
        public static float GetBestTime(GameMode mode, int level)
        {
            return Mathf.Max(0f, PlayerPrefs.GetFloat(BestKey(mode, level), 0f));
        }

        public static void SetBestTime(GameMode mode, int level, float seconds)
        {
            PlayerPrefs.SetFloat(BestKey(mode, level), Mathf.Max(0f, seconds));
            PlayerPrefs.Save();
        }

        // same shape as the stars keys: Classic "ap_best_<n>", others "ap_best_<mode>_<n>"
        static string BestKey(GameMode mode, int level)
        {
            return mode == GameMode.Classic ? "ap_best_" + level : "ap_best_" + Tag(mode) + "_" + level;
        }

        /// <summary>Completed levels on the whole path.</summary>
        public static int CompletedLevels()
        {
            int n = 0;
            for (int g = 1; g <= LevelPath.Count; g++)
                if (IsCompleted(g)) n++;
            return n;
        }

        /// <summary>Stars over all three modes, levels 1..LevelsPerMode.</summary>
        public static int TotalStars()
        {
            int sum = 0;
            for (int m = 0; m < ModeInfo.Count; m++)
                for (int n = 1; n <= ModeInfo.LevelsPerMode; n++) sum += Stars((GameMode)m, n);
            return sum;
        }
    }
}
