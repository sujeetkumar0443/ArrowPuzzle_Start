using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ArrowPuzzle
{
    /// <summary>Game: header (pause, level, hearts), arrows-left chip, board card, Hint and Restart.</summary>
    public class ScreenGame : AppScreen
    {
        // header (design px from the safe-area top): pause 46 at y 24, LEVEL / MOVES in the centre, Skip right, hearts under
        const float Pad = 20f, TopY = 24f, PauseD = 46f, PauseR = 14f;
        const float LevelY = 22f, LevelFontD = 13f, MovesY = 38f, MovesH = 50f, MovesFontD = 44f, MovesLabelD = 14f;
        const float HeartsY = 92f, HeartD = 18f, HeartGap = 6f;
        const float BoardTop = HeartsY + HeartD + 12f;      // 122: the board area starts under the hearts
        const float BoardSide = 10f;                        // board area side margin (no card any more)
        // bottom: two round 62 buttons side by side, centred; the hint count pill hangs below the Hint button
        const float BtnBottom = 40f, BtnH = 62f, BtnGapX = 30f, BadgeH = 20f, BadgeOver = 9f;
        const float BoardGap = 12f;                         // between the buttons (with the badge) and the board area
        const float BoardBottom = BtnBottom + BtnH + BoardGap; // no banner
        const float CardPad = 4f;
        const float BannerGap = 8f, BtnEdge = 4f;           // the buttons' raised edge sits 8 above the banner
        const float ChipH = 34f;                            // (old chip height, kept for Chip())

        // wrong-move feedback (approved demo values)
        const float FlashStrength = 1.0f;   // peak alpha of the red screen flash
        const float FlashSeconds = 0.8f;
        const float ShakeAmount = 1.0f;     // 1 = 7 x 3 design px
        const float ShakeSeconds = 0.38f;

        // loss: wait for the wrong-move recoil, board collapse, red tint, then the lose popup (s after collapse start)
        const float RecoilWait = 0.65f;
        const float LoseTintT0 = 0.35f, LoseTintT1 = 0.8f, LoseTintAlpha = 0.18f;
        const float LosePopupAt = 1.35f;

        // solve timer (unscaled s): one frame never adds more than this (a resume from background is not play time)
        const float TimerMaxStep = 0.25f;
        const float TimerCapSeconds = 99f * 60f + 59f; // display cap 99:59

        // "+3 hints" toast (design px / s): just above the Hint / Restart buttons
        const float ToastW = 150f, ToastH = 40f, ToastY = BtnBottom + BtnH + 18f, ToastRise = 12f;
        const float ToastIn = 0.2f, ToastHold = 1.2f, ToastOut = 0.3f;

        // Skip pill (design px): right end of the chip band, 44 high
        const float SkipH = 42f, SkipEdgeD = 3f, SkipFont = 17f, SkipIconD = 14f, SkipBadgeD = 22f, SkipWaitAlpha = 0.55f;
        const int SkipBonus = 2;
        const string SkipToast = "Ad not ready, try again";
        static readonly Color SkipGoldEdge = Theme.Hex("#C98410");
        static readonly Color SkipPurple = Theme.Hex("#9B5DE5"), SkipPurpleEdge = Theme.Hex("#6E3BB8");
        // Skip pill animation (unscaled s), driven from TimerLoop: pulse, icon nudge, press, first-attention bounce
        const float SkipPulsePeriod = 1.4f, SkipPulseScale = 1.06f, SkipNudge = 2f;
        const float SkipPressScale = 0.94f, SkipPressTime = 0.08f;
        const float SkipAttentionDelay = 1f, SkipAttentionTime = 0.5f, SkipAttentionScale = 1.18f;

        public BoardView Board { get; private set; }

        RectTransform skipRt;
        Image skipImg, skipBadge;
        Shadow skipShadow;
        Text skipLabel, skipBadgeText;
        CanvasGroup skipGroup;
        RectTransform skipIconFill; // the play icon's sprite (not under the layout group: free to nudge)
        Image[] skipIcon;
        Shimmer skipShimmer;
        float skipAttentionT0 = -100f, skipPressT0 = -100f;
        bool skipWaiting; // a Skip / +2 skips video is loading or playing: taps are ignored
        int skipRequest;  // bumped per request, so a late result can't act on a newer state

        Text levelText, movesText, hintCount;
        int moves; // taps that hit an arrow (free or blocked); reset on every level start / restart
        GameMode mode = GameMode.Classic;
        readonly Image[] hearts = new Image[BoardView.MaxLives];
        readonly RectTransform[] heartRts = new RectTransform[BoardView.MaxLives];
        readonly Coroutine[] heartFx = new Coroutine[BoardView.MaxLives];
        RectTransform area, card, hintRt, restartRt;
        Image backdropDots;
        BoardZoom zoom;
        int level; // global level (LevelPath / Campaign)
        float btnBottom = BtnBottom, toastY = ToastY; // move up above the banner (ApplyBannerLayout)
        bool bannerLayoutPending; // the banner height changed while a popup had it hidden (RestoreBanner)

        Image flash;
        Coroutine flashFx, shakeFx, popFx, loseFx, toastFx;
        CanvasGroup toast;
        Text toastText;
        Image loseTint;
        int shownLives = -1, shownRemaining = -1;

        // solve timer: starts on the first tap, freezes when the last arrow is sent off
        float solveTime;
        bool timing, timerStarted;
        Coroutine timerFx;

        protected override void Build()
        {
            BuildBackdrop();
            BannerPlaceholder.Create(Root); // buttons, board and flying arrows draw over it
            BuildHeader();
            BuildSkip();

            // the board uses the whole free area between the header and the bottom buttons (also the zoom / pan area)
            area = Ui.New(Root, "BoardArea");
            Ui.Fill(area);
            area.offsetMin = new Vector2(Theme.D(BoardSide), Theme.D(BoardBottom));
            area.offsetMax = new Vector2(-Theme.D(BoardSide), -Theme.D(BoardTop));

            // no visible card: the arrows are drawn on the screen background (the card only holds the board for zoom)
            var cardImg = Ui.Img(area, "BoardCard", null, Color.clear);
            cardImg.raycastTarget = false;
            card = cardImg.rectTransform;
            Ui.PlaceD(card, Ui.C, Vector2.zero, Vector2.zero);

            // transparent image so touches on the board reach BoardZoom on the area (arrows and dots are not raycast targets)
            var boardImg = Ui.Img(card, "Board", null, Color.clear);
            Board = boardImg.gameObject.AddComponent<BoardView>();
            // own canvas: moving arrows only re-batch the board, and header / chip effects don't re-batch the board
            boardImg.gameObject.AddComponent<Canvas>();
            boardImg.gameObject.AddComponent<GraphicRaycaster>();
            Board.RemainingChanged += OnRemaining;
            Board.LivesChanged += OnLives;
            Board.Bumped += OnBumped;
            Board.Won += OnWon;
            Board.Lost += OnLost;
            Board.Tapped += OnTapped;
            zoom = BoardZoom.Attach(area, card, Board); // owns board input: taps, pinch / pan, reset button

            BuildButtons();
            area.SetAsLastSibling(); // exiting arrows fly over the buttons too (nothing clips the board)
            BuildToast();
            BuildFlash();
            BannerDebug.Create(Root, "ScreenGame"); // only when Ads.ShowBannerDebug
        }

        /// <summary>Small ink pill above the buttons ("+3 hints"), hidden until Toast() runs. Never takes taps.</summary>
        void BuildToast()
        {
            var pill = Ui.Img(Root, "Toast", Theme.Rounded, Theme.Panel, ToastH * 0.5f);
            pill.raycastTarget = false;
            Ui.PlaceD(pill.rectTransform, Ui.BC, new Vector2(0f, ToastY), new Vector2(ToastW, ToastH));
            toastText = Ui.Label(pill.transform, "", 18f, Theme.PanelText, Weight.Bold);
            Ui.Fill(toastText.rectTransform);
            toast = pill.gameObject.AddComponent<CanvasGroup>();
            toast.blocksRaycasts = false;
            toast.interactable = false;
            pill.gameObject.SetActive(false);
        }

        /// <summary>Fades and rises in, holds, fades out (unscaled time).</summary>
        IEnumerator Toast(string text)
        {
            toastText.text = text;
            toast.gameObject.SetActive(true);
            toast.transform.SetAsLastSibling();
            var trt = (RectTransform)toast.transform;
            trt.sizeDelta = new Vector2(Mathf.Max(Theme.D(ToastW), toastText.preferredWidth + Theme.D(32f)), trt.sizeDelta.y); // fits longer texts
            float total = ToastIn + ToastHold + ToastOut;
            float t = 0f;
            while (t < total)
            {
                t += Time.unscaledDeltaTime;
                float a = t < ToastIn ? EaseOutCubic(t / ToastIn)
                        : t < ToastIn + ToastHold ? 1f
                        : 1f - Mathf.Clamp01((t - ToastIn - ToastHold) / ToastOut);
                toast.alpha = a;
                float rise = EaseOutCubic(Mathf.Clamp01(t / ToastIn));
                trt.anchoredPosition = new Vector2(0f, Theme.D(toastY - ToastRise * (1f - rise)));
                yield return null;
            }
            toast.gameObject.SetActive(false);
            toastFx = null;
        }

        /// <summary>
        /// Full-canvas red overlay (outside the safe area too), above this screen and below the popups.
        /// Lives on the canvas, not under Root, so the screen shake does not move it.
        /// </summary>
        void BuildFlash()
        {
            var canvas = GameApp.I.CanvasRoot;
            flash = Ui.Img(canvas, "HitFlash", MakeFlashSprite(), new Color(1f, 1f, 1f, 0f));
            flash.raycastTarget = false;
            Ui.Fill(flash.rectTransform);
            var popups = canvas.Find("Popups");
            if (popups != null) flash.transform.SetSiblingIndex(popups.GetSiblingIndex());
            flash.gameObject.SetActive(false);

            // flat red tint for the board collapse, same layer as the flash
            loseTint = Ui.Img(canvas, "LoseTint", null, new Color(Theme.Error.r, Theme.Error.g, Theme.Error.b, 0f));
            loseTint.raycastTarget = false;
            Ui.Fill(loseTint.rectTransform);
            if (popups != null) loseTint.transform.SetSiblingIndex(popups.GetSiblingIndex());
            loseTint.gameObject.SetActive(false);
        }

        void OnLost()
        {
            if (loseFx != null) GameApp.I.StopCoroutine(loseFx);
            loseFx = GameApp.I.StartCoroutine(LoseSequence());
        }

        /// <summary>Recoil finishes, the board collapses under a short red tint, then the lose popup opens.</summary>
        IEnumerator LoseSequence()
        {
            float w = 0f;
            while (w < RecoilWait) { w += Time.unscaledDeltaTime; yield return null; }

            Board.PlayCollapse(null);
            loseTint.gameObject.SetActive(true);
            float t = 0f;
            bool opened = false;
            while (!opened || t < LoseTintT1)
            {
                t += Time.unscaledDeltaTime;
                float x = Mathf.Clamp01((t - LoseTintT0) / (LoseTintT1 - LoseTintT0));
                var c = loseTint.color;
                c.a = LoseTintAlpha * Mathf.Sin(x * Mathf.PI);
                loseTint.color = c;
                if (!opened && t >= LosePopupAt)
                {
                    opened = true;
                    GameApp.I.Close("PopupPause"); // opened during the collapse: must not stay behind the popup
                    GameApp.I.Open("PopupLose");
                }
                yield return null;
            }
            loseTint.gameObject.SetActive(false);
            loseFx = null;
        }

        /// <summary>
        /// Radial gradient from the centre (farthest-corner ellipse): clear to 34 %, Error at 0.5 alpha at 74 %,
        /// ErrorDeep at 0.92 at 100 %; plus Error at 0.38 fading to 0 over the top and bottom 24 %, drawn over it.
        /// </summary>
        static Sprite MakeFlashSprite()
        {
            const int w = 128, h = 256;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color[w * h];
            Color red = Theme.Error, deep = Theme.ErrorDeep;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float nx = (x + 0.5f) / w * 2f - 1f, ny = (y + 0.5f) / h * 2f - 1f;
                    float r = Mathf.Sqrt(nx * nx + ny * ny) / Mathf.Sqrt(2f);

                    Color rc = red;
                    float ra = 0f;
                    if (r > 0.74f)
                    {
                        float f = Mathf.Clamp01((r - 0.74f) / 0.26f);
                        rc = Color.Lerp(red, deep, f);
                        ra = Mathf.Lerp(0.5f, 0.92f, f);
                    }
                    else if (r > 0.34f) ra = 0.5f * (r - 0.34f) / 0.40f;

                    float v = (y + 0.5f) / h;
                    float edge = Mathf.Min(v, 1f - v);
                    float va = edge < 0.24f ? 0.38f * (1f - edge / 0.24f) : 0f;

                    // vertical layer over the radial one
                    float a = va + ra * (1f - va);
                    Color c = a > 0f ? (red * va + rc * (ra * (1f - va))) / a : red;
                    c.a = a;
                    px[y * w + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Screen background (theme Bg or gradient) with the dot pattern when the theme has dots, under the insets too.</summary>
        void BuildBackdrop()
        {
            var bg = Ui.Img(Root, "Bg", null, Theme.Bg);
            Theme.PaintBg(bg);
            bg.raycastTarget = false;
            Ui.Fill(bg.rectTransform);
            bg.rectTransform.offsetMin = new Vector2(0f, -Theme.D(400f));
            bg.rectTransform.offsetMax = new Vector2(0f, Theme.D(400f));
            backdropDots = Ui.Img(bg.rectTransform, "Dots", Theme.DotTile, Theme.Dot);
            backdropDots.type = Image.Type.Tiled;
            backdropDots.pixelsPerUnitMultiplier = 64f / (34f * Theme.K);
            backdropDots.raycastTarget = false;
            Ui.Fill(backdropDots.rectTransform);
            backdropDots.gameObject.SetActive(Theme.HasDots);
        }

        /// <summary>
        /// Pause (46, Card with a bottom edge) left; centre "LEVEL n" (small, InkSoft) over the big MOVES count (Accent) and
        /// "MOVES"; the 3 hearts (18) centred under it. The Skip pill sits at the right of the top row (BuildSkip).
        /// </summary>
        void BuildHeader()
        {
            var pause = Ui.Btn(Root, "", Theme.Card, Theme.Ink, 0f, PauseR, Theme.CardShadow, 3f,
                               () => GameApp.I.Open("PopupPause"), Icon.Pause, 20f);
            Ui.PlaceD((RectTransform)pause.transform, Ui.TL, new Vector2(Pad, -TopY), new Vector2(PauseD, PauseD));

            levelText = Ui.Label(Root, "", LevelFontD, Theme.InkSoft, Weight.Semi);
            Ui.PlaceD(levelText.rectTransform, Ui.TC, new Vector2(0.5f, 1f), new Vector2(0f, -LevelY), new Vector2(180f, 18f));

            var movesRow = Ui.New(Root, "Moves");
            Ui.PlaceD(movesRow, Ui.TC, new Vector2(0.5f, 1f), new Vector2(0f, -MovesY), new Vector2(180f, MovesH));
            var row = Ui.Row(movesRow, 6f, TextAnchor.LowerCenter);
            row.childForceExpandWidth = false;
            movesText = Ui.Label(movesRow, "0", MovesFontD, Theme.Accent, Weight.Bold);
            var movesLabel = Ui.Label(movesRow, "MOVES", MovesLabelD, Theme.InkSoft, Weight.Semi);
            movesLabel.alignment = TextAnchor.LowerCenter;
            movesLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = Theme.D(MovesH * 0.62f);

            // 3 hearts, centred under the moves
            var hrow = Ui.New(Root, "Hearts");
            float w = HeartD * hearts.Length + HeartGap * (hearts.Length - 1);
            Ui.PlaceD(hrow, Ui.TC, new Vector2(0.5f, 1f), new Vector2(0f, -HeartsY), new Vector2(w, HeartD));
            for (int i = 0; i < hearts.Length; i++)
            {
                var h = Icons.Make(hrow, Icon.Heart, Theme.Error, HeartD);
                Ui.PlaceD(h, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(HeartD * 0.5f + i * (HeartD + HeartGap), 0f), new Vector2(HeartD, HeartD));
                heartRts[i] = h;
                hearts[i] = h.GetComponentInChildren<Image>();
            }
        }

        void SetMoves(int n)
        {
            moves = n;
            if (movesText != null) movesText.text = n.ToString();
        }

        // ------------------------------------------------------------------ Skip level (rewarded, daily limit)

        /// <summary>
        /// Small pill at the right end of the chip band, 44 high: teal, play icon, "Skip" and an ink badge with the skips
        /// left; gold "+2 skips" (no badge) when none are left (RefreshSkip, also on Save.SkipsChanged).
        /// </summary>
        void BuildSkip()
        {
            var b = Ui.Btn(Root, "SKIP", SkipPurple, Theme.OnColor, SkipFont, SkipH * 0.5f, SkipPurpleEdge, SkipEdgeD,
                           OnSkipTap, Icon.Skip, SkipIconD);
            skipRt = (RectTransform)b.transform;
            // pivot right-middle: every scale keeps the right edge on the margin and grows left / up / down only
            // (at the 1.18 bounce: y 77..129, between the header (72) and the board (136), far from the chip row)
            Ui.PlaceD(skipRt, Ui.TR, new Vector2(1f, 0.5f), new Vector2(-Pad, -(TopY + PauseD * 0.5f)), new Vector2(0f, SkipH));
            var squash = b.GetComponent<PressScale>(); // its own 0.95 squash would fight ApplySkipAnim's scale
            if (squash != null) UnityEngine.Object.Destroy(squash);
            var iconRt = skipRt.Find("Icon_" + Icon.Skip);
            skipIcon = iconRt != null ? iconRt.GetComponentsInChildren<Image>() : new Image[0];
            skipIconFill = skipIcon.Length > 0 ? skipIcon[0].rectTransform : null;
            var hlg = b.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = Theme.D(6f);
            int padL = Mathf.RoundToInt(Theme.D(12f)), padR = Mathf.RoundToInt(Theme.D(10f));
            hlg.padding = new RectOffset(padL, padR, 0, 0);
            b.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            skipImg = b.GetComponent<Image>();
            skipShadow = b.GetComponent<Shadow>();
            skipLabel = b.GetComponentInChildren<Text>();

            skipBadge = Ui.Img(skipRt, "Badge", WinArt.Disc, Theme.OnColor);
            skipBadge.raycastTarget = false;
            Ui.Size(skipBadge, SkipBadgeD, SkipBadgeD);
            skipBadgeText = Ui.Label(skipBadge.transform, "4", 13f, SkipPurple, Weight.Bold);
            Ui.Fill(skipBadgeText.rectTransform);
            // the soft shine (Ui.AddShimmer: white streak, skewed -20 deg, clipped to the pill); paused while stopped
            skipShimmer = Ui.AddShimmer(skipRt, SkipH * 0.5f);
            skipGroup = b.gameObject.AddComponent<CanvasGroup>();
        }

        /// <summary>
        /// Purple "Skip" (white text and icon) + a white "N" badge while skips are left; gold "+2 skips" with ink text
        /// at 0. Dimmed while its video loads / plays.
        /// </summary>
        void RefreshSkip()
        {
            if (skipImg == null) return;
            int left = Save.SkipsLeft;
            bool any = left > 0;
            skipImg.color = any ? SkipPurple : Theme.Gold;
            if (skipShadow != null) skipShadow.effectColor = any ? SkipPurpleEdge : SkipGoldEdge;
            Color fg = any ? Theme.OnColor : Theme.Ink;
            skipLabel.color = fg;
            foreach (var img in skipIcon) img.color = fg;
            skipLabel.text = skipWaiting ? "..." : any ? "SKIP" : "+" + SkipBonus + " skips";
            skipBadge.gameObject.SetActive(any && !skipWaiting);
            skipBadgeText.text = left.ToString();
            skipGroup.alpha = skipWaiting ? SkipWaitAlpha : 1f;
        }

        /// <summary>
        /// Skips left: one rewarded ad skips this level (Save.UseSkip + Save.SkipLevel: 1 star only if it had none, the
        /// next level unlocked; no win popup, no bonus spin, no coins, no best time) and the next level starts at once,
        /// with no interstitial (after level 150: Home). None left: one rewarded ad adds SkipBonus skips for today.
        /// Ignored while an arrow is moving, the board is finished / locked, or a video is loading.
        /// </summary>
        void OnSkipTap()
        {
            skipPressT0 = Time.unscaledTime; // press feel on every tap (ApplySkipAnim)
            if (skipWaiting || Board.AnyMoving || Board.Locked) return;
            if (LongSamplesDev.Active > 0) { ShowToast("No skip in samples"); return; } // never touches real progress
            if (Ads.IsRewardedBusy) { ShowToast(SkipToast); return; } // ShowRewarded would ignore it: never wait for it
            int id = ++skipRequest;
            int g = level;
            bool bonus = Save.SkipsLeft <= 0;
            skipWaiting = true;
            RefreshSkip();
            if (bonus) Ads.ShowRewarded(() => OnSkipBonusReward(id), () => OnSkipFailed(id));
            else Ads.ShowRewarded(() => OnSkipReward(id, g), () => OnSkipFailed(id));
        }

        void OnSkipReward(int id, int g)
        {
            if (id != skipRequest) return;
            skipWaiting = false;
            // still this level, on this screen, with nothing finished meanwhile: otherwise nothing is spent
            if (g != level || !Root.gameObject.activeInHierarchy || Board.Locked || Save.SkipsLeft <= 0) { RefreshSkip(); return; }
            Save.UseSkip();   // raises SkipsChanged -> RefreshSkip
            Save.SkipLevel(g);
            int next = g + 1;
            if (next > LevelPath.Count || !Save.IsUnlocked(next))
            {
                GameApp.I.Show("ScreenHome"); // the last level skipped: Home
                return;
            }
            StartLevel(next); // straight to the next level: no popup, no interstitial
        }

        void OnSkipBonusReward(int id)
        {
            if (id != skipRequest) return;
            skipWaiting = false;
            Save.AddSkipBonus(SkipBonus); // raises SkipsChanged -> RefreshSkip
            RefreshSkip();
        }

        /// <summary>Not ready, failed to show or closed early: nothing is spent; the short toast.</summary>
        void OnSkipFailed(int id)
        {
            if (id != skipRequest) return;
            skipWaiting = false;
            RefreshSkip();
            ShowToast(SkipToast);
        }

        /// <summary>
        /// Every frame from TimerLoop (unscaled): the first-attention bounce 1 s after the level starts (1 -> 1.18 -> 1
        /// over 0.5 s), then the soft pulse (1 -> 1.06 -> 1 every 1.4 s, ease in-out) with the play icon nudging 2 px
        /// right and back, the shine sweep, and the press dip (0.94 for 0.08 s, then back). All stop (at rest, no
        /// shine) while the video loads, an arrow is moving, or the board is finished / locked; the press dip still shows.
        /// </summary>
        void ApplySkipAnim()
        {
            if (skipRt == null || Board == null) return;
            float now = Time.unscaledTime;
            bool stopped = skipWaiting || Board.AnyMoving || Board.Locked;
            float s = 1f, p = 0f;
            if (!stopped)
            {
                float a = (now - skipAttentionT0) / SkipAttentionTime;
                float pulseFrom = skipAttentionT0 + SkipAttentionTime;
                if (a >= 0f && a < 1f) s = 1f + (SkipAttentionScale - 1f) * Mathf.Sin(Mathf.PI * a);
                else if (now >= pulseFrom)
                {
                    float ph = Mathf.Repeat(now - pulseFrom, SkipPulsePeriod) / SkipPulsePeriod;
                    p = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * ph); // 0 -> 1 -> 0, ease in-out
                    s = 1f + (SkipPulseScale - 1f) * p;
                }
            }
            float pr = (now - skipPressT0) / SkipPressTime; // 0..1 down, 1..2 back
            if (pr >= 0f && pr < 2f) s *= Mathf.Lerp(1f, SkipPressScale, pr < 1f ? pr : 2f - pr);
            skipRt.localScale = new Vector3(s, s, 1f);
            if (skipIconFill != null) skipIconFill.anchoredPosition = new Vector2(Theme.D(SkipNudge * p), 0f);
            if (skipShimmer != null && skipShimmer.enabled == stopped) skipShimmer.enabled = !stopped;
        }

        /// <summary>Back at rest (leaving the screen).</summary>
        void ResetSkipAnim()
        {
            if (skipRt == null) return;
            skipRt.localScale = Vector3.one;
            if (skipIconFill != null) skipIconFill.anchoredPosition = Vector2.zero;
            skipAttentionT0 = skipPressT0 = -100f;
        }

        void ShowToast(string text)
        {
            if (!Root.gameObject.activeInHierarchy) return;
            if (toastFx != null) GameApp.I.StopCoroutine(toastFx);
            toastFx = GameApp.I.StartCoroutine(Toast(text));
        }

        /// <summary>Pill of height ChipH, width from its content (side padding padD, gap 8).</summary>
        static Image Chip(RectTransform parent, float padD, Color bg)
        {
            var chip = Ui.Img(parent, "Chip", Theme.Rounded, bg, ChipH * 0.5f);
            chip.raycastTarget = false;
            Ui.Size(chip, 0f, ChipH);
            var r = Ui.Row(chip.rectTransform, 8f);
            int side = Mathf.RoundToInt(Theme.D(padD));
            r.padding = new RectOffset(side, side, 0, 0);
            return chip;
        }

        /// <summary>Round Hint and Restart buttons (62, Card with a bottom edge) side by side, centred above the banner.</summary>
        void BuildButtons()
        {
            var hint = Ui.Btn(Root, "", Theme.Card, Theme.Ink, 0f, BtnH * 0.5f, Theme.CardShadow, BtnEdge, OnHint, Icon.Bulb, 28f);
            hintRt = (RectTransform)hint.transform;
            Ui.PlaceD(hintRt, Ui.BC, new Vector2(0.5f, 0f), new Vector2(-(BtnH + BtnGapX) * 0.5f, BtnBottom), new Vector2(BtnH, BtnH));

            // hints left: small Accent pill hanging below the button
            var badge = ScreenKit.Rounded(hint.transform, "Badge", Theme.Accent, BadgeH * 0.5f);
            badge.raycastTarget = false;
            badge.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Ui.PlaceD(badge.rectTransform, Ui.BC, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(30f, BadgeH));
            hintCount = Ui.Label(badge.transform, "", 13f, Theme.AccentText, Weight.Bold);
            Ui.Fill(hintCount.rectTransform);

            var restart = Ui.Btn(Root, "", Theme.Card, Theme.Ink, 0f, BtnH * 0.5f, Theme.CardShadow, BtnEdge, Restart, Icon.Restart, 28f);
            restartRt = (RectTransform)restart.transform;
            Ui.PlaceD(restartRt, Ui.BC, new Vector2(0.5f, 0f), new Vector2((BtnH + BtnGapX) * 0.5f, BtnBottom), new Vector2(BtnH, BtnH));
        }

        // ------------------------------------------------------------------ banner

        /// <summary>
        /// Puts Hint / Restart (and their toast) 8 above the banner and ends the board area BoardGap above them.
        /// No banner (height 0): the old 40 from the bottom. Returns true when something moved.
        /// </summary>
        bool ApplyBannerLayout()
        {
            float banner = Ads.BannerHeightDesign;
            float y = banner > 0f ? banner + BannerGap + BtnEdge : BtnBottom;
            if (Mathf.Abs(y - btnBottom) < 0.5f) return false; // the banner keeps its size across refreshes
            btnBottom = y;
            toastY = y + BtnH + 18f;
            hintRt.anchoredPosition = new Vector2(-Theme.D((BtnH + BtnGapX) * 0.5f), Theme.D(y));
            restartRt.anchoredPosition = new Vector2(Theme.D((BtnH + BtnGapX) * 0.5f), Theme.D(y));
            area.offsetMin = new Vector2(Theme.D(BoardSide), Theme.D(y + BtnH + BoardGap));
            return true;
        }

        /// <summary>
        /// The real banner height arrived during a level: move the buttons and shrink the loaded board to the new
        /// area by scaling it (no reload, so the level keeps its state; taps map through the board's own transform).
        /// </summary>
        void OnBannerHeight()
        {
            if (!Ads.IsBannerWanted)
            {
                // a popup hid the banner (win / lose): nothing moves behind it; applied in RestoreBanner
                bannerLayoutPending = true;
                return;
            }
            bannerLayoutPending = false;
            if (!ApplyBannerLayout() || Board.Size.x <= 0f) return;
            FitBoard();
            if (zoom.Zoom > 1.001f) zoom.ResetZoom(true); // back to 1x = fit, within the new pan limits
        }

        /// <summary>
        /// The win / lose popup closed (any route): the banner comes back, and a banner height that arrived while it
        /// was hidden is applied now. Going Home is fine too: Home wants the banner as well.
        /// </summary>
        public void RestoreBanner(string who)
        {
            Ads.Banner(true, who);
            if (bannerLayoutPending) OnBannerHeight();
        }

        /// <summary>Card = board (scaled down if the area got smaller than the size it was loaded for) + padding.</summary>
        void FitBoard()
        {
            Vector2 free = area.rect.size - Vector2.one * Theme.D(CardPad * 2f);
            Vector2 size = Board.Size;
            float s = Mathf.Min(1f, free.x / Mathf.Max(1f, size.x), free.y / Mathf.Max(1f, size.y));
            Board.transform.localScale = new Vector3(s, s, 1f);
            card.sizeDelta = size * s + Vector2.one * Theme.D(CardPad * 2f);
        }

        // ------------------------------------------------------------------ flow

        public override void OnShow()
        {
            Ads.BannerHeightChanged -= OnBannerHeight; // never twice
            Ads.BannerHeightChanged += OnBannerHeight;
            Save.SkipsChanged -= RefreshSkip;
            Save.SkipsChanged += RefreshSkip; // the lose popup's skips / bonus update this pill too
            StartLevel(GameApp.I.CurrentLevel);
        }

        public override void OnHide()
        {
            LongSamplesDev.Stop(); // the dev sample route ends when the game screen goes away
            Ads.BannerHeightChanged -= OnBannerHeight;
            Save.SkipsChanged -= RefreshSkip;
            skipRequest++;       // a video still running can't skip a level on a screen that is gone
            skipWaiting = false;
            ResetSkipAnim();     // TimerLoop (which drives it) stops below
            Ads.Banner(false, "ScreenGame"); // ignored when Home asks for it in the same frame
            StopFx();
            if (timerFx != null) GameApp.I.StopCoroutine(timerFx);
            timerFx = null;
            timing = false;
        }

        public override bool OnBack()
        {
            GameApp.I.Open("PopupPause");
            return true;
        }

        /// <summary>Theme rebuild: the flash and the lose tint live on the canvas, outside Root.</summary>
        public override void Teardown()
        {
            if (flash != null) Object.Destroy(flash.gameObject);
            if (loseTint != null) Object.Destroy(loseTint.gameObject);
        }

        /// <summary>
        /// Loads global level n (also for popups: Next / Retry while this screen is already showing). n is kept within
        /// 1..LevelPath.Count; the level is campaign level n (Levels.For, long arrows).
        /// </summary>
        public void StartLevel(int n)
        {
            Time.timeScale = 1f; // safety: a level never starts frozen
            int g = Mathf.Clamp(n, 1, LevelPath.Count);
            var type = GameMode.Shapes; // kept for GameApp.CurrentMode / ClearedMode (one level type now)
            LevelData data;
            bool sample = LongSamplesDev.Active > 0;
            if (sample)
            {
                // TEMPORARY dev route: the first start plays the picked sample; the same n again = Restart; another n
                // (PopupWin's NEXT) = the next sample. g stays a real level so the popups' level checks keep working.
                if (!LongSamplesDev.Pending && n != level) LongSamplesDev.Next();
                LongSamplesDev.Started();
                // a real, unlocked level whose next one is unlocked too, so PopupWin's NEXT comes back here
                g = Mathf.Max(1, Save.UnlockedGlobal - 1);
                data = LongSamplesDev.Load();
            }
            else data = Levels.For(g);
            if (data == null)
            {
                Debug.LogError("[ArrowPuzzle] No level " + g + " (" + Campaign.ShapeOf(g) + ")");
                return;
            }
            mode = type;
            level = g;
            GameApp.I.CurrentMode = mode;
            GameApp.I.CurrentLevel = level;
            levelText.text = sample ? "SAMPLE " + LongSamplesDev.Active : "LEVEL " + level;
            SetMoves(0);
            RefreshHints();

            skipWaiting = false;
            RefreshSkip();
            skipAttentionT0 = Time.unscaledTime + SkipAttentionDelay; // one stronger bounce 1 s into every level
            StopFx();
            ResetTimer();
            SoundFx.ResetCombo();
            MusicPlayer.PlayLevel(level);
            Board.ResetCollapse(); // Load also resets; explicit so Restart / StartLevel never keep a collapsed board
            shownLives = -1;      // the Load below sets hearts and counter without animating
            shownRemaining = -1;
            zoom.ResetZoom(false); // every level start / restart is at 1x

            Ads.Banner(true, "ScreenGame"); // every level start (first, Restart, Next): this screen wants it
            bannerLayoutPending = false;
            ApplyBannerLayout(); // buttons and board area above the banner's current height
            if (!sample) AnalyticsService.LevelStart(level, Campaign.ShapeOf(level), LevelPath.Difficulty(level));
            CrashReporter.SetKey("level", level.ToString());
            CrashReporter.SetKey("type", sample ? "sample" : Campaign.ShapeOf(level));
            MrecSlot.Preload(MrecKind.Bonus); // the MRECs of the popups that can open from here (no-op when loading /
            MrecSlot.Preload(MrecKind.Lose);  // loaded at the same place); Level complete's loads while the wheel spins

            Canvas.ForceUpdateCanvases(); // make sure the area rect is up to date
            Vector2 free = area.rect.size - Vector2.one * Theme.D(CardPad * 2f);
            Board.transform.localScale = Vector3.one; // a level loads at full size for the current area
            Board.Load(data, new Vector2(Mathf.Max(1f, free.x), Mathf.Max(1f, free.y)));

            // card wraps the board (BoardView centres itself on its parent) with 10 padding
            card.sizeDelta = Board.Size + Vector2.one * Theme.D(CardPad * 2f);
        }

        public void Restart()
        {
            StartLevel(level);
        }

        void OnWon()
        {
            timing = false; // normally already frozen at the last tap
            if (LongSamplesDev.Active > 0)
            {
                // dev samples save nothing (no best time, stars, unlock); the popups still run as usual
                GameApp.I.ResultTime = solveTime;
                GameApp.I.ResultPrevBest = 0f;
                GameApp.I.ResultNewBest = false;
                GameApp.I.ResultLives = Board.Lives;
                GameApp.I.UnlockPending = false;
                GameApp.I.ClearedLevel = level;
                GameApp.I.ClearedStars = Mathf.Clamp(Board.Lives, 1, 3);
                GameApp.I.ClearedMode = mode;
                GameApp.I.ClearedPrevStars = 0;
                GameApp.I.ClearedUnlocked = false;
                GameApp.I.Close("PopupPause");
                GameApp.I.BonusMult = 0;
                GameApp.I.SkipInterstitialOnce = false;
                if (!GameApp.I.Open("PopupBonusSpin")) GameApp.I.Open("PopupWin");
                return;
            }
            float prevBest = Save.GetBestTime(level);
            bool newBest = prevBest <= 0f || solveTime < prevBest;
            if (newBest) Save.SetBestTime(level, solveTime);
            GameApp.I.ResultTime = solveTime;
            GameApp.I.ResultPrevBest = prevBest;
            GameApp.I.ResultNewBest = newBest;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[ArrowPuzzle] Solve time: " + FormatTime(solveTime) + " (" + solveTime.ToString("F2") + " s, "
                      + (prevBest > 0f ? "best was " + FormatTime(prevBest) + " (" + prevBest.ToString("F2") + " s)" : "no best yet")
                      + (newBest ? " / new best" : "") + ")");
#endif

            GameApp.I.ResultLives = Board.Lives;
            int stars = Mathf.Clamp(Board.Lives, 1, 3);
            int prevStars = Save.Stars(level);                                          // for the map (read only)
            bool unlocks = level == Save.UnlockedGlobal && level < LevelPath.Count;     // what LevelWon will do
            Save.SetStars(level, stars);
            Save.LevelWon(level);
            // hand-off to the map's unlock animation (played only after PopupWin's NEXT)
            GameApp.I.UnlockPending = true;
            GameApp.I.ClearedLevel = level;
            GameApp.I.ClearedStars = stars;
            GameApp.I.ClearedMode = mode;
            GameApp.I.ClearedPrevStars = prevStars;
            GameApp.I.ClearedUnlocked = unlocks;
            GameApp.I.Close("PopupPause"); // opened during the last arrow's exit: must not stay behind the popup
            // Bonus Spin first (it hands over to Level complete, whose Next / Home show the interstitial)
            GameApp.I.BonusMult = 0;
            GameApp.I.SkipInterstitialOnce = false;
            if (!GameApp.I.Open("PopupBonusSpin")) GameApp.I.Open("PopupWin");
        }

        // ------------------------------------------------------------------ solve timer (unscaled time)

        /// <summary>Restart / StartLevel: back to 0, not running (a revive keeps the timer).</summary>
        void ResetTimer()
        {
            solveTime = 0f;
            timing = false;
            timerStarted = false;
            if (timerFx == null) timerFx = GameApp.I.StartCoroutine(TimerLoop());
        }

        /// <summary>Any arrow tap (wrong moves too). left = arrows not yet removed / leaving after it.</summary>
        void OnTapped(int left)
        {
            SetMoves(moves + 1); // every tap that hits an arrow, free or blocked (no effect on stars)
            if (popFx != null) GameApp.I.StopCoroutine(popFx);
            popFx = GameApp.I.StartCoroutine(CountPop());
            if (!timerStarted)
            {
                timerStarted = true;
                timing = true;
            }
            if (left <= 0) timing = false; // the last arrow was tapped: freeze before its exit animation
        }

        /// <summary>Counts only while playing: not paused, no lose popup (ads), not in the lose / collapse sequence.</summary>
        bool TimerRuns()
        {
            return timing
                && Board.Lives > 0
                && loseFx == null
                && !GameApp.I.IsOpen<PopupPause>()
                && !GameApp.I.IsOpen<PopupLose>()
                && !Ads.IsFullScreenAdShowing   // ad time is not play time
                && !Ads.JustReturned;           // nor the frame that carries the time away (background / ad)
        }

        IEnumerator TimerLoop()
        {
            while (true)
            {
                if (TimerRuns()) solveTime = Mathf.Min(solveTime + Mathf.Min(Time.unscaledDeltaTime, TimerMaxStep), TimerCapSeconds);
                ApplySkipAnim(); // the Skip pill's look (no timer of its own)
                yield return null;
            }
        }

        /// <summary>mm:ss, seconds rounded down, capped at 99:59.</summary>
        public static string FormatTime(float seconds)
        {
            int s = Mathf.Clamp(Mathf.FloorToInt(seconds), 0, 99 * 60 + 59);
            return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
        }

        /// <summary>
        /// The only way a hint starts. With hints left: show one and take one, but only if a hint was really shown
        /// (none while one is still playing, none without a free arrow). With none left: offer the video popup.
        /// </summary>
        void OnHint()
        {
            if (Save.Hints <= 0)
            {
                GameApp.I.Open("PopupHints");
                return;
            }
            if (Board.ShowHint())
            {
                Save.Hints = Save.Hints - 1;
                AnalyticsService.HintUsed(level);
            }
            RefreshHints();
        }

        /// <summary>Reward from PopupHints: adds n hints, saves, updates the badge and shows "+n hints". Shows no hint.</summary>
        public void GrantHints(int n)
        {
            Save.Hints = Save.Hints + n;
            RefreshHints();
            if (toastFx != null) GameApp.I.StopCoroutine(toastFx);
            toastFx = GameApp.I.StartCoroutine(Toast("+" + n + " hints"));
        }

        void RefreshHints()
        {
            hintCount.text = Save.Hints.ToString();
        }

        void SetHearts(int lives)
        {
            for (int i = 0; i < hearts.Length; i++)
                if (hearts[i] != null) hearts[i].color = i < lives ? Theme.Error : Theme.HeartLost;
        }

        // ------------------------------------------------------------------ feedback (unscaled time)

        void OnLives(int lives)
        {
            if (lives == shownLives - 1 && lives >= 0 && lives < hearts.Length)
            {
                if (heartFx[lives] != null) GameApp.I.StopCoroutine(heartFx[lives]);
                heartFx[lives] = GameApp.I.StartCoroutine(HeartBreak(lives));
            }
            else SetHearts(lives);
            shownLives = lives;
        }

        void OnRemaining(int n)
        {
            // (the old arrows-left chip is gone; the count is still tracked for the pop / analytics)
            bool pop = n < shownRemaining;
            shownRemaining = n;
            if (!pop) return;
            // no counter to pop any more
        }

        void OnBumped()
        {
            if (flashFx != null) GameApp.I.StopCoroutine(flashFx);
            flashFx = GameApp.I.StartCoroutine(Flash());
            if (shakeFx != null) GameApp.I.StopCoroutine(shakeFx);
            shakeFx = GameApp.I.StartCoroutine(Shake());
        }

        /// <summary>Stops every running effect and puts things back at rest.</summary>
        void StopFx()
        {
            var app = GameApp.I;
            if (flashFx != null) app.StopCoroutine(flashFx);
            if (shakeFx != null) app.StopCoroutine(shakeFx);
            if (popFx != null) app.StopCoroutine(popFx);
            if (loseFx != null) app.StopCoroutine(loseFx);
            if (toastFx != null) app.StopCoroutine(toastFx);
            flashFx = shakeFx = popFx = loseFx = toastFx = null;
            if (loseTint != null) loseTint.gameObject.SetActive(false);
            if (toast != null) toast.gameObject.SetActive(false);
            for (int i = 0; i < heartFx.Length; i++)
            {
                if (heartFx[i] != null) app.StopCoroutine(heartFx[i]);
                heartFx[i] = null;
                if (heartRts[i] != null) { heartRts[i].localScale = Vector3.one; heartRts[i].localEulerAngles = Vector3.zero; }
            }
            if (flash != null) flash.gameObject.SetActive(false);
            Root.anchoredPosition = Vector2.zero;
            if (movesText != null) movesText.rectTransform.localScale = Vector3.one;
        }

        /// <summary>Rise to the peak in the first 12 % (ease-out), then fall as (1-x)^2.</summary>
        IEnumerator Flash()
        {
            flash.gameObject.SetActive(true);
            float t = 0f;
            while (t < FlashSeconds)
            {
                t += Time.unscaledDeltaTime;
                float x = Mathf.Clamp01(t / FlashSeconds);
                float env;
                if (x < 0.12f) env = EaseOutCubic(x / 0.12f);
                else { float f = 1f - (x - 0.12f) / 0.88f; env = f * f; }
                flash.color = new Color(1f, 1f, 1f, Mathf.Clamp01(env * FlashStrength));
                yield return null;
            }
            flash.gameObject.SetActive(false);
            flashFx = null;
        }

        IEnumerator Shake()
        {
            float t = 0f;
            while (t < ShakeSeconds)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / ShakeSeconds);
                float m = (1f - u) * (1f - u) * ShakeAmount;
                Root.anchoredPosition = new Vector2(Mathf.Sin(t * 38f) * m * 7f, Mathf.Cos(t * 31f) * m * 3f) * Theme.K;
                yield return null;
            }
            Root.anchoredPosition = Vector2.zero;
            shakeFx = null;
        }

        /// <summary>Lost heart: 1 -> 1.6 (-10 deg) -> 0.85 (+7 deg) -> 1 over 0.43 s, turns grey 0.13 s in.</summary>
        IEnumerator HeartBreak(int i)
        {
            var hrt = heartRts[i];
            float[] keyT = { 0f, 0.35f, 0.7f, 1f };
            float[] keyS = { 1f, 1.6f, 0.85f, 1f };
            float[] keyR = { 0f, 10f, -7f, 0f }; // Unity z: positive = counter-clockwise (CSS -10deg)
            const float dur = 0.43f;
            float t = 0f;
            bool grey = false;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                if (!grey && t >= 0.13f) { hearts[i].color = Theme.HeartLost; grey = true; }
                float x = Mathf.Clamp01(t / dur);
                int k = 0;
                while (k < keyT.Length - 2 && x > keyT[k + 1]) k++;
                float f = Mathf.SmoothStep(0f, 1f, (x - keyT[k]) / (keyT[k + 1] - keyT[k]));
                hrt.localScale = Vector3.one * Mathf.Lerp(keyS[k], keyS[k + 1], f);
                hrt.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(keyR[k], keyR[k + 1], f));
                yield return null;
            }
            hearts[i].color = Theme.HeartLost;
            hrt.localScale = Vector3.one;
            hrt.localEulerAngles = Vector3.zero;
            heartFx[i] = null;
        }

        /// <summary>MOVES count: scale 1 -> 1.35 -> 1 over 0.26 s.</summary>
        IEnumerator CountPop()
        {
            var crt = movesText.rectTransform;
            const float dur = 0.26f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                crt.localScale = Vector3.one * (1f + 0.35f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / dur)));
                yield return null;
            }
            crt.localScale = Vector3.one;
            popFx = null;
        }

        static float EaseOutCubic(float x)
        {
            float a = 1f - x;
            return 1f - a * a * a;
        }
    }
}
