using System;
using UnityEngine;

namespace ArrowPuzzle
{
    /// <summary>Sound effects and background music. Loads Resources/Audio/*.ogg; falls back to simple generated beeps if a file is missing.</summary>
    public class Sfx : MonoBehaviour
    {
        public enum Id { Click = 0, Slide = 1, Error = 2, Win = 3, Hint = 4, Lose = 5, Star = 6 }
        static readonly string[] Files = { "click", "slide", "error", "win", "hint", "lose", "star" };
        const float MusicVolume = 0.3f;
        AudioSource music;

        static Sfx inst;
        AudioSource src;
        AudioClip[] clips;
        const int Rate = 44100;

        public static void Init(GameObject host)
        {
            inst = host.AddComponent<Sfx>();
            inst.src = host.AddComponent<AudioSource>();
            inst.src.playOnAwake = false;
            inst.Build();
            inst.StartMusic(host);
        }

        void StartMusic(GameObject host)
        {
            var clip = Resources.Load<AudioClip>("Audio/music");
            if (clip == null) return;
            music = host.AddComponent<AudioSource>();
            music.clip = clip;
            music.loop = true;
            music.playOnAwake = false;
            music.volume = MusicVolume;
            if (Save.Sound) music.Play();
        }

        // Music follows the Sound switch (Pause / Settings), no extra wiring needed.
        void Update()
        {
            if (music == null) return;
            if (Save.Sound && !music.isPlaying) music.Play();
            else if (!Save.Sound && music.isPlaying) music.Pause();
        }

        public static void Play(Id id)
        {
            if (inst == null || !Save.Sound) return;
            inst.src.PlayOneShot(inst.clips[(int)id]);
        }

        void Build()
        {
            clips = new AudioClip[Files.Length];
            clips[(int)Id.Click] = Make("click", 0.07f, t => Mathf.Sin(2f * Mathf.PI * 900f * t) * Mathf.Exp(-t * 60f));
            clips[(int)Id.Slide] = Make("slide", 0.22f, t =>
            {
                float dur = 0.22f;
                float phase = 2f * Mathf.PI * (300f * t + 0.5f * (1200f / dur) * t * t);
                return Mathf.Sin(phase) * Mathf.Sin(Mathf.PI * t / dur) * 0.8f;
            });
            clips[(int)Id.Error] = Make("error", 0.26f, t => Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 150f * t)) * 0.7f * Mathf.Exp(-t * 8f));
            clips[(int)Id.Win] = Make("win", 0.7f, t =>
            {
                float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
                int idx = Mathf.Min(3, (int)(t / 0.12f));
                float lt = t - idx * 0.12f;
                return Mathf.Sin(2f * Mathf.PI * notes[idx] * t) * Mathf.Exp(-lt * 6f);
            });
            clips[(int)Id.Hint] = Make("hint", 0.18f, t => Mathf.Sin(2f * Mathf.PI * (t < 0.08f ? 1100f : 1500f) * t) * Mathf.Exp(-t * 14f));
            clips[(int)Id.Lose] = clips[(int)Id.Error];
            clips[(int)Id.Star] = clips[(int)Id.Hint];

            // Real audio files (Resources/Audio/*.ogg) replace the beeps above when present.
            for (int i = 0; i < Files.Length; i++)
            {
                var c = Resources.Load<AudioClip>("Audio/" + Files[i]);
                if (c != null) clips[i] = c;
            }
        }

        static AudioClip Make(string name, float dur, Func<float, float> f)
        {
            int n = (int)(Rate * dur);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate) * 0.5f, -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
