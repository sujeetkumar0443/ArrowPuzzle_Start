using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ArrowPuzzle
{
    /// <summary>Import settings for everything in Resources/Music, and the "Check music cuts" menu item.</summary>
    public class MusicEditorTools : AssetPostprocessor
    {
        const float VorbisQuality = 0.4f; // the Inspector's Quality 40

        public override uint GetVersion() { return 1; } // raise to re-import the existing music files

        void OnPreprocessAudio()
        {
            if (!assetPath.Contains("/Resources/Music/")) return;
            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = true;
            importer.loadInBackground = false;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = VorbisQuality;
            settings.preloadAudioData = false;
            importer.defaultSampleSettings = settings;
        }

        [MenuItem("ArrowPuzzle/Check music cuts")]
        static void CheckCuts()
        {
            foreach (GameMode mode in Enum.GetValues(typeof(GameMode)))
            {
                var clips = Resources.LoadAll<AudioClip>("Music/" + mode);
                if (clips.Length == 0)
                {
                    Debug.LogWarning("Music " + mode + ": no clips in Resources/Music/" + mode);
                    continue;
                }
                Array.Sort(clips, (a, b) => string.CompareOrdinal(a.name, b.name));

                var lengths = new float[clips.Length];
                int capacity = 0;
                for (int i = 0; i < clips.Length; i++)
                {
                    lengths[i] = clips[i].length;
                    capacity += MusicCuts.Slots(lengths[i]);
                }

                var unique = new HashSet<string>();
                var list = new StringBuilder();
                for (int level = 1; level <= ModeInfo.LevelsPerMode; level++)
                {
                    var cut = MusicCuts.For(level, lengths);
                    string start = cut.Start.ToString("0.0", CultureInfo.InvariantCulture);
                    unique.Add(cut.Track + "/" + start);
                    list.Append("\n  L").Append(level).Append(": ").Append(clips[cut.Track].name).Append(" start ").Append(start).Append('s');
                }
                Debug.Log("Music " + mode + ": " + clips.Length + " tracks, capacity " + capacity + " non-overlapping cuts, "
                          + unique.Count + " of " + ModeInfo.LevelsPerMode + " levels unique" + list);
            }
        }
    }
}
