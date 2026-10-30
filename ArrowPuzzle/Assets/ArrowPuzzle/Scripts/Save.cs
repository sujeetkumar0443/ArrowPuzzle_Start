using UnityEngine;

namespace ArrowPuzzle
{
    /// <summary>Tiny PlayerPrefs wrapper for progress and settings.</summary>
    public static class Save
    {
        public static int Unlocked
        {
            get { return Mathf.Max(1, PlayerPrefs.GetInt("ap_unlocked", 1)); }
            set { PlayerPrefs.SetInt("ap_unlocked", Mathf.Max(1, value)); PlayerPrefs.Save(); }
        }

        public static bool Sound
        {
            get { return PlayerPrefs.GetInt("ap_sound", 1) == 1; }
            set { PlayerPrefs.SetInt("ap_sound", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool Haptics
        {
            get { return PlayerPrefs.GetInt("ap_haptics", 1) == 1; }
            set { PlayerPrefs.SetInt("ap_haptics", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static int Hints
        {
            get { return PlayerPrefs.GetInt("ap_hints", 3); }
            set { PlayerPrefs.SetInt("ap_hints", Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        public static int Stars(int level)
        {
            return PlayerPrefs.GetInt("ap_stars_" + level, 0);
        }

        public static void SetStars(int level, int stars)
        {
            if (stars > Stars(level)) PlayerPrefs.SetInt("ap_stars_" + level, stars);
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------------ per mode (Classic = the keys above)

        /// <summary>Dev switch: every mode is playable. Set false before release.</summary>
        public const bool UnlockAllModes = true;

        static bool AllModes { get { return UnlockAllModes; } } // property, so "UnlockAllModes || x" gives no unreachable-code warning

        static string Tag(GameMode mode)
        {
            return mode == GameMode.Shapes ? "shapes" : "mosaic";
        }

        // (a method can't be called Unlocked next to the Unlocked property, hence Get/SetUnlocked)
        public static int GetUnlocked(GameMode mode)
        {
            if (mode == GameMode.Classic) return Unlocked;
            return Mathf.Max(1, PlayerPrefs.GetInt("ap_unlocked_" + Tag(mode), 1));
        }

        public static void SetUnlocked(GameMode mode, int value)
        {
            if (mode == GameMode.Classic) { Unlocked = value; return; }
            PlayerPrefs.SetInt("ap_unlocked_" + Tag(mode), Mathf.Max(1, value));
            PlayerPrefs.Save();
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

        /// <summary>Classic always; Shapes after 15 Classic levels; Mosaic after 10 Shapes levels.</summary>
        public static bool IsModeUnlocked(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.Shapes: return AllModes || GetUnlocked(GameMode.Classic) > 15;
                case GameMode.Mosaic: return AllModes || GetUnlocked(GameMode.Shapes) > 10;
                default: return true;
            }
        }

        /// <summary>Levels with at least one star over all modes (a win always saves 1 to 3 stars).</summary>
        public static int CompletedLevels()
        {
            int n = 0;
            for (int m = 0; m < ModeInfo.Count; m++)
                for (int l = 1; l <= ModeInfo.LevelsPerMode; l++)
                    if (Stars((GameMode)m, l) > 0) n++;
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
