using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace ArrowPuzzle
{
    /// <summary>
    /// Single entry point for level data, any level number (no cap here; Campaign.ReleaseLevelCap limits play).
    /// Levels 1..Campaign.PrebuiltCount are parsed from their pack file (the last file's text is cached). Later levels
    /// are generated from their number on a background thread (LevelStream) and kept in memory only (the last few);
    /// nothing is written to the phone. TryGet never blocks: null means "still building", and the screen shows a
    /// short loading state and asks again.
    /// </summary>
    public static class Levels
    {
        static string cachedFile, cachedText;
        static readonly HashSet<int> logged = new HashSet<int>();

        /// <summary>Level g can be shown now (prebuilt, or generated and still in memory).</summary>
        public static bool IsReady(int g)
        {
            return Campaign.IsPrebuilt(g) || LevelStream.Has(g);
        }

        /// <summary>Level g now, or null while it is being generated (the build is started / moved to the front).</summary>
        public static LevelData TryGet(int g)
        {
            if (g < 1) g = 1;
            if (Campaign.IsPrebuilt(g)) return FromFile(g);
            string text = LevelStream.Get(g);
            if (text == null)
            {
                LevelStream.Request(g, true);
                return null;
            }
            return Finish(g, ShapeLevels.ParseLevel(text, g), "generated");
        }

        /// <summary>Blocking version (editor tools, checks): generates level g on this thread if it is not ready.</summary>
        public static LevelData For(int g)
        {
            var d = TryGet(g);
            if (d != null) return d;
            return Finish(g, ShapeLevels.ParseLevel(LevelStream.BuildNow(g), g), "generated");
        }

        /// <summary>Starts building level g in the background if it is not prebuilt or built yet (next levels, Home).</summary>
        public static void Prefetch(int g)
        {
            if (g >= 1 && !Campaign.IsPrebuilt(g)) LevelStream.Request(g, false);
        }

        static LevelData FromFile(int g)
        {
            string file = "Levels/" + Campaign.FileOf(g);
            if (file != cachedFile)
            {
                var ta = Resources.Load<TextAsset>(file);
                if (ta == null)
                {
                    Debug.LogError("[ArrowPuzzle] Missing level file Resources/" + file + ".txt");
                    return null;
                }
                cachedText = ta.text;
                cachedFile = file;
                Resources.UnloadAsset(ta);
            }
            return Finish(g, ShapeLevels.ParseLevel(cachedText, g), file);
        }

        static LevelData Finish(int g, LevelData data, string source)
        {
            if (data == null)
            {
                Debug.LogError("[ArrowPuzzle] Level " + g + " not found (" + source + ")");
                return null;
            }
            data.longArrows = true;
            if (logged.Add(g))
            {
                var v = ShapeLibrary.VariationOf(data.shape);
                Debug.Log("[Level] " + g + " shape=" + ShapeLibrary.BaseOf(data.shape)
                          + (v != ShapeVariation.None ? " variation=" + ShapeLibrary.VariationName(v) : "")
                          + " key=" + data.shape + " seed=" + data.seed + " grid=" + data.cols + "x" + data.rows
                          + " arrows=" + data.pieces.Count + " source=" + source);
            }
            return data;
        }
    }

    /// <summary>
    /// Background level builder: one worker thread at a time works through a queue (urgent requests go first) and
    /// keeps the text of the last Keep levels it built or handed out. Campaign.Generate has no Unity calls.
    /// </summary>
    public static class LevelStream
    {
        const int Keep = 6;
        static readonly object gate = new object();
        static readonly Dictionary<int, string> done = new Dictionary<int, string>();
        static readonly List<int> recent = new List<int>(); // least recently used first
        static readonly List<int> queue = new List<int>();
        static int working = -1;
        static bool running;

        public static bool Has(int g)
        {
            lock (gate) return done.ContainsKey(g);
        }

        public static bool Busy(int g)
        {
            lock (gate) return working == g || queue.Contains(g);
        }

        /// <summary>The level's text (and marks it recently used), or null if not built.</summary>
        public static string Get(int g)
        {
            lock (gate)
            {
                string t;
                if (!done.TryGetValue(g, out t)) return null;
                Touch(g);
                return t;
            }
        }

        /// <summary>Queues level g (urgent: to the front) and starts the worker if it is idle.</summary>
        public static void Request(int g, bool urgent)
        {
            lock (gate)
            {
                if (done.ContainsKey(g) || working == g) return;
                int at = queue.IndexOf(g);
                if (at >= 0)
                {
                    if (!urgent || at == 0) return;
                    queue.RemoveAt(at);
                }
                if (urgent) queue.Insert(0, g);
                else queue.Add(g);
                if (running) return;
                running = true;
            }
            var t = new Thread(Work) { IsBackground = true, Name = "LevelGen" };
            try { t.Priority = System.Threading.ThreadPriority.BelowNormal; }
            catch (System.Exception) { /* not supported everywhere */ }
            t.Start();
        }

        /// <summary>Builds level g on the calling thread (blocking) and keeps it.</summary>
        public static string BuildNow(int g)
        {
            string text = Build(g);
            lock (gate) Store(g, text);
            return text;
        }

        static void Work()
        {
            while (true)
            {
                int g;
                lock (gate)
                {
                    if (queue.Count == 0)
                    {
                        running = false;
                        working = -1;
                        return;
                    }
                    g = queue[0];
                    queue.RemoveAt(0);
                    working = g;
                }
                string text = Build(g);
                lock (gate)
                {
                    Store(g, text);
                    working = -1;
                }
            }
        }

        static string Build(int g)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var l = Campaign.Generate(g, s => Debug.LogWarning("[LevelGen] " + s));
                Debug.Log("[LevelGen] level " + g + " built in " + sw.ElapsedMilliseconds + " ms (" + l.Shape + ", " + l.Tries + " seeds)");
                return Campaign.ToText(g, l);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[LevelGen] level " + g + " failed: " + e);
                var sp = Campaign.Spec(g);
                return Campaign.ToText(g, LongLevelGen.Generate("heart", sp.D, sp.Seed));
            }
        }

        static void Store(int g, string text)
        {
            done[g] = text;
            Touch(g);
            while (recent.Count > Keep)
            {
                done.Remove(recent[0]);
                recent.RemoveAt(0);
            }
        }

        static void Touch(int g)
        {
            recent.Remove(g);
            recent.Add(g);
        }
    }
}
