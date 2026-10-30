using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ArrowPuzzle
{
    /// <summary>
    /// "Winding road" level map for GameApp.I.CurrentMode: 50 nodes on a curvy road (PathGraphic) in a vertical
    /// ScrollRect over a mode-tinted sky with drifting clouds, a fixed header and a PLAY button.
    /// After a win that unlocked a new level (GameApp.I.UnlockPending) it plays the "level cleared" sequence.
    /// Design px on a 390 x 760 reference (y from the top of the safe area), unscaled time.
    /// </summary>
    public class ScreenLevels : AppScreen
    {
        // ---- road (design px; content y from its top)
        const int Count = ModeInfo.LevelsPerMode;
        const float Step = 104f, ContentH = Count * Step + 360f, BottomMargin = 230f;
        const float RoadX = 195f, RoadAmp = 86f, RoadFreq = 1.05f, RoadPhase = 0.4f;
        const float RoadW = 24f, ShadowW = 28f, ShadowDrop = 4f, DotD = 4f, DotEvery = 14f, DotAlpha = 0.85f, ProgressWhite = 0.5f;
        const int SamplesPerSegment = 10;

        // ---- nodes
        const float DoneR = 28f, DoneEdge = 5f, DoneFont = 24f;
        const float CurOuterR = 38f, CurInnerR = 31f, CurEdge = 6f, CurFont = 30f;
        const float LockedR = 26f, LockedFont = 22f, UnlockedR = 30f;
        const float BadgeX = 20f, BadgeY = 20f, BadgeR = 10f, BadgeIcon = 11f;
        const float StarsBelow = 44f, StarR = 6.5f, StarMidR = 7.5f, StarMidRaise = 2f, StarGap = 15f;
        const float PulseR0 = 38f, PulseR1 = 60f, PulseW = 5f, PulsePeriod = 1.1f;
        const float PinUp = 62f, PinR = 16f, PinBob = 5f, PinSpeed = 3.2f;
        const float ShakeAmp = 3f, ShakeTime = 0.25f, DragStartD = 12f;

        // ---- header / footer
        const float BackX = 38f, HeaderY = 64f, BackD = 40f, SubY = 86f, PillW = 98f, PillH = 36f, TopFadeH = 128f;
        const float BottomFadeH = 170f, PlayY = 664f, PlayW = 300f, PlayH = 72f, PlayRadius = 36f, PlayEdge = 6f;

        // ---- normal intro
        const float GlideDelay = 0.35f, GlideTime = 1.95f, FocusY = 430f;
        const int IntroFromLevel = 3;
        const float NodesFade = 0.5f, HeaderFade = 0.4f, PlayRise = 70f, PlayT0 = 0.6f, PlayT1 = 1.2f;

        // ---- sky and clouds
        const float SkyTopWhite = 0.68f, SkyMidWhite = 0.88f, SkyMidAt = 0.55f;
        const int CloudCount = 10;
        const float CloudMinW = 80f, CloudMaxW = 170f, CloudAspect = 0.55f;
        const float FarScale = 0.72f, FarAlpha = 0.65f, FarParallax = 0.2f, NearAlpha = 0.95f, NearParallax = 0.42f;
        const float CloudDriftMin = 4f, CloudDriftMax = 11f;

        // ---- level-cleared sequence (s since OnShow)
        const float SeqWhiteOut = 0.4f;
        const float Chip1T0 = 0.25f, Chip1T1 = 2.1f, Chip2T0 = 2.55f, Chip2T1 = 4.8f, ChipY = 118f, ChipH = 34f, ChipSlide = 12f;
        const float ChipIn = 0.25f, ChipOut = 0.3f;
        const float MorphT0 = 0.5f, MorphT1 = 0.85f, MorphPop = 0.12f;
        const float StarPopT0 = 0.7f, StarPopEvery = 0.3f, StarPopTime = 0.4f, StarPopBack = 2.6f, StarPopBig = 0.35f;
        const float PinOutT0 = 1.3f, PinOutT1 = 1.6f;
        const float RunT0 = 1.6f, RunT1 = 2.5f, RunBody = 48f, RunThick = 11f, RunHead = 24f, RunLag = 0.05f, RunGhostAlpha = 0.28f;
        const int RunGhosts = 4;
        const float UnlockT = 2.5f, PadlockTime = 0.45f, PadlockRise = 30f, PadlockSpin = 40f, NodePopTime = 0.35f, NodePopBack = 2.4f;
        const int BurstCount = 14;
        const float BurstR = 74f, BurstTime = 0.9f, RingR0 = 24f, RingR1 = 90f;
        const float PinDropT0 = 3.0f, PinDropT1 = 3.5f, PinDropFrom = 320f, PinSquash = 0.8f, PinSquashTime = 0.3f, LandGrowTime = 0.25f;
        const float CamT0 = 3.6f, CamT1 = 4.4f, PlayPulseTime = 0.35f, PlayPulse = 0.07f;
        const float SeqEnd = 4.4f;

        static readonly Color CreamBottom = Theme.Hex("#FBF6EC");
        static readonly Color Track = Theme.Hex("#EFE6D2"), RoadShadow = new Color(120f / 255f, 100f / 255f, 60f / 255f, 0.13f);
        static readonly Color StarOff = Theme.Hex("#D8D0C0"), CloudBottom = Theme.Hex("#DDEDEB");
        static readonly Color LockedFill = Theme.Hex("#DCD5C4"), LockedEdge = Theme.Hex("#C9C1AF"), LockedNum = Theme.Hex("#A59D8B");
        static readonly Color ChipCoral = Theme.Hex("#E4572E"), ChipGold = Theme.Hex("#FFB703"), ChipGoldInk = Theme.Hex("#3A2A00");

        // ------------------------------------------------------------------ node visuals

        /// <summary>Everything that differs between the done / current / locked looks, so they can be blended.</summary>
        struct Look
        {
            public float bodyR, bodyEdgeD, ringR, ringA, ringEdgeD, font;
            public Color body, bodyEdge, ringEdge, num;

            public static Look Lerp(Look a, Look b, float t)
            {
                float c = Mathf.Clamp01(t);
                return new Look
                {
                    bodyR = Mathf.LerpUnclamped(a.bodyR, b.bodyR, t), ringR = Mathf.LerpUnclamped(a.ringR, b.ringR, t),
                    bodyEdgeD = Mathf.Lerp(a.bodyEdgeD, b.bodyEdgeD, c), ringEdgeD = Mathf.Lerp(a.ringEdgeD, b.ringEdgeD, c),
                    ringA = Mathf.Lerp(a.ringA, b.ringA, c), font = Mathf.Lerp(a.font, b.font, c),
                    body = Color.Lerp(a.body, b.body, c), bodyEdge = Color.Lerp(a.bodyEdge, b.bodyEdge, c),
                    ringEdge = Color.Lerp(a.ringEdge, b.ringEdge, c), num = Color.Lerp(a.num, b.num, c),
                };
            }
        }

        class Node
        {
            public int level;
            public Vector2 pos;               // content local, design px (y up)
            public RectTransform root, visual, stars, badge;
            public Image ring, body;
            public Shadow ringEdge, bodyEdge;
            public Text num;
            public readonly Image[] starImg = new Image[3];
            public readonly RectTransform[] starRt = new RectTransform[3];
            public CanvasGroup badgeGroup;
            public Shimmer shimmer;
        }

        Look DoneLook(ModeInfo.Entry m, float r)
        {
            return new Look { bodyR = r, bodyEdgeD = DoneEdge, ringR = r, ringA = 0f, ringEdgeD = 0f, font = DoneFont,
                              body = m.Color, bodyEdge = m.Edge, ringEdge = m.Edge, num = Theme.Card };
        }

        Look CurrentLook(ModeInfo.Entry m)
        {
            var clear = new Color(m.Edge.r, m.Edge.g, m.Edge.b, 0f);
            return new Look { bodyR = CurInnerR, bodyEdgeD = 0f, ringR = CurOuterR, ringA = 1f, ringEdgeD = CurEdge, font = CurFont,
                              body = m.Color, bodyEdge = clear, ringEdge = m.Edge, num = Theme.Card };
        }

        static Look LockedLook()
        {
            return new Look { bodyR = LockedR, bodyEdgeD = DoneEdge, ringR = LockedR, ringA = 0f, ringEdgeD = 0f, font = LockedFont,
                              body = LockedFill, bodyEdge = LockedEdge, ringEdge = LockedEdge, num = LockedNum };
        }

        static void Apply(Node n, Look l)
        {
            n.body.rectTransform.sizeDelta = Vector2.one * Theme.D(l.bodyR * 2f);
            n.body.color = l.body;
            n.bodyEdge.effectColor = l.bodyEdge;
            n.bodyEdge.effectDistance = new Vector2(0f, -Theme.D(l.bodyEdgeD));
            n.ring.rectTransform.sizeDelta = Vector2.one * Theme.D(l.ringR * 2f);
            n.ring.color = new Color(Theme.Card.r, Theme.Card.g, Theme.Card.b, l.ringA);
            var re = l.ringEdge;
            re.a *= l.ringA;
            n.ringEdge.effectColor = re;
            n.ringEdge.effectDistance = new Vector2(0f, -Theme.D(l.ringEdgeD));
            n.num.color = l.num;
            n.num.fontSize = Mathf.RoundToInt(Theme.D(l.font));
        }

        // ------------------------------------------------------------------ state

        GameMode mode;
        ModeInfo.Entry info;
        int current;            // level with the pin
        int starTotal;
        float t, glideFrom, glideTo;
        bool gliding, thresholdPending;
        int savedThreshold = -1;

        // sequence
        bool seq, seqDone;
        int seqLevel, seqStars, starsLanded;
        bool[] starPopped = new bool[3];
        bool unlockFired, landed, playSwapped;
        Node nodeN, nodeN1;

        ScrollRect scroll;
        RectTransform viewport, content, nodeLayer, fxLayer, pin, pinShadow, footerPlay, header;
        CanvasGroup contentGroup, headerGroup, pinGroup;
        PathGraphic shadow, track, progress, dots;
        ArrowGraphic pulse;
        Text title, subtitle, starsText, playLabel;
        Image sky, topFade, playImg, whiteOut;
        Shadow playEdge;
        GameObject catcher;
        RectTransform chip1, chip2;
        CanvasGroup chip1Group, chip2Group;
        Text chip1Text, chip2Text;

        readonly List<Node> nodes = new List<Node>(Count);
        readonly List<Vector2> road = new List<Vector2>(Count * SamplesPerSegment + 8);
        float[] roadCum;
        readonly int[] nodeSample = new int[Count + 1];
        readonly List<Vector2> tmp = new List<Vector2>(Count * SamplesPerSegment + 8);
        readonly List<Vector2> ringPts = new List<Vector2>(49);

        // pools (made once)
        readonly ArrowGraphic[] run = new ArrowGraphic[RunGhosts + 1];
        readonly ArrowGraphic[] rings = new ArrowGraphic[4]; // 0..2 star sparkles, 3 unlock ring
        readonly float[] ringStart = { -1f, -1f, -1f, -1f };
        readonly Vector2[] ringCenter = new Vector2[4];
        readonly Image[] burst = new Image[BurstCount];
        readonly Vector2[] burstDir = new Vector2[BurstCount];

        // clouds
        readonly RectTransform[] clouds = new RectTransform[CloudCount];
        readonly Vector2[] cloudBase = new Vector2[CloudCount];
        readonly float[] cloudSpeed = new float[CloudCount];
        readonly float[] cloudParallax = new float[CloudCount];
        static Sprite[] cloudSprites;
        static readonly Dictionary<GameMode, Sprite> skySprites = new Dictionary<GameMode, Sprite>();
        static readonly Dictionary<GameMode, Sprite> fadeSprites = new Dictionary<GameMode, Sprite>();

        RectTransform shaking;
        Vector2 shakeHome;
        float shakeT = -1f;
        Vector2 pinHome, playHome;

        protected override void Build()
        {
            sky = Ui.Img(Root, "Sky", null, Theme.Card);
            sky.raycastTarget = false;
            Ui.Fill(sky.rectTransform);
            sky.rectTransform.offsetMin = new Vector2(0f, -Theme.D(60f)); // a little past the safe area
            sky.rectTransform.offsetMax = new Vector2(0f, Theme.D(60f));

            BuildScroll();
            BuildFooter();

            // tap anywhere during the unlock sequence = skip (the back button sits above it)
            var c = Ui.Img(Root, "SkipCatcher", null, Color.clear);
            Ui.Fill(c.rectTransform);
            var cb = c.gameObject.AddComponent<Button>();
            cb.transition = Selectable.Transition.None;
            cb.onClick.AddListener(SkipSequence);
            catcher = c.gameObject;
            catcher.SetActive(false);

            BuildHeader();
            chip1 = Chip("Chip1", ChipCoral, Theme.Card, out chip1Group, out chip1Text);
            chip2 = Chip("Chip2", ChipGold, ChipGoldInk, out chip2Group, out chip2Text);

            whiteOut = Ui.Img(Root, "WhiteOut", null, Theme.Card);
            whiteOut.raycastTarget = false;
            Ui.Fill(whiteOut.rectTransform);
            whiteOut.rectTransform.offsetMin = new Vector2(0f, -Theme.D(60f));
            whiteOut.rectTransform.offsetMax = new Vector2(0f, Theme.D(60f));
            whiteOut.gameObject.SetActive(false);

            BuildRoadPoints();
        }

        // ------------------------------------------------------------------ build

        void BuildScroll()
        {
            var area = Ui.New(Root, "Scroll");
            Ui.Fill(area);
            scroll = area.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.inertia = true;
            var input = area.gameObject.AddComponent<LevelsMapInput>(); // touch cancels the intro glide
            input.touch = () => gliding = false;
            input.tick = Tick;

            var vp = Ui.Img(area, "Viewport", null, Color.clear);
            viewport = vp.rectTransform;
            Ui.Fill(viewport);
            vp.gameObject.AddComponent<RectMask2D>();
            scroll.viewport = viewport;

            BuildClouds(viewport);

            content = Ui.New(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, Theme.D(ContentH));
            content.anchoredPosition = Vector2.zero;
            scroll.content = content;
            contentGroup = content.gameObject.AddComponent<CanvasGroup>();

            shadow = Path(content, "RoadShadow", Theme.D(ShadowW), 0f, RoadShadow);
            shadow.rectTransform.anchoredPosition = new Vector2(0f, -Theme.D(ShadowDrop));
            track = Path(content, "Track", Theme.D(RoadW), 0f, Track);
            progress = Path(content, "Progress", Theme.D(RoadW), 0f, Theme.Card);
            dots = Path(content, "CenterDots", Theme.D(DotD), Theme.D(DotEvery),
                        new Color(Theme.Card.r, Theme.Card.g, Theme.Card.b, DotAlpha));

            // running arrow + ghosts (ghosts first, so they are behind)
            for (int g = RunGhosts; g >= 0; g--)
            {
                var rt = Ui.New(content, g == 0 ? "RunArrow" : "RunGhost" + g);
                Ui.PlaceD(rt, Ui.TL, Vector2.zero, new Vector2(390f, ContentH));
                var a = rt.gameObject.AddComponent<ArrowGraphic>();
                a.raycastTarget = false;
                a.thickness = Theme.D(RunThick);
                a.headSize = Theme.D(RunHead);
                var col = ChipCoral;
                col.a = g == 0 ? 1f : RunGhostAlpha / g;
                a.color = col;
                rt.gameObject.SetActive(false);
                run[g] = a;
            }

            // pulse behind the nodes
            var pulseRt = Ui.New(content, "Pulse");
            Ui.PlaceD(pulseRt, Ui.TL, Ui.C, Vector2.zero, Vector2.one * (PulseR1 * 2f + 10f)); // real rect: RectMask2D culling
            pulse = pulseRt.gameObject.AddComponent<ArrowGraphic>();
            pulse.drawHead = false;
            pulse.raycastTarget = false;
            pulse.thickness = Theme.D(PulseW);

            nodeLayer = Ui.New(content, "Nodes");
            Ui.PlaceD(nodeLayer, Ui.TL, Vector2.zero, Vector2.zero);

            // pin (with its shadow) above the nodes
            var sh = Ui.Img(content, "PinShadow", Theme.Circle, new Color(0f, 0f, 0f, 0.15f));
            sh.raycastTarget = false;
            pinShadow = sh.rectTransform;
            Ui.PlaceD(pinShadow, Ui.TL, Ui.C, Vector2.zero, new Vector2(22f, 6f));
            pin = Ui.New(content, "Pin");
            Ui.PlaceD(pin, Ui.TL, Ui.C, Vector2.zero, new Vector2(PinR * 2f, PinR * 2f));
            pinGroup = pin.gameObject.AddComponent<CanvasGroup>();
            var tip = Ui.Img(pin, "Tip", Theme.Triangle, Theme.Accent);
            tip.raycastTarget = false;
            Ui.PlaceD(tip.rectTransform, Ui.C, new Vector2(0f, -PinR * 0.9f), new Vector2(PinR * 1.3f, PinR * 1.3f));
            tip.rectTransform.localEulerAngles = new Vector3(0f, 0f, -90f); // triangle sprite points right -> down
            var headImg = Ui.Img(pin, "Head", Theme.Circle, Theme.Accent);
            headImg.raycastTarget = false;
            Ui.Fill(headImg.rectTransform);
            var tri = Icons.Make(headImg.transform, Icon.Play, Theme.Card, 14f);
            Ui.PlaceD(tri, Ui.C, new Vector2(1f, 0f), new Vector2(14f, 14f));

            // effects above everything on the road
            fxLayer = Ui.New(content, "Fx");
            Ui.PlaceD(fxLayer, Ui.TL, Vector2.zero, Vector2.zero);
            for (int i = 0; i < rings.Length; i++)
            {
                var rrt = Ui.New(fxLayer, "Ring" + i);
                Ui.PlaceD(rrt, Ui.TL, Ui.C, Vector2.zero, Vector2.one * (RingR1 * 2f + 10f));
                var g = rrt.gameObject.AddComponent<ArrowGraphic>();
                g.drawHead = false;
                g.raycastTarget = false;
                g.color = Theme.Gold;
                rrt.gameObject.SetActive(false);
                rings[i] = g;
            }
            var rng = new System.Random(3);
            for (int i = 0; i < BurstCount; i++)
            {
                bool star = i % 2 == 0;
                var b = Ui.Img(fxLayer, "Burst", star ? Theme.Star : Theme.Circle, Theme.Gold);
                b.raycastTarget = false;
                float size = star ? 12f : 8f;
                Ui.PlaceD(b.rectTransform, Ui.TL, Ui.C, Vector2.zero, new Vector2(size, size));
                b.gameObject.SetActive(false);
                burst[i] = b;
                float a = (i + (float)rng.NextDouble() * 0.6f) / BurstCount * Mathf.PI * 2f;
                burstDir[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (0.75f + (float)rng.NextDouble() * 0.25f);
            }
        }

        static PathGraphic Path(RectTransform parent, string name, float width, float spacing, Color color)
        {
            var rt = Ui.New(parent, name);
            // content-sized rect with the pivot at the top-left: points are (x, -y) from there, and RectMask2D
            // doesn't cull the road while any of it is on screen
            Ui.PlaceD(rt, Ui.TL, Vector2.zero, new Vector2(390f, ContentH));
            var g = rt.gameObject.AddComponent<PathGraphic>();
            g.raycastTarget = false;
            g.width = width;
            g.dotSpacing = spacing;
            g.color = color;
            return g;
        }

        void BuildClouds(RectTransform parent)
        {
            if (cloudSprites == null)
            {
                cloudSprites = new[]
                {
                    CloudSprite(new[] { .30f, .02f, .20f, .55f, -.22f, .27f, .78f, .06f, .17f }),
                    CloudSprite(new[] { .24f, .06f, .16f, .45f, -.26f, .24f, .68f, -.06f, .20f, .84f, .12f, .12f }),
                    CloudSprite(new[] { .28f, -.04f, .22f, .52f, -.30f, .25f, .74f, -.02f, .19f }),
                };
            }

            var far = Ui.New(parent, "CloudsFar");
            Ui.Fill(far);
            var near = Ui.New(parent, "CloudsNear");
            Ui.Fill(near);
            var rng = new System.Random(5);
            for (int i = 0; i < CloudCount; i++)
            {
                bool isFar = rng.NextDouble() < 0.5;
                float w = Mathf.Lerp(CloudMinW, CloudMaxW, (float)rng.NextDouble()) * (isFar ? FarScale : 1f);
                var img = Ui.Img(isFar ? far : near, "Cloud", cloudSprites[i % cloudSprites.Length],
                                 new Color(1f, 1f, 1f, isFar ? FarAlpha : NearAlpha));
                img.raycastTarget = false;
                Ui.PlaceD(img.rectTransform, Ui.TL, Ui.C, Vector2.zero, new Vector2(w, w * CloudAspect));
                clouds[i] = img.rectTransform;
                cloudBase[i] = new Vector2((float)rng.NextDouble() * 390f, (float)rng.NextDouble() * 900f);
                cloudSpeed[i] = Mathf.Lerp(CloudDriftMin, CloudDriftMax, (float)rng.NextDouble()) * (rng.NextDouble() < 0.5 ? -1f : 1f);
                cloudParallax[i] = isFar ? FarParallax : NearParallax;
            }
        }

        /// <summary>
        /// Flat-bottomed puffy cloud: a pill base (height 0.34 x width) united with round bumps
        /// (x, y offset in base heights, radius; fractions of the width), cut flat at the base bottom.
        /// White over the top 62 %, fading to #DDEDEB at the bottom edge. 3x3 supersampled.
        /// </summary>
        static Sprite CloudSprite(float[] bumps)
        {
            const int w = 256, ss = 3;
            int h = Mathf.RoundToInt(w * CloudAspect);
            const float baseH = 0.34f;
            float texH = CloudAspect, baseTop = texH - baseH, r = baseH * 0.5f;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float frac = 1f - (y + 0.5f) / h; // 0 at the top
                Color c = frac < 0.62f ? Color.white : Color.Lerp(Color.white, CloudBottom, (frac - 0.62f) / 0.38f);
                for (int x = 0; x < w; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < ss; sy++)
                    {
                        for (int sx = 0; sx < ss; sx++)
                        {
                            float u = (x + (sx + 0.5f) / ss) / w;                  // 0..1 across
                            float v = (1f - (y + (sy + 0.5f) / ss) / h) * texH;    // down from the top, in widths
                            if (v > texH) continue;                               // flat bottom
                            bool inside = false;
                            if (v >= baseTop)
                            {
                                float cx = Mathf.Clamp(u, r, 1f - r), cy = baseTop + r;
                                inside = (u - cx) * (u - cx) + (v - cy) * (v - cy) <= r * r || (u >= r && u <= 1f - r);
                            }
                            for (int k = 0; !inside && k + 2 < bumps.Length; k += 3)
                            {
                                float bx = bumps[k], by = baseTop + (0.5f + bumps[k + 1]) * baseH, br = bumps[k + 2];
                                inside = (u - bx) * (u - bx) + (v - by) * (v - by) <= br * br;
                            }
                            if (inside) hit++;
                        }
                    }
                    var col = c;
                    col.a = hit / (float)(ss * ss);
                    px[y * w + x] = col;
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        void BuildHeader()
        {
            topFade = Ui.Img(Root, "TopFade", null, Theme.Card);
            topFade.raycastTarget = false;
            Ui.PlaceD(topFade.rectTransform, Ui.TC, Vector2.zero, new Vector2(390f, TopFadeH));

            header = Ui.New(Root, "Header");
            Ui.Fill(header);
            headerGroup = header.gameObject.AddComponent<CanvasGroup>();

            var back = Ui.Btn(header, "", Theme.Card, Theme.Ink, 0f, BackD * 0.5f, Theme.CardShadow, 0f, () =>
            {
                if (seq) SkipSequence(); // jump to the end first, then leave
                GameApp.I.Show("ScreenHome");
            }, Icon.Back, 22f);
            Ui.PlaceD((RectTransform)back.transform, Ui.TL, Ui.C, new Vector2(BackX, -HeaderY), new Vector2(BackD, BackD));

            title = Ui.Label(header, "", 26f, Theme.Ink, Weight.Bold);
            Ui.PlaceD(title.rectTransform, Ui.TC, Ui.C, new Vector2(0f, -HeaderY), new Vector2(200f, 32f));
            subtitle = Ui.Label(header, "", 14f, Theme.InkSoft, Weight.Medium);
            Ui.PlaceD(subtitle.rectTransform, Ui.TC, Ui.C, new Vector2(0f, -SubY), new Vector2(200f, 18f));

            var pill = Ui.Img(header, "Stars", Theme.Rounded, Theme.Card, PillH * 0.5f);
            pill.raycastTarget = false;
            Ui.PlaceD(pill.rectTransform, Ui.TR, Ui.C, new Vector2(-(24f + PillW * 0.5f), -HeaderY), new Vector2(PillW, PillH));
            Ui.Row(pill.rectTransform, 6f);
            var star = Icons.Make(pill.transform, Icon.Star, Theme.Gold, 18f);
            Ui.Size(star, 18f, 18f);
            starsText = Ui.Label(pill.transform, "0/150", 17f, Theme.Ink, Weight.Bold);
        }

        RectTransform Chip(string name, Color bg, Color fg, out CanvasGroup group, out Text text)
        {
            var img = Ui.Img(Root, name, Theme.Rounded, bg, ChipH * 0.5f);
            img.raycastTarget = false;
            var rt = img.rectTransform;
            Ui.PlaceD(rt, Ui.TC, new Vector2(0f, -ChipY), new Vector2(0f, ChipH));
            var row = Ui.Row(rt, 0f);
            int pad = Mathf.RoundToInt(Theme.D(16f));
            row.padding = new RectOffset(pad, pad, 0, 0);
            img.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            text = Ui.Label(rt, "", 16f, fg, Weight.Bold);
            group = img.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            return rt;
        }

        void BuildFooter()
        {
            var fade = Ui.Img(Root, "BottomFade", Gradient(new Color(CreamBottom.r, CreamBottom.g, CreamBottom.b, 0f), CreamBottom), Theme.Card);
            fade.raycastTarget = false;
            Ui.PlaceD(fade.rectTransform, Ui.BC, Vector2.zero, new Vector2(390f, BottomFadeH));

            var play = Ui.Btn(Root, "PLAY  Lv 1", Theme.Teal, Theme.Card, 26f, PlayRadius, Theme.TealShadow, PlayEdge,
                              () => { if (!seq) StartLevel(current); }, Icon.Play, 24f);
            footerPlay = (RectTransform)play.transform;
            Ui.PlaceD(footerPlay, Ui.TC, Ui.C, new Vector2(0f, -PlayY), new Vector2(PlayW, PlayH));
            playHome = footerPlay.anchoredPosition;
            playImg = play.GetComponent<Image>();
            playEdge = play.GetComponent<Shadow>();
            playLabel = play.GetComponentInChildren<Text>();
            Ui.AddShimmer(footerPlay, PlayRadius, 0.5f);
        }

        // ------------------------------------------------------------------ road geometry

        static Vector2 NodeD(int level)
        {
            return new Vector2(RoadX + RoadAmp * Mathf.Sin(level * RoadFreq + RoadPhase),
                               ContentH - BottomMargin - (level - 1) * Step);
        }

        static Vector2 NodeLocal(int level)
        {
            var d = NodeD(level);
            return new Vector2(d.x, -d.y);
        }

        /// <summary>
        /// Smooth road: straight from node 1 to the first midpoint, quadratics through the midpoints with the
        /// nodes as control points, straight to node 50. nodeSample[L] = the sample nearest to node L.
        /// Stored in canvas units as (x, -y) from the content's top-left; roadCum = arc length at each sample.
        /// </summary>
        void BuildRoadPoints()
        {
            road.Clear();
            Add(NodeD(1));
            nodeSample[1] = 0;
            Vector2 prevMid = (NodeD(1) + NodeD(2)) * 0.5f;
            AddLine(NodeD(1), prevMid, SamplesPerSegment / 2);
            for (int L = 2; L < Count; L++)
            {
                Vector2 mid = (NodeD(L) + NodeD(L + 1)) * 0.5f;
                Vector2 c = NodeD(L);
                for (int k = 1; k <= SamplesPerSegment; k++)
                {
                    float u = k / (float)SamplesPerSegment;
                    float a = 1f - u;
                    Add(a * a * prevMid + 2f * a * u * c + u * u * mid);
                    if (k == SamplesPerSegment / 2) nodeSample[L] = road.Count - 1;
                }
                prevMid = mid;
            }
            AddLine(prevMid, NodeD(Count), SamplesPerSegment / 2);
            nodeSample[Count] = road.Count - 1;
            roadCum = Poly.Cum(road);

            shadow.SetPath(road);
            track.SetPath(road);
            dots.SetPath(road);
        }

        void AddLine(Vector2 a, Vector2 b, int n)
        {
            for (int k = 1; k <= n; k++) Add(Vector2.Lerp(a, b, k / (float)n));
        }

        void Add(Vector2 d)
        {
            road.Add(new Vector2(d.x, -d.y) * Theme.K);
        }

        /// <summary>Progress layer from level 1 to arc length s (exact end, no allocation).</summary>
        void ProgressTo(float s)
        {
            tmp.Clear();
            for (int i = 0; i < road.Count && roadCum[i] < s; i++) tmp.Add(road[i]);
            tmp.Add(Poly.PointAt(road, roadCum, s));
            progress.SetPath(tmp);
        }

        float ArcAt(int level)
        {
            return roadCum[nodeSample[Mathf.Clamp(level, 1, Count)]];
        }

        // ------------------------------------------------------------------ show

        public override void OnShow()
        {
            mode = GameApp.I.CurrentMode;
            info = ModeInfo.Get(mode);
            int unlocked = Save.GetUnlocked(mode);

            var app = GameApp.I;
            seq = app.UnlockPending && app.ClearedMode == mode && app.ClearedLevel >= 1 &&
                  app.ClearedLevel < Count && app.ClearedLevel + 1 == unlocked;
            if (app.UnlockPending && !seq) app.UnlockPending = false; // out of date, show the map normally
            seqDone = false;

            starTotal = 0;
            for (int n = 1; n <= Count; n++) starTotal += Save.Stars(mode, n);

            ApplyModeColors();
            t = 0f;
            thresholdPending = true;
            Canvas.ForceUpdateCanvases();

            if (seq)
            {
                seqLevel = app.ClearedLevel;
                seqStars = Mathf.Clamp(app.ClearedStars, 1, 3);
                starsLanded = 0;
                for (int k = 0; k < 3; k++) starPopped[k] = false;
                unlockFired = landed = playSwapped = false;
                for (int i = 0; i < ringStart.Length; i++) ringStart[i] = -1f;

                // draw as if N were still the current level and N+1 locked
                current = seqLevel;
                BuildNodes(current);
                nodeN = nodes[seqLevel - 1];
                nodeN1 = nodes[seqLevel];
                EnsureShimmer(nodeN1, false);
                PlaceCurrentFx(nodeN);
                ProgressTo(ArcAt(current));
                SetHeaderTexts(current, starTotal - seqStars);
                playLabel.text = "PLAY  Lv " + current;

                gliding = false;
                scroll.StopMovement();
                SetScroll(FocusFor(seqLevel));
                catcher.SetActive(true);
                whiteOut.gameObject.SetActive(true);
                contentGroup.alpha = 1f;
                footerPlay.anchoredPosition = playHome;
                Sequence(0f);
            }
            else
            {
                current = Mathf.Clamp(unlocked, 1, Count);
                ShowNormal();

                // normal intro: start with level 3 in focus, glide to the current level
                glideFrom = FocusFor(IntroFromLevel);
                glideTo = FocusFor(current);
                gliding = true;
                scroll.StopMovement();
                SetScroll(glideFrom);
                catcher.SetActive(false);
                whiteOut.gameObject.SetActive(false);
                chip1Group.alpha = chip2Group.alpha = 0f;
                NormalApply();
            }
        }

        /// <summary>The normal map for `current`: nodes, progress, pin, pulse, texts.</summary>
        void ShowNormal()
        {
            BuildNodes(current);
            ProgressTo(ArcAt(current));
            SetHeaderTexts(current, starTotal);
            playLabel.text = "PLAY  Lv " + current;
            PlaceCurrentFx(nodes[current - 1]);
        }

        void ApplyModeColors()
        {
            sky.sprite = SkySprite(mode, info.Color);
            topFade.sprite = FadeSprite(mode, info.Color);
            progress.color = Color.Lerp(info.Color, Color.white, ProgressWhite);
            pulse.color = info.Color;
            playImg.color = info.Color;
            playEdge.effectColor = info.Edge;
        }

        void SetHeaderTexts(int level, int starsShown)
        {
            title.text = info.Name;
            subtitle.text = "Level " + level + " of " + Count;
            starsText.text = starsShown + "/" + Count * 3;
        }

        public override void OnHide()
        {
            if (seq) SkipSequence();
            gliding = false;
            var es = EventSystem.current;
            if (es != null && savedThreshold >= 0) es.pixelDragThreshold = savedThreshold;
            savedThreshold = -1;
            if (shaking != null) shaking.anchoredPosition = shakeHome;
            shaking = null;
        }

        public override bool OnBack()
        {
            if (seq) SkipSequence();
            GameApp.I.Show("ScreenHome");
            return true;
        }

        /// <summary>Scroll offset (design px) that puts level L at screen y FocusY, clamped to the content.</summary>
        float FocusFor(int level)
        {
            float viewH = viewport.rect.height / Theme.K;
            return Mathf.Clamp(NodeD(level).y - FocusY, 0f, Mathf.Max(0f, ContentH - viewH));
        }

        void SetScroll(float offsetD)
        {
            content.anchoredPosition = new Vector2(0f, Theme.D(offsetD));
            scroll.velocity = Vector2.zero;
        }

        void StartLevel(int level)
        {
            // CurrentMode stays; ScreenGame.OnShow starts CurrentLevel (via StartLevel) in that mode
            GameApp.I.CurrentMode = mode;
            GameApp.I.CurrentLevel = level;
            GameApp.I.Show("ScreenGame");
        }

        // ------------------------------------------------------------------ nodes

        void BuildNodes(int cur)
        {
            for (int i = nodeLayer.childCount - 1; i >= 0; i--)
            {
                var child = nodeLayer.GetChild(i).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }
            nodes.Clear();

            for (int L = 1; L <= Count; L++)
            {
                var n = MakeNode(L);
                nodes.Add(n);
                if (L == cur) { Apply(n, CurrentLook(info)); SetStars(n, false); SetBadge(n, false); EnsureShimmer(n, true); }
                else if (L < cur) { Apply(n, DoneLook(info, DoneR)); SetStars(n, true); SetBadge(n, false); }
                else { Apply(n, LockedLook()); SetStars(n, false); SetBadge(n, true); }
            }
        }

        Node MakeNode(int level)
        {
            var n = new Node { level = level, pos = NodeLocal(level) };
            n.root = Ui.New(nodeLayer, "Node" + level);
            Ui.PlaceD(n.root, Ui.TL, Ui.C, n.pos, new Vector2(CurOuterR * 2f, CurOuterR * 2f));

            n.visual = Ui.New(n.root, "Visual");
            Ui.Fill(n.visual);
            n.visual.pivot = Ui.C;

            n.ring = Ui.Img(n.visual, "Ring", Theme.Circle, Theme.Card);
            n.ring.raycastTarget = false;
            Ui.PlaceD(n.ring.rectTransform, Ui.C, Vector2.zero, Vector2.one * CurOuterR * 2f);
            n.ringEdge = n.ring.gameObject.AddComponent<Shadow>();
            n.ringEdge.useGraphicAlpha = false;

            n.body = Ui.Img(n.visual, "Body", Theme.Circle, info.Color);
            Ui.PlaceD(n.body.rectTransform, Ui.C, Vector2.zero, Vector2.one * DoneR * 2f);
            n.bodyEdge = n.body.gameObject.AddComponent<Shadow>();
            n.bodyEdge.useGraphicAlpha = false;

            n.num = Ui.Label(n.body.transform, level.ToString(), DoneFont, Theme.Card, Weight.Bold);
            Ui.PlaceD(n.num.rectTransform, Ui.C, Vector2.zero, new Vector2(80f, 40f));

            // tap: start a reachable level, shake a locked one (the body is the hit area)
            var btn = n.root.gameObject.AddComponent<Button>();
            btn.targetGraphic = n.body;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => OnNodeTap(n));

            // three mini stars under done nodes
            n.stars = Ui.New(n.root, "Stars");
            Ui.PlaceD(n.stars, Ui.C, new Vector2(0f, -StarsBelow), Vector2.zero);
            int earned = Save.Stars(mode, level);
            for (int s = 0; s < 3; s++)
            {
                float r = s == 1 ? StarMidR : StarR;
                var st = Icons.Make(n.stars, Icon.Star, s < earned ? Theme.Gold : StarOff, r * 2f);
                Ui.PlaceD(st, Ui.C, new Vector2((s - 1) * StarGap, s == 1 ? StarMidRaise : 0f), new Vector2(r * 2f, r * 2f));
                n.starRt[s] = st;
                n.starImg[s] = st.GetComponentInChildren<Image>();
            }

            // padlock badge for locked nodes
            var badge = Ui.Img(n.root, "Padlock", Theme.Circle, LockedNum);
            badge.raycastTarget = false;
            n.badge = badge.rectTransform;
            Ui.PlaceD(n.badge, Ui.C, new Vector2(BadgeX, BadgeY), Vector2.one * BadgeR * 2f);
            var lockIcon = Icons.Make(n.badge, Icon.Lock, Theme.Card, BadgeIcon);
            Ui.PlaceD(lockIcon, Ui.C, Vector2.zero, Vector2.one * BadgeIcon);
            n.badgeGroup = badge.gameObject.AddComponent<CanvasGroup>();
            return n;
        }

        static void SetStars(Node n, bool on)
        {
            n.stars.gameObject.SetActive(on);
        }

        static void SetBadge(Node n, bool on)
        {
            n.badge.gameObject.SetActive(on);
            n.badge.anchoredPosition = new Vector2(Theme.D(BadgeX), Theme.D(BadgeY));
            n.badge.localEulerAngles = Vector3.zero;
            n.badgeGroup.alpha = 1f;
        }

        void EnsureShimmer(Node n, bool on)
        {
            if (n.shimmer == null) n.shimmer = Ui.AddShimmer(n.ring.rectTransform, CurOuterR, 0.8f);
            n.shimmer.gameObject.SetActive(on);
        }

        void OnNodeTap(Node n)
        {
            if (seq) return; // only from the final state
            Sfx.Play(Sfx.Id.Click);
            if (n.level <= current) { StartLevel(n.level); return; }
            if (shaking != null) shaking.anchoredPosition = shakeHome;
            shaking = n.root;
            shakeHome = n.root.anchoredPosition;
            shakeT = 0f;
        }

        void PlaceCurrentFx(Node n)
        {
            pulse.rectTransform.anchoredPosition = n.pos * Theme.K;
            pulse.gameObject.SetActive(true);
            pinHome = (n.pos + new Vector2(0f, PinUp)) * Theme.K;
            pin.anchoredPosition = pinHome;
            pin.localScale = Vector3.one;
            pinGroup.alpha = 1f;
            pinShadow.anchoredPosition = (n.pos + new Vector2(0f, CurOuterR + 3f)) * Theme.K;
            pinShadow.gameObject.SetActive(true);
        }

        // ------------------------------------------------------------------ level-cleared sequence

        /// <summary>The sequence at time s (continuous parts are functions of s; one-shot events fire once).</summary>
        void Sequence(float s)
        {
            whiteOut.color = new Color(Theme.Card.r, Theme.Card.g, Theme.Card.b, 1f - Mathf.Clamp01(s / SeqWhiteOut));
            if (s >= SeqWhiteOut && whiteOut.gameObject.activeSelf) whiteOut.gameObject.SetActive(false);
            headerGroup.alpha = Mathf.Clamp01(s / SeqWhiteOut);

            ChipAt(chip1, chip1Group, chip1Text, "LEVEL " + seqLevel + " COMPLETE!", s, Chip1T0, Chip1T1);

            // node N: current -> done, with a pop
            float m = Mathf.Clamp01((s - MorphT0) / (MorphT1 - MorphT0));
            Apply(nodeN, Look.Lerp(CurrentLook(info), DoneLook(info, DoneR), EaseInOutCubic(m)));
            float pop = 1f + MorphPop * Mathf.Sin(m * Mathf.PI);
            nodeN.visual.localScale = new Vector3(pop, pop, 1f);
            if (nodeN.shimmer != null && nodeN.shimmer.gameObject.activeSelf && s >= MorphT0) nodeN.shimmer.gameObject.SetActive(false);
            if (s >= MorphT0 && !nodeN.stars.gameObject.activeSelf)
            {
                nodeN.stars.gameObject.SetActive(true);
                for (int k = 0; k < 3; k++) nodeN.starImg[k].color = StarOff; // earned ones pop gold below
            }

            // stars pop one by one (earned count only)
            for (int k = 0; k < 3; k++)
            {
                float st = StarPopT0 + StarPopEvery * k;
                bool earned = k < seqStars;
                float x = (s - st) / StarPopTime;
                if (earned && x >= 0f)
                {
                    if (!starPopped[k])
                    {
                        starPopped[k] = true;
                        nodeN.starImg[k].color = Theme.Gold;
                        Sfx.Play(Sfx.Id.Star);
                        StartRing(k, nodeN.pos + nodeN.stars.anchoredPosition / Theme.K + nodeN.starRt[k].anchoredPosition / Theme.K, s);
                    }
                    float e = Mathf.Clamp01(x);
                    float sc = EaseOutBack(e, StarPopBack) * (1f + StarPopBig * (1f - e)); // 35 % bigger while popping
                    nodeN.starRt[k].localScale = new Vector3(sc, sc, 1f);
                    if (x >= 1f && starsLanded <= k) { starsLanded = k + 1; SetHeaderTexts(current, starTotal - seqStars + starsLanded); }
                }
                else if (nodeN.stars.gameObject.activeSelf) nodeN.starRt[k].localScale = Vector3.one;
            }

            // pulse stops when N stops being current; the pin fades away from N
            if (s < MorphT0) PulseAt(s);
            else if (!landed && pulse.gameObject.activeSelf) pulse.gameObject.SetActive(false);
            if (s < PinDropT0)
            {
                pinGroup.alpha = 1f - Mathf.Clamp01((s - PinOutT0) / (PinOutT1 - PinOutT0));
                bool shadowOn = pinGroup.alpha > 0.01f;
                if (pinShadow.gameObject.activeSelf != shadowOn) pinShadow.gameObject.SetActive(shadowOn);
                PinBobAt(s, 1f);
            }

            // the arrow runs from N to N+1, the progress fills behind it
            RunArrow(s);

            // N+1 unlocks
            if (s >= UnlockT)
            {
                if (!unlockFired)
                {
                    unlockFired = true;
                    Sfx.Play(Sfx.Id.Star);
                    StartRing(3, nodeN1.pos, s);
                    subtitle.text = "Level " + (seqLevel + 1) + " of " + Count;
                    ProgressTo(ArcAt(seqLevel + 1));
                }
                float u = Mathf.Clamp01((s - UnlockT) / PadlockTime);
                nodeN1.badge.anchoredPosition = new Vector2(Theme.D(BadgeX), Theme.D(BadgeY + PadlockRise * EaseOutCubic(u)));
                nodeN1.badge.localEulerAngles = new Vector3(0f, 0f, -PadlockSpin * u);
                nodeN1.badgeGroup.alpha = 1f - u;
                if (u >= 1f && nodeN1.badge.gameObject.activeSelf) nodeN1.badge.gameObject.SetActive(false);

                if (s < PinDropT1)
                {
                    float v = Mathf.Clamp01((s - UnlockT) / NodePopTime);
                    Apply(nodeN1, Look.Lerp(LockedLook(), DoneLook(info, UnlockedR), EaseOutBack(v, NodePopBack)));
                }
                Burst(s - UnlockT);
            }
            ChipAt(chip2, chip2Group, chip2Text, "LEVEL " + (seqLevel + 1) + " UNLOCKED!", s, Chip2T0, Chip2T1);

            // the pin drops onto N+1 and N+1 becomes the current level
            if (s >= PinDropT0)
            {
                float x = Mathf.Clamp01((s - PinDropT0) / (PinDropT1 - PinDropT0));
                Vector2 home = (nodeN1.pos + new Vector2(0f, PinUp)) * Theme.K;
                pinHome = home;
                pinGroup.alpha = 1f;
                if (!pinShadow.gameObject.activeSelf) pinShadow.gameObject.SetActive(true);
                pinShadow.anchoredPosition = (nodeN1.pos + new Vector2(0f, CurOuterR + 3f)) * Theme.K;
                if (s < PinDropT1)
                {
                    pin.anchoredPosition = home + new Vector2(0f, Theme.D(PinDropFrom) * (1f - EaseOutBounce(x)));
                    pin.localScale = Vector3.one;
                }
                else
                {
                    float q = (s - PinDropT1) / PinSquashTime;
                    float sy = q < 1f ? 1f - (1f - PinSquash) * Mathf.Sin(q * Mathf.PI) : 1f;
                    pin.localScale = new Vector3(1f, sy, 1f);
                    PinBobAt(s, Mathf.Clamp01(q - 1f)); // bob fades in after the squash
                }
            }
            if (s >= PinDropT1)
            {
                if (!landed)
                {
                    landed = true;
                    pulse.rectTransform.anchoredPosition = nodeN1.pos * Theme.K;
                    pulse.gameObject.SetActive(true);
                    EnsureShimmer(nodeN1, true);
                }
                float g = Mathf.Clamp01((s - PinDropT1) / LandGrowTime);
                Apply(nodeN1, Look.Lerp(DoneLook(info, UnlockedR), CurrentLook(info), EaseOutCubic(g)));
                PulseAt(s);
            }

            // camera to N+1, PLAY pulses and switches to N+1
            if (s >= CamT0)
            {
                float c = Mathf.Clamp01((s - CamT0) / (CamT1 - CamT0));
                SetScroll(Mathf.Lerp(FocusFor(seqLevel), FocusFor(seqLevel + 1), EaseInOutCubic(c)));
                if (!playSwapped) { playSwapped = true; playLabel.text = "PLAY  Lv " + (seqLevel + 1); }
                float p = Mathf.Clamp01((s - CamT0) / PlayPulseTime);
                float ps = 1f + PlayPulse * Mathf.Sin(p * Mathf.PI);
                footerPlay.localScale = new Vector3(ps, ps, 1f);
            }

            UpdateRings(s);
        }

        void RunArrow(float s)
        {
            float from = ArcAt(seqLevel), to = ArcAt(seqLevel + 1), body = Theme.D(RunBody);
            for (int g = 0; g <= RunGhosts; g++)
            {
                float sg = s - g * RunLag;
                bool on = sg >= RunT0 && sg < RunT1;
                if (run[g].gameObject.activeSelf != on) run[g].gameObject.SetActive(on);
                if (!on) continue;
                float head = Mathf.Lerp(from, to, EaseInOutCubic((sg - RunT0) / (RunT1 - RunT0)));
                Poly.Slice(road, roadCum, head - body, head, tmp);
                run[g].SetPoints(tmp);
                if (g == 0) ProgressTo(head); // progress ends exactly at the arrow
            }
        }

        void Burst(float s)
        {
            float x = s / BurstTime;
            bool on = x >= 0f && x < 1f;
            float d = Theme.D(BurstR) * EaseOutCubic(Mathf.Clamp01(x));
            for (int i = 0; i < BurstCount; i++)
            {
                var b = burst[i];
                if (b.gameObject.activeSelf != on) b.gameObject.SetActive(on);
                if (!on) continue;
                Color c = i % 3 == 0 ? Theme.Gold : i % 3 == 1 ? info.Color : Theme.Card;
                c.a = 1f - x;
                b.color = c;
                b.rectTransform.anchoredPosition = nodeN1.pos * Theme.K + burstDir[i] * d;
            }
        }

        void StartRing(int i, Vector2 posD, float s)
        {
            ringStart[i] = s;
            ringCenter[i] = posD;
            rings[i].rectTransform.anchoredPosition = posD * Theme.K;
        }

        void UpdateRings(float s)
        {
            for (int i = 0; i < rings.Length; i++)
            {
                bool big = i == 3;
                float dur = big ? BurstTime : StarPopTime;
                float x = ringStart[i] < 0f ? -1f : (s - ringStart[i]) / dur;
                bool on = x >= 0f && x < 1f;
                if (rings[i].gameObject.activeSelf != on) rings[i].gameObject.SetActive(on);
                if (!on) continue;
                float r = big ? Mathf.Lerp(RingR0, RingR1, EaseOutCubic(x)) : Mathf.Lerp(8f, 22f, EaseOutCubic(x));
                DrawRing(rings[i], r, big ? Mathf.Lerp(4f, 1f, x) : Mathf.Lerp(3f, 1f, x));
                var c = Theme.Gold;
                c.a = 1f - x;
                rings[i].color = c;
            }
        }

        void ChipAt(RectTransform chip, CanvasGroup g, Text text, string label, float s, float t0, float t1)
        {
            if (s < t0 || s >= t1) { g.alpha = 0f; return; }
            if (text.text != label) text.text = label; // set once per chip
            float a = Mathf.Clamp01((s - t0) / ChipIn);
            float o = Mathf.Clamp01((t1 - s) / ChipOut);
            g.alpha = Mathf.Min(a, o);
            chip.anchoredPosition = new Vector2(0f, -Theme.D(ChipY - ChipSlide * (1f - EaseOutCubic(a))));
        }

        /// <summary>Tap / back during the sequence: straight to the final state (no sounds or particles).</summary>
        void SkipSequence()
        {
            if (!seq) return;
            t = Mathf.Max(t, SeqEnd);
            FinishSequence();
        }

        /// <summary>Final state: a normal map with N+1 current; the "unlocked" chip may still be fading out.</summary>
        void FinishSequence()
        {
            seq = false;
            seqDone = true;
            GameApp.I.UnlockPending = false;
            current = seqLevel + 1;

            for (int g = 0; g <= RunGhosts; g++) run[g].gameObject.SetActive(false);
            for (int i = 0; i < rings.Length; i++) { ringStart[i] = -1f; rings[i].gameObject.SetActive(false); }
            for (int i = 0; i < BurstCount; i++) burst[i].gameObject.SetActive(false);
            whiteOut.gameObject.SetActive(false);
            catcher.SetActive(false);
            chip1Group.alpha = 0f;
            chip2Text.text = "LEVEL " + current + " UNLOCKED!"; // shown until 4.8 s, also after a skip
            headerGroup.alpha = 1f;
            footerPlay.localScale = Vector3.one;

            ShowNormal();
            SetScroll(FocusFor(current));
        }

        // ------------------------------------------------------------------ per frame

        /// <summary>Glide, sequence, pulse, pin, clouds and the locked-node shake. No allocations.</summary>
        void Tick()
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;

            if (thresholdPending)
            {
                // applied a frame after Show, so another screen's OnHide restoring its own value can't undo it
                thresholdPending = false;
                var es = EventSystem.current;
                var canvas = Root.GetComponentInParent<Canvas>();
                if (es != null && canvas != null)
                {
                    if (savedThreshold < 0) savedThreshold = es.pixelDragThreshold;
                    es.pixelDragThreshold = Mathf.Max(1, Mathf.RoundToInt(Theme.D(DragStartD) * canvas.scaleFactor));
                }
            }

            if (seq)
            {
                Sequence(t);
                if (t >= SeqEnd) FinishSequence();
            }
            else
            {
                if (seqDone)
                {
                    // after the sequence only the "unlocked" chip may still be fading out
                    ChipAt(chip2, chip2Group, chip2Text, chip2Text.text, t, Chip2T0, Chip2T1);
                    if (t >= Chip2T1) seqDone = false;
                }
                if (gliding)
                {
                    float x = Mathf.Clamp01((t - GlideDelay) / GlideTime);
                    SetScroll(Mathf.Lerp(glideFrom, glideTo, EaseInOutCubic(x)));
                    if (x >= 1f) gliding = false;
                }
                NormalApply();
            }

            if (shaking != null)
            {
                shakeT += dt;
                float s = shakeT >= ShakeTime ? 0f : Mathf.Sin(shakeT * 60f) * ShakeAmp * (1f - shakeT / ShakeTime);
                shaking.anchoredPosition = shakeHome + new Vector2(Theme.D(s), 0f);
                if (shakeT >= ShakeTime) shaking = null;
            }

            UpdateClouds();
        }

        /// <summary>Normal (non-sequence) frame: intro fades, PLAY rise, pulse and pin.</summary>
        void NormalApply()
        {
            if (!seqDone)
            {
                contentGroup.alpha = Mathf.Clamp01(t / NodesFade);
                headerGroup.alpha = Mathf.Clamp01(t / HeaderFade);
                float rise = Mathf.Clamp01((t - PlayT0) / (PlayT1 - PlayT0));
                footerPlay.anchoredPosition = playHome + new Vector2(0f, -Theme.D(PlayRise) * (1f - EaseOutBack(rise, 1.70158f)));
            }
            PulseAt(t);
            PinBobAt(t, 1f);
        }

        void PulseAt(float s)
        {
            if (!pulse.gameObject.activeSelf) return;
            float u = Mathf.Repeat(s, PulsePeriod) / PulsePeriod;
            DrawRing(pulse, Mathf.Lerp(PulseR0, PulseR1, u), PulseW);
            var c = pulse.color;
            c.a = 1f - u;
            pulse.color = c;
        }

        void PinBobAt(float s, float amount)
        {
            float bob = Mathf.Sin(s * PinSpeed) * amount;
            pin.anchoredPosition = pinHome + new Vector2(0f, Theme.D(bob * PinBob));
            float sc = 1f - 0.15f * bob; // shadow shrinks as the pin goes up
            pinShadow.localScale = new Vector3(sc, sc, 1f);
        }

        void DrawRing(ArrowGraphic g, float radiusD, float strokeD)
        {
            float r = Theme.D(radiusD);
            ringPts.Clear();
            for (int i = 0; i <= 48; i++)
            {
                float a = i / 48f * Mathf.PI * 2f;
                ringPts.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
            g.thickness = Theme.D(strokeD);
            g.SetPoints(ringPts);
        }

        /// <summary>Clouds drift sideways (wrapping) and follow the road scroll at their layer's parallax.</summary>
        void UpdateClouds()
        {
            float scrollD = content.anchoredPosition.y / Theme.K;
            float viewH = viewport.rect.height / Theme.K;
            for (int i = 0; i < CloudCount; i++)
            {
                float x = Mathf.Repeat(cloudBase[i].x + cloudSpeed[i] * t + 100f, 390f + 200f) - 100f;
                float y = Mathf.Repeat(cloudBase[i].y - scrollD * cloudParallax[i] + 100f, viewH + 200f) - 100f;
                clouds[i].anchoredPosition = new Vector2(Theme.D(x), -Theme.D(y));
            }
        }

        // ------------------------------------------------------------------ sprites / easing

        /// <summary>Sky: mode colour 68 % white at the top, 88 % white at 55 % height, cream at the bottom. Cached per mode.</summary>
        static Sprite SkySprite(GameMode m, Color modeColor)
        {
            Sprite s;
            if (skySprites.TryGetValue(m, out s) && s != null) return s;
            Color top = Color.Lerp(modeColor, Color.white, SkyTopWhite), mid = Color.Lerp(modeColor, Color.white, SkyMidWhite);
            const int h = 128;
            var tex = new Texture2D(2, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color[2 * h];
            for (int y = 0; y < h; y++)
            {
                float f = 1f - y / (h - 1f); // 0 at the top
                var c = f < SkyMidAt ? Color.Lerp(top, mid, f / SkyMidAt) : Color.Lerp(mid, CreamBottom, (f - SkyMidAt) / (1f - SkyMidAt));
                px[y * 2] = px[y * 2 + 1] = c;
            }
            tex.SetPixels(px);
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, 2, h), new Vector2(0.5f, 0.5f), 100f);
            skySprites[m] = s;
            return s;
        }

        /// <summary>Header fade: the sky's top colour fading to transparent. Cached per mode.</summary>
        static Sprite FadeSprite(GameMode m, Color modeColor)
        {
            Sprite s;
            if (fadeSprites.TryGetValue(m, out s) && s != null) return s;
            Color top = Color.Lerp(modeColor, Color.white, SkyTopWhite);
            s = Gradient(top, new Color(top.r, top.g, top.b, 0f));
            fadeSprites[m] = s;
            return s;
        }

        /// <summary>Vertical gradient sprite (top colour at the top), stretched by the Image.</summary>
        static Sprite Gradient(Color topCol, Color bottomCol)
        {
            const int h = 64;
            var tex = new Texture2D(2, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color[2 * h];
            for (int y = 0; y < h; y++)
            {
                var c = Color.Lerp(bottomCol, topCol, y / (h - 1f)); // texture y goes up
                px[y * 2] = px[y * 2 + 1] = c;
            }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 2, h), new Vector2(0.5f, 0.5f), 100f);
        }

        static float EaseInOutCubic(float x)
        {
            x = Mathf.Clamp01(x);
            return x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) * 0.5f;
        }

        static float EaseOutCubic(float x)
        {
            float a = 1f - Mathf.Clamp01(x);
            return 1f - a * a * a;
        }

        static float EaseOutBack(float x, float c1)
        {
            float c3 = c1 + 1f, a = x - 1f;
            return 1f + c3 * a * a * a + c1 * a * a;
        }

        static float EaseOutBounce(float x)
        {
            const float n1 = 7.5625f, d1 = 2.75f;
            if (x < 1f / d1) return n1 * x * x;
            if (x < 2f / d1) { x -= 1.5f / d1; return n1 * x * x + 0.75f; }
            if (x < 2.5f / d1) { x -= 2.25f / d1; return n1 * x * x + 0.9375f; }
            x -= 2.625f / d1;
            return n1 * x * x + 0.984375f;
        }
    }

    /// <summary>On the level map's ScrollRect: a touch or drag cancels the intro glide; also drives the per-frame tick.</summary>
    public class LevelsMapInput : MonoBehaviour, IPointerDownHandler, IBeginDragHandler
    {
        public Action touch, tick;

        public void OnPointerDown(PointerEventData e) { if (touch != null) touch(); }
        public void OnBeginDrag(PointerEventData e) { if (touch != null) touch(); }
        void Update() { if (tick != null) tick(); }
    }
}
