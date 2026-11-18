using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

// Editor-only half of the long-arrow tools (split from Scripts/LongLevelGen.cs, which now also runs on phones):
// the sample set and the PNG previews / contact sheets.
namespace ArrowPuzzle
{
    /// <summary>The sample set: generates, writes long_samples.txt and the PNG previews.</summary>
    public static class LongSamples
    {
        public struct Sample { public string Shape; public double D; public int Seed; }

        public static readonly Sample[] List =
        {
            new Sample { Shape = "heart", D = 0.15, Seed = 1001 },
            new Sample { Shape = "hourglass", D = 0.5, Seed = 2001 },
            new Sample { Shape = "paw", D = 1.0, Seed = 3001 },
            new Sample { Shape = "star", D = 1.0, Seed = 4001 },
            new Sample { Shape = "cat", D = 1.0, Seed = 5001 },
            new Sample { Shape = "fish", D = 1.0, Seed = 6001 },
            new Sample { Shape = "heart", D = 1.0, Seed = 7001 },
        };

        public const string LevelFile = "Assets/ArrowPuzzle/Resources/Levels/long_samples.txt"; // next to shapes.txt
        public const string PreviewDir = "LongPreview";

        /// <summary>projectRoot = the Unity project folder (parent of Assets). Returns the generated levels.</summary>
        public static List<LongLevel> Run(string projectRoot, Action<string, float> progress, out string text)
        {
            var levels = new List<LongLevel>();
            for (int i = 0; i < List.Length; i++)
            {
                if (progress != null) progress(List[i].Shape, i / (float)List.Length);
                levels.Add(LongLevelGen.Generate(List[i].Shape, List[i].D, List[i].Seed));
            }

            text = LongLevelGen.ToText(levels);
            File.WriteAllText(Path.Combine(projectRoot, LevelFile), text.Replace("\r", ""));

            string dir = Path.Combine(projectRoot, PreviewDir);
            Directory.CreateDirectory(dir);
            foreach (var old in Directory.GetFiles(dir)) File.Delete(old); // no stale previews from an older sample list
            var summary = new StringBuilder();
            for (int i = 0; i < levels.Count; i++)
            {
                var l = levels[i];
                string name = (i + 1) + "_" + l.Shape;
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), LongPreview.Png(l, 20));
                File.WriteAllText(Path.Combine(dir, name + ".txt"), LongLevelGen.StatLine(l) + "\n");
                summary.Append(i + 1).Append(". ").Append(LongLevelGen.StatLine(l)).Append('\n');
            }
            File.WriteAllText(Path.Combine(dir, "summary.txt"), summary.ToString());
            return levels;
        }

        // ------------------------------------------------------------------ all shapes (previews only)

        public const string AllDir = "LongPreview/all";
        public static readonly double[] AllDs = { 1.0, 0.5 };

        /// <summary>Seed base per shape index and d (fixed, so a rerun gives the same levels).</summary>
        public static int AllSeed(int shapeIndex, double d) { return 20001 + shapeIndex * 1000 + (d >= 1.0 ? 0 : 500); }

        /// <summary>
        /// Every registered shape at every d in AllDs: PNG per level in LongPreview/all, the d = 1 contact sheet
        /// (sheet_d1.png) and summary_all.txt. verify = the game's peel test on level text (ShapeLevels.VerifyText):
        /// level number -> null when valid, else the problem. Nothing is written to Resources.
        /// </summary>
        public static List<LongLevel> RunAll(string projectRoot, Action<string, float> progress,
                                             Func<string, IDictionary<int, string>> verify, out string summaryText)
        {
            LongShapes.LoadDefs(projectRoot);
            var names = new List<string>(ShapeLibrary.All);
            var levels = new List<LongLevel>();
            int total = names.Count * AllDs.Length, k = 0;
            for (int i = 0; i < names.Count; i++)
            {
                foreach (double d in AllDs)
                {
                    if (progress != null) progress(names[i] + " d=" + d.ToString(CultureInfo.InvariantCulture), k++ / (float)total);
                    levels.Add(LongLevelGen.Generate(names[i], d, AllSeed(i, d)));
                }
            }

            var check = verify != null ? verify(LongLevelGen.ToText(levels)) : null;

            string dir = Path.Combine(projectRoot, AllDir);
            Directory.CreateDirectory(dir);
            foreach (var old in Directory.GetFiles(dir)) File.Delete(old);
            var sheet = new List<LongLevel>();
            for (int i = 0; i < levels.Count; i++)
            {
                var l = levels[i];
                File.WriteAllBytes(Path.Combine(dir, l.Shape + "_" + DTag(l.D) + ".png"), LongPreview.Png(l, 20));
                if (l.D >= 1.0) sheet.Add(l);
            }
            File.WriteAllBytes(Path.Combine(dir, "sheet_d1.png"), LongPreview.Sheet(sheet, 7));

            var ic = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append(string.Format(ic, "{0,-11} {1,-4} {2,-6} {3,5} {4,6} {5,9} {6,6} {7,5} {8,6} {9,10} {10,6} {11,6} {12,-5} {13}\n",
                "shape", "d", "grid", "cells", "arrows", "avg/max", "free%", "depth", "splits", "seed(try)", "thin%", "ms", "peel", "gates failed"));
            int pass = 0, gatesOk = 0;
            for (int i = 0; i < levels.Count; i++)
            {
                var l = levels[i];
                string err = null;
                bool ok = check != null && check.TryGetValue(i + 1, out err) && err == null;
                if (ok) pass++;
                string gates = LongLevelGen.GateFailures(l);
                if (gates == "-") gatesOk++;
                sb.Append(string.Format(ic, "{0,-11} {1,-4} {2,-6} {3,5} {4,6} {5,9} {6,6:0.0} {7,5} {8,6} {9,10} {10,6:0.0} {11,6:0} {12,-5} {13}\n",
                    l.Shape, l.D.ToString("0.0#", ic), l.Cols + "x" + l.Rows, l.MaskCells, l.Count,
                    l.AvgLen.ToString("0.0", ic) + "/" + l.MaxLen, l.FreePct, l.Depth, l.Splits,
                    l.Seed + "(" + l.Tries + ")", LongLevelGen.ThinPct(l.Mask, l.Cols, l.Rows), l.Ms,
                    check == null ? "?" : ok ? "PASS" : "FAIL", gates + (ok || check == null ? "" : " | peel: " + (err ?? "missing"))));
            }
            sb.Append(string.Format(ic, "\n{0} levels: peel test {1}/{0} PASS, quality gates met {2}/{0}\n", levels.Count, pass, gatesOk));
            summaryText = sb.ToString();
            File.WriteAllText(Path.Combine(dir, "summary_all.txt"), summaryText);
            return levels;
        }

        static string DTag(double d) { return "d" + d.ToString("0.##", CultureInfo.InvariantCulture).Replace(".", ""); }
    }

    /// <summary>
    /// PNG preview: white background, faint grey mask cells, each arrow a 2 px line through its cell centres with a small
    /// arrowhead. Free at the start = green, others black.
    /// </summary>
    public static class LongPreview
    {
        const int Margin = 12;

        public static byte[] Png(LongLevel l, int cellPx)
        {
            int w, h;
            var img = Render(l, cellPx, Margin, 1.0, out w, out h);
            return Encode(w, h, img);
        }

        /// <summary>The preview as an RGB buffer (0..1), w x h px; lineHw = half line width in px.</summary>
        static double[] Render(LongLevel l, int cellPx, int margin, double lineHw, out int w, out int h)
        {
            w = l.Cols * cellPx + 2 * margin;
            h = l.Rows * cellPx + 2 * margin;
            var img = new double[w * h * 3]; // double, not float: Mono and .NET round float intermediates differently
            for (int i = 0; i < img.Length; i++) img[i] = 1.0;

            int inset = cellPx >= 8 ? 1 : 0;
            for (int r = 0; r < l.Rows; r++)
                for (int c = 0; c < l.Cols; c++)
                    if (l.Mask[r * l.Cols + c])
                        for (int y = inset; y < cellPx - inset; y++)
                            for (int x = inset; x < cellPx - inset; x++)
                                Blend(img, w, h, margin + c * cellPx + x, margin + r * cellPx + y, 0.945, 0.945, 0.945, 1.0);

            for (int i = 0; i < l.Count; i++)
            {
                bool free = l.FreeAtStart != null && l.FreeAtStart[i];
                double cr = free ? 0.10 : 0, cg = free ? 0.62 : 0, cb = free ? 0.25 : 0;
                var a = l.Arrows[i];
                if (a.Count < 2) continue;
                var pts = new List<double[]>();
                foreach (int cell in a)
                    pts.Add(new[] { margin + (cell % l.Cols + 0.5) * cellPx, margin + (cell / l.Cols + 0.5) * cellPx });
                var hp = pts[pts.Count - 1];
                var pp = pts[pts.Count - 2];
                double dx = hp[0] - pp[0], dy = hp[1] - pp[1], len = Math.Sqrt(dx * dx + dy * dy);
                dx /= len; dy /= len;
                for (int j = 1; j < pts.Count; j++) Segment(img, w, h, pts[j - 1], pts[j], lineHw, cr, cg, cb);
                double hs = cellPx;
                var apex = new[] { hp[0] + dx * 0.42 * hs, hp[1] + dy * 0.42 * hs };
                var bc = new[] { hp[0] - dx * 0.06 * hs, hp[1] - dy * 0.06 * hs };
                double hw = 0.24 * hs;
                Triangle(img, w, h, apex, new[] { bc[0] - dy * hw, bc[1] + dx * hw }, new[] { bc[0] + dy * hw, bc[1] - dx * hw }, cr, cg, cb);
            }
            return img;
        }

        // ------------------------------------------------------------------ contact sheet

        const int ThumbW = 186, ThumbH = 280, LabelH = 26, Gap = 10, TextScale = 2;

        /// <summary>All levels as thumbnails in a grid of columns, each with its shape name under it.</summary>
        public static byte[] Sheet(IList<LongLevel> levels, int columns)
        {
            int rowsN = (levels.Count + columns - 1) / columns;
            int cw = ThumbW + Gap, ch = ThumbH + LabelH + Gap;
            int w = columns * cw + Gap, h = rowsN * ch + Gap;
            var img = new double[w * h * 3];
            for (int i = 0; i < img.Length; i++) img[i] = 0.86;
            for (int n = 0; n < levels.Count; n++)
            {
                var l = levels[n];
                int ox = Gap + (n % columns) * cw, oy = Gap + (n / columns) * ch;
                Fill(img, w, ox, oy, ThumbW, ThumbH + LabelH, 1.0);
                const int pad = 4;
                int cell = Math.Max(2, Math.Min((ThumbW - 2 * pad) / l.Cols, (ThumbH - 2 * pad) / l.Rows));
                int tw, th;
                var t = Render(l, cell, pad, cell >= 6 ? 0.6 : 0.45, out tw, out th);
                int tx = ox + (ThumbW - tw) / 2, ty = oy + (ThumbH - th) / 2;
                for (int y = 0; y < th; y++)
                    for (int x = 0; x < tw; x++)
                    {
                        int px = tx + x, py = ty + y;
                        if (px < ox || py < oy || px >= ox + ThumbW || py >= oy + ThumbH) continue;
                        int si = (y * tw + x) * 3, di = (py * w + px) * 3;
                        img[di] = t[si]; img[di + 1] = t[si + 1]; img[di + 2] = t[si + 2];
                    }
                string label = l.Shape.ToUpperInvariant();
                int textW = label.Length * 6 * TextScale - TextScale;
                Text(img, w, h, ox + (ThumbW - textW) / 2, oy + ThumbH + (LabelH - 7 * TextScale) / 2, label, TextScale);
            }
            return Encode(w, h, img);
        }

        static void Fill(double[] img, int w, int x0, int y0, int fw, int fh, double v)
        {
            for (int y = y0; y < y0 + fh; y++)
                for (int x = x0; x < x0 + fw; x++)
                {
                    int i = (y * w + x) * 3;
                    img[i] = img[i + 1] = img[i + 2] = v;
                }
        }

        /// <summary>Text in a 5 x 7 pixel font (A-Z, space), each font pixel scale x scale, dark grey.</summary>
        static void Text(double[] img, int w, int h, int x0, int y0, string s, int scale)
        {
            for (int k = 0; k < s.Length; k++)
            {
                int gi = s[k] >= 'A' && s[k] <= 'Z' ? s[k] - 'A' : -1;
                if (gi < 0) continue;
                string g = Glyphs[gi];
                for (int row = 0; row < 7; row++)
                    for (int col = 0; col < 5; col++)
                    {
                        if (g[row * 5 + col] != '#') continue;
                        for (int sy = 0; sy < scale; sy++)
                            for (int sx = 0; sx < scale; sx++)
                                Blend(img, w, h, x0 + (k * 6 + col) * scale + sx, y0 + row * scale + sy, 0.15, 0.15, 0.15, 1.0);
                    }
            }
        }

        // 5 x 7 glyphs A-Z, rows top to bottom
        static readonly string[] Glyphs =
        {
            ".###.#...##...#######...##...##...#", "####.#...##...#####.#...##...#####.", ".###.#...##....#....#....#...#.###.",
            "####.#...##...##...##...##...#####.", "######....#....####.#....#....#####", "######....#....####.#....#....#....",
            ".###.#...##....#.####...##...#.###.", "#...##...##...#######...##...##...#", ".###...#....#....#....#....#...###.",
            "..###...#....#....#....#.#..#..##..", "#...##..#.#.#..##...#.#..#..#.#...#", "#....#....#....#....#....#....#####",
            "#...###.###.#.##.#.##...##...##...#", "#...##...###..##.#.##..###...##...#", ".###.#...##...##...##...##...#.###.",
            "####.#...##...#####.#....#....#....", ".###.#...##...##...##.#.##..#..##.#", "####.#...##...#####.#.#..#..#.#...#",
            ".#####....#.....###.....#....#####.", "#####..#....#....#....#....#....#..", "#...##...##...##...##...##...#.###.",
            "#...##...##...##...##...#.#.#...#..", "#...##...##...##.#.##.#.##.#.#.#.#.", "#...##...#.#.#...#...#.#.#...##...#",
            "#...##...#.#.#...#....#....#....#..", "#####....#...#...#...#...#....#####",
        };

        static void Blend(double[] img, int w, int h, int x, int y, double r, double g, double b, double a)
        {
            if (x < 0 || y < 0 || x >= w || y >= h || a <= 0) return;
            int i = (y * w + x) * 3;
            img[i] += (r - img[i]) * a;
            img[i + 1] += (g - img[i + 1]) * a;
            img[i + 2] += (b - img[i + 2]) * a;
        }

        /// <summary>Anti-aliased line of half width hw (coverage from the distance to the segment).</summary>
        static void Segment(double[] img, int w, int h, double[] a, double[] b, double hw, double r, double g, double bl)
        {
            int x0 = (int)Math.Floor(Math.Min(a[0], b[0]) - hw - 2), x1 = (int)Math.Ceiling(Math.Max(a[0], b[0]) + hw + 2);
            int y0 = (int)Math.Floor(Math.Min(a[1], b[1]) - hw - 2), y1 = (int)Math.Ceiling(Math.Max(a[1], b[1]) + hw + 2);
            double vx = b[0] - a[0], vy = b[1] - a[1], vv = Math.Max(1e-9, vx * vx + vy * vy);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    double px = x + 0.5 - a[0], py = y + 0.5 - a[1];
                    double t = Math.Max(0, Math.Min(1, (px * vx + py * vy) / vv));
                    double ex = px - vx * t, ey = py - vy * t;
                    double cov = hw + 0.5 - Math.Sqrt(ex * ex + ey * ey);
                    if (cov > 0) Blend(img, w, h, x, y, r, g, bl, Math.Min(1, cov));
                }
            }
        }

        /// <summary>Filled triangle, 4 x 4 supersampled.</summary>
        static void Triangle(double[] img, int w, int h, double[] p0, double[] p1, double[] p2, double r, double g, double b)
        {
            int x0 = (int)Math.Floor(Math.Min(p0[0], Math.Min(p1[0], p2[0]))), x1 = (int)Math.Ceiling(Math.Max(p0[0], Math.Max(p1[0], p2[0])));
            int y0 = (int)Math.Floor(Math.Min(p0[1], Math.Min(p1[1], p2[1]))), y1 = (int)Math.Ceiling(Math.Max(p0[1], Math.Max(p1[1], p2[1])));
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < 4; sy++)
                        for (int sx = 0; sx < 4; sx++)
                            if (InTri(x + (sx + 0.5) / 4, y + (sy + 0.5) / 4, p0, p1, p2)) hit++;
                    if (hit > 0) Blend(img, w, h, x, y, r, g, b, hit / 16.0);
                }
            }
        }

        static bool InTri(double x, double y, double[] a, double[] b, double[] c)
        {
            double d1 = (x - b[0]) * (a[1] - b[1]) - (a[0] - b[0]) * (y - b[1]);
            double d2 = (x - c[0]) * (b[1] - c[1]) - (b[0] - c[0]) * (y - c[1]);
            double d3 = (x - a[0]) * (c[1] - a[1]) - (c[0] - a[0]) * (y - a[1]);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        // ------------------------------------------------------------------ minimal PNG encoder (RGB 8 bit, zlib)

        static byte[] Encode(int w, int h, double[] img)
        {
            var raw = new byte[h * (w * 3 + 1)];
            int k = 0;
            for (int y = 0; y < h; y++)
            {
                raw[k++] = 0; // filter: none
                for (int x = 0; x < w * 3; x++)
                    raw[k++] = (byte)Math.Max(0, Math.Min(255, (int)Math.Round(img[y * w * 3 + x] * 255.0)));
            }

            byte[] deflated;
            using (var ms = new MemoryStream())
            {
                using (var ds = new DeflateStream(ms, CompressionLevel.Optimal, true)) ds.Write(raw, 0, raw.Length);
                deflated = ms.ToArray();
            }
            uint s1 = 1, s2 = 0;
            foreach (byte b in raw) { s1 = (s1 + b) % 65521; s2 = (s2 + s1) % 65521; }
            var z = new byte[deflated.Length + 6];
            z[0] = 0x78; z[1] = 0x9C;
            Buffer.BlockCopy(deflated, 0, z, 2, deflated.Length);
            uint adler = (s2 << 16) | s1;
            z[z.Length - 4] = (byte)(adler >> 24); z[z.Length - 3] = (byte)(adler >> 16);
            z[z.Length - 2] = (byte)(adler >> 8); z[z.Length - 1] = (byte)adler;

            using (var png = new MemoryStream())
            {
                png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
                var ihdr = new byte[13];
                Be(ihdr, 0, (uint)w); Be(ihdr, 4, (uint)h);
                ihdr[8] = 8; ihdr[9] = 2; // 8 bit, RGB
                Chunk(png, "IHDR", ihdr);
                Chunk(png, "IDAT", z);
                Chunk(png, "IEND", new byte[0]);
                return png.ToArray();
            }
        }

        static void Be(byte[] b, int o, uint v)
        {
            b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v;
        }

        static uint[] crcTable;

        static void Chunk(Stream s, string type, byte[] data)
        {
            var len = new byte[4];
            Be(len, 0, (uint)data.Length);
            s.Write(len, 0, 4);
            var td = new byte[4 + data.Length];
            for (int i = 0; i < 4; i++) td[i] = (byte)type[i];
            Buffer.BlockCopy(data, 0, td, 4, data.Length);
            s.Write(td, 0, td.Length);
            var crc = new byte[4];
            Be(crc, 0, Crc(td));
            s.Write(crc, 0, 4);
        }

        static uint Crc(byte[] d)
        {
            if (crcTable == null)
            {
                crcTable = new uint[256];
                for (uint n = 0; n < 256; n++)
                {
                    uint c = n;
                    for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                    crcTable[n] = c;
                }
            }
            uint x = 0xFFFFFFFFu;
            foreach (byte b in d) x = crcTable[(x ^ b) & 0xFF] ^ (x >> 8);
            return x ^ 0xFFFFFFFFu;
        }
    }
}
