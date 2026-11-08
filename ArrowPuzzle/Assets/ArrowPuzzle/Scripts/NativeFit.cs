using System;
using System.Collections.Generic;
using System.Text;

namespace ArrowPuzzle
{
    /// <summary>
    /// Pure placement maths for the result-screen native ad (no Unity types, so it can be checked outside Unity:
    /// Ads.PlanNative uses it on device, "ArrowPuzzle/Native ad placement table" prints it for a list of phones).
    /// All heights in dp from the window top; layout positions in design px (390 = the window width).
    ///
    /// The medium template (googlemobileads-unity.aar, gnt_medium_template_view.xml) stacks, inside a 5 dp padding:
    /// media 190, 5 margin, icon / headline / Ad badge / rating row 60, body (wrap_content, 10 dp bottom padding,
    /// any number of lines), 5 margin, the INSTALL button (0dp height, min 35, its natural height is the button's
    /// 48 dp min height), 5 padding. The plugin fixes the root at 350 dp, so when the body needs more than about
    /// 2 lines the button is squeezed under its 35 dp minimum and pushed out of the bottom: cut in half. Ads now lets
    /// that root wrap its content, measures it, and asks for the measured height + SafetyDp.
    /// </summary>
    public static class NativeFit
    {
        public const float FrameWidthD = 342f, PadD = 7f; // frame width and ad padding, design px
        public const float GapDp = 12f;                   // bottom of the button row -> frame top
        public const float BottomMarginDp = 8f;           // frame bottom -> bottom inset (gesture bar / nav bar), at least
        public const float SafetyDp = 8f;                 // asked for on top of the medium ad's measured height
        public const float MediumNominalDp = 350f;        // the template's own root height (fits a 1 - 2 line body)
        public const float MediumPlanDp = MediumNominalDp + SafetyDp; // planning height before the real one is measured
        public const float MediumProbeMaxDp = 480f;       // the measuring pass never asks for more than this
        public const float SmallMarginDp = 10f;           // small template: 10 dp around its 4:1 block
        public const int CompactSteps = 4;                // 100 / 95 / 90 / 85 / 80 % spacing

        public enum Size { None, Small, Medium }

        public struct Result
        {
            public Size Size;
            public float FrameTopDp, FrameHDp, FrameWDp, AdTopDp, AdHDp, AdWDp, PadDp;
            public float FreeDp, MediumMaxDp, SmallDp, LimitDp, BottomMarginDp;
        }

        public static float DesignToDp(float d, float screenWDp) { return d * screenWDp / 390f; }

        /// <summary>Small card height for an ad width (dp): its 4:1 block + 2 x 10 dp.</summary>
        public static float SmallCardDp(float wDp) { return (wDp - 2f * SmallMarginDp) / 4f + 2f * SmallMarginDp; }

        /// <summary>
        /// One layout step. rowBottomD = bottom of the last button row (design px from the safe-area top).
        /// mediumDp = the medium height to fit (MediumPlanDp before the ad is measured).
        /// </summary>
        public static Result Evaluate(float screenWDp, float screenHDp, float safeTopDp, float bottomInsetDp,
                                      float rowBottomD, float mediumDp = MediumPlanDp)
        {
            var r = new Result();
            r.PadDp = DesignToDp(PadD, screenWDp);
            r.FrameWDp = DesignToDp(FrameWidthD, screenWDp);
            r.AdWDp = r.FrameWDp - 2f * r.PadDp;
            r.FrameTopDp = safeTopDp + DesignToDp(rowBottomD, screenWDp) + GapDp;
            r.AdTopDp = r.FrameTopDp + r.PadDp;
            r.LimitDp = screenHDp - bottomInsetDp - BottomMarginDp; // the frame bottom may not pass this
            r.FreeDp = r.LimitDp - r.FrameTopDp;
            r.MediumMaxDp = r.FreeDp - 2f * r.PadDp;                // tallest medium ad that keeps the frame inside
            r.SmallDp = SmallCardDp(r.AdWDp);
            if (r.MediumMaxDp >= mediumDp) r.Size = Size.Medium;
            else if (r.MediumMaxDp >= r.SmallDp) r.Size = Size.Small;
            else r.Size = Size.None;
            r.AdHDp = r.Size == Size.Medium ? mediumDp : r.Size == Size.Small ? r.SmallDp : 0f;
            r.FrameHDp = r.Size == Size.None ? 0f : r.AdHDp + 2f * r.PadDp;
            r.BottomMarginDp = (screenHDp - bottomInsetDp) - (r.FrameTopDp + r.FrameHDp);
            return r;
        }

        /// <summary>Medium first: normal spacing, then each compact step, until medium fits (as NativeAdSlot.Plan).</summary>
        public static Result Choose(float screenWDp, float screenHDp, float safeTopDp, float bottomInsetDp,
                                    Func<float, float> rowBottomAt, out int step)
        {
            var r = new Result();
            step = 0;
            for (int i = 0; i <= CompactSteps; i++)
            {
                step = i;
                r = Evaluate(screenWDp, screenHDp, safeTopDp, bottomInsetDp, rowBottomAt(i / (float)CompactSteps));
                if (r.Size == Size.Medium) break;
            }
            return r;
        }

        // ------------------------------------------------------------------ the check table

        /// <summary>A test screen: dp size, density, safe-area top (cutout) and bottom inset (gesture / nav bar).</summary>
        public struct Device
        {
            public string Name;
            public float W, H, Density, TopInset, BottomInset;
            public Device(string name, float w, float h, float density, float top, float bottom)
            {
                Name = name; W = w; H = h; Density = density; TopInset = top; BottomInset = bottom;
            }
        }

        /// <summary>
        /// The phones to check. Fullscreen with "render outside safe area": the safe-area top is the cutout (0 on 16:9
        /// phones without one), the bottom inset the gesture area (24 dp assumed; 0 for a 16:9 phone with hidden
        /// 3-button navigation).
        /// </summary>
        public static readonly Device[] Devices =
        {
            new Device("360x640", 360f, 640f, 2f, 0f, 0f),
            new Device("360x780", 360f, 780f, 3f, 32f, 24f),
            new Device("384x854", 384f, 854f, 2.8125f, 32f, 24f),
            new Device("393x852", 393f, 852f, 2.75f, 32f, 24f),
            new Device("411x914", 411f, 914f, 2.625f, 32f, 24f),
            new Device("412x915", 412f, 915f, 2.625f, 32f, 24f),
            new Device("600x960 tablet", 600f, 960f, 2f, 0f, 24f),
        };

        /// <summary>Button row bottoms (design px from the safe-area top) at compact amount t, per result screen.</summary>
        public static float WinRowBottom(float t) { return Lerp(346f, 286f, t) + 60f + 5f + 13f; } // PopupWin Home / NEXT LEVEL row (+ its 13 px extra gap)
        public static float LoseRowBottom(float t) { return Lerp(336f, 286f, t) + 54f + 4f; }  // PopupLose Home / TRY AGAIN row

        static float Lerp(float a, float b, float t) { return a + (b - a) * t; }

        /// <summary>One line per device and screen: template, compact step, frame / ad rect, insets, bottom margin.</summary>
        public static List<string> Table()
        {
            var lines = new List<string>();
            foreach (var d in Devices)
            {
                AddLine(lines, d, "Level complete", WinRowBottom);
                AddLine(lines, d, "Level failed", LoseRowBottom);
            }
            return lines;
        }

        static void AddLine(List<string> lines, Device d, string screen, Func<float, float> rowBottomAt)
        {
            int step;
            var r = Choose(d.W, d.H, d.TopInset, d.BottomInset, rowBottomAt, out step);
            var sb = new StringBuilder();
            sb.Append(d.Name.PadRight(15)).Append(screen.PadRight(15)).Append(r.Size.ToString().PadRight(7))
              .Append("step ").Append(step).Append(" (").Append(100 - 5 * step).Append(" %)");
            if (r.Size != Size.None)
                sb.Append("  frame y ").Append(F(r.FrameTopDp)).Append(" h ").Append(F(r.FrameHDp)).Append(" w ").Append(F(r.FrameWDp))
                  .Append("  ad y ").Append(F(r.AdTopDp)).Append(" h ").Append(F(r.AdHDp)).Append(" w ").Append(F(r.AdWDp));
            sb.Append("  medium max ").Append(F(r.MediumMaxDp))
              .Append("  bottom inset ").Append(F(d.BottomInset)).Append("  bottom margin ").Append(F(r.BottomMarginDp)).Append(" dp");
            lines.Add(sb.ToString());
        }

        static string F(float v) { return v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture); }
    }
}
