using UnityEngine;

namespace ArrowPuzzle
{
    /// <summary>Which part of which track a level plays. Deterministic: the same level always gives the same cut.</summary>
    public static class MusicCuts
    {
        public const float CutLength = 32f, Fade = 1.5f; // s
        const float MinCut = 1f;                         // s: below this a clip is too short for fades, loop half of it

        public struct Cut
        {
            public int Track;
            public float Start, Length;
        }

        /// <summary>Whole cuts that fit in a clip, keeping Fade free at both ends. At least 1.</summary>
        public static int Slots(float clipLength)
        {
            return Mathf.Max(1, Mathf.FloorToInt((clipLength - 2f * Fade) / CutLength));
        }

        /// <summary>The cut for a level (kept within 1..LevelsPerMode). trackLengths = clip lengths in s, sorted by name, not empty.</summary>
        public static Cut For(int level, float[] trackLengths)
        {
            int l = Mathf.Clamp(level, 1, ModeInfo.LevelsPerMode) - 1;
            int t = trackLengths.Length;
            int track = l % t;
            float clip = trackLengths[track];
            int slots = Slots(clip);
            int slot = (l / t) % slots;
            int lap = l / (t * slots);

            float offset = slot * CutLength + lap * (CutLength * 0.5f);
            float room = clip - 2f * Fade - CutLength; // the last offset where the cut and its fade-out still fit
            if (room <= 0f) offset = 0f;
            else if (offset > room) offset = Mathf.Repeat(offset, room);

            float length = Mathf.Min(CutLength, clip - 2f * Fade);
            if (length < MinCut) return new Cut { Track = track, Start = 0f, Length = Mathf.Max(clip * 0.5f, 0.1f) };
            return new Cut { Track = track, Start = Fade + offset, Length = length };
        }
    }
}
