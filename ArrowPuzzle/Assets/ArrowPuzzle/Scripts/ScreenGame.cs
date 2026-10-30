using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ArrowPuzzle
{
    /// <summary>Game: header (pause, level, hearts), arrows-left chip, board card, Hint and Restart.</summary>
    public class ScreenGame : AppScreen
    {
        const float Pad = 24f;
        const float HeaderH = 48f;
        const float ChipTop = Pad + HeaderH + 14f;          // 86
        const float ChipH = 34f;
        const float BoardTop = ChipTop + ChipH + 16f;       // 136
        const float BtnBottom = 40f, BtnH = 72f;
        const float BoardGap = 24f;                         // between the buttons and the board area
        const float BoardBottom = BtnBottom + BtnH + BoardGap; // 136 (no banner)
        const float CardPad = 10f;
        const float BannerGap = 8f, BtnEdge = 4f;           // the buttons' raised edge sits 8 above the banner

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

        public BoardView Board { get; private set; }

        Text levelText, countText, hintCount, modeText;
        Image modePill;
        GameMode mode = GameMode.Classic;
        readonly Image[] hearts = new Image[BoardView.MaxLives];
        readonly RectTransform[] heartRts = new RectTransform[BoardView.MaxLives];
        readonly Coroutine[] heartFx = new Coroutine[BoardView.MaxLives];
        RectTransform area, card, hintRt, restartRt;
        BoardZoom zoom;
        int level;
        float btnBottom = BtnBottom, toastY = ToastY; // move up above the banner (ApplyBannerLayout)

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
            BannerPlaceholder.Create(Root); // first child: buttons, board and flying arrows draw over it
            BuildHeader();
            BuildChip();

            // free space for the board card, between the chip and the bottom buttons
            area = Ui.New(Root, "BoardArea");
            Ui.Fill(area);
            area.offsetMin = new Vector2(Theme.D(Pad), Theme.D(BoardBottom));
            area.offsetMax = new Vector2(-Theme.D(Pad), -Theme.D(BoardTop));

            var cardImg = Ui.Img(area, "BoardCard", Theme.Rounded, Theme.Card, 28f);
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
            var pill = Ui.Img(Root, "Toast", Theme.Rounded, Theme.Ink, ToastH * 0.5f);
            pill.raycastTarget = false;
            Ui.PlaceD(pill.rectTransform, Ui.BC, new Vector2(0f, ToastY), new Vector2(ToastW, ToastH));
            toastText = Ui.Label(pill.transform, "", 18f, Theme.Card, Weight.Bold);
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

        void BuildHeader()
        {
            var pause = Ui.Btn(Root, "", Theme.Card, Theme.Ink, 0f, HeaderH * 0.5f, Theme.CardShadow, 3f,
                               () => GameApp.I.Open("PopupPause"), Icon.Pause, 22f);
            Ui.PlaceD((RectTransform)pause.transform, Ui.TL, new Vector2(Pad, -Pad), new Vector2(HeaderH, HeaderH));

            levelText = Ui.Label(Root, "", 30f, Theme.Ink, Weight.Bold);
            Ui.PlaceD(levelText.rectTransform, Ui.TC, new Vector2(0f, -Pad), new Vector2(180f, HeaderH));

            // 3 hearts of 28, gap 4, right aligned
            var row = Ui.New(Root, "Hearts");
            float w = 28f * hearts.Length + 4f * (hearts.Length - 1);
            Ui.PlaceD(row, Ui.TR, new Vector2(-Pad, -Pad), new Vector2(w, HeaderH));
            for (int i = 0; i < hearts.Length; i++)
            {
                var h = Icons.Make(row, Icon.Heart, Theme.Error, 28f);
                Ui.PlaceD(h, new Vector2(0f, 0.5f), new Vector2(i * 32f, 0f), new Vector2(28f, 28f));
                heartRts[i] = h;
                hearts[i] = h.GetComponentInChildren<Image>();
            }
        }

        /// <summary>One centered row, gap 8: mode pill + "Arrows left N" chip, both 34 high with radius 17.</summary>
        void BuildChip()
        {
            var row = Ui.New(Root, "ChipRow");
            Ui.PlaceD(row, Ui.TC, new Vector2(0f, -ChipTop), new Vector2(0f, ChipH));
            Ui.Row(row, 8f);
            row.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            modePill = Chip(row, 14f, ModeInfo.Get(GameMode.Classic).Color);
            modeText = Ui.Label(modePill.transform, "", 16f, Theme.Card, Weight.Semi);

            var count = Chip(row, 18f, Theme.Card);
            Ui.Label(count.transform, "Arrows left", 18f, Theme.InkSoft, Weight.Medium);
            countText = Ui.Label(count.transform, "0", 18f, Theme.Ink, Weight.Bold);
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

        void BuildButtons()
        {
            const float w = (342f - 14f) * 0.5f;

            var hint = Ui.Btn(Root, "Hint", Theme.Teal, Theme.Ink, 22f, 24f, Theme.TealShadow, 4f, OnHint, Icon.Bulb, 26f);
            hintRt = (RectTransform)hint.transform;
            Ui.PlaceD(hintRt, Ui.BL, new Vector2(Pad, BtnBottom), new Vector2(w, BtnH));

            // badge: circle 30 sticking out 6 right / 10 up of the top-right corner
            var badge = Ui.Img(hint.transform, "Badge", Theme.Circle, Theme.Ink);
            badge.raycastTarget = false;
            badge.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Ui.PlaceD(badge.rectTransform, Ui.TR, new Vector2(6f, 10f), new Vector2(30f, 30f));
            hintCount = Ui.Label(badge.transform, "", 16f, Theme.Card, Weight.Bold);
            Ui.Fill(hintCount.rectTransform);

            var restart = Ui.Btn(Root, "Restart", Theme.Card, Theme.Ink, 22f, 24f, Theme.CardShadow, 4f, Restart, Icon.Restart, 26f);
            restartRt = (RectTransform)restart.transform;
            Ui.PlaceD(restartRt, Ui.BR, new Vector2(-Pad, BtnBottom), new Vector2(w, BtnH));
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
            hintRt.anchoredPosition = new Vector2(Theme.D(Pad), Theme.D(y));
            restartRt.anchoredPosition = new Vector2(-Theme.D(Pad), Theme.D(y));
            area.offsetMin = new Vector2(Theme.D(Pad), Theme.D(y + BtnH + BoardGap));
            return true;
        }

        /// <summary>
        /// The real banner height arrived during a level: move the buttons and shrink the loaded board to the new
        /// area by scaling it (no reload, so the level keeps its state; taps map through the board's own transform).
        /// </summary>
        void OnBannerHeight()
        {
            if (!ApplyBannerLayout() || Board.Size.x <= 0f) return;
            FitBoard();
            if (zoom.Zoom > 1.001f) zoom.ResetZoom(true); // back to 1x = fit, within the new pan limits
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
            mode = GameApp.I.CurrentMode;
            Ads.BannerHeightChanged -= OnBannerHeight; // never twice
            Ads.BannerHeightChanged += OnBannerHeight;
            StartLevel(GameApp.I.CurrentLevel);
        }

        public override void OnHide()
        {
            Ads.BannerHeightChanged -= OnBannerHeight;
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

        /// <summary>
        /// Loads level n of the current mode (also for popups: Next / Retry while this screen is already showing).
        /// n is kept within 1..LevelsPerMode.
        /// </summary>
        public void StartLevel(int n)
        {
            Time.timeScale = 1f; // safety: a level never starts frozen
            GameApp.I.UnlockPending = false; // a new level started: an unplayed unlock sequence is out of date
            var data = Levels.For(mode, Mathf.Clamp(n, 1, ModeInfo.LevelsPerMode));
            if (data == null)
            {
                Debug.LogError("[ArrowPuzzle] No level " + n + " for mode " + mode);
                return;
            }
            level = data.number;
            GameApp.I.CurrentMode = mode;
            GameApp.I.CurrentLevel = level;
            levelText.text = "Level " + level;
            RefreshHints();

            var info = ModeInfo.Get(mode);
            modePill.color = info.Color;
            modeText.text = string.IsNullOrEmpty(data.shape) || mode == GameMode.Classic
                ? info.Name
                : info.Name + " · " + char.ToUpper(data.shape[0]) + data.shape.Substring(1);
            StopFx();
            ResetTimer();
            Board.ResetCollapse(); // Load also resets; explicit so Restart / StartLevel never keep a collapsed board
            shownLives = -1;      // the Load below sets hearts and counter without animating
            shownRemaining = -1;
            zoom.ResetZoom(false); // every level start / restart is at 1x

            Ads.Banner(true, "ScreenGame"); // every level start (first, Restart, Next): this screen wants it
            ApplyBannerLayout(); // buttons and board area above the banner's current height

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
            float prevBest = Save.GetBestTime(mode, level);
            bool newBest = prevBest <= 0f || solveTime < prevBest;
            if (newBest) Save.SetBestTime(mode, level, solveTime);
            GameApp.I.ResultTime = solveTime;
            GameApp.I.ResultPrevBest = prevBest;
            GameApp.I.ResultNewBest = newBest;
            Debug.Log("[ArrowPuzzle] Solve time: " + FormatTime(solveTime) + " (" + solveTime.ToString("F2") + " s, "
                      + (prevBest > 0f ? "best was " + FormatTime(prevBest) + " (" + prevBest.ToString("F2") + " s)" : "no best yet")
                      + (newBest ? " / new best" : "") + ")");

            GameApp.I.ResultLives = Board.Lives;
            int stars = Mathf.Clamp(Board.Lives, 1, 3);
            Save.SetStars(mode, level, stars);
            if (level == Save.GetUnlocked(mode))
            {
                Save.SetUnlocked(mode, Mathf.Min(level + 1, ModeInfo.LevelsPerMode + 1));
                // this clear opened a new level: let the level map play the unlock sequence
                if (level < ModeInfo.LevelsPerMode)
                {
                    GameApp.I.UnlockPending = true;
                    GameApp.I.ClearedLevel = level;
                    GameApp.I.ClearedStars = stars;
                    GameApp.I.ClearedMode = mode;
                }
            }
            GameApp.I.Open("PopupWin"); // its Next / Home show the interstitial
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
            if (Board.ShowHint()) Save.Hints = Save.Hints - 1;
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
            countText.text = n.ToString();
            bool pop = n < shownRemaining;
            shownRemaining = n;
            if (!pop) return;
            if (popFx != null) GameApp.I.StopCoroutine(popFx);
            popFx = GameApp.I.StartCoroutine(CountPop());
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
            if (countText != null) countText.rectTransform.localScale = Vector3.one;
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

        /// <summary>"Arrows left" count: scale 1 -> 1.35 -> 1 over 0.26 s.</summary>
        IEnumerator CountPop()
        {
            var crt = countText.rectTransform;
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
