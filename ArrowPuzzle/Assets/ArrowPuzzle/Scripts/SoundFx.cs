using System;
using UnityEngine;

namespace ArrowPuzzle
{
    /// <summary>
    /// The "whoosh + pop" of an arrow leaving the board, generated once on first use. Quick exits in a row raise the
    /// pitch one semitone each. Has its own AudioSource (a child of GameApp) because the pitch must not touch Sfx.
    /// </summary>
    public static class SoundFx
    {
        const int Rate = 44100;
        const float Duration = 0.18f, Peak = 0.5f;
        const float PopStart = 0.135f;                  // s: the sine pop fills the end of the clip
        const float ComboWindow = 1.2f, MinGap = 0.04f; // s
        const float SemitoneRatio = 1.0595f;
        const int MaxSteps = 7;

        static AudioSource source;
        static AudioClip clip;
        static int steps;

        // real time, like Ads: unscaled game time stops while the app is in the background
        static DateTime lastExit = DateTime.MinValue;

        /// <summary>An arrow starts to slide out. Silent with Sound off or during a full-screen ad; at most one per MinGap.</summary>
        public static void PlayExit()
        {
            if (!Save.Sound || Ads.IsFullScreenAdShowing || !Ensure()) return;

            var now = DateTime.UtcNow;
            double since = (now - lastExit).TotalSeconds;
            bool recent = since >= 0.0; // false only if the device clock was moved back
            if (recent && since < MinGap) return;

            steps = recent && since < ComboWindow ? Mathf.Min(steps + 1, MaxSteps) : 0;
            lastExit = now;
            source.pitch = Mathf.Pow(SemitoneRatio, steps);
            source.PlayOneShot(clip);
        }

        /// <summary>Back to the base pitch (a level starts or restarts).</summary>
        public static void ResetCombo()
        {
            steps = 0;
            lastExit = DateTime.MinValue;
            if (source != null) source.pitch = 1f;
        }

        static bool Ensure()
        {
            if (source == null)
            {
                if (GameApp.I == null) return false;
                var go = new GameObject("SoundFx");
                go.transform.SetParent(GameApp.I.transform, false);
                source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
            }
            if (clip == null)
            {
                var samples = BuildSamples();
                clip = AudioClip.Create("arrow_exit", samples.Length, 1, Rate, false);
                clip.SetData(samples, 0);
            }
            return true;
        }

        /// <summary>
        /// Noise through a low-pass whose cutoff sweeps down and back up, then a short rising sine pop. Both envelopes
        /// start and end at exactly 0, so the clip has no clicks. Own Random: the game's UnityEngine.Random is untouched.
        /// </summary>
        static float[] BuildSamples()
        {
            int n = (int)(Rate * Duration);
            var data = new float[n];
            var rng = new System.Random(23);
            float end = (n - 1) / (float)Rate;
            float low1 = 0f, low2 = 0f, popPhase = 0f, max = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float u = t / end;

                // whoosh: 3600 Hz -> 700 Hz -> 3000 Hz over the clip (two one-pole filters in a row)
                float cutoff = u < 0.5f ? Mathf.Lerp(3600f, 700f, u * 2f) : Mathf.Lerp(700f, 3000f, u * 2f - 1f);
                float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate);
                float noise = (float)rng.NextDouble() * 2f - 1f;
                low1 += a * (noise - low1);
                low2 += a * (low1 - low2);
                float whooshEnv = Mathf.Sin(Mathf.PI * u);
                float s = low2 * whooshEnv * whooshEnv;

                // pop: 520 Hz -> 780 Hz
                if (t >= PopStart)
                {
                    float p = Mathf.Clamp01((t - PopStart) / (end - PopStart));
                    popPhase += 2f * Mathf.PI * Mathf.Lerp(520f, 780f, p) / Rate;
                    s += Mathf.Sin(popPhase) * Mathf.Sin(Mathf.PI * p) * 0.35f;
                }

                data[i] = s;
                max = Mathf.Max(max, Mathf.Abs(s));
            }

            float gain = Peak / Mathf.Max(max, 0.0001f);
            for (int i = 0; i < n; i++) data[i] *= gain;
            return data;
        }
    }
}
