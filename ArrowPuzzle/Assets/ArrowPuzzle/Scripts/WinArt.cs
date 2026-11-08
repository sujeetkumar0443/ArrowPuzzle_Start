using UnityEngine;

namespace ArrowPuzzle
{
    /// <summary>
    /// Generated sprites for PopupWin (Level complete) and PopupLose (its sad stars: StarGrey, SleepyEye, SadMouth,
    /// Tear): the half wheel, the highlight wedge, the pointer, the white
    /// half hub, the rounded smiley / sleepy star bodies, the smile, the closed eye, the sparkle and a smooth disc.
    /// Like Theme's procedural sprites: drawn once in code into a Texture2D, 4 x 4 supersampled (adaptive: 2 x 2 first,
    /// all 16 samples only where the samples differ, i.e. on edges), then cached for the app's lifetime.
    /// Wheel parts are in design px, drawn at Res texture px per design px; origin = hub centre on the diameter, y up.
    /// </summary>
    public static class WinArt
    {
        // ---- wheel geometry (design px)
        public const float WheelR = 115f, Ring = 8f, Gap = 1.5f, StudD = 7f, BaseH = 10f, BaseOver = 8f;
        public const float WheelOuter = WheelR + Ring;                 // 123
        public const float WheelHalfW = WheelOuter + BaseOver;         // 131: half width of the base strip
        public const float HubR = 32f, HubEdge = 3f;
        public const int Slices = 7;
        public const float SliceDeg = 180f / Slices;
        public const float GlowHalfW = 26f;                            // highlight wedge box: x +-26, y 0..WheelR
        public const float Res = 2f;                                   // texture px per design px (wheel parts)
        // pointer: white piece pointing up from the hub, 3 px dark outline; tip at 69 = 60 % of the radius
        public const float PtrOutline = 3f, PtrHalfW = 8f + PtrOutline, PtrBottom = -4f - PtrOutline, PtrTop = 66f + PtrOutline;
        // star sprite box in units of the star size (star centre = origin): x -0.55..0.55, y -0.54..0.56
        public const float StarBox = 1.1f, StarBoxCY = 0.01f;

        public static readonly Color[] SliceColor =
        {
            Theme.Hex("#5C6BC0"), Theme.Hex("#F15BB5"), Theme.Hex("#9B5DE5"), Theme.Hex("#FFB703"),
            Theme.Hex("#9B5DE5"), Theme.Hex("#F15BB5"), Theme.Hex("#5C6BC0"),
        };

        static readonly Color Ink = Theme.Hex("#26323F");
        static readonly Color White = Color.white;
        static readonly Color Clear = new Color(1f, 1f, 1f, 0f);
        static readonly Color GoldBack = Theme.Hex("#C98410"), GoldBody = Theme.Hex("#FFB703"), GoldInner = Theme.Hex("#FFC82E");
        static readonly Color GreyBack = Theme.Hex("#A9A294"), GreyBody = Theme.Hex("#D9D2C4");
        static readonly Color Tongue = Theme.Hex("#FF7A90");
        static readonly Color GoldRing = Theme.Hex("#FFC82E"), GoldEdge = Theme.Hex("#8A5A0A"); // Bonus Spin rim

        static Sprite wheel, glow, pointer, hub, disc, starGold, starGrey, smile, sleepyEye, sparkle;

        public static Sprite Wheel { get { return wheel != null ? wheel : (wheel = Render(-WheelHalfW, -BaseH, 2f * WheelHalfW, WheelOuter + BaseH, Res, ShadeWheel, false)); } }
        public static Sprite Glow { get { return glow != null ? glow : (glow = Render(-GlowHalfW, 0f, 2f * GlowHalfW, WheelR, Res, ShadeGlow, false)); } }
        public static Sprite Pointer { get { return pointer != null ? pointer : (pointer = Render(-PtrHalfW, PtrBottom, 2f * PtrHalfW, PtrTop - PtrBottom, 3f, ShadePointer, false)); } }
        public static Sprite Hub { get { return hub != null ? hub : (hub = Render(-(HubR + HubEdge), 0f, 2f * (HubR + HubEdge), HubR + HubEdge, Res, ShadeHub, false)); } }
        public static Sprite Disc { get { return disc != null ? disc : (disc = Render(-1f, -1f, 2f, 2f, 64f, ShadeDisc, true)); } }
        public static Sprite StarGold { get { return starGold != null ? starGold : (starGold = Render(-0.55f, -0.54f, StarBox, StarBox, 192f / StarBox, ShadeStarGold, true)); } }
        public static Sprite StarGrey { get { return starGrey != null ? starGrey : (starGrey = Render(-0.55f, -0.54f, StarBox, StarBox, 192f / StarBox, ShadeStarGrey, true)); } }
        /// <summary>Open smile, box 2 : 1 (dark lower half disc, pink tongue). Colours baked: use a white Image.</summary>
        public static Sprite Smile { get { return smile != null ? smile : (smile = Render(-0.5f, -0.5f, 1f, 0.5f, 128f, ShadeSmile, true)); } }
        /// <summary>Closed sleepy eye (a lower arc with round ends), box 2 : 1, white: tint it.</summary>
        public static Sprite SleepyEye { get { return sleepyEye != null ? sleepyEye : (sleepyEye = Render(-0.5f, -0.5f, 1f, 0.5f, 128f, ShadeSleepyEye, true)); } }
        /// <summary>4-point sparkle (concave sides), square, white: tint it.</summary>
        public static Sprite Sparkle { get { return sparkle != null ? sparkle : (sparkle = Render(-1f, -1f, 2f, 2f, 48f, ShadeSparkle, true)); } }
        /// <summary>Sad mouth (an upper arc with round ends) for PopupLose's stars, box 2 : 1, white: tint it.</summary>
        public static Sprite SadMouth { get { return sadMouth != null ? sadMouth : (sadMouth = Render(-0.5f, -0.5f, 1f, 0.5f, 128f, ShadeSadMouth, true)); } }
        /// <summary>Tear drop (point up), box 1 : 1.4, white: tint it.</summary>
        public static Sprite Tear { get { return tear != null ? tear : (tear = Render(-0.5f, -0.7f, 1f, 1.4f, 64f, ShadeTear, true)); } }
        static Sprite sadMouth, tear;
        /// <summary>The Bonus Spin wheel: same box and geometry as Wheel, gold rim, no studs (PopupBonusSpin adds blinking bulbs).</summary>
        public static Sprite WheelGold { get { return wheelGold != null ? wheelGold : (wheelGold = Render(-WheelHalfW, -BaseH, 2f * WheelHalfW, WheelOuter + BaseH, Res, ShadeWheelGold, false)); } }
        /// <summary>"No thanks" pill, NoThanksW x NoThanksH design px: white 16 % fill and a 2 px white 55 % outline (colours baked).</summary>
        public static Sprite NoThanksPill { get { return noThanks != null ? noThanks : (noThanks = Render(-NoThanksW * 0.5f, -NoThanksH * 0.5f, NoThanksW, NoThanksH, 3f, ShadeNoThanks, false)); } }
        public const float NoThanksW = 170f, NoThanksH = 44f, NoThanksLine = 2f;
        static Sprite wheelGold, noThanks;

        // ------------------------------------------------------------------ rasteriser

        delegate Color Shade(float x, float y);

        /// <summary>
        /// Texture of the box (x0, y0, w, h) in shape units at pxPerUnit, straight alpha. Each pixel: 2 x 2 samples of
        /// the 4 x 4 grid first; when they all agree that is the pixel, otherwise all 16 are averaged (premultiplied).
        /// </summary>
        static Sprite Render(float x0, float y0, float w, float h, float pxPerUnit, Shade f, bool mips)
        {
            int tw = Mathf.Max(2, Mathf.CeilToInt(w * pxPerUnit)), th = Mathf.Max(2, Mathf.CeilToInt(h * pxPerUnit));
            float ux = w / tw, uy = h / th; // units per texture px (the texture covers the box exactly)
            var px = new Color32[tw * th];
            const int ss = 4;
            var s = new Color[ss * ss];
            for (int y = 0; y < th; y++)
            {
                for (int x = 0; x < tw; x++)
                {
                    float bx = x0 + x * ux, by = y0 + y * uy;
                    // corners of the 4 x 4 grid
                    s[0] = f(bx + 0.125f * ux, by + 0.125f * uy);
                    s[3] = f(bx + 0.875f * ux, by + 0.125f * uy);
                    s[12] = f(bx + 0.125f * ux, by + 0.875f * uy);
                    s[15] = f(bx + 0.875f * ux, by + 0.875f * uy);
                    Color c;
                    if (Same(s[0], s[3]) && Same(s[0], s[12]) && Same(s[0], s[15])) c = s[0];
                    else
                    {
                        float r = 0f, g = 0f, b = 0f, a = 0f;
                        for (int j = 0; j < ss; j++)
                        {
                            for (int i = 0; i < ss; i++)
                            {
                                int k = j * ss + i;
                                if (k != 0 && k != 3 && k != 12 && k != 15)
                                    s[k] = f(bx + (i + 0.5f) / ss * ux, by + (j + 0.5f) / ss * uy);
                                var q = s[k];
                                r += q.r * q.a; g += q.g * q.a; b += q.b * q.a; a += q.a;
                            }
                        }
                        c = a > 0f ? new Color(r / a, g / a, b / a, a / (ss * ss)) : Clear;
                    }
                    px[y * tw + x] = c;
                }
            }
            var tex = new Texture2D(tw, th, TextureFormat.RGBA32, mips);
            tex.filterMode = mips ? FilterMode.Trilinear : FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(px);
            tex.Apply(mips, true); // upload, then free the CPU copy
            return Sprite.Create(tex, new Rect(0, 0, tw, th), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        static bool Same(Color a, Color b)
        {
            return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
        }

        // ------------------------------------------------------------------ wheel parts (design px)

        static Color ShadeWheel(float x, float y) { return WheelColor(x, y, false); }

        /// <summary>The Bonus Spin wheel: gold rim (#FFC82E with #8A5A0A edge lines), #8A5A0A base, no studs (bulbs are Images).</summary>
        static Color ShadeWheelGold(float x, float y) { return WheelColor(x, y, true); }

        static Color WheelColor(float x, float y, bool gold)
        {
            // white studs on the ring centre line at every slice boundary (8), over the ring and the base
            float sr = StudD * 0.5f, rc = WheelR + Ring * 0.5f;
            if (!gold && y >= -sr)
            {
                for (int b = 0; b <= Slices; b++)
                {
                    float a = (180f - b * SliceDeg) * Mathf.Deg2Rad;
                    float dx = x - Mathf.Cos(a) * rc, dy = y - Mathf.Sin(a) * rc;
                    if (dx * dx + dy * dy <= sr * sr) return White;
                }
            }
            if (y >= 0f)
            {
                float r = Mathf.Sqrt(x * x + y * y);
                if (r <= WheelR)
                {
                    float deg = Mathf.Atan2(y, x) * Mathf.Rad2Deg; // 0 (right) .. 180 (left)
                    int k = Mathf.Clamp(Mathf.FloorToInt((180f - deg) / SliceDeg), 0, Slices - 1);
                    // white gap: within Gap / 2 of one of the slice's inner boundary rays
                    for (int b = k; b <= k + 1; b++)
                    {
                        if (b < 1 || b > Slices - 1) continue;
                        float a = (180f - b * SliceDeg) * Mathf.Deg2Rad;
                        if (Mathf.Abs(x * Mathf.Sin(a) - y * Mathf.Cos(a)) < Gap * 0.5f) return White;
                    }
                    return SliceColor[k];
                }
                if (r <= WheelOuter) return !gold ? Ink : r <= WheelR + 1.5f || r >= WheelOuter - 2f ? GoldEdge : GoldRing;
            }
            // flat base strip under the half circle: y -BaseH .. 0, rounded ends
            if (InRoundRect(x, y + BaseH * 0.5f, WheelHalfW, BaseH * 0.5f, BaseH * 0.5f)) return gold ? GoldEdge : Ink;
            return Clear;
        }

        /// <summary>White wedge of one slice, centred on straight up (rotate it about the hub), gaps kept out.</summary>
        static Color ShadeGlow(float x, float y)
        {
            if (y < 0f || x * x + y * y > WheelR * WheelR) return Clear;
            float h = SliceDeg * 0.5f * Mathf.Deg2Rad;
            float sin = Mathf.Sin(h), cos = Mathf.Cos(h);
            // signed distances to the two boundary rays (positive = inside the wedge)
            float dRight = x * -cos + y * sin;  // ray at +h from vertical (right side)
            float dLeft = x * cos + y * sin;    // ray at -h (left side)
            return dRight >= Gap * 0.5f && dLeft >= Gap * 0.5f ? White : Clear;
        }

        static readonly Vector2[] PtrPoly =
        {
            new Vector2(-8f, -4f), new Vector2(8f, -4f), new Vector2(6f, 52f), new Vector2(0f, 66f), new Vector2(-6f, 52f),
        };

        static Color ShadePointer(float x, float y)
        {
            float d = SdPoly(PtrPoly, x, y);
            if (d <= 0f) return White;
            if (d <= PtrOutline) return Ink;
            return Clear;
        }

        static Color ShadeHub(float x, float y)
        {
            if (y < 0f) return Clear;
            float r2 = x * x + y * y;
            if (r2 <= HubR * HubR) return White;
            if (r2 <= (HubR + HubEdge) * (HubR + HubEdge)) return Ink;
            return Clear;
        }

        static Color ShadeDisc(float x, float y)
        {
            return x * x + y * y <= 1f ? White : Clear;
        }

        // ------------------------------------------------------------------ stars (units of the star size)

        const float StarRound = 0.1f;                  // corner radius (10 % of the size)
        const float StarRo = 0.526f - StarRound;       // sharp core star: outer radius (+ rounding = 0.526, width ~1.0)
        const float StarRi = StarRo * 0.36f;           // inner (valley) radius of the core
        static readonly Vector2[] StarPoly = MakeStar(StarRo, StarRi);

        static Vector2[] MakeStar(float ro, float ri)
        {
            var v = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = (90f + i * 36f) * Mathf.Deg2Rad;
                float r = i % 2 == 0 ? ro : ri;
                v[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }
            return v;
        }

        /// <summary>Rounded star (core star grown by StarRound): &lt;= 0 inside.</summary>
        static float SdStar(float x, float y)
        {
            return SdPoly(StarPoly, x, y) - StarRound;
        }

        static Color ShadeStarGold(float x, float y)
        {
            float body = SdStar(x, y);
            if (body > 0f) return SdStar(x, y + 0.06f) <= 0f ? GoldBack : Clear; // back copy, 6 % lower
            // lighter inner star (62 %, a little higher), then the white highlight arc at the top left
            const float k = 0.62f;
            Color c = SdStar(x / k, (y - 0.03f) / k) * k <= 0f ? GoldInner : GoldBody;
            if (ArcDist(x, y, 0f, 0.02f, 0.2f, 112f, 158f) <= 0.026f) c = Color.Lerp(c, White, 0.9f);
            return c;
        }

        static Color ShadeStarGrey(float x, float y)
        {
            if (SdStar(x, y) <= 0f) return GreyBody;
            return SdStar(x, y + 0.06f) <= 0f ? GreyBack : Clear;
        }

        static Color ShadeSmile(float x, float y)
        {
            if (y > 0f || x * x + y * y > 0.25f) return Clear;  // lower half disc, radius 0.5
            float tx = x, ty = y + 0.33f;
            return tx * tx + ty * ty <= 0.2f * 0.2f ? Tongue : Ink;
        }

        static Color ShadeSleepyEye(float x, float y)
        {
            return ArcDist(x, y, 0f, 0f, 0.38f, 200f, 340f) <= 0.08f ? White : Clear;
        }

        static Color ShadeSparkle(float x, float y)
        {
            return Mathf.Sqrt(Mathf.Abs(x)) + Mathf.Sqrt(Mathf.Abs(y)) <= 1f ? White : Clear;
        }

        /// <summary>Capsule (half-height radius): fill inside the line, the line on the outer NoThanksLine px.</summary>
        static Color ShadeNoThanks(float x, float y)
        {
            float r = NoThanksH * 0.5f;
            float qx = Mathf.Max(Mathf.Abs(x) - (NoThanksW * 0.5f - r), 0f);
            float sd = Mathf.Sqrt(qx * qx + y * y) - r; // <= 0 inside
            if (sd > 0f) return Clear;
            return sd >= -NoThanksLine ? new Color(1f, 1f, 1f, 0.55f) : new Color(1f, 1f, 1f, 0.16f);
        }

        static Color ShadeSadMouth(float x, float y)
        {
            return ArcDist(x, y, 0f, -0.45f, 0.38f, 20f, 160f) <= 0.08f ? White : Clear; // top at y -0.07
        }

        static readonly Vector2[] TearTip = { new Vector2(-0.39f, 0.025f), new Vector2(0.39f, 0.025f), new Vector2(0f, 0.7f) };

        /// <summary>Disc (radius 0.45 at y -0.2) plus the triangle to the tip, tangent to it, so the outline is smooth.</summary>
        static Color ShadeTear(float x, float y)
        {
            float dy = y + 0.2f;
            if (x * x + dy * dy <= 0.45f * 0.45f) return White;
            return SdPoly(TearTip, x, y) <= 0f ? White : Clear;
        }

        // ------------------------------------------------------------------ geometry

        /// <summary>Signed distance to a closed polygon (any shape), negative inside.</summary>
        static float SdPoly(Vector2[] v, float px, float py)
        {
            float d = (px - v[0].x) * (px - v[0].x) + (py - v[0].y) * (py - v[0].y);
            float s = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                float ex = v[j].x - v[i].x, ey = v[j].y - v[i].y;
                float wx = px - v[i].x, wy = py - v[i].y;
                float t = Mathf.Clamp01((wx * ex + wy * ey) / (ex * ex + ey * ey));
                float bx = wx - ex * t, by = wy - ey * t;
                d = Mathf.Min(d, bx * bx + by * by);
                bool c1 = py >= v[i].y, c2 = py < v[j].y, c3 = ex * wy > ey * wx;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }

        /// <summary>Distance to an arc (centre, radius, a0..a1 degrees counter-clockwise from +x, within 0..360) with round ends.</summary>
        static float ArcDist(float x, float y, float cx, float cy, float rho, float a0, float a1)
        {
            float dx = x - cx, dy = y - cy;
            float a = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            if (a < 0f) a += 360f;
            if (a >= a0 && a <= a1) return Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - rho);
            float r0 = a0 * Mathf.Deg2Rad, r1 = a1 * Mathf.Deg2Rad;
            float e0x = cx + Mathf.Cos(r0) * rho - x, e0y = cy + Mathf.Sin(r0) * rho - y;
            float e1x = cx + Mathf.Cos(r1) * rho - x, e1y = cy + Mathf.Sin(r1) * rho - y;
            return Mathf.Sqrt(Mathf.Min(e0x * e0x + e0y * e0y, e1x * e1x + e1y * e1y));
        }

        /// <summary>Point inside a rect centred on the origin (half sizes hw, hh) with corner radius rad.</summary>
        static bool InRoundRect(float x, float y, float hw, float hh, float rad)
        {
            float dx = Mathf.Max(Mathf.Abs(x) - (hw - rad), 0f), dy = Mathf.Max(Mathf.Abs(y) - (hh - rad), 0f);
            return dx * dx + dy * dy <= rad * rad;
        }
    }
}
