using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArrowPuzzle
{
    /// <summary>
    /// "Game cards" Home: header row (logo, title, star pill, settings), "Pick a game to play" and one card per mode
    /// (picture tile drawn with ArrowGraphic, name, level, progress bar, Play). Locked cards show the unlock hint and
    /// shake + toast when tapped. The stack scales down a little to fit short phones, scrolls only if it still can't,
    /// and is centred in the free space on tall phones. Design px (y from the top of the safe area), unscaled time.
    /// </summary>
    public class ScreenHome : AppScreen
    {
        // ---- layout (design px, y from the top)
        const float Side = 20f, RowCY = 50f;                           // top row: 22 from the top, 56 high
        const float LogoD = 40f, LogoRadius = 12f, TitleX = 70f;
        const float SettingsD = 56f, PillH = 40f, PillGap = 10f, IconEdge = 3f;
        const float HeadingY = 100f, HeadingH = 32f;
        const float ViewTop = 136f, StackTop = 146f, BottomPad = 20f, MinScale = 0.78f;
        const float BannerGap = 8f;                                    // between the card area and the banner
        const float CardW = 350f, CardH = 196f, CardGap = 16f, CardRadius = 30f, CardEdge = 6f, Pad = 16f;
        const float StackH = CardH * 3f + CardGap * 2f + CardEdge;     // visual height incl. the last shadow
        const float TileD = 132f, TileRadius = 22f;
        const float ColX = Pad + TileD + 16f, ColW = CardW - ColX - Pad;
        const float NameY = 14f, LevelY = 52f, BarY = 84f, BarH = 16f, PlayY = 116f, PlayH = 56f, PlayRadius = 18f, PlayEdge = 5f;
        const float HintH = 50f, PlayPhase = 0.4f, LockD = 56f, LockIconD = 26f;
        const float ToastW = 330f, ToastH = 44f, ToastY = 32f, ToastRise = 12f;

        // ---- behaviour (s, design px)
        const float Loop = 5f, RestEnd = 2f, OutEnd = 2.8f, BackStart = 3.4f, BackEnd = 4.1f, ReturnFrom = 18f;
        const float ClassicSlide = 60f, HeartSlide = 40f;
        const float ShakeAmp = 5f, ShakeTime = 0.3f;
        const float ToastIn = 0.2f, ToastHold = 1.4f, ToastOut = 0.3f;
        const float LockedAlpha = 0.85f, MosaicAlpha = 0.8f;

        // ---- colours that only exist on this screen
        static readonly Color CardShadowCol = Theme.Hex("#D9D1C1");
        static readonly Color Grey = Theme.Hex("#66717E");
        static readonly Color Track = Theme.Hex("#E6DFD0");
        static readonly Color TileFill = Theme.Hex("#F7F3EA");
        static readonly Color MosaicFill = Theme.Hex("#EEE8DA");
        static readonly Color LockGrey = Theme.Hex("#9AA5B1");
        static readonly Color[] MosaicCorals = { Theme.Hex("#F2B6A3"), Theme.Hex("#F7CDBF"), Theme.Hex("#EBA28C") };
        static readonly Color[] HeartRows =
        {
            Theme.Hex("#F46B45"), Theme.Hex("#E4572E"), Theme.Hex("#EC5FA6"), Theme.Hex("#D94A6A"),
            Theme.Hex("#F4A300"), Theme.Hex("#F46B45"), Theme.Hex("#E4572E"), Theme.Hex("#EC5FA6"),
        };

        // Classic tile: 5 x 5, cell 26, y down. x0, y0, x1, y1 (head at x1, y1), Palette index. The first one animates.
        static readonly int[,] ClassicArrows =
        {
            { 0, 0, 3, 0, 1 }, // teal, right
            { 4, 3, 4, 0, 0 }, // coral, up
            { 0, 1, 0, 4, 4 }, // purple, down
            { 1, 1, 3, 1, 2 }, // blue, right
            { 1, 2, 1, 4, 5 }, // green, down
            { 2, 2, 3, 2, 3 }, // gold, right
            { 2, 4, 4, 4, 6 }, // pink, right
            { 3, 3, 2, 3, 7 }, // blue, left
        };
        const float ClassicCell = 26f, ClassicThick = 9f, ClassicHead = 19f;

        // Shapes tile: heart on 11 x 8, per row the start/end columns of each arrow
        static readonly int[][] HeartArrows =
        {
            new[] { 1, 3, 7, 9 }, new[] { 0, 4, 6, 10 }, new[] { 0, 4, 6, 10 }, new[] { 0, 4, 6, 10 },
            new[] { 1, 4, 6, 9 }, new[] { 2, 4, 6, 8 }, new[] { 3, 4, 6, 7 }, new[] { 4, 6 },
        };
        const int HeartCols = 11, HeartRowsN = 8, HeartMoverRow = 1, HeartMoverIndex = 1;
        const float HeartMargin = 10f, HeartThick = 0.45f, HeartHead = 0.8f; // thickness / head relative to the cell

        // Mosaic tile
        const float MosaicTile = 36f, MosaicGap = 4f, MosaicRadius = 8f;

        class Card
        {
            public GameMode mode;
            public bool open;
            public int level;
            public RectTransform rt, barFill;
            public CanvasGroup group;
            public Text levelText, hint;
            public GameObject bar, play, lockBadge;
            public RectTransform mover;
            public CanvasGroup moverGroup;
            public float slide;
            public Vector2 home;
            public float shakeT = -1f;
        }

        readonly Card[] cards = new Card[3];
        Text stars;
        ScrollRect scroll;
        RectTransform content, stack;
        CanvasGroup toast;
        Text toastText;
        RectTransform cardArea;
        float toastT = -1f, loopT, laidOutH = -1f, laidOutUnder = -1f, toastY = ToastY;

        protected override void Build()
        {
            var bg = Ui.Img(Root, "Bg", null, Theme.Bg);
            bg.raycastTarget = false;
            Ui.Fill(bg.rectTransform);
            bg.rectTransform.offsetMin = new Vector2(0f, -Theme.D(400f)); // also under the insets
            bg.rectTransform.offsetMax = new Vector2(0f, Theme.D(400f));
            BannerPlaceholder.Create(Root); // just above the background, under everything else

            BuildHeader();

            var heading = Ui.Label(Root, "Pick a game to play", 25f, Theme.Ink, Weight.Bold, TextAnchor.MiddleLeft);
            Ui.PlaceD(heading.rectTransform, Ui.TL, new Vector2(Side, -HeadingY), new Vector2(CardW, HeadingH));

            BuildScroll();
            for (int i = 0; i < cards.Length; i++) cards[i] = BuildCard(i);
            BuildToast();

            Root.gameObject.AddComponent<HomeTicker>().tick = Tick;
            BannerDebug.Create(Root, "ScreenHome"); // only when Ads.ShowBannerDebug
            Refresh();
        }

        // ------------------------------------------------------------------ build

        void BuildHeader()
        {
            var left = new Vector2(0f, 0.5f);
            var right = new Vector2(1f, 0.5f);

            // logo: Classic teal rounded square with a white arrow
            var logo = Ui.Img(Root, "Logo", Theme.Rounded, ModeInfo.Get(GameMode.Classic).Color, LogoRadius);
            logo.raycastTarget = false;
            Ui.PlaceD(logo.rectTransform, Ui.TL, left, new Vector2(Side, -RowCY), new Vector2(LogoD, LogoD));
            var mark = Ui.New(logo.transform, "Mark");
            Ui.Fill(mark);
            mark.pivot = Ui.C;
            var g = mark.gameObject.AddComponent<ArrowGraphic>();
            g.raycastTarget = false;
            g.color = Theme.Card;
            g.thickness = Theme.D(5f);
            g.headSize = Theme.D(13f);
            g.SetPoints(new List<Vector2>
            {
                new Vector2(-8.5f, -9.5f) * Theme.K, new Vector2(-8.5f, 3f) * Theme.K, new Vector2(7.5f, 3f) * Theme.K,
            });

            var title = Ui.Label(Root, "Arrow Puzzle", 22f, Theme.Ink, Weight.Bold, TextAnchor.MiddleLeft);
            Ui.PlaceD(title.rectTransform, Ui.TL, left, new Vector2(TitleX, -RowCY), new Vector2(150f, 30f));

            // settings (round, right)
            var settings = Ui.Btn(Root, "", Theme.Card, Theme.Ink, 0f, SettingsD * 0.5f, Theme.CardShadow, IconEdge,
                                  () => GameApp.I.Show("ScreenSettings"), Icon.Gear, 26f);
            Ui.PlaceD((RectTransform)settings.transform, Ui.TR, right, new Vector2(-Side, -RowCY), new Vector2(SettingsD, SettingsD));

            // star pill, grows to the left with the count
            var pill = Ui.Img(Root, "Stars", Theme.Rounded, Theme.Card, PillH * 0.5f);
            pill.raycastTarget = false;
            Ui.PlaceD(pill.rectTransform, Ui.TR, right, new Vector2(-(Side + SettingsD + PillGap), -RowCY), new Vector2(80f, PillH));
            var edge = pill.gameObject.AddComponent<Shadow>();
            edge.effectColor = Theme.CardShadow;
            edge.effectDistance = new Vector2(0f, -Theme.D(IconEdge));
            edge.useGraphicAlpha = false;
            var row = Ui.Row(pill.rectTransform, 6f);
            row.padding = new RectOffset(Mathf.RoundToInt(Theme.D(10f)), Mathf.RoundToInt(Theme.D(14f)), 0, 0);
            pill.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var star = Icons.Make(pill.transform, Icon.Star, Theme.Gold, 22f);
            Ui.Size(star, 22f, 22f);
            stars = Ui.Label(pill.transform, "0", 22f, Theme.Ink, Weight.Bold);
        }

        /// <summary>Area under the heading; the ScrollRect only turns on when the stack can't fit (see Layout).</summary>
        void BuildScroll()
        {
            var area = Ui.Img(Root, "Cards", null, Color.clear); // catches drags in the gaps
            var art = area.rectTransform;
            cardArea = art; // bottom moves up above the banner (Layout)
            art.anchorMin = Vector2.zero;
            art.anchorMax = Vector2.one;
            art.offsetMin = Vector2.zero;
            art.offsetMax = new Vector2(0f, -Theme.D(ViewTop));
            scroll = area.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.inertia = true;

            var viewport = Ui.New(art, "Viewport");
            Ui.Fill(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll.viewport = viewport;

            content = Ui.New(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, Theme.D(StackH));
            scroll.content = content;

            stack = Ui.New(content, "Stack");
            Ui.PlaceD(stack, Ui.TC, new Vector2(0f, -(StackTop - ViewTop)), new Vector2(CardW, StackH - CardEdge));
            scroll.enabled = false;
        }

        Card BuildCard(int i)
        {
            var mode = (GameMode)i;
            var info = ModeInfo.Get(mode);
            var c = new Card { mode = mode };

            // card: tap = level map (or toast + shake when locked); same press-down + click as Ui.Btn
            var img = Ui.Img(stack, "Card_" + info.Name, Theme.Rounded, Theme.Card, CardRadius);
            c.rt = img.rectTransform;
            Ui.PlaceD(c.rt, Ui.TC, new Vector2(0f, -i * (CardH + CardGap)), new Vector2(CardW, CardH));
            c.home = c.rt.anchoredPosition;
            var sh = img.gameObject.AddComponent<Shadow>();
            sh.effectColor = CardShadowCol;
            sh.effectDistance = new Vector2(0f, -Theme.D(CardEdge));
            sh.useGraphicAlpha = false;
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            img.gameObject.AddComponent<PressScale>();
            btn.onClick.AddListener(() =>
            {
                Sfx.Play(Sfx.Id.Click);
                OnCardTap(c);
            });
            c.group = img.gameObject.AddComponent<CanvasGroup>();

            // picture tile (no raycasts, so taps fall through to the card)
            var tile = Ui.Img(c.rt, "Tile", Theme.Rounded, mode == GameMode.Mosaic ? MosaicFill : TileFill, TileRadius);
            tile.raycastTarget = false;
            Ui.PlaceD(tile.rectTransform, Ui.TL, new Vector2(Pad, -(CardH - TileD) * 0.5f), new Vector2(TileD, TileD));
            tile.gameObject.AddComponent<RectMask2D>(); // the sliding arrows fade out at the tile edge
            switch (mode)
            {
                case GameMode.Classic: BuildClassicArt(tile.rectTransform, c); break;
                case GameMode.Shapes: BuildHeartArt(tile.rectTransform, c); break;
                default: BuildMosaicArt(tile.rectTransform); break;
            }

            var lockCircle = Ui.Img(tile.transform, "Lock", Theme.Circle, LockGrey);
            lockCircle.raycastTarget = false;
            Ui.PlaceD(lockCircle.rectTransform, Ui.C, Vector2.zero, new Vector2(LockD, LockD));
            var lockIcon = Icons.Make(lockCircle.transform, Icon.Lock, Theme.Card, LockIconD);
            Ui.PlaceD(lockIcon, Ui.C, Vector2.zero, new Vector2(LockIconD, LockIconD));
            c.lockBadge = lockCircle.gameObject;

            // right column
            var name = Ui.Label(c.rt, info.Name, 30f, Theme.Ink, Weight.Bold, TextAnchor.MiddleLeft);
            Ui.PlaceD(name.rectTransform, Ui.TL, new Vector2(ColX, -NameY), new Vector2(ColW, 36f));
            c.levelText = Ui.Label(c.rt, "", 18f, Grey, Weight.Medium, TextAnchor.MiddleLeft);
            Ui.PlaceD(c.levelText.rectTransform, Ui.TL, new Vector2(ColX, -LevelY), new Vector2(ColW, 22f));

            var track = Ui.Img(c.rt, "Bar", Theme.Rounded, Track, BarH * 0.5f);
            track.raycastTarget = false;
            Ui.PlaceD(track.rectTransform, Ui.TL, new Vector2(ColX, -BarY), new Vector2(ColW, BarH));
            var fill = Ui.Img(track.transform, "Fill", Theme.Rounded, info.Color, BarH * 0.5f);
            fill.raycastTarget = false;
            c.barFill = fill.rectTransform;
            Ui.PlaceD(c.barFill, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, BarH));
            c.bar = track.gameObject;

            var play = Ui.Btn(c.rt, "Play", info.Color, Theme.Card, 24f, PlayRadius, info.Edge, PlayEdge,
                              () => Play(mode), Icon.Play, 22f);
            var playRt = (RectTransform)play.transform;
            Ui.PlaceD(playRt, Ui.TL, new Vector2(ColX, -PlayY), new Vector2(ColW, PlayH));
            Ui.AddShimmer(playRt, PlayRadius, i * PlayPhase);
            c.play = play.gameObject;

            c.hint = Ui.Label(c.rt, info.UnlockHint, 18f, Grey, Weight.Medium, TextAnchor.UpperLeft);
            c.hint.horizontalOverflow = HorizontalWrapMode.Wrap;
            c.hint.verticalOverflow = VerticalWrapMode.Truncate; // up to 2 lines
            Ui.PlaceD(c.hint.rectTransform, Ui.TL, new Vector2(ColX, -BarY), new Vector2(ColW, HintH));

            return c;
        }

        // ------------------------------------------------------------------ picture tiles

        static ArrowGraphic Arrow(Transform parent, Vector2 fromD, Vector2 toD, Color color, float thickD, float headD)
        {
            var rt = Ui.New(parent, "Arrow");
            Ui.Fill(rt);
            rt.pivot = Ui.C;
            var g = rt.gameObject.AddComponent<ArrowGraphic>();
            g.raycastTarget = false;
            g.color = color;
            g.thickness = Theme.D(thickD);
            g.headSize = Theme.D(headD);
            g.SetPoints(new List<Vector2> { fromD * Theme.K, toD * Theme.K });
            return g;
        }

        /// <summary>Holder that slides / fades one arrow (driven by Tick).</summary>
        static RectTransform Mover(Transform parent, Card c, float slide)
        {
            var rt = Ui.New(parent, "Mover");
            Ui.Fill(rt);
            rt.pivot = Ui.C;
            c.mover = rt;
            c.moverGroup = rt.gameObject.AddComponent<CanvasGroup>();
            c.slide = slide;
            return rt;
        }

        static void BuildClassicArt(RectTransform tile, Card c)
        {
            // teal (index 0) first, so it slides under the coral head on its way out
            for (int a = 0; a < ClassicArrows.GetLength(0); a++)
            {
                Transform parent = a == 0 ? Mover(tile, c, ClassicSlide) : (Transform)tile;
                Arrow(parent, ClassicCell5(ClassicArrows[a, 0], ClassicArrows[a, 1]),
                      ClassicCell5(ClassicArrows[a, 2], ClassicArrows[a, 3]),
                      Theme.Palette[ClassicArrows[a, 4]], ClassicThick, ClassicHead);
            }
        }

        static Vector2 ClassicCell5(int x, int y)
        {
            return new Vector2((x - 2) * ClassicCell, (2 - y) * ClassicCell);
        }

        static void BuildHeartArt(RectTransform tile, Card c)
        {
            float inner = TileD - HeartMargin * 2f;
            float cell = Mathf.Min(inner / HeartCols, inner / HeartRowsN);
            for (int r = 0; r < HeartArrows.Length; r++)
            {
                var row = HeartArrows[r];
                float y = ((HeartRowsN - 1) * 0.5f - r) * cell;
                for (int k = 0; k * 2 + 1 < row.Length; k++)
                {
                    float x0 = (row[k * 2] - (HeartCols - 1) * 0.5f) * cell;
                    float x1 = (row[k * 2 + 1] - (HeartCols - 1) * 0.5f) * cell;
                    bool toRight = (r + k) % 2 == 0;
                    Transform parent = r == HeartMoverRow && k == HeartMoverIndex
                        ? Mover(tile, c, toRight ? HeartSlide : -HeartSlide) : (Transform)tile;
                    Arrow(parent, new Vector2(toRight ? x0 : x1, y), new Vector2(toRight ? x1 : x0, y),
                          HeartRows[r], cell * HeartThick, cell * HeartHead);
                }
            }
        }

        static void BuildMosaicArt(RectTransform tile)
        {
            var grid = Ui.New(tile, "Mosaic");
            Ui.Fill(grid);
            grid.gameObject.AddComponent<CanvasGroup>().alpha = MosaicAlpha;
            float step = MosaicTile + MosaicGap;
            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    var sq = Ui.Img(grid, "Tile", Theme.Rounded, MosaicCorals[(x + y * 2) % 3], MosaicRadius);
                    sq.raycastTarget = false;
                    Ui.PlaceD(sq.rectTransform, Ui.C, new Vector2((x - 1) * step, (1 - y) * step), new Vector2(MosaicTile, MosaicTile));
                }
            }
        }

        void BuildToast()
        {
            var pill = Ui.Img(Root, "Toast", Theme.Rounded, Theme.Ink, ToastH * 0.5f);
            pill.raycastTarget = false;
            Ui.PlaceD(pill.rectTransform, Ui.BC, new Vector2(0f, ToastY), new Vector2(ToastW, ToastH));
            toastText = Ui.Label(pill.transform, "", 17f, Theme.Card, Weight.Bold);
            Ui.Fill(toastText.rectTransform);
            toast = pill.gameObject.AddComponent<CanvasGroup>();
            toast.blocksRaycasts = false;
            toast.interactable = false;
            pill.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ show / refresh

        public override void OnShow()
        {
            loopT = 0f;
            laidOutH = -1f;
            Ads.BannerHeightChanged -= OnBannerHeight; // never twice
            Ads.BannerHeightChanged += OnBannerHeight;
            Ads.Banner(true, "ScreenHome"); // popups on top keep it; only leaving Home hides it
            Refresh();
            Layout();
        }

        public override void OnHide()
        {
            Ads.BannerHeightChanged -= OnBannerHeight;
            Ads.Banner(false, "ScreenHome"); // ignored when the next screen (game) asks for it in the same frame
            toastT = -1f;
            toast.gameObject.SetActive(false);
            foreach (var c in cards)
            {
                c.shakeT = -1f;
                c.rt.anchoredPosition = c.home;
            }
        }

        /// <summary>Stars, levels, progress bars and lock states.</summary>
        void Refresh()
        {
            stars.text = Save.TotalStars().ToString();
            foreach (var c in cards)
            {
                c.open = Save.IsModeUnlocked(c.mode);
                c.level = Mathf.Min(Save.GetUnlocked(c.mode), ModeInfo.LevelsPerMode);
                c.levelText.text = "Level " + c.level + " of " + ModeInfo.LevelsPerMode;
                c.group.alpha = c.open ? 1f : LockedAlpha;
                c.bar.SetActive(c.open);
                c.play.SetActive(c.open);
                c.hint.gameObject.SetActive(!c.open);
                c.lockBadge.SetActive(!c.open);

                float w = ColW * (c.level - 1) / ModeInfo.LevelsPerMode;
                c.barFill.gameObject.SetActive(w > 0f);
                c.barFill.sizeDelta = new Vector2(Theme.D(Mathf.Max(w, BarH)), Theme.D(BarH)); // never thinner than round

                if (c.mover != null)
                {
                    c.mover.anchoredPosition = Vector2.zero;
                    c.moverGroup.alpha = 1f;
                }
            }
        }

        /// <summary>
        /// Fits the stack under the heading: full size and centred when there is room, scaled down to MinScale when
        /// short, and scrolling only when even that doesn't fit. Re-runs whenever the safe-area height changes.
        /// </summary>
        void Layout()
        {
            float h = Root.rect.height / Theme.K;
            float banner = Ads.BannerHeightDesign;
            float under = banner > 0f ? banner + BannerGap : 0f; // the card area ends above the banner
            if (h <= 0f || (Mathf.Abs(h - laidOutH) < 0.5f && Mathf.Abs(under - laidOutUnder) < 0.5f)) return;
            laidOutH = h;
            laidOutUnder = under;

            float endPad = banner > 0f ? 0f : BottomPad;
            float avail = h - StackTop - under - endPad;
            float s = Mathf.Clamp(avail / StackH, MinScale, 1f);
            bool fits = StackH * s <= avail + 0.5f;
            float pad = StackTop - ViewTop;

            cardArea.offsetMin = new Vector2(0f, Theme.D(under));
            stack.localScale = new Vector3(s, s, 1f);
            stack.anchoredPosition = new Vector2(0f, -Theme.D(fits ? pad + (avail - StackH * s) * 0.5f : pad));
            scroll.StopMovement();
            scroll.enabled = !fits;
            content.sizeDelta = new Vector2(0f, Theme.D(fits ? h - ViewTop - under : pad + StackH * s + endPad));
            content.anchoredPosition = Vector2.zero;
            toastY = ToastY + under;
        }

        void OnBannerHeight()
        {
            Layout();
        }

        void OnCardTap(Card c)
        {
            if (Save.IsModeUnlocked(c.mode))
            {
                GameApp.I.CurrentMode = c.mode;
                GameApp.I.Show("ScreenLevels");
                return;
            }
            c.shakeT = 0f;
            toastText.text = ModeInfo.Get(c.mode).UnlockHint;
            toastT = 0f;
            toast.alpha = 0f;
            toast.gameObject.SetActive(true);
            toast.transform.SetAsLastSibling();
        }

        static void Play(GameMode mode)
        {
            if (!Save.IsModeUnlocked(mode)) return;
            GameApp.I.CurrentMode = mode;
            GameApp.I.CurrentLevel = Mathf.Min(Save.GetUnlocked(mode), ModeInfo.LevelsPerMode);
            GameApp.I.Show("ScreenGame");
        }

        // ------------------------------------------------------------------ per frame

        /// <summary>From HomeTicker.Update: layout check, arrow loops, shakes and the toast. No allocations.</summary>
        void Tick()
        {
            float dt = Time.unscaledDeltaTime;
            Layout();

            loopT += dt;
            float alpha;
            float k = LoopOffset(Mathf.Repeat(loopT, Loop), out alpha);
            foreach (var c in cards)
            {
                if (c.mover != null && c.open)
                {
                    c.mover.anchoredPosition = new Vector2(Theme.D(c.slide * k), 0f);
                    c.moverGroup.alpha = alpha;
                }

                if (c.shakeT < 0f) continue;
                c.shakeT += dt;
                float x = c.shakeT >= ShakeTime ? 0f : Mathf.Sin(c.shakeT * 60f) * ShakeAmp * (1f - c.shakeT / ShakeTime);
                c.rt.anchoredPosition = c.home + new Vector2(Theme.D(x), 0f);
                if (c.shakeT >= ShakeTime) c.shakeT = -1f;
            }

            if (toastT >= 0f)
            {
                toastT += dt;
                float t = toastT;
                toast.alpha = t < ToastIn ? EaseOutCubic(t / ToastIn)
                            : t < ToastIn + ToastHold ? 1f
                            : 1f - Mathf.Clamp01((t - ToastIn - ToastHold) / ToastOut);
                float rise = EaseOutCubic(Mathf.Clamp01(t / ToastIn));
                ((RectTransform)toast.transform).anchoredPosition = new Vector2(0f, Theme.D(toastY - ToastRise * (1f - rise)));
                if (t >= ToastIn + ToastHold + ToastOut)
                {
                    toastT = -1f;
                    toast.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Slide-out loop: rest, slide out (ease in) while fading, hidden, come back along the path (ease out) while
        /// fading in, rest. Returns the offset as a fraction of the slide distance.
        /// </summary>
        static float LoopOffset(float u, out float alpha)
        {
            if (u < RestEnd) { alpha = 1f; return 0f; }
            if (u < OutEnd)
            {
                float k = (u - RestEnd) / (OutEnd - RestEnd);
                alpha = 1f - k;
                return k * k;
            }
            if (u < BackStart) { alpha = 0f; return 0f; }
            if (u < BackEnd)
            {
                float e = EaseOutCubic((u - BackStart) / (BackEnd - BackStart));
                alpha = e;
                return ReturnFrom / ClassicSlide * (1f - e);
            }
            alpha = 1f;
            return 0f;
        }

        static float EaseOutCubic(float x)
        {
            float a = 1f - x;
            return 1f - a * a * a;
        }
    }

    /// <summary>Per-frame tick for ScreenHome (lives on the screen's Root, so it only runs while Home is shown).</summary>
    public class HomeTicker : MonoBehaviour
    {
        public Action tick;

        void Update() { if (tick != null) tick(); }
    }
}
