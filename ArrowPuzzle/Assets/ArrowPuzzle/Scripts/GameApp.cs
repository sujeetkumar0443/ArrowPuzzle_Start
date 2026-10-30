using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ArrowPuzzle
{
    /// <summary>
    /// App shell: camera, event system, canvas (1080 units wide = 390 design px), safe area and screen switching.
    /// Every class that derives from AppScreen is created automatically and can be shown with Show / Open.
    /// No scene setup is needed; this starts itself in any scene.
    /// </summary>
    public class GameApp : MonoBehaviour
    {
        public static GameApp I { get; private set; }

        /// <summary>The level currently being played (set by Home / Levels, read by the Game screen).</summary>
        public int CurrentLevel = 1;

        /// <summary>The mode CurrentLevel belongs to (Classic, Shapes, Mosaic).</summary>
        public GameMode CurrentMode = GameMode.Classic;

        /// <summary>
        /// Level-cleared hand-off to the level map: set by ScreenGame when a win raised the mode's unlocked level,
        /// played (and cleared) by ScreenLevels.
        /// </summary>
        public bool UnlockPending;
        public int ClearedLevel, ClearedStars;
        public GameMode ClearedMode = GameMode.Classic;

        /// <summary>Lives left when the last level was won (Game sets it, the Win popup reads it for the stars).</summary>
        public int ResultLives = 3;

        /// <summary>Solve time of the last win (s), the best before it (0 = none) and whether it beat that best.</summary>
        public float ResultTime, ResultPrevBest;
        public bool ResultNewBest;

        public RectTransform CanvasRoot { get; private set; }
        public RectTransform SafeRoot { get; private set; }

        RectTransform popupLayer;
        Rect lastSafe;

        class Entry
        {
            public AppScreen screen;
            public GameObject container; // what gets toggled
        }

        readonly Dictionary<Type, Entry> entries = new Dictionary<Type, Entry>();
        readonly List<Type> openPopups = new List<Type>();
        Type current;

        /// <summary>Class name of the screen shown before the current one (null at start); Settings goes back to it.</summary>
        public string PreviousScreen { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstObjectByType<GameApp>() != null) return;
            var go = new GameObject("ArrowPuzzleApp");
            go.AddComponent<GameApp>();
            DontDestroyOnLoad(go);
        }

        // ------------------------------------------------------------------ setup

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Theme.Init();
            Sfx.Init(gameObject);
            Ads.Init(); // once per launch; never throws
            EnsureCamera();
            EnsureEventSystem();
            BuildCanvas();
            CreateScreens();
        }

        void Start()
        {
            if (!Show("ScreenSplash")) ShowFoundationCheck();
        }

        void EnsureCamera()
        {
            if (FindFirstObjectByType<Camera>() != null) return;
            var go = new GameObject("Camera");
            go.transform.SetParent(transform, false);
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Theme.Bg;
            cam.orthographic = true;
            go.AddComponent<AudioListener>();
        }

        void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.transform.SetParent(transform, false);
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        void BuildCanvas()
        {
            var cgo = new GameObject("Canvas");
            cgo.transform.SetParent(transform, false);
            var canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f; // match width: 390 design px always equals 1080 units
            cgo.AddComponent<GraphicRaycaster>();
            CanvasRoot = (RectTransform)cgo.transform;

            var bg = Ui.Img(CanvasRoot, "Bg", null, Theme.Bg);
            bg.raycastTarget = false;
            Ui.Fill(bg.rectTransform);

            SafeRoot = Ui.New(CanvasRoot, "Safe");
            Ui.Fill(SafeRoot);
            popupLayer = Ui.New(CanvasRoot, "Popups");
            Ui.Fill(popupLayer);
            ApplySafeArea();
        }

        void ApplySafeArea()
        {
            lastSafe = Screen.safeArea;
            if (Screen.width <= 0 || Screen.height <= 0) return;
            SafeRoot.anchorMin = new Vector2(lastSafe.xMin / Screen.width, lastSafe.yMin / Screen.height);
            SafeRoot.anchorMax = new Vector2(lastSafe.xMax / Screen.width, lastSafe.yMax / Screen.height);
            SafeRoot.offsetMin = Vector2.zero;
            SafeRoot.offsetMax = Vector2.zero;
        }

        void CreateScreens()
        {
            foreach (var t in typeof(AppScreen).Assembly.GetTypes())
            {
                if (t.IsAbstract || !typeof(AppScreen).IsAssignableFrom(t)) continue;
                var s = (AppScreen)Activator.CreateInstance(t);
                var e = new Entry { screen = s };

                if (s.IsPopup)
                {
                    var host = Ui.New(popupLayer, t.Name + "Host");
                    Ui.Fill(host);
                    var dim = host.gameObject.AddComponent<Image>();
                    dim.color = Theme.Dim; // blocks taps behind the popup
                    s.Init(host);
                    e.container = host.gameObject;
                    host.gameObject.SetActive(false);
                }
                else
                {
                    s.Init(s.UseSafeArea ? SafeRoot : CanvasRoot);
                    e.container = s.Root.gameObject;
                    e.container.SetActive(false);
                }
                entries[t] = e;
            }
        }

        // ------------------------------------------------------------------ navigation

        public T Get<T>() where T : AppScreen
        {
            Entry e;
            return entries.TryGetValue(typeof(T), out e) ? (T)e.screen : null;
        }

        public void Show<T>() where T : AppScreen
        {
            Show(typeof(T));
        }

        /// <summary>Show a screen by class name. Returns false if that screen doesn't exist (yet).</summary>
        public bool Show(string typeName)
        {
            foreach (var kv in entries)
            {
                if (kv.Key.Name == typeName && !kv.Value.screen.IsPopup) { Show(kv.Key); return true; }
            }
            Debug.LogWarning("[ArrowPuzzle] Screen '" + typeName + "' is not built yet.");
            return false;
        }

        void Show(Type t)
        {
            CloseAllPopups();
            if (current != null && current != t) PreviousScreen = current.Name;
            foreach (var kv in entries)
            {
                if (kv.Value.screen.IsPopup) continue;
                bool on = kv.Key == t;
                if (kv.Value.container.activeSelf == on) continue;
                kv.Value.container.SetActive(on);
                if (on) kv.Value.screen.OnShow(); else kv.Value.screen.OnHide();
            }
            current = t;
            Entry e;
            if (entries.TryGetValue(t, out e) && !e.container.activeSelf)
            {
                e.container.SetActive(true);
                e.screen.OnShow();
            }
        }

        public void Open<T>() where T : AppScreen
        {
            var t = typeof(T);
            Entry e;
            if (!entries.TryGetValue(t, out e)) return;
            if (!openPopups.Contains(t)) openPopups.Add(t);
            e.container.transform.SetAsLastSibling();
            e.container.SetActive(true);
            e.screen.OnShow();
        }

        public void Close<T>() where T : AppScreen
        {
            ClosePopup(typeof(T));
        }

        /// <summary>Open a popup by class name (so screens can be built in any order). False if it doesn't exist yet.</summary>
        public bool Open(string typeName)
        {
            foreach (var kv in entries)
            {
                if (kv.Key.Name != typeName || !kv.Value.screen.IsPopup) continue;
                if (!openPopups.Contains(kv.Key)) openPopups.Add(kv.Key);
                kv.Value.container.transform.SetAsLastSibling();
                kv.Value.container.SetActive(true);
                kv.Value.screen.OnShow();
                return true;
            }
            Debug.LogWarning("[ArrowPuzzle] Popup '" + typeName + "' is not built yet.");
            return false;
        }

        public void Close(string typeName)
        {
            foreach (var kv in entries)
                if (kv.Key.Name == typeName) { ClosePopup(kv.Key); return; }
        }

        void ClosePopup(Type t)
        {
            Entry e;
            if (!entries.TryGetValue(t, out e)) return;
            openPopups.Remove(t);
            if (e.container.activeSelf)
            {
                e.container.SetActive(false);
                e.screen.OnHide();
            }
        }

        public void CloseAllPopups()
        {
            for (int i = openPopups.Count - 1; i >= 0; i--) ClosePopup(openPopups[i]);
        }

        public bool IsOpen<T>() where T : AppScreen
        {
            return openPopups.Contains(typeof(T));
        }

        // ------------------------------------------------------------------ back button

        void Update()
        {
            if (Screen.safeArea != lastSafe) ApplySafeArea();
            if (BackPressed()) HandleBack();
        }

        static bool BackPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#else
            return false;
#endif
        }

        void HandleBack()
        {
            if (openPopups.Count > 0)
            {
                var top = openPopups[openPopups.Count - 1];
                if (!entries[top].screen.OnBack()) ClosePopup(top);
                return;
            }
            Entry e;
            if (current != null && entries.TryGetValue(current, out e) && e.screen.OnBack()) return;
            if (current != null && current.Name == "ScreenHome") Application.Quit();
        }

        // ------------------------------------------------------------------ foundation smoke test

        void ShowFoundationCheck()
        {
            var t = Ui.Label(SafeRoot, "ARROW PUZZLE\nfoundation ready", 34, Theme.Ink, Weight.Bold);
            Ui.Fill(t.rectTransform);
            t.alignment = TextAnchor.MiddleCenter;
        }
    }
}
