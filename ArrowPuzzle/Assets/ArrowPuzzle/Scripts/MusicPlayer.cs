using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using System.Globalization;
#endif
using UnityEngine;

namespace ArrowPuzzle
{
    /// <summary>
    /// Background music: one object for the whole app, two AudioSources that alternate. Each level loops its own cut of
    /// its mode's tracks (MusicCuts). Everything runs from Update on unscaled time, with no coroutines. Silent during
    /// full-screen ads and in the background; AudioListener.pause stays the job of Ads.
    /// </summary>
    public class MusicPlayer : MonoBehaviour
    {
        public const float WinDuck = 0.4f, LoseDuck = 0.3f;

        const float BaseVolume = 0.45f;
        const float LevelCrossfade = 1.2f, ToggleFade = 0.3f, ResumeFade = 1f, DuckTime = 0.4f; // s
        const float MaxStep = 0.1f;                                                             // s: a resume frame is not a fade

        class ModeMusic
        {
            public AudioClip[] clips;
            public float[] lengths; // read once: unloading a clip must not force a reload just to ask its length
        }

        static MusicPlayer inst;

        readonly Dictionary<GameMode, ModeMusic> music = new Dictionary<GameMode, ModeMusic>();
        readonly AudioSource[] sources = new AudioSource[2];
        readonly float[] gain = new float[2], gainTarget = new float[2], gainSpeed = new float[2];
        int active;
        AudioClip wantedClip;
        MusicCuts.Cut wantedCut;
        bool musicOn, away, inBackground, noFocus;
        float master, masterSpeed = 1f / ToggleFade, duck = 1f, duckTarget = 1f, duckSpeed = 1f;

        // ------------------------------------------------------------------ API

        /// <summary>Creates the player once, as a child of the app root.</summary>
        public static void Init(GameObject host)
        {
            if (inst != null) return;
            var go = new GameObject("MusicPlayer");
            go.transform.SetParent(host.transform, false);
            inst = go.AddComponent<MusicPlayer>();
            for (int i = 0; i < inst.sources.Length; i++)
            {
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.loop = false;
                s.priority = 0;
                inst.sources[i] = s;
            }
            inst.musicOn = Save.Music;
            inst.master = inst.musicOn ? 1f : 0f;
        }

        /// <summary>Global level g starts: its type's tracks, its type level's cut. The same level again (Restart) keeps playing.</summary>
        public static void PlayLevel(int g)
        {
            if (inst == null) return;
            inst.Request(LevelPath.TypeOf(g), LevelPath.TypeLevelOf(g));
        }

        /// <summary>Home: the music of the highest unlocked level.</summary>
        public static void PlayHome()
        {
            PlayLevel(Save.UnlockedGlobal);
        }

        /// <summary>Volume factor for a popup (WinDuck, LoseDuck), 1 = back to normal.</summary>
        public static void Duck(float factor)
        {
            if (inst != null) inst.SetDuck(factor);
        }

        /// <summary>The Music switch: saves the setting and fades at once.</summary>
        public static void SetEnabled(bool on)
        {
            Save.Music = on;
            if (inst != null) inst.ApplyEnabled(on);
        }

        // ------------------------------------------------------------------ requests

        void Request(GameMode mode, int level)
        {
            SetDuck(1f);
            var m = MusicFor(mode);
            if (m.clips.Length == 0)
            {
                wantedClip = null;
                gainTarget[0] = gainTarget[1] = 0f;
                gainSpeed[0] = gainSpeed[1] = 1f / LevelCrossfade;
                return;
            }

            var cut = MusicCuts.For(level, m.lengths);
            var clip = m.clips[cut.Track];
            if (clip == wantedClip && Mathf.Approximately(cut.Start, wantedCut.Start)) return;
            wantedClip = clip;
            wantedCut = cut;
#if UNITY_EDITOR
            Debug.Log("Music " + mode + " L" + level + " -> " + clip.name + " start " + cut.Start.ToString("0.0", CultureInfo.InvariantCulture) + "s");
#endif
            if (musicOn) StartCut(gain[active] > 0f ? LevelCrossfade : MusicCuts.Fade, LevelCrossfade);
        }

        ModeMusic MusicFor(GameMode mode)
        {
            ModeMusic m;
            if (music.TryGetValue(mode, out m)) return m;

            string folder = "Music/" + mode;
            var clips = Resources.LoadAll<AudioClip>(folder);
            Array.Sort(clips, (a, b) => string.CompareOrdinal(a.name, b.name));
            if (clips.Length == 0) Debug.LogWarning("[Music] No clips in Resources/" + folder + ": this mode plays no music.");

            m = new ModeMusic { clips = clips, lengths = new float[clips.Length] };
            for (int i = 0; i < clips.Length; i++) m.lengths[i] = clips[i].length;
            music[mode] = m;
            return m;
        }

        void SetDuck(float factor)
        {
            duckTarget = factor;
            duckSpeed = Mathf.Abs(factor - duck) / DuckTime;
        }

        void ApplyEnabled(bool on)
        {
            musicOn = on;
            masterSpeed = 1f / ToggleFade;
            if (on && wantedClip != null && !sources[active].isPlaying) StartCut(0f, ToggleFade); // the master fade does the fade-in
        }

        // ------------------------------------------------------------------ playback

        /// <summary>Starts the wanted cut on the quieter source and fades the other one out. inFade 0 = no fade of its own.</summary>
        void StartCut(float inFade, float outFade)
        {
            int incoming = gain[1 - active] <= gain[active] ? 1 - active : active;
            int outgoing = 1 - incoming;
            Release(incoming);

            if (wantedClip.loadState != AudioDataLoadState.Loaded) wantedClip.LoadAudioData(); // loads now: Load In Background is off
            if (wantedClip.loadState == AudioDataLoadState.Failed)
            {
                Debug.LogWarning("[Music] Could not load " + wantedClip.name);
                wantedClip = null;
                return;
            }

            gain[incoming] = inFade > 0f ? 0f : 1f;
            gainTarget[incoming] = 1f;
            gainSpeed[incoming] = inFade > 0f ? 1f / inFade : 1f;
            gainTarget[outgoing] = 0f;
            gainSpeed[outgoing] = 1f / outFade;
            active = incoming;
            ApplyVolumes();

            var s = sources[incoming];
            s.clip = wantedClip;
            s.time = wantedCut.Start;
            s.Play();
            if (away) s.Pause();
        }

        /// <summary>Stops a source and unloads its clip, unless that clip is still needed.</summary>
        void Release(int i)
        {
            var s = sources[i];
            var clip = s.clip;
            gain[i] = 0f;
            gainTarget[i] = 0f;
            if (clip == null) return;
            s.Stop();
            s.clip = null;
            if (clip != sources[1 - i].clip && (!musicOn || clip != wantedClip)) Resources.UnloadAsset(clip);
        }

        void ApplyVolumes()
        {
            for (int i = 0; i < sources.Length; i++) sources[i].volume = BaseVolume * duck * master * gain[i];
        }

        void Update()
        {
            SyncAway();
            if (away) return;

            float dt = Mathf.Min(Time.unscaledDeltaTime, MaxStep);
            master = Mathf.MoveTowards(master, musicOn ? 1f : 0f, dt * masterSpeed);
            duck = Mathf.MoveTowards(duck, duckTarget, dt * duckSpeed);

            if (!musicOn && master <= 0f)
            {
                Release(0);
                Release(1);
                return;
            }

            for (int i = 0; i < sources.Length; i++)
            {
                gain[i] = Mathf.MoveTowards(gain[i], gainTarget[i], dt * gainSpeed[i]);
                if (gain[i] <= 0f && gainTarget[i] <= 0f) Release(i);
            }

            // the cut is over (or the clip stopped by itself): crossfade back to its start
            var s = sources[active];
            if (musicOn && wantedClip != null && s.clip == wantedClip && (!s.isPlaying || s.time - wantedCut.Start >= wantedCut.Length))
                StartCut(MusicCuts.Fade, MusicCuts.Fade);

            ApplyVolumes();
        }

        // ------------------------------------------------------------------ background and ads

        void OnApplicationPause(bool paused)
        {
            inBackground = paused;
            SyncAway();
        }

        void OnApplicationFocus(bool focused)
        {
            noFocus = !focused;
            SyncAway();
        }

        /// <summary>Pauses at once when the app goes away or a full-screen ad opens; on return, resumes with a fade-in.</summary>
        void SyncAway()
        {
            bool now = inBackground || noFocus || Ads.IsFullScreenAdShowing;
            if (now == away) return;
            away = now;
            if (away)
            {
                foreach (var s in sources) s.Pause();
                master = 0f;
                ApplyVolumes();
                return;
            }
            masterSpeed = 1f / ResumeFade;
            foreach (var s in sources) s.UnPause();
        }
    }
}
