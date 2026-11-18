using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ArrowPuzzle
{
    /// <summary>Calls onHold once when a finger stays down on this graphic for Seconds (unscaled). Used by the dev route.</summary>
    public class LongPressTrigger : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public const float Seconds = 1f;
        public Action onHold;
        float downAt = -1f;

        public void OnPointerDown(PointerEventData e) { downAt = Time.unscaledTime; }
        public void OnPointerUp(PointerEventData e) { downAt = -1f; }
        public void OnPointerExit(PointerEventData e) { downAt = -1f; }
        void OnDisable() { downAt = -1f; }

        void Update()
        {
            if (downAt < 0f || Time.unscaledTime - downAt < Seconds) return;
            downAt = -1f;
            if (onHold != null) onHold();
        }
    }

    /// <summary>
    /// TEMPORARY dev route to the long-arrow sample levels (Resources/Levels/long_samples.txt): long-press the ARROW
    /// wordmark on Home (editor / development builds) -> PopupLongSamples -> ScreenGame. While a sample is active,
    /// ScreenGame saves no progress (no stars, best time, unlock, skip). Leaving ScreenGame ends it.
    /// </summary>
    public static class LongSamplesDev
    {
        public const string File = "long_samples";

        /// <summary>1-based sample being played, 0 = normal levels.</summary>
        public static int Active { get; private set; }

        /// <summary>Set by the popup; ScreenGame's next StartLevel uses Active as is (later starts: Restart or Next).</summary>
        public static bool Pending { get; private set; }

        public static int Count { get { return ShapeLevels.CountInFile(File); } }

        public static void Start(int n)
        {
            Active = Mathf.Clamp(n, 1, Mathf.Max(1, Count));
            Pending = true;
        }

        public static void Started() { Pending = false; }

        /// <summary>Next sample (wraps), used by PopupWin's NEXT.</summary>
        public static void Next()
        {
            int c = Mathf.Max(1, Count);
            Active = Active % c + 1;
        }

        public static void Stop()
        {
            Active = 0;
            Pending = false;
        }

        /// <summary>The active sample's data, flagged for the long renderer. Null if missing.</summary>
        public static LevelData Load()
        {
            var d = ShapeLevels.GetFile(File, Active);
            if (d != null) d.longArrows = true;
            return d;
        }
    }
}
