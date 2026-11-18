using UnityEngine;

namespace ArrowPuzzle
{
    /// <summary>
    /// Dev helper (Editor / Development Build only; a no-op in release): cycles through all themes every Step seconds on
    /// the current screen (Theme.Apply, so GameApp rebuilds it), until stopped. Stopping goes back to the saved theme.
    /// Started / stopped from the dev list (PopupLongSamples). Changes nothing the player owns.
    /// </summary>
    public class ThemeTour : MonoBehaviour
    {
        public const float Step = 2f;

        static ThemeTour run;
        int index;
        float next;

        public static bool Running { get { return run != null; } }

        public static void Toggle()
        {
            if (Running) End(); else Begin();
        }

        public static void Begin()
        {
            if (!Debug.isDebugBuild) { Debug.Log("[Dev] theme tour blocked: release build"); return; }
            if (run != null) return;
            var go = new GameObject("ThemeTour");
            DontDestroyOnLoad(go);
            run = go.AddComponent<ThemeTour>();
            run.index = -1;
            run.next = 0f; // first theme at once
            Debug.Log("[Dev] theme tour started (" + Themes.All.Length + " themes, " + Step + " s each)");
        }

        public static void End()
        {
            if (run == null) return;
            Destroy(run.gameObject);
            run = null;
            Theme.Apply(Save.ThemeId);
            Debug.Log("[Dev] theme tour stopped, back to " + Save.ThemeId);
        }

        void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + Step;
            index = (index + 1) % Themes.All.Length;
            var t = Themes.All[index];
            Debug.Log("[Dev] theme tour: " + t.Name);
            Theme.Apply(t.Id);
        }

        void OnDestroy()
        {
            if (run == this) run = null;
        }
    }
}
