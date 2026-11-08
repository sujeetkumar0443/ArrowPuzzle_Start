using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArrowPuzzle
{
    /// <summary>
    /// "Domino Cascade" splash: a grid of arrow tiles pops in, a tap ring fires in the centre, the tiles slide away
    /// in a wave (each along its own arrow) and uncover the title, tagline and loading bar. Then goes Home.
    /// All timing is unscaled, t = seconds since OnShow.
    /// </summary>
    public class ScreenSplash : AppScreen
    {
        const int Cols = 7;
        const float CellD = 54f, TileD = 48f;
        const float Ascent = 0.974f;          // Fredoka ascent / em, to place text by its baseline
        const float LineH = 1.21f;            // Fredoka line height / em

        // timeline (s)
        const float PopStart = 0.05f, PopPerCell = 0.045f, PopTime = 0.4f;
        const float RingStart = 0.95f, RingTime = 0.5f;
        const float WaveStart = 1.15f, WavePerCell = 0.07f, WaveTime = 0.55f, WaveDistD = 520f;
        const float TitleIn0 = 1.75f, TitleIn1 = 2.4f;
        const float RestIn0 = 2.4f, RestIn1 = 2.9f;
        const float BarEnd = 4.0f;
        const float Out0 = 4.0f, Out1 = 4.4f;
        const float AdWait = 4f;              // max extra hold at Out0 for the app open ad to finish loading

        static bool appOpenTried;             // at most one app open ad from the splash per launch

        public override bool UseSafeArea { get { return false; } }

        class Tile
        {
            public RectTransform rt;
            public CanvasGroup group;
            public Vector2 home, dir;
            public float dist;
            public int state; // 0 waiting, 1 popping, 2 resting, 3 leaving, 4 gone
        }

        readonly List<Tile> tiles = new List<Tile>();
        readonly System.Random rng = new System.Random(11);
        int activeCount;
        RectTransform tileLayer;

        CanvasGroup titleGroup, restGroup;
        RectTransform barFill;
        ArrowGraphic ring;
        readonly List<Vector2> ringPts = new List<Vector2>(65);
        Coroutine anim;

        static readonly Vector2[] Dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

        protected override void Build()
        {
            // background: cream + faint dot grid over the whole canvas
            var bg = Ui.Img(Root, "Bg", null, Theme.Bg);
            bg.raycastTarget = false;
            Ui.Fill(bg.rectTransform);

            var dots = Ui.Img(Root, "Dots", Theme.DotTile, Theme.Dot);
            dots.type = Image.Type.Tiled;
            dots.pixelsPerUnitMultiplier = 64f / (34f * Theme.K);
            dots.raycastTarget = false;
            Ui.Fill(dots.rectTransform);

            BuildTitle();
            BuildLoading();

            // tiles above the title, so the wave uncovers it
            tileLayer = Ui.New(Root, "Tiles");
            Ui.Fill(tileLayer);
            EnsureTiles(RowsNeeded());

            // tap ring, above everything
            var rrt = Ui.New(Root, "Ring");
            Ui.PlaceD(rrt, Ui.C, Vector2.zero, Vector2.zero);
            ring = rrt.gameObject.AddComponent<ArrowGraphic>();
            ring.drawHead = false;
            ring.raycastTarget = false;
            ring.color = Theme.Accent;
            rrt.gameObject.SetActive(false);
        }

        void BuildTitle()
        {
            var holder = Ui.New(Root, "Title");
            Ui.Fill(holder);
            titleGroup = holder.gameObject.AddComponent<CanvasGroup>();

            // baselines at centre - 8 and centre + 72 (design px, y down)
            TextAt(holder, "ARROW", 76f, Theme.Ink, Weight.Bold, -8f);
            TextAt(holder, "PUZZLE", 76f, Theme.Accent, Weight.Bold, 72f);
        }

        void BuildLoading()
        {
            var holder = Ui.New(Root, "Rest");
            Ui.Fill(holder);
            restGroup = holder.gameObject.AddComponent<CanvasGroup>();

            TextAt(holder, "Tap. Slide. Escape.", 21f, Theme.InkSoft, Weight.Medium, 72f + 46f);

            // loading block, bottom 110 from the screen bottom: bar, 12 gap, text
            var loading = Ui.Label(holder, "Loading…", 15f, Theme.InkSoft, Weight.Medium);
            Ui.PlaceD(loading.rectTransform, Ui.BC, new Vector2(0f, 110f), new Vector2(220f, 18f));

            var track = Ui.Img(holder, "BarTrack", Theme.Rounded, Theme.CardShadow, 6f);
            track.raycastTarget = false;
            Ui.PlaceD(track.rectTransform, Ui.BC, new Vector2(0f, 110f + 18f + 12f), new Vector2(220f, 12f));

            var fill = Ui.Img(track.transform, "BarFill", Theme.Rounded, Theme.Accent, 6f);
            fill.raycastTarget = false;
            barFill = fill.rectTransform;
            barFill.anchorMin = new Vector2(0f, 0f);
            barFill.anchorMax = new Vector2(0f, 1f);
            barFill.pivot = new Vector2(0f, 0.5f);
            barFill.anchoredPosition = Vector2.zero;
            barFill.sizeDelta = Vector2.zero;
        }

        /// <summary>One centred text line whose baseline is baselineD design px below the screen centre.</summary>
        static void TextAt(RectTransform parent, string text, float sizeD, Color color, Weight weight, float baselineD)
        {
            var t = Ui.Label(parent, text, sizeD, color, weight, TextAnchor.UpperCenter);
            float top = -baselineD + Ascent * sizeD; // y up from the centre
            Ui.PlaceD(t.rectTransform, Ui.C, Ui.TC, new Vector2(0f, top), new Vector2(390f, sizeD * LineH));
        }

        // ------------------------------------------------------------------ tiles

        /// <summary>Rows covering the full screen height plus one.</summary>
        static int RowsNeeded()
        {
            float heightD = Screen.width > 0 ? Screen.height * 390f / Screen.width : 844f; // canvas matches width
            return Mathf.CeilToInt(heightD / CellD) + 1;
        }

        /// <summary>Grows the pool to rows x Cols tiles. Tile k always gets the same random arrow (seed 11).</summary>
        void EnsureTiles(int rows)
        {
            while (tiles.Count < rows * Cols)
            {
                var root = Ui.New(tileLayer, "Tile");
                Ui.PlaceD(root, Ui.C, Vector2.zero, new Vector2(TileD, TileD));
                var t = new Tile { rt = root, group = root.gameObject.AddComponent<CanvasGroup>() };
                t.group.blocksRaycasts = false;

                var face = Ui.Img(root, "Face", Theme.Rounded, Theme.Card, 14f);
                face.raycastTarget = false;
                Ui.Fill(face.rectTransform);
                var edge = face.gameObject.AddComponent<Shadow>();
                edge.effectColor = Theme.CardShadow;
                edge.effectDistance = new Vector2(0f, -Theme.D(4f));
                edge.useGraphicAlpha = false;

                t.dir = Dirs[rng.Next(Dirs.Length)];
                var color = Theme.Palette[rng.Next(Theme.Palette.Length)];

                var art = Ui.New(root, "Arrow");
                Ui.Fill(art);
                var g = art.gameObject.AddComponent<ArrowGraphic>();
                g.raycastTarget = false;
                g.color = color;
                g.thickness = Theme.D(7f);
                g.headSize = Theme.D(15f);
                g.SetPoints(new List<Vector2> { -t.dir * Theme.D(14f), t.dir * Theme.D(14f) });

                tiles.Add(t);
            }
        }

        /// <summary>Lays out rows x Cols tiles centred on the screen and puts them back to "not popped yet".</summary>
        void ResetTiles()
        {
            int rows = RowsNeeded();
            EnsureTiles(rows);
            activeCount = rows * Cols;
            float cx = (Cols - 1) * 0.5f, cy = (rows - 1) * 0.5f;

            for (int i = 0; i < tiles.Count; i++)
            {
                var t = tiles[i];
                bool on = i < activeCount;
                t.rt.gameObject.SetActive(on);
                if (!on) continue;

                int c = i % Cols, r = i / Cols;
                float dx = c - cx, dy = r - cy;
                t.dist = Mathf.Sqrt(dx * dx + dy * dy);
                t.home = new Vector2(dx, -dy) * Theme.D(CellD);
                t.rt.anchoredPosition = t.home;
                t.rt.localScale = Vector3.zero;
                t.group.alpha = 1f;
                t.state = 0;
            }
        }

        // ------------------------------------------------------------------ timeline

        public override void OnShow()
        {
            ResetTiles();
            titleGroup.alpha = 0f;
            restGroup.alpha = 0f;
            barFill.sizeDelta = Vector2.zero;
            ring.gameObject.SetActive(false);
            Ads.SetSplashOnScreen(true);

            if (!appOpenTried)
            {
                Ads.Init();
                Ads.LoadAppOpen();
            }

            if (anim != null) GameApp.I.StopCoroutine(anim);
            anim = GameApp.I.StartCoroutine(Run());
        }

        public override void OnHide()
        {
            Ads.SetSplashOnScreen(false);
            if (anim != null) GameApp.I.StopCoroutine(anim);
            anim = null;
        }

        IEnumerator Run()
        {
            float t = 0f;
            bool click = false, slide = false;
            while (t < Out1)
            {
                t += Time.unscaledDeltaTime;

                // app open hand-off, held on the full splash (before the fade): wait up to AdWait for the load,
                // then show the ad and go Home when it closes; without an ad just carry on
                if (!appOpenTried && t >= Out0)
                {
                    appOpenTried = true;
                    t = Out0;
                    float w = 0f;
                    while (!Ads.IsAppOpenReady && Ads.IsAppOpenLoading && !AppStartup.AdsBlocked && w < AdWait)
                    {
                        w += Time.unscaledDeltaTime;
                        yield return null;
                    }
                    if (Ads.IsAppOpenReady)
                    {
                        Ads.Log("app open at launch: loaded, showing (held the splash " + w.ToString("0.00") + " s)");
                        anim = null;
                        Ads.ShowAppOpen(() => GameApp.I.Show("ScreenHome"));
                        yield break;
                    }
                    // a load that finishes later is kept and only used under the return rules (Ads.ReturnSkipReason)
                    Ads.Log("app open skipped at launch: " + (
                        AppStartup.AdsBlocked ? "consent does not allow ad requests" :
                        Ads.IsAppOpenLoading ? "not loaded within " + AdWait + " s (still loading, kept for a later return)" :
                        Ads.AppOpenLastError != null ? "load failed (" + Ads.AppOpenLastError + ")" :
                        "no ad loaded"));
                }

                if (!click && t >= RingStart) { click = true; Sfx.Play(Sfx.Id.Click); }
                if (!slide && t >= WaveStart) { slide = true; Sfx.Play(Sfx.Id.Slide); }

                UpdateTiles(t);
                UpdateRing(t);

                float fadeOut = 1f - Mathf.Clamp01((t - Out0) / (Out1 - Out0));
                titleGroup.alpha = Mathf.Clamp01((t - TitleIn0) / (TitleIn1 - TitleIn0)) * fadeOut;
                restGroup.alpha = Mathf.Clamp01((t - RestIn0) / (RestIn1 - RestIn0)) * fadeOut;
                float bar = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - RestIn0) / (BarEnd - RestIn0)));
                barFill.sizeDelta = new Vector2(Theme.D(220f * bar), 0f);

                yield return null;
            }
            anim = null;
            GameApp.I.Show("ScreenHome");
        }

        /// <summary>Only tiles inside their pop or wave window are touched.</summary>
        void UpdateTiles(float t)
        {
            for (int i = 0; i < activeCount; i++)
            {
                var tile = tiles[i];
                switch (tile.state)
                {
                    case 0:
                    case 1:
                    {
                        float s = PopStart + tile.dist * PopPerCell;
                        if (t < s) break;
                        float x = (t - s) / PopTime;
                        if (x >= 1f) { tile.rt.localScale = Vector3.one; tile.state = 2; }
                        else { tile.rt.localScale = Vector3.one * EaseOutBack(x); tile.state = 1; }
                        break;
                    }
                    case 2:
                    case 3:
                    {
                        float s = WaveStart + tile.dist * WavePerCell;
                        if (t < s) break;
                        float x = (t - s) / WaveTime;
                        if (x >= 1f) { tile.rt.gameObject.SetActive(false); tile.state = 4; break; }
                        float e = x * x; // ease-in quadratic
                        tile.rt.anchoredPosition = tile.home + tile.dir * Theme.D(WaveDistD) * e;
                        tile.rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.4f, e);
                        tile.group.alpha = Mathf.Lerp(1f, 0.2f, e);
                        tile.state = 3;
                        break;
                    }
                }
            }
        }

        /// <summary>Accent ring in the centre: radius 20 -> 110, stroke 4 -> 1, alpha 1 -> 0 over 0.5 s.</summary>
        void UpdateRing(float t)
        {
            float x = (t - RingStart) / RingTime;
            bool on = x >= 0f && x < 1f;
            if (ring.gameObject.activeSelf != on) ring.gameObject.SetActive(on);
            if (!on) return;

            float r = Theme.D(Mathf.Lerp(20f, 110f, x));
            ringPts.Clear();
            const int seg = 64;
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                ringPts.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
            ring.thickness = Theme.D(Mathf.Lerp(4f, 1f, x));
            var c = Theme.Accent;
            c.a = 1f - x;
            ring.color = c;
            ring.SetPoints(ringPts);
        }

        static float EaseOutBack(float x)
        {
            const float c1 = 1.9f, c3 = c1 + 1f;
            float a = x - 1f;
            return 1f + c3 * a * a * a + c1 * a * a;
        }
    }
}
