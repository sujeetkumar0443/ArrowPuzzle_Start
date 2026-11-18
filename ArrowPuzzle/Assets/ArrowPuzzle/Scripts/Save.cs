using UnityEngine;

namespace ArrowPuzzle
{
    /// <summary>Tiny PlayerPrefs wrapper for progress and settings.</summary>
    public static class Save
    {
        const string UnlockedKey = "unlockedGlobal";

        /// <summary>Dev switch: all levels are playable. Keep false for release.</summary>
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
                Migrate(); // the first-run fallback below reads the per-level stars
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
            return Stars(g) > 0;
        }

        /// <summary>A win of global level g opens the next one and pays the base win reward (WinCoins, every win).</summary>
        public static void LevelWon(int g)
        {
            UnlockAfter(g);
            AddCoins(WinCoins, "level " + g + " won");
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
            if (Stars(g) <= 0) SetStars(g, 1);
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

        /// <summary>Adds n coins (negative spends; the balance stays within 0..int.MaxValue), saves and logs it.</summary>
        public static void AddCoins(int n, string reason = null)
        {
            if (n == 0) return;
            long total = (long)Coins + n;
            PlayerPrefs.SetInt(CoinsKey, (int)System.Math.Max(0L, System.Math.Min(int.MaxValue, total)));
            PlayerPrefs.Save();
            Debug.Log("[Coins] " + (n > 0 ? "+" : "") + n + " -> total " + Coins + " (" + (reason ?? "no reason given") + ")");
            if (CoinsChanged != null) CoinsChanged();
        }

        /// <summary>Raised after every coin change (the result screen's coin chip can listen).</summary>
        public static event System.Action CoinsChanged;

        /// <summary>Takes n coins if there are enough (raises CoinsChanged); false and nothing spent otherwise.</summary>
        public static bool SpendCoins(int n, string reason = null)
        {
            if (n <= 0) return true;
            if (Coins < n) return false;
            AddCoins(-n, reason ?? "spent");
            return true;
        }

        // ------------------------------------------------------------------ themes

        const string ThemeKey = "ap_theme", ThemesOwnedKey = "ap_themes_owned";

        /// <summary>The chosen theme id (default cream). Setting it only saves; Theme.Apply switches the look.</summary>
        public static string ThemeId
        {
            get
            {
                string id = PlayerPrefs.GetString(ThemeKey, Themes.Default);
                return Themes.Exists(id) && IsThemeOwned(id) ? id : Themes.Default;
            }
            set { PlayerPrefs.SetString(ThemeKey, value); PlayerPrefs.Save(); }
        }

        /// <summary>Paper and cream are owned by everyone; the others once unlocked (ap_themes_owned, a comma list).</summary>
        public static bool IsThemeOwned(string id)
        {
            if (Themes.Exists(id) && Themes.Get(id).OwnedByDefault) return true;
            foreach (var s in PlayerPrefs.GetString(ThemesOwnedKey, "").Split(','))
                if (s == id) return true;
            return false;
        }

        public static void UnlockTheme(string id)
        {
            if (!Themes.Exists(id) || IsThemeOwned(id)) return;
            string list = PlayerPrefs.GetString(ThemesOwnedKey, "");
            PlayerPrefs.SetString(ThemesOwnedKey, list.Length == 0 ? id : list + "," + id);
            PlayerPrefs.Save();
        }

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

        // ------------------------------------------------------------------ stars and best times per global level

        static string StarsKey(int g) { return "ap_lvl_stars_" + g; }
        static string BestKey(int g) { return "ap_lvl_best_" + g; }

        /// <summary>Stars (0..3) of global level g; 0 = not completed.</summary>
        public static int Stars(int g)
        {
            Migrate();
            return PlayerPrefs.GetInt(StarsKey(g), 0);
        }

        /// <summary>Keeps the best result: only a higher star count is saved.</summary>
        public static void SetStars(int g, int stars)
        {
            if (stars > Stars(g)) PlayerPrefs.SetInt(StarsKey(g), stars);
            PlayerPrefs.Save();
        }

        /// <summary>Best solve time of global level g in seconds, 0 when it has no best yet.</summary>
        public static float GetBestTime(int g)
        {
            Migrate();
            return Mathf.Max(0f, PlayerPrefs.GetFloat(BestKey(g), 0f));
        }

        public static void SetBestTime(int g, float seconds)
        {
            PlayerPrefs.SetFloat(BestKey(g), Mathf.Max(0f, seconds));
            PlayerPrefs.Save();
        }

        /// <summary>Completed levels on the whole path.</summary>
        public static int CompletedLevels()
        {
            int n = 0;
            for (int g = 1; g <= LevelPath.Count; g++)
                if (IsCompleted(g)) n++;
            return n;
        }

        /// <summary>Stars over the whole path.</summary>
        public static int TotalStars()
        {
            int sum = 0;
            for (int g = 1; g <= LevelPath.Count; g++) sum += Stars(g);
            return sum;
        }

        /// <summary>A campaign shape (0-based Campaign index) is open once UnlockedGlobal reaches its first level.</summary>
        public static bool IsShapeUnlocked(int shapeIndex) { return Campaign.IsShapeUnlocked(shapeIndex); }

        public static bool IsShapeUnlocked(string shape) { return Campaign.IsShapeUnlocked(shape); }

        // ------------------------------------------------------------------ one-time migration from the per-type keys

        const string ProgressV2Key = "ap_progress_v2"; // 1 = the old per-type stars / best times were copied
        static bool migrated;

        /// <summary>
        /// Once per install: stars and best times of the old Classic / Shapes / Mosaic levels are copied to the global
        /// level they had in the old 150-level cycle (g = 1 + 3 * (typeLevel - 1) + type). unlockedGlobal keeps its
        /// value and meaning. The old keys are left in place (removed in the cleanup step).
        /// </summary>
        static void Migrate()
        {
            if (migrated) return;
            migrated = true;
            if (PlayerPrefs.GetInt(ProgressV2Key, 0) == 1) return;
            int stars = 0, best = 0;
            for (int g = 1; g <= LevelPath.OldCount; g++)
            {
                var type = LevelPath.OldTypeOf(g);
                int n = LevelPath.OldTypeLevelOf(g);
                int s = PlayerPrefs.GetInt(OldStarsKey(type, n), 0);
                if (s > 0 && PlayerPrefs.GetInt(StarsKey(g), 0) < s) { PlayerPrefs.SetInt(StarsKey(g), Mathf.Clamp(s, 1, 3)); stars++; }
                float b = PlayerPrefs.GetFloat(OldBestKey(type, n), 0f);
                if (b > 0f && !PlayerPrefs.HasKey(BestKey(g))) { PlayerPrefs.SetFloat(BestKey(g), b); best++; }
            }
            PlayerPrefs.SetInt(ProgressV2Key, 1);
            PlayerPrefs.Save();
            Debug.Log("[Save] progress migrated to per-level keys: " + stars + " levels with stars, " + best + " best times");
        }

        // the old per-type keys: Classic "ap_stars_<n>" / "ap_best_<n>", others "ap_stars_<mode>_<n>" / "ap_best_<mode>_<n>"
        static string OldTag(GameMode mode) { return mode == GameMode.Shapes ? "shapes" : "mosaic"; }

        static string OldStarsKey(GameMode mode, int n)
        {
            return mode == GameMode.Classic ? "ap_stars_" + n : "ap_stars_" + OldTag(mode) + "_" + n;
        }

        static string OldBestKey(GameMode mode, int n)
        {
            return mode == GameMode.Classic ? "ap_best_" + n : "ap_best_" + OldTag(mode) + "_" + n;
        }
    }
}
