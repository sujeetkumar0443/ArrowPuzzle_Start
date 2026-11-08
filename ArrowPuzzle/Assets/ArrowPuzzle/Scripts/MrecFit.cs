using System;
using System.Collections.Generic;
using System.Text;

namespace ArrowPuzzle
{
    /// <summary>
    /// Pure placement maths for the 300x250 dp MREC on the three result popups (no Unity types: MrecSlot uses it on
    /// device, "ArrowPuzzle/MREC placement table" prints it for a list of phones). Layout values are design px
    /// (390 = the window width) from the safe-area top; the ad itself is always 300x250 dp, so in design px it is
    /// 300 x 390 / widthDp wide (325 on a 360 dp phone, 285 on a 411 dp one). The ad never shrinks: the popup's
    /// gaps / compact layout give way first, and when even that is not enough there is no ad (no_fit).
    ///
    /// The frame around the ad: a header strip of HeaderD on top ("Ad" tag + "ADVERTISEMENT"), PadD on the sides and
    /// the bottom. AdTopD is the ad's own top (the design y values); the frame starts HeaderD above it.
    /// </summary>
    public static class MrecFit
    {
        public const float AdWDp = 300f, AdHDp = 250f;   // IAB medium rectangle
        public const float PadD = 7f;                    // frame padding left / right / bottom, design px
        public const float HeaderD = 16f;                // frame header (labels) above the ad, design px
        public const float BottomMarginD = 8f;           // frame bottom -> bottom limit (above the nav / gesture bar)
        public const float BonusGapMinD = 24f;           // Bonus Spin: "No thanks" bottom -> frame top, at least

        // the design positions (390 x 844 reference, design px from the safe-area top)
        public const float BonusAdTopD = 564f;
        public const float WinAdTopD = 444f;
        public const float LoseAdTopD = 412f;

        public struct Result
        {
            public bool Fits;      // false = no ad on this screen (layout at T = 0)
            public float T;        // the popup's compact amount (0 = normal .. 1 = compact)
            public float AdTopD, AdWD, AdHD, FrameTopD, FrameBottomD, AvailD, GapD;
            public string Why;     // why it does not fit (null when it fits)
        }

        public static float DpToDesign(float dp, float screenWDp) { return dp * 390f / Math.Max(1f, screenWDp); }

        static float Lerp(float a, float b, float t) { return a + (b - a) * t; }
        static float Clamp01(float t) { return t < 0f ? 0f : t > 1f ? 1f : t; }

        static Result Start(float availD, float screenWDp)
        {
            return new Result { AvailD = availD, AdWD = DpToDesign(AdWDp, screenWDp), AdHD = DpToDesign(AdHDp, screenWDp) };
        }

        /// <summary>Lowest allowed ad top: the frame bottom (ad + PadD) stays BottomMarginD above the bottom limit.</summary>
        static float MaxTop(Result r) { return r.AvailD - BottomMarginD - PadD - r.AdHD; }

        static void Frame(ref Result r)
        {
            r.FrameTopD = r.AdTopD - HeaderD;
            r.FrameBottomD = r.AdTopD + r.AdHD + PadD;
        }

        /// <summary>
        /// Level complete / Level failed: the ad sits under the button row and keeps its gap to it, so its top moves
        /// with the row: adTopD at T = 0, adTopCompactD at T = 1. T is the smallest compact amount that fits.
        /// availD = safe-area top -> bottom limit, design px.
        /// </summary>
        public static Result UnderRow(float availD, float screenWDp, float adTopD, float adTopCompactD)
        {
            var r = Start(availD, screenWDp);
            float maxTop = MaxTop(r);
            float t = adTopD <= maxTop ? 0f : (adTopD - maxTop) / Math.Max(0.001f, adTopD - adTopCompactD);
            r.Fits = t <= 1f;
            r.T = Clamp01(t);
            r.AdTopD = Lerp(adTopD, adTopCompactD, r.T);
            Frame(ref r);
            r.GapD = availD - r.FrameBottomD;
            if (!r.Fits)
            {
                r.Why = "needs " + (adTopCompactD + r.AdHD + PadD + BottomMarginD).ToString("0") + " design px at the compact layout, has " + availD.ToString("0");
                r.T = 0f;
            }
            return r;
        }

        /// <summary>
        /// Bonus Spin: the ad top is BonusAdTopD (or higher when the bottom forces it). The gap from "No thanks" to the
        /// frame may shrink down to BonusGapMinD; below that the wheel group compacts (T up to 1). noBottomD /
        /// noBottomCompactD = the bottom of "No thanks" at T = 0 / 1.
        /// </summary>
        public static Result Bonus(float availD, float screenWDp, float adTopD, float noBottomD, float noBottomCompactD)
        {
            var r = Start(availD, screenWDp);
            r.AdTopD = Math.Min(adTopD, MaxTop(r));
            Frame(ref r);
            float limit = r.FrameTopD - BonusGapMinD; // "No thanks" bottom may not pass this
            float t = noBottomD <= limit ? 0f : (noBottomD - limit) / Math.Max(0.001f, noBottomD - noBottomCompactD);
            r.Fits = t <= 1f;
            r.T = Clamp01(t);
            r.GapD = r.FrameTopD - Lerp(noBottomD, noBottomCompactD, r.T);
            if (!r.Fits)
            {
                r.Why = "the gap under No thanks would be " + r.GapD.ToString("0") + " design px at the compact layout (min " + BonusGapMinD + ")";
                r.T = 0f;
            }
            return r;
        }

        // ------------------------------------------------------------------ the check table

        /// <summary>A test screen: dp size, safe-area top (cutout) and bottom inset (gesture / nav bar), all dp.</summary>
        public struct Device
        {
            public string Name;
            public float W, H, TopInset, BottomInset;
            public Device(string name, float w, float h, float top, float bottom) { Name = name; W = w; H = h; TopInset = top; BottomInset = bottom; }
            public float AvailD { get { return (H - TopInset - BottomInset) * 390f / W; } }
        }

        /// <summary>The phones to check (fullscreen; 24 dp gesture area assumed, 0 for a 16:9 phone with hidden nav).</summary>
        public static readonly Device[] Devices =
        {
            new Device("360x640", 360f, 640f, 0f, 0f),
            new Device("360x780", 360f, 780f, 32f, 24f),
            new Device("384x854", 384f, 854f, 32f, 24f),
            new Device("390x844 ref", 390f, 844f, 47f, 34f),
            new Device("393x852", 393f, 852f, 32f, 24f),
            new Device("411x914", 411f, 914f, 32f, 24f),
            new Device("412x915", 412f, 915f, 32f, 24f),
            new Device("600x960 tablet", 600f, 960f, 0f, 24f),
        };

        // the popups' own numbers (mirrored here for the table; the popups pass theirs at run time)
        const float WinRowShift = 346f - 286f;   // PopupWin NextTop - CNextTop
        const float LoseRowShift = 336f - 286f;  // PopupLose RowTop - CRowTop
        const float NoBottom = 532f + 22f, NoBottomCompact = 385f + 22f; // PopupBonusSpin NoY / CNoY + NoThanksH / 2

        public static List<string> Table()
        {
            var lines = new List<string>();
            foreach (var d in Devices)
            {
                float a = d.AvailD;
                Add(lines, d, "Bonus Spin", Bonus(a, d.W, BonusAdTopD, NoBottom, NoBottomCompact));
                Add(lines, d, "Level complete", UnderRow(a, d.W, WinAdTopD, WinAdTopD - WinRowShift));
                Add(lines, d, "Level failed", UnderRow(a, d.W, LoseAdTopD, LoseAdTopD - LoseRowShift));
            }
            return lines;
        }

        static void Add(List<string> lines, Device d, string screen, Result r)
        {
            var sb = new StringBuilder();
            sb.Append(d.Name.PadRight(16)).Append(screen.PadRight(16)).Append(r.Fits ? "FITS   " : "NO FIT ")
              .Append("t ").Append(F(r.T)).Append("  avail ").Append(F(r.AvailD))
              .Append("  ad ").Append(F(r.AdWD)).Append("x").Append(F(r.AdHD)).Append(" at y ").Append(F(r.AdTopD))
              .Append("  frame ").Append(F(r.FrameTopD)).Append("..").Append(F(r.FrameBottomD))
              .Append(screen == "Bonus Spin" ? "  gap under No thanks " : "  room under frame ").Append(F(r.GapD));
            if (!r.Fits) sb.Append("  (").Append(r.Why).Append(")");
            lines.Add(sb.ToString());
        }

        static string F(float v) { return v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture); }
    }
}
