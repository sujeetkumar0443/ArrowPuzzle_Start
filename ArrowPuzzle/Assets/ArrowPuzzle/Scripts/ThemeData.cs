using UnityEngine;

namespace ArrowPuzzle
{
    /// <summary>How arrow colours are picked from a theme's Lines.</summary>
    public enum LineMode { Random, Zones }

    /// <summary>
    /// One theme's tokens. Theme.Apply copies them into Theme's static colour fields (and derives the secondary greys).
    /// Fixed colours (coin gold, success green, error red, hint gold, Skip purple) are not part of a theme.
    /// </summary>
    public sealed class ThemeData
    {
        public string Id, Name;
        public Color BgTop, BgBottom;      // equal = solid background
        public Color Card, Ink, InkSoft;
        public Color Accent, AccentDark, AccentText;
        public Color Panel, PanelEdge, PanelText;
        public Color Tile;
        public Color[] Lines;
        public LineMode LineMode;
        public bool Glow;
        public bool Dark;                  // light text on dark surfaces (derived greys go darker, not lighter)

        // Step 7 (Themes screen): how a theme is obtained
        public bool OwnedByDefault;
        public int PriceCoins;             // 0 = not sold for coins
        public bool UnlockByAd;            // one rewarded ad

        public bool Gradient { get { return BgTop != BgBottom; } }
    }

    /// <summary>The theme table. Default (and the Splash, always) = cream.</summary>
    public static class Themes
    {
        public const string Default = "cream";

        static Color H(string hex) { return Theme.Hex(hex); }

        static ThemeData[] all;

        public static ThemeData[] All
        {
            get
            {
                if (all != null) return all;
                all = new[]
                {
                    new ThemeData
                    {
                        Id = "paper", Name = "Paper", BgTop = H("#FFFFFF"), BgBottom = H("#FFFFFF"), Card = H("#FFFFFF"),
                        Ink = H("#1B2430"), InkSoft = H("#6B7684"), Accent = H("#E4572E"), AccentDark = H("#B23F1D"), AccentText = H("#FFFFFF"),
                        Panel = H("#1B2430"), PanelEdge = H("#0E131B"), PanelText = H("#FFFFFF"), Tile = H("#F1F2F4"),
                        Lines = new[] { H("#161B26") }, LineMode = LineMode.Random, OwnedByDefault = true,
                    },
                    new ThemeData
                    {
                        Id = "cream", Name = "Cream", BgTop = H("#F4EFE6"), BgBottom = H("#F4EFE6"), Card = H("#FFFFFF"),
                        Ink = H("#26323F"), InkSoft = H("#5A6675"), Accent = H("#E4572E"), AccentDark = H("#B23F1D"), AccentText = H("#FFFFFF"),
                        Panel = H("#26323F"), PanelEdge = H("#12181F"), PanelText = H("#FFFFFF"), Tile = H("#F5F0E6"),
                        Lines = new[] { H("#E4572E"), H("#2EC4B6"), H("#FFB703"), H("#9B5DE5"), H("#3B82F6"), H("#3DBB5E") },
                        LineMode = LineMode.Random, OwnedByDefault = true,
                    },
                    new ThemeData
                    {
                        Id = "night", Name = "Night", BgTop = H("#141A38"), BgBottom = H("#0B0F26"), Card = H("#1B2250"),
                        Ink = H("#EAF0FF"), InkSoft = H("#8C98C4"), Accent = H("#3DE6FF"), AccentDark = H("#1B9DB5"), AccentText = H("#0B0F26"),
                        Panel = H("#1E2650"), PanelEdge = H("#0A0E24"), PanelText = H("#EAF0FF"), Tile = H("#232B5C"),
                        Lines = new[] { H("#3DE6FF"), H("#FF5FB2"), H("#B8FF5C"), H("#FFD23F"), H("#A78BFF") },
                        LineMode = LineMode.Random, Glow = true, Dark = true, PriceCoins = 500,
                    },
                    new ThemeData
                    {
                        Id = "ocean", Name = "Ocean", BgTop = H("#EAF5FF"), BgBottom = H("#CFE6FF"), Card = H("#FFFFFF"),
                        Ink = H("#17306B"), InkSoft = H("#4D6AA8"), Accent = H("#1E6BE6"), AccentDark = H("#154DA8"), AccentText = H("#FFFFFF"),
                        Panel = H("#17306B"), PanelEdge = H("#0D1D45"), PanelText = H("#FFFFFF"), Tile = H("#E6F0FB"),
                        Lines = new[] { H("#17306B"), H("#17306B"), H("#1E6BE6") }, LineMode = LineMode.Random, PriceCoins = 300,
                    },
                    new ThemeData
                    {
                        Id = "sunset", Name = "Sunset", BgTop = H("#FFF3E8"), BgBottom = H("#FFD9C2"), Card = H("#FFFFFF"),
                        Ink = H("#4A2A5E"), InkSoft = H("#8A5F94"), Accent = H("#F26B3A"), AccentDark = H("#B94A22"), AccentText = H("#FFFFFF"),
                        Panel = H("#4A2A5E"), PanelEdge = H("#2C1639"), PanelText = H("#FFFFFF"), Tile = H("#FBEBDD"),
                        Lines = new[] { H("#4A2A5E"), H("#4A2A5E"), H("#7A3FA0"), H("#F26B3A") }, LineMode = LineMode.Random, PriceCoins = 300,
                    },
                    new ThemeData
                    {
                        Id = "zones", Name = "Zones", BgTop = H("#FFFFFF"), BgBottom = H("#FFFFFF"), Card = H("#FFFFFF"),
                        Ink = H("#1B2430"), InkSoft = H("#6B7684"), Accent = H("#1677E8"), AccentDark = H("#0E55B0"), AccentText = H("#FFFFFF"),
                        Panel = H("#1B2430"), PanelEdge = H("#0E131B"), PanelText = H("#FFFFFF"), Tile = H("#F1F2F4"),
                        Lines = new[] { H("#161B26") }, LineMode = LineMode.Zones, UnlockByAd = true,
                    },
                };
                return all;
            }
        }

        /// <summary>The theme with this id, or cream when unknown.</summary>
        public static ThemeData Get(string id)
        {
            foreach (var t in All) if (t.Id == id) return t;
            foreach (var t in All) if (t.Id == Default) return t;
            return All[0];
        }

        public static bool Exists(string id)
        {
            foreach (var t in All) if (t.Id == id) return true;
            return false;
        }

        // ------------------------------------------------------------------ arrow colours

        static readonly Color ZonePink = H("#FF2E93"), ZoneYellow = H("#FFB703"), ZoneGreen = H("#7ED321"),
                              ZoneCyan = H("#18B5F0"), ZoneInk = H("#161B26");

        /// <summary>
        /// Colour of arrow index i of level `level` in theme t. Random: picked from t.Lines by a hash of (level, i), so it
        /// never changes during a level and is the same on every device. Zones: by the arrow's centre (x right, y DOWN,
        /// both 0..1 over the grid): y &lt; 0.38: pink (x &lt; 0.5) / yellow; y &lt; 0.58: green (|x - 0.5| &lt; 0.2) / cyan;
        /// else ink.
        /// </summary>
        public static Color ArrowColor(ThemeData t, int level, int i, float cx, float cyDown)
        {
            if (t.LineMode == LineMode.Zones)
            {
                if (cyDown < 0.38f) return cx < 0.5f ? ZonePink : ZoneYellow;
                if (cyDown < 0.58f) return Mathf.Abs(cx - 0.5f) < 0.2f ? ZoneGreen : ZoneCyan;
                return ZoneInk;
            }
            var lines = t.Lines;
            if (lines == null || lines.Length == 0) return t.Ink;
            if (lines.Length == 1) return lines[0];
            unchecked
            {
                uint h = (uint)level * 2654435761u ^ (uint)(i + 1) * 2246822519u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return lines[(int)(h % (uint)lines.Length)];
            }
        }
    }
}
