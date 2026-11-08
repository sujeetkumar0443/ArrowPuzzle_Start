using UnityEngine;

namespace ArrowPuzzle
{
    public enum Weight { Medium, Semi, Bold }

    /// <summary>
    /// Look & feel, matched to the "Arrow Puzzle UI" design (390 px wide phone artboards).
    /// All layout numbers in the scripts are design pixels; D() converts them to canvas units
    /// (the canvas is 1080 units wide, so 1 design px = K units).
    /// </summary>
    public static class Theme
    {
        public const float K = 1080f / 390f;
        public static float D(float designPx) { return designPx * K; }

        // palette (from the design)
        public static Color Bg = Hex("#F4EFE6");
        public static Color Ink = Hex("#26323F");
        public static Color InkSoft = Hex("#5A6675");
        public static Color Card = Hex("#FFFFFF");
        public static Color Accent = Hex("#E4572E");
        public static Color AccentShadow = Hex("#B23F1D");
        public static Color Teal = Hex("#2EC4B6");
        public static Color TealShadow = Hex("#1F8E84");
        public static Color CardShadow = Hex("#E3DACB");
        public static Color Error = Hex("#E63946");
        public static Color ErrorDeep = Hex("#CD2234"); // outer edge of the wrong-move screen flash
        public static Color HeartLost = Hex("#D9D3C7");
        public static Color Gold = Hex("#FFB703");
        public static Color ModeGold = Hex("#F4A300");      // Shapes mode color
        public static Color ModeGoldEdge = Hex("#B97C00");  // Shapes mode button edge
        public static Color Locked = Hex("#E3DACB");
        public static Color Dot = Hex("#DDD5C6");
        public static Color Tile = Hex("#F5F0E6"); // board cell tile on shaped (masked) boards
        public static Color SwitchOff = Hex("#BDB5A6");
        public static Color Hint = Hex("#1D3557");
        // Settings lists (ScreenSettings)
        public static Color SectionLabel = Hex("#66717E"); // "GAME" / "MORE" section labels
        public static Color ListEdge = Hex("#D9D1C1");     // bottom edge of the list card
        public static Color ListPress = Hex("#F7F3EA");    // pressed list row (white list card)
        public static Color ListDivider = Hex("#F0EADD");  // 2 px line between list rows
        public static Color Chevron = Hex("#B8B1A2");
        public static Color FootNote = Hex("#9A9384");     // "Arrow Puzzle · Version x"
        public static Color LinkBlue = Hex("#3E8EDE");     // Share icon square
        public static Color LinkGreen = Hex("#2FA866");    // green icon square (no Settings row uses it now)
        // Home level ruler (ScreenHome)
        public static Color RulerTrack = Hex("#E6DFD0");      // ruler track behind the teal fill
        public static Color LockedLevelFill = Hex("#EDE7DA"); // locked level circle
        public static Color LockedLevelEdge = Hex("#DCD3C1");
        public static Color LockedLevelIcon = Hex("#B3A994");
        // Level map (ScreenMap)
        public static Color MapStarEmpty = Hex("#E6E0D3");    // unearned star above a done tile
        public static Color MapGrassA = Hex("#8CCB6A");       // placeholder segment A (until valley_seg_A.png)
        public static Color MapGrassB = Hex("#82C25F");       // placeholder segment B
        public static Color MapRoad = Hex("#EBD9A8");         // placeholder sand road
        public static Color MapRoadEdge = Hex("#D3BC84");     // placeholder road edge
        // map tiles (the current tile uses Accent / AccentShadow); edit only these to try other tile colours
        public static Color MapDoneFace = Hex("#3D7BF0");     // done tile face (white number); also the blue lock-burst bits
        public static Color MapDoneSide = Hex("#2556B8");     // done tile side face
        public static Color MapLockedFace = Hex("#A9B4BF");   // locked tile face
        public static Color MapLockedSide = Hex("#7E8A96");   // locked tile side face; also the lock badge icon
        public static Color MapLockedNumber = Hex("#F4F6F8"); // locked tile number; also the disabled map PLAY label
        public static Color MapPlayFace = Hex("#E4572E");     // the map's PLAY button (white label and icon)
        public static Color MapPlayShadow = Hex("#B23F1D");   // its bottom edge
        public static Color Dim = new Color(38f / 255f, 50f / 255f, 63f / 255f, 0.62f);
        public static Color PopupShadow = new Color(0f, 0f, 0f, 0.12f);

        public static Color[] Palette =
        {
            Hex("#FF6B6B"), Hex("#2EC4B6"), Hex("#5C6BC0"), Hex("#F4A300"),
            Hex("#9B5DE5"), Hex("#52B788"), Hex("#F15BB5"), Hex("#00A6FB"),
        };

        public static Sprite Rounded, Circle, Heart, Star, Triangle, DotTile;
        static Font fMedium, fSemi, fBold;
        static bool inited;

        public static Color Hex(string h)
        {
            Color c;
            ColorUtility.TryParseHtmlString(h, out c);
            return c;
        }

        public static Font GetFont(Weight w)
        {
            switch (w)
            {
                case Weight.Medium: return fMedium;
                case Weight.Semi: return fSemi;
                default: return fBold;
            }
        }

        public static void Init()
        {
            if (inited) return;
            inited = true;

            Rounded = MakeRounded(64, 24, true);
            Circle = MakeRounded(64, 32, false);
            Heart = MakeHeart(128);
            Star = MakePolygon(64, new[]
            {
                12f, 2f, 15f, 9f, 22f, 9.5f, 16.5f, 14.5f, 18.5f, 22f, 12f, 18f, 5.5f, 22f, 7.5f, 14.5f, 2f, 9.5f, 9f, 9f
            }, 24f);
            Triangle = MakePolygon(64, new[] { 7f, 4.5f, 21f, 12f, 7f, 19.5f }, 24f);
            DotTile = MakeDotTile();

            Font fallback = LoadFallback();
            fMedium = Load("Fredoka_500Medium", fallback);
            fSemi = Load("Fredoka_600SemiBold", fallback);
            fBold = Load("Fredoka_700Bold", fallback);
        }

        static Font Load(string name, Font fallback)
        {
            var f = Resources.Load<Font>("Fonts/" + name);
            return f != null ? f : fallback;
        }

        static Font LoadFallback()
        {
            Font f = null;
            try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { f = null; }
            if (f == null)
            {
                try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { f = null; }
            }
            if (f == null) f = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Roboto", "Helvetica" }, 16);
            return f;
        }

        // ------------------------------------------------------------------ procedural sprites

        static Texture2D NewTex(int size, bool repeat)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Bilinear;
            t.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            return t;
        }

        static Sprite MakeRounded(int size, int radius, bool sliced)
        {
            var tex = NewTex(size, false);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    float cx = Mathf.Clamp(fx, radius, size - radius);
                    float cy = Mathf.Clamp(fy, radius, size - radius);
                    float d = Mathf.Sqrt((fx - cx) * (fx - cx) + (fy - cy) * (fy - cy));
                    float a = Mathf.Clamp01(radius - d + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            Vector4 border = sliced ? new Vector4(radius, radius, radius, radius) : Vector4.zero;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        /// <summary>Heart curve (x²+y²-1)³ = x²y³, 4x4 supersampled for a crisp 1-pixel edge, with mipmaps for small sizes.</summary>
        static Sprite MakeHeart(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.filterMode = FilterMode.Trilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[size * size];
            const int ss = 4;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < ss; sy++)
                    {
                        for (int sx = 0; sx < ss; sx++)
                        {
                            float nx = ((x + (sx + 0.5f) / ss) / size * 2f - 1f) * 1.25f;
                            float ny = ((y + (sy + 0.5f) / ss) / size * 2f - 1f) * 1.25f + 0.15f;
                            float a1 = nx * nx + ny * ny - 1f;
                            if (a1 * a1 * a1 - nx * nx * ny * ny * ny <= 0f) hit++;
                        }
                    }
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(hit * 255 / (ss * ss)));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Anti-aliased filled polygon. Points are x,y pairs in a box of boxSize (y pointing down, like SVG).</summary>
        static Sprite MakePolygon(int size, float[] xy, float boxSize)
        {
            var tex = NewTex(size, false);
            var px = new Color32[size * size];
            int n = xy.Length / 2;
            const int ss = 4;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < ss; sy++)
                    {
                        for (int sx = 0; sx < ss; sx++)
                        {
                            float u = (x + (sx + 0.5f) / ss) / size * boxSize;
                            float v = (1f - (y + (sy + 0.5f) / ss) / size) * boxSize; // flip: texture y goes up
                            if (Inside(xy, n, u, v)) hit++;
                        }
                    }
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(hit * 255 / (ss * ss)));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static bool Inside(float[] xy, int n, float px, float py)
        {
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = xy[i * 2], yi = xy[i * 2 + 1], xj = xy[j * 2], yj = xy[j * 2 + 1];
                if (((yi > py) != (yj > py)) && (px < (xj - xi) * (py - yi) / (yj - yi) + xi)) inside = !inside;
            }
            return inside;
        }

        /// <summary>One faint dot in a tile; used with Image.Type.Tiled for the dotted backgrounds.</summary>
        static Sprite MakeDotTile()
        {
            const int size = 64;
            var tex = NewTex(size, true);
            var px = new Color32[size * size];
            float r = 6f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - size * 0.5f, dy = y + 0.5f - size * 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }
    }
}
