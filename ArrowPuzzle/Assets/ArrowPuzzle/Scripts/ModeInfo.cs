using UnityEngine;

namespace ArrowPuzzle
{
    public enum GameMode { Classic, Shapes, Mosaic }

    /// <summary>Display data for each level type: ModeInfo.Get(mode).Name, .Color, ... The player no longer picks one (LevelPath).</summary>
    public static class ModeInfo
    {
        public const int LevelsPerMode = 50;

        public class Entry
        {
            public string Name, Tagline;
            public Color Color, Edge, Tint; // Tint = Color at low alpha, for backgrounds
        }

        static Entry[] table;

        static Entry[] Table
        {
            get
            {
                if (table == null)
                {
                    table = new[]
                    {
                        Make("Classic", "Quick and relaxed", Theme.Teal, Theme.TealShadow, 0.18f),
                        Make("Shapes", "Fill the shape with arrows", Theme.ModeGold, Theme.ModeGoldEdge, 0.22f),
                        Make("Mosaic", "Huge packed boards, plan ahead", Theme.Accent, Theme.AccentShadow, 0.16f),
                    };
                }
                return table;
            }
        }

        static Entry Make(string name, string tagline, Color color, Color edge, float tintAlpha)
        {
            var tint = color;
            tint.a = tintAlpha;
            return new Entry { Name = name, Tagline = tagline, Color = color, Edge = edge, Tint = tint };
        }

        public static Entry Get(GameMode mode)
        {
            return Table[(int)mode];
        }

        public static int Count { get { return Table.Length; } }
    }
}
