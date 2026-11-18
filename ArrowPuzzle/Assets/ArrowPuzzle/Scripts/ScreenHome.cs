using System;
using UnityEngine;
using UnityEngine.UI;

namespace ArrowPuzzle
{
    /// <summary>
    /// Home ("Home with shape preview"): Settings and the coin pill on top; a hero card with the current level's real board
    /// drawn in the current theme (LevelPreview of Levels.For) and two chips (shape name or BOARD -> Shapes, theme -> Themes);
    /// the five-slot level ruler; PLAY (breathing); three Panel tiles: SKIPS n/total (+2 by rewarded ad when none are
    /// left), THEMES, SHAPES. The hero card takes the height that is left, so everything fits above the banner.
    /// Dev route (Editor / Development Build): hold the coin pill 1 s, or tap it 7 times in 3 s.
    /// </summary>
    public class ScreenHome : AppScreen
    {
        // ---- top bar
        const float Side = 20f, TopY = 16f;
        const float SettingsD = 52f, SettingsR = 15f, GearD = 26f;
        const float PillH = 40f, CoinD = 24f, PillPadL = 8f, PillGap = 8f, PillPadR = 16f, CoinFont = 20f;

        // ---- hero card (design 92 from the top, 22 from the sides, 346 wide, radius 30); its height flexes
        const float CardTop = 92f, CardX = 22f, CardW = 346f, CardR = 30f, CardEdge = 5f, CardMinH = 200f;
        const float ChipH = 36f, ChipPad = 14f, ChipInset = 14f, ChipFont = 15f, PreviewPad = 18f;

        // ---- ruler (x from the centre)
        const float BigD = 78f, SmallD = 40f, NearX = 68f, FarX = 115f, TrackH = 6f;
        const float BigFont = 34f, SmallFont = 16f, LockD = 16f, SmallEdge = 3f;
        const float StepD = 40f, StepR = 12f, StepX = 163f, StepIconD = 22f, StepDisabledAlpha = 0.35f;
        const float RingPeriod = 1.4f, RingGrow = 0.4f, RingAlpha = 0.35f;
        const float PopTime = 0.2f, PopScale = 1.12f;

        // ---- PLAY: 280 x 100 face + 8 px edge
        const float PlayW = 280f, PlayH = 100f, PlayEdge = 8f, PlayR = 26f;
        const float LevelFont = 17f, LevelY = 25f, PlayFont = 46f, PlayY = -12f; // label centres, y up from the face centre
        const float BreathPeriod = 1.8f, BreathAmp = 0.03f;

        // ---- bottom tiles: three 106 x 68, Panel with a PanelEdge bottom
        const float TileW = 106f, TileH = 68f, TileR = 18f, TileEdge = 4f;
        const int SkipBonus = 2;

        // ---- vertical spacing (design px)
        const float BottomPad = 16f, BannerGap = 8f, TilesToPlay = 20f, PlayToRuler = 16f, RulerToCard = 18f;

        static readonly Color CoinEdge = Theme.Hex("#D98E00");
        static readonly Vector3[] corners = new Vector3[4];
        static readonly float[] SlotX = { -FarX, -NearX, 0f, NearX, FarX };

        class Slot
        {
            public RectTransform rt;
            public Image face;
            public Text number;
            public GameObject lockIcon;
            public int level;
        }

        RectTransform backdrop, settings, coinPill, ruler, rulerTrack, ring, prev, next, playRoot;
        RectTransform card, previewHolder, tilesRow;
        Text coins, levelLabel, shapeChipText, themeChipText, skipsValue;
        Image ringImg, shapeChip, themeChip;
        CanvasGroup prevGroup, nextGroup;
        readonly Slot[] slots = new Slot[5];
        GameObject preview;
        string previewKey;
        Vector2 previewSize; // design px, set by Layout
        ScreenKit.Toast toast;
        bool skipWaiting;
        int skipRequest;

        int selected = 1, unlocked = 1;
        float popT = -1f;
        float laidOutH = -1f, laidOutUnder = -1f, laidOutScale = -1f;
        Rect laidOutSafe;

        protected override void Build()
        {
            BuildBackdrop();
            BannerPlaceholder.Create(Root); // just above the background, under everything else
            BuildTopBar();
            BuildCard();
            BuildRuler();
            BuildPlay();
            BuildTiles();

            // TEMPORARY dev routes to the long-arrow samples (LongSamplesDev.OpenList: Editor / Development Build only,
            // logs "blocked" otherwise), both on the coin pill now that the logo is gone: hold it 1 s, or tap it 7 times
            // within 3 s (the pill takes taps only for these)
            var pillImg = coinPill.GetComponent<Image>();
            pillImg.raycastTarget = true;
            coinPill.gameObject.AddComponent<LongPressTrigger>().onHold = () => LongSamplesDev.OpenList("coin long press");
            coinPill.gameObject.AddComponent<MultiTapTrigger>().onTaps = () => LongSamplesDev.OpenList("coin taps");

            toast = new ScreenKit.Toast(Root);
            Root.gameObject.AddComponent<HomeTicker>().tick = Tick;
            BannerDebug.Create(Root, "ScreenHome"); // only when Ads.ShowBannerDebug
            Refresh();
        }

        // ------------------------------------------------------------------ build

        /// <summary>Theme background (solid or gradient) over the whole canvas.</summary>
        void BuildBackdrop()
        {
            backdrop = Ui.New(Root, "Backdrop");
            var bg = Ui.Img(backdrop, "Bg", null, Theme.Bg);
            bg.raycastTarget = false;
            Ui.Fill(bg.rectTransform);
            Theme.PaintBg(bg);
        }

        void BuildTopBar()
        {
            var sImg = Rounded(Root, "Settings", Theme.Panel, SettingsR);
            settings = sImg.rectTransform;
            MakeButton(sImg, () => GameApp.I.Show("ScreenSettings"));
            var gear = Icons.Make(settings, Icon.Gear, Theme.PanelText, GearD);
            Ui.PlaceD(gear, Ui.C, Vector2.zero, new Vector2(GearD, GearD));

            var pill = Rounded(Root, "Coins", Theme.Panel, PillH * 0.5f);
            pill.raycastTarget = false;
            coinPill = pill.rectTransform;
            var coin = Coin(coinPill, CoinD);
            Ui.PlaceD(coin, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(PillPadL, 0f), new Vector2(CoinD, CoinD));
            coins = Ui.Label(coinPill, "0", CoinFont, Theme.PanelText, Weight.Bold, TextAnchor.MiddleLeft);
            Ui.PlaceD(coins.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(PillPadL + CoinD + PillGap, 0f), new Vector2(120f, PillH));
        }

        /// <summary>Hero card: Card colour with a bottom edge; the preview (Layout sizes it) and the two chips.</summary>
        void BuildCard()
        {
            var img = Rounded(Root, "Hero", Theme.Card, CardR);
            card = img.rectTransform;
            var edge = img.gameObject.AddComponent<Shadow>();
            edge.effectColor = Theme.CardShadow;
            edge.effectDistance = new Vector2(0f, -Theme.D(CardEdge));
            edge.useGraphicAlpha = false;
            MakeButton(img, ScreenShapes.Open, false); // tapping the preview opens the Shapes gallery

            previewHolder = Ui.New(card, "PreviewHolder");

            // left: dark chip "PAW  7/12" (-> Shapes)
            shapeChip = Rounded(card, "ShapeChip", Theme.Panel, ChipH * 0.5f);
            shapeChipText = Ui.Label(shapeChip.rectTransform, "", ChipFont, Theme.PanelText, Weight.Bold);
            Ui.Fill(shapeChipText.rectTransform);
            MakeButton(shapeChip, ScreenShapes.Open);

            // right: soft chip "CREAM THEME" (-> Themes)
            var soft = Color.Lerp(Theme.Card, Theme.Ink, Theme.Current != null && Theme.Current.Dark ? 0.16f : 0.07f);
            soft.a = 1f;
            themeChip = Rounded(card, "ThemeChip", soft, ChipH * 0.5f);
            themeChipText = Ui.Label(themeChip.rectTransform, "", ChipFont, Theme.Ink, Weight.Bold);
            Ui.Fill(themeChipText.rectTransform);
            MakeButton(themeChip, ScreenThemes.Open);
        }

        void BuildRuler()
        {
            ruler = Ui.New(Root, "Ruler");

            var trackImg = Rounded(ruler, "Track", Theme.RulerTrack, TrackH * 0.5f);
            trackImg.raycastTarget = false;
            rulerTrack = trackImg.rectTransform;

            // pulsing ring behind the selected circle
            ringImg = Ui.Img(ruler, "Ring", Theme.CircleHD, Theme.Accent);
            ringImg.raycastTarget = false;
            ring = ringImg.rectTransform;
            Ui.PlaceD(ring, Ui.C, Vector2.zero, new Vector2(BigD, BigD));

            for (int i = 0; i < slots.Length; i++)
            {
                bool big = i == 2;
                float d = big ? BigD : SmallD;
                var s = new Slot();
                s.face = Ui.Img(ruler, "Level_" + (i - 2), Theme.CircleHD, big ? Theme.Accent : Theme.Card);
                s.rt = s.face.rectTransform;
                Ui.PlaceD(s.rt, Ui.C, new Vector2(SlotX[i], 0f), new Vector2(d, d));
                if (!big)
                {
                    var edge = s.face.gameObject.AddComponent<Shadow>();
                    edge.effectColor = Theme.ListEdge;
                    edge.effectDistance = new Vector2(0f, -Theme.D(SmallEdge));
                    edge.useGraphicAlpha = false;
                }
                s.number = Ui.Label(s.rt, "", big ? BigFont : SmallFont, big ? Theme.AccentText : Theme.Ink, Weight.Bold);
                Ui.PlaceD(s.number.rectTransform, Ui.C, Vector2.zero, new Vector2(d, d));
                var lockRt = Icons.Make(s.rt, Icon.Lock, Theme.LockedLevelIcon, LockD);
                Ui.PlaceD(lockRt, Ui.C, Vector2.zero, new Vector2(LockD, LockD));
                s.lockIcon = lockRt.gameObject;

                var btn = s.face.gameObject.AddComponent<Button>();
                btn.targetGraphic = s.face;
                btn.transition = Selectable.Transition.None;
                var slot = s;
                btn.onClick.AddListener(() =>
                {
                    if (slot.level < 1 || slot.level > unlocked || slot.level == selected) return;
                    Sfx.Play(Sfx.Id.Click);
                    Pick(slot.level);
                });
                slots[i] = s;
            }

            prev = StepButton("Prev", Icon.Back, -1, out prevGroup);
            next = StepButton("Next", Icon.Chevron, 1, out nextGroup);
        }

        RectTransform StepButton(string name, Icon icon, int dir, out CanvasGroup group)
        {
            var img = Rounded(ruler, name, Theme.Card, StepR);
            var edge = img.gameObject.AddComponent<Shadow>();
            edge.effectColor = Theme.ListEdge;
            edge.effectDistance = new Vector2(0f, -Theme.D(SmallEdge));
            edge.useGraphicAlpha = false;
            MakeButton(img, () => Step(dir));
            var ic = Icons.Make(img.rectTransform, icon, Theme.Ink, StepIconD);
            Ui.PlaceD(ic, Ui.C, Vector2.zero, new Vector2(StepIconD, StepIconD));
            Ui.PlaceD(img.rectTransform, Ui.C, new Vector2(dir * StepX, 0f), new Vector2(StepD, StepD));
            group = img.gameObject.AddComponent<CanvasGroup>();
            return img.rectTransform;
        }

        void BuildPlay()
        {
            // playRoot (snapped, breathes) > press (PressScale, Button) > edge + face + labels
            playRoot = Ui.New(Root, "Play");
            var press = Ui.New(playRoot, "Press");
            Ui.Fill(press);

            var edge = Rounded(press, "Edge", Theme.AccentShadow, PlayR);
            edge.raycastTarget = false;
            Ui.PlaceD(edge.rectTransform, Ui.BC, Vector2.zero, new Vector2(PlayW, PlayH));

            var face = Rounded(press, "Face", Theme.Accent, PlayR);
            Ui.PlaceD(face.rectTransform, Ui.TC, Vector2.zero, new Vector2(PlayW, PlayH));

            levelLabel = Ui.Label(face.rectTransform, "LEVEL 1", LevelFont, Theme.AccentText, Weight.Semi);
            Ui.PlaceD(levelLabel.rectTransform, Ui.C, new Vector2(0f, LevelY), new Vector2(PlayW, LevelFont * 1.4f));
            var play = Ui.Label(face.rectTransform, "PLAY", PlayFont, Theme.AccentText, Weight.Bold);
            Ui.PlaceD(play.rectTransform, Ui.C, new Vector2(0f, PlayY), new Vector2(PlayW, PlayFont * 1.3f));

            var btn = press.gameObject.AddComponent<Button>();
            btn.targetGraphic = face;
            btn.transition = Selectable.Transition.None;
            press.gameObject.AddComponent<PressScale>();
            btn.onClick.AddListener(() =>
            {
                Sfx.Play(Sfx.Id.Click);
                Play();
            });
        }

        /// <summary>SKIPS n/total, THEMES, SHAPES: three Panel tiles with a PanelEdge bottom, centred, 14 apart.</summary>
        void BuildTiles()
        {
            tilesRow = Ui.New(Root, "Tiles");
            float gap = (CardW - TileW * 3f) * 0.5f;
            var skips = Tile("Skips", -(TileW + gap));
            var top = Ui.Label(skips, "SKIPS", 13f, Theme.PanelText, Weight.Semi);
            top.color = new Color(Theme.PanelText.r, Theme.PanelText.g, Theme.PanelText.b, 0.8f);
            Ui.PlaceD(top.rectTransform, Ui.C, new Vector2(0f, 13f), new Vector2(TileW, 18f));
            skipsValue = Ui.Label(skips, "4/4", 24f, Theme.PanelText, Weight.Bold);
            Ui.PlaceD(skipsValue.rectTransform, Ui.C, new Vector2(0f, -9f), new Vector2(TileW, 30f));
            MakeButton(skips.GetComponent<Image>(), OnSkipsTile);

            var themes = Tile("Themes", 0f);
            IconTile(themes, Icon.Palette, "THEMES");
            MakeButton(themes.GetComponent<Image>(), ScreenThemes.Open);

            var shapes = Tile("Shapes", TileW + gap);
            IconTile(shapes, Icon.Heart, "SHAPES");
            MakeButton(shapes.GetComponent<Image>(), ScreenShapes.Open);
        }

        RectTransform Tile(string name, float x)
        {
            var img = Rounded(tilesRow, name, Theme.Panel, TileR);
            var edge = img.gameObject.AddComponent<Shadow>();
            edge.effectColor = Theme.PanelEdge;
            edge.effectDistance = new Vector2(0f, -Theme.D(TileEdge));
            edge.useGraphicAlpha = false;
            Ui.PlaceD(img.rectTransform, Ui.C, new Vector2(x, 0f), new Vector2(TileW, TileH));
            return img.rectTransform;
        }

        static void IconTile(RectTransform tile, Icon icon, string label)
        {
            var ic = Icons.Make(tile, icon, Theme.PanelText, 24f);
            Ui.PlaceD(ic, Ui.C, new Vector2(0f, 10f), new Vector2(24f, 24f));
            var t = Ui.Label(tile, label, 13f, Theme.PanelText, Weight.Bold);
            Ui.PlaceD(t.rectTransform, Ui.C, new Vector2(0f, -17f), new Vector2(TileW, 18f));
        }

        /// <summary>RoundedHD image with a radiusD design px corner (sharp at any size).</summary>
        static Image Rounded(Transform parent, string name, Color color, float radiusD)
        {
            var img = Ui.Img(parent, name, Theme.RoundedHD, color);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = Theme.RoundedHDRadius / Theme.D(radiusD);
            return img;
        }

        static void MakeButton(Image img, Action onClick, bool squash = true)
        {
            img.raycastTarget = true;
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            if (squash) img.gameObject.AddComponent<PressScale>();
            btn.onClick.AddListener(() =>
            {
                Sfx.Play(Sfx.Id.Click);
                onClick();
            });
        }

        /// <summary>Gold coin (like the Level complete card): amber rim, gold face, small shine.</summary>
        static RectTransform Coin(Transform parent, float d)
        {
            var rim = Ui.Img(parent, "Coin", Theme.CircleHD, CoinEdge);
            rim.raycastTarget = false;
            var rt = rim.rectTransform;
            Dot(rt, "Face", Theme.Gold, new Vector2(0f, 0.04f * d), 0.76f * d);
            Dot(rt, "Shine", new Color(1f, 1f, 1f, 0.55f), new Vector2(-0.16f * d, 0.18f * d), 0.2f * d);
            return rt;
        }

        static void Dot(RectTransform parent, string name, Color c, Vector2 posD, float d)
        {
            var img = Ui.Img(parent, name, Theme.CircleHD, c);
            img.raycastTarget = false;
            Ui.PlaceD(img.rectTransform, Ui.C, posD, new Vector2(d, d));
        }

        // ------------------------------------------------------------------ show / refresh

        public override void OnShow()
        {
            laidOutH = -1f;
            Ads.BannerHeightChanged -= OnBannerHeight; // never twice
            Ads.BannerHeightChanged += OnBannerHeight;
            Ads.Banner(true, "ScreenHome"); // popups on top keep it; only leaving Home hides it
            Save.CoinsChanged -= SetCoins;
            Save.CoinsChanged += SetCoins;
            Save.SkipsChanged -= SetSkips;
            Save.SkipsChanged += SetSkips;
            MusicPlayer.PlayHome();
            previewKey = null; // the hero preview is drawn again (theme / progress may have changed elsewhere)
            Refresh();
            Layout();
            // past the prebuilt set: the level PLAY starts (and the one after) begin building now
            if (Campaign.IsPlayable(Save.UnlockedGlobal)) Levels.Prefetch(Save.UnlockedGlobal);
            if (Campaign.IsPlayable(Save.UnlockedGlobal + 1)) Levels.Prefetch(Save.UnlockedGlobal + 1);
            Debug.Log("[Coins] Home shows " + coins.text + " (saved " + Save.Coins + ")");
        }

        public override void OnHide()
        {
            Ads.BannerHeightChanged -= OnBannerHeight;
            Save.CoinsChanged -= SetCoins;
            Save.SkipsChanged -= SetSkips;
            Ads.Banner(false, "ScreenHome"); // ignored when the next screen (game) asks for it in the same frame
            skipRequest++;
            skipWaiting = false;
            toast.Hide();
            popT = -1f;
            slots[2].rt.localScale = Vector3.one;
            if (preview != null) Destroy(preview); // frees the preview meshes
            preview = null;
            previewKey = null;
        }

        static void Destroy(GameObject go) { UnityEngine.Object.Destroy(go); }

        /// <summary>Coins, skips, chips and the ruler back on the highest unlocked level.</summary>
        void Refresh()
        {
            // highest level the ruler offers: the unlocked one, kept within this release (ReleaseLevelCap)
            unlocked = Campaign.ReleaseLevelCap > 0 ? Mathf.Min(Save.UnlockedGlobal, Campaign.ReleaseLevelCap) : Save.UnlockedGlobal;
            popT = -1f;
            slots[2].rt.localScale = Vector3.one;
            SetSelected(Save.UnlockedGlobal);
            SetCoins();
            SetSkips();
            SetTypeChip();
            themeChipText.text = ((Theme.Current != null ? Theme.Current.Name : "Cream") + " theme").ToUpperInvariant();
        }

        void SetCoins()
        {
            coins.text = Save.Coins.ToString();
            float w = PillPadL + CoinD + PillGap + coins.preferredWidth / Theme.K + PillPadR;
            Ui.PlaceD(coinPill, Ui.TR, new Vector2(-Side, -(TopY + (SettingsD - PillH) * 0.5f)), new Vector2(Mathf.Ceil(w), PillH));
            Snap(coinPill);
        }

        /// <summary>SKIPS tile: skips left today / today's total (4, more with a bonus); "..." while its video runs.</summary>
        void SetSkips()
        {
            int total = Save.SkipsPerDay + Save.SkipsBonusToday;
            skipsValue.text = skipWaiting ? "..." : Mathf.Min(Save.SkipsLeft, total) + "/" + total;
        }

        /// <summary>
        /// SKIPS tile: with no skips left, one rewarded ad adds SkipBonus skips for today (as the game's Skip pill);
        /// with skips left, a short note (they are used from a level).
        /// </summary>
        void OnSkipsTile()
        {
            if (skipWaiting) return;
            if (Save.SkipsLeft > 0)
            {
                toast.Show(Save.SkipsLeft + " skips left today. Use them in a level.", ToastY());
                return;
            }
            if (Ads.IsRewardedBusy) { toast.Show("Ad not ready, try again", ToastY()); return; }
            int id = ++skipRequest;
            skipWaiting = true;
            SetSkips();
            Ads.ShowRewarded(() =>
            {
                if (id != skipRequest) return;
                skipWaiting = false;
                Save.AddSkipBonus(SkipBonus); // raises SkipsChanged -> SetSkips
                SetSkips();
                toast.Show("+" + SkipBonus + " skips", ToastY());
            }, () =>
            {
                if (id != skipRequest) return;
                skipWaiting = false;
                SetSkips();
                toast.Show("Ad not ready, try again", ToastY());
            });
        }

        float ToastY()
        {
            float banner = Ads.BannerHeightDesign;
            return (banner > 0f ? banner + BannerGap : BottomPad) + TileH + TileEdge + 12f;
        }

        /// <summary>Kept within the unlocked levels; updates the ruler slots, prev / next and the PLAY label.</summary>
        void SetSelected(int level)
        {
            selected = Mathf.Clamp(level, 1, unlocked);
            levelLabel.text = "LEVEL " + selected;

            float minX = 0f, maxX = 0f;
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                int l = selected + i - 2;
                bool on = Campaign.IsPlayable(l);
                s.rt.gameObject.SetActive(on);
                s.level = on ? l : 0;
                if (!on) continue;
                minX = Mathf.Min(minX, SlotX[i]);
                maxX = Mathf.Max(maxX, SlotX[i]);
                bool locked = l > unlocked;
                if (i != 2) s.face.color = locked ? Theme.Locked : Theme.Card;
                s.number.text = l.ToString();
                s.number.gameObject.SetActive(!locked);
                s.lockIcon.SetActive(locked);
            }

            // the track joins the visible circles
            rulerTrack.gameObject.SetActive(maxX > minX);
            Ui.PlaceD(rulerTrack, Ui.C, new Vector2((minX + maxX) * 0.5f, 0f), new Vector2(maxX - minX, TrackH));

            SetStep(prevGroup, selected > 1);
            SetStep(nextGroup, selected < unlocked);

            // the hero card follows the level PLAY starts: its type chip and its board
            if (shapeChipText != null)
            {
                SetTypeChip();
                LayoutChips();
            }
            if (previewSize.x > 0f) DrawPreview(previewSize);
        }

        /// <summary>Left chip: the shape name in capitals for a shape level, "BOARD" for a normal one.</summary>
        void SetTypeChip()
        {
            shapeChipText.text = Campaign.TypeTitle(selected).ToUpperInvariant();
        }

        static void SetStep(CanvasGroup g, bool on)
        {
            g.alpha = on ? 1f : StepDisabledAlpha;
            g.interactable = g.blocksRaycasts = on;
        }

        void Step(int dir)
        {
            int level = Mathf.Clamp(selected + dir, 1, unlocked);
            if (level != selected) Pick(level);
        }

        void Pick(int level)
        {
            SetSelected(level);
            popT = 0f;
        }

        void Play()
        {
            // every level of this release done and the last one picked: "More levels coming soon" (it offers a replay)
            if (Campaign.PastCap(Save.UnlockedGlobal) && selected == unlocked)
            {
                GameApp.I.Show("ScreenComingSoon");
                return;
            }
            GameApp.I.CurrentLevel = selected;
            GameApp.I.Show("ScreenGame");
        }

        // ------------------------------------------------------------------ layout

        /// <summary>
        /// Top bar from the top; tiles, PLAY and the ruler from the bottom, above the banner (or BottomPad without one);
        /// the hero card fills what is left between the top bar and the ruler. Re-run when the height, the banner, the
        /// canvas scale or the safe area change. Button rects end on whole pixels.
        /// </summary>
        void Layout()
        {
            float h = Root.rect.height / Theme.K;
            float banner = Ads.BannerHeightDesign;
            float under = banner > 0f ? banner + BannerGap : BottomPad;
            float scale = Root.lossyScale.x;
            Rect safe = Screen.safeArea;
            if (h <= 0f || (Mathf.Abs(h - laidOutH) < 0.5f && Mathf.Abs(under - laidOutUnder) < 0.5f &&
                            Mathf.Approximately(scale, laidOutScale) && safe == laidOutSafe && previewKey != null)) return;
            laidOutH = h;
            laidOutUnder = under;
            laidOutScale = scale;
            laidOutSafe = safe;

            FitBackdrop();

            Ui.PlaceD(settings, Ui.TL, new Vector2(Side, -TopY), new Vector2(SettingsD, SettingsD));
            Snap(settings);
            SetCoins();

            // bottom up: tiles, PLAY, ruler
            float tilesCY = h - under - TileEdge - TileH * 0.5f;
            Ui.PlaceD(tilesRow, Ui.TC, Ui.C, new Vector2(0f, -tilesCY), new Vector2(CardW, TileH));
            Snap(tilesRow);

            float playTop = tilesCY - TileH * 0.5f - TilesToPlay - (PlayH + PlayEdge);
            playRoot.localScale = Vector3.one;
            Ui.PlaceD(playRoot, Ui.TC, new Vector2(0f, -playTop), new Vector2(PlayW, PlayH + PlayEdge));
            Snap(playRoot);

            float rulerY = playTop - PlayToRuler - BigD * 0.5f;
            Ui.PlaceD(ruler, Ui.TC, Ui.C, new Vector2(0f, -rulerY), new Vector2(390f, BigD));
            Snap(ruler);
            // children back on their design positions first, so repeated snaps never add up
            Ui.PlaceD(prev, Ui.C, new Vector2(-StepX, 0f), new Vector2(StepD, StepD));
            Ui.PlaceD(next, Ui.C, new Vector2(StepX, 0f), new Vector2(StepD, StepD));
            Snap(prev);
            Snap(next);
            for (int i = 0; i < slots.Length; i++)
            {
                float d = i == 2 ? BigD : SmallD;
                Ui.PlaceD(slots[i].rt, Ui.C, new Vector2(SlotX[i], 0f), new Vector2(d, d));
                Snap(slots[i].rt);
            }

            // hero card: from CardTop down to the ruler (flexes with the screen and the banner)
            float cardH = Mathf.Max(CardMinH, rulerY - BigD * 0.5f - RulerToCard - CardEdge - CardTop);
            Ui.PlaceD(card, Ui.TL, new Vector2(0f, 1f), new Vector2(CardX, -CardTop), new Vector2(CardW, cardH));
            Snap(card);
            LayoutChips();
            float previewH = cardH - PreviewPad - ChipInset - ChipH - 10f;
            var size = new Vector2(CardW - PreviewPad * 2f, Mathf.Max(60f, previewH - PreviewPad));
            Ui.PlaceD(previewHolder, Ui.TC, new Vector2(0.5f, 1f), new Vector2(0f, -PreviewPad), size);
            previewSize = size;
            DrawPreview(size);
        }

        /// <summary>The two chips along the bottom inside the card: shape chip left, theme chip right.</summary>
        void LayoutChips()
        {
            float sw = shapeChipText.preferredWidth / Theme.K + ChipPad * 2f;
            Ui.PlaceD(shapeChip.rectTransform, Ui.BL, new Vector2(0f, 0f), new Vector2(ChipInset, ChipInset), new Vector2(sw, ChipH));
            float tw = themeChipText.preferredWidth / Theme.K + ChipPad * 2f;
            Ui.PlaceD(themeChip.rectTransform, Ui.BR, new Vector2(1f, 0f), new Vector2(-ChipInset, ChipInset), new Vector2(tw, ChipH));
        }

        /// <summary>
        /// The current level's real board (the level PLAY starts: the shape for a shape level, the rectangle of arrows for a
        /// normal one) in the current theme, as large as fits; redrawn only on a change.
        /// </summary>
        void DrawPreview(Vector2 sizeD)
        {
            int level = selected;
            var theme = Theme.Current ?? Themes.Get(Themes.Default);
            string key = level + "|" + theme.Id + "|" + Mathf.RoundToInt(sizeD.x) + "x" + Mathf.RoundToInt(sizeD.y);
            if (key == previewKey && preview != null) return;
            var data = HeroLevel(level);
            if (data == null)
            {
                previewKey = null; // a generated level still building: the layout check tries again next frame
                return;
            }
            if (preview != null) Destroy(preview);
            preview = LevelPreview.Create(previewHolder, data, theme, sizeD, false).gameObject;
            previewKey = key;
        }

        // the last parsed hero level (Levels.TryGet: null while a generated level is still building)
        static int heroLevel;
        static LevelData heroData;

        static LevelData HeroLevel(int level)
        {
            if (heroData == null || heroLevel != level)
            {
                heroData = Levels.TryGet(level);
                heroLevel = level;
            }
            return heroData;
        }

        /// <summary>The backdrop covers the whole canvas (Root is the safe area), like the splash's background.</summary>
        void FitBackdrop()
        {
            var canvas = GameApp.I.CanvasRoot;
            canvas.GetWorldCorners(corners);
            Vector2 bl = Root.InverseTransformPoint(corners[0]);
            Vector2 tr = Root.InverseTransformPoint(corners[2]);
            Vector2 min = Root.rect.min;
            backdrop.anchorMin = backdrop.anchorMax = backdrop.pivot = Vector2.zero;
            backdrop.anchoredPosition = bl - min;
            backdrop.sizeDelta = tr - bl;
        }

        /// <summary>Moves and resizes rt (fixed anchors, unscaled) so its screen rect starts and ends on whole pixels.</summary>
        static void Snap(RectTransform rt)
        {
            float s = rt.lossyScale.x;
            if (s <= 0f) return;
            rt.GetWorldCorners(corners); // screen space overlay: world units are pixels
            Vector2 bl = corners[0], size = (Vector2)corners[2] - bl;
            var blR = new Vector2(Mathf.Round(bl.x), Mathf.Round(bl.y));
            var sizeR = new Vector2(Mathf.Round(size.x), Mathf.Round(size.y));
            Vector2 pivotOld = bl + Vector2.Scale(rt.pivot, size);
            Vector2 pivotNew = blR + Vector2.Scale(rt.pivot, sizeR);
            rt.sizeDelta += (sizeR - size) / s;
            rt.position += (Vector3)(pivotNew - pivotOld);
        }

        void OnBannerHeight()
        {
            laidOutH = -1f;
            Layout();
        }

        // ------------------------------------------------------------------ per frame

        /// <summary>From HomeTicker.Update: layout check, PLAY breathing, the pulsing ring and the pick pop.</summary>
        void Tick()
        {
            Layout();
            float t = Time.unscaledTime, dt = Time.unscaledDeltaTime;

            float breath = 1f + BreathAmp * (0.5f - 0.5f * Mathf.Cos(t * Mathf.PI * 2f / BreathPeriod));
            playRoot.localScale = new Vector3(breath, breath, 1f);

            float k = Mathf.Repeat(t, RingPeriod) / RingPeriod;
            float rs = 1f + RingGrow * EaseOutCubic(k);
            ring.localScale = new Vector3(rs, rs, 1f);
            var c = Theme.Accent;
            c.a = RingAlpha * (1f - k);
            ringImg.color = c;

            if (popT >= 0f)
            {
                popT += dt;
                float p = Mathf.Clamp01(popT / PopTime);
                float s = 1f + (PopScale - 1f) * Mathf.Sin(Mathf.PI * p); // 1 -> 1.12 -> 1
                slots[2].rt.localScale = new Vector3(s, s, 1f);
                if (p >= 1f) popT = -1f;
            }
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
