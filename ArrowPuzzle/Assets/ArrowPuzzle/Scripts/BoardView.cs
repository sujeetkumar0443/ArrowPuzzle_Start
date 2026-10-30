using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArrowPuzzle
{
    /// <summary>Runs one level: draws the arrows, handles taps, blocking, sliding, lives and hints.</summary>
    public class BoardView : MonoBehaviour
    {
        // "Board Collapse" loss animation (s / design px), see PlayCollapse
        const float JitterT1 = 0.5f, JitterRamp = 0.17f, JitterAmp = 3f, JitterFreq = 60f;
        const float GreyT0 = 0.35f, GreyT1 = 0.6f;
        const float FallT0 = 0.6f, FallSpread = 0.6f, FallTime = 0.9f, FallFadeFrom = 0.8f;
        const float FallDist = 900f, FallSpin = 2.4f, FallDrift = 30f;   // spin in radians
        const float CollapseDoneT = 1.4f;
        const float ReviveScaleFrom = 0.6f, ReviveScaleTime = 0.3f;
        static readonly Color CollapseGrey = Theme.Hex("#9C9486");

        enum State { Free, Blocked, Waiting }

        class Piece
        {
            public int index;
            public PieceData d;
            public ArrowGraphic g;
            public RectTransform rt;
            public bool removed, moving, busy;
            public List<Vector2> ext;      // tail extension + body + run-out past the head
            public float[] cum;
            public float bodyLen, edgeDist, baseOff;
            public float dist;             // "off": how far the arrow has moved along its own path
            public Color baseColor;
            public Vector2 dirV;
            public Coroutine fx, tint, glow;
            public Vector2 center; // centroid of the cell centres, pivot for collapse rotation / revive scale
            public ArrowGraphic glowG;
            public ArrowGraphic[] trail;
        }

        public event Action<int> RemainingChanged;
        public event Action<int> LivesChanged;
        public event Action Won;
        public event Action Lost;
        /// <summary>A blocked arrow hit its blocker (the moment the life is lost). Used for screen flash / shake.</summary>
        public event Action Bumped;
        /// <summary>
        /// An arrow was tapped and starts moving (free exit or wrong move). Arg = arrows still on the board and not
        /// leaving after this tap (0 = the last arrow was just sent off). Used for the solve timer.
        /// </summary>
        public event Action<int> Tapped;

        public const int MaxLives = 3;

        public Vector2 Size { get; private set; }
        public int Lives { get { return lives; } }
        public int Remaining { get { return remaining; } }
        public bool Locked { get; set; }

        RectTransform rt;
        LevelData level;
        float cell;
        int[,] owner;
        int remaining, lives;
        readonly List<Piece> pieces = new List<Piece>();
        readonly List<GameObject> extras = new List<GameObject>();
        readonly List<Vector2> tmp = new List<Vector2>();
        Coroutine hintFx;
        int[] pickStamp; // per piece: last tap that measured it (avoids a HashSet per tap)
        int pickId;

        void Awake()
        {
            rt = (RectTransform)transform;
        }

        // ------------------------------------------------------------------ loading

        public void Load(LevelData data, Vector2 area)
        {
            ResetCollapse();
            Clear();
            level = data;
            // fit the area (no lower limit, so big Mosaic boards always fit); cap stops tiny boards getting huge
            cell = Mathf.Floor(Mathf.Min(area.x / data.cols, area.y / data.rows));
            cell = Mathf.Clamp(cell, 1f, 170f);
            Size = new Vector2(cell * data.cols, cell * data.rows);

            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = Vector2.zero;
            rt.sizeDelta = Size;
            rt.anchoredPosition = -Size * 0.5f;

            owner = new int[data.cols, data.rows];
            for (int x = 0; x < data.cols; x++) for (int y = 0; y < data.rows; y++) owner[x, y] = -1;

            // shaped boards: a rounded beige tile under every mask cell (all tiles first, then all dots, so each batches)
            if (data.mask != null)
            {
                float tileSize = cell * 0.9f;
                for (int x = 0; x < data.cols; x++)
                {
                    for (int y = 0; y < data.rows; y++)
                    {
                        if (!data.mask[x, y]) continue;
                        var tile = Ui.Img(transform, "tile", Theme.Rounded, Theme.Tile);
                        tile.raycastTarget = false;
                        tile.pixelsPerUnitMultiplier = 24f / (0.2f * cell); // corner radius 0.2 cell
                        var trt = tile.rectTransform;
                        trt.anchorMin = trt.anchorMax = Vector2.zero;
                        trt.pivot = new Vector2(0.5f, 0.5f);
                        trt.sizeDelta = new Vector2(tileSize, tileSize);
                        trt.anchoredPosition = CenterOf(new Vector2Int(x, y));
                        extras.Add(tile.gameObject);
                    }
                }
            }

            // grid dots (only on mask cells when there is a mask)
            float dotSize = cell * 0.152f;
            for (int x = 0; x < data.cols; x++)
            {
                for (int y = 0; y < data.rows; y++)
                {
                    if (data.mask != null && !data.mask[x, y]) continue;
                    var dot = Ui.Img(transform, "dot", Theme.Circle, Theme.Dot);
                    dot.raycastTarget = false;
                    var drt = dot.rectTransform;
                    drt.anchorMin = drt.anchorMax = Vector2.zero;
                    drt.pivot = new Vector2(0.5f, 0.5f);
                    drt.sizeDelta = new Vector2(dotSize, dotSize);
                    drt.anchoredPosition = CenterOf(new Vector2Int(x, y));
                    extras.Add(dot.gameObject);
                }
            }

            for (int i = 0; i < data.pieces.Count; i++) BuildPiece(i, data.pieces[i]);
            pickStamp = new int[pieces.Count];

            lives = MaxLives;
            remaining = pieces.Count;
            Locked = false;
            if (RemainingChanged != null) RemainingChanged(remaining);
            if (LivesChanged != null) LivesChanged(lives);
        }

        void BuildPiece(int i, PieceData pd)
        {
            var p = new Piece { index = i, d = pd };

            p.g = NewArrow("Piece" + i);
            p.rt = p.g.rectTransform;
            p.g.thickness = Mathf.Max(8f, cell * 0.196f);
            p.g.headSize = cell * 0.6f;
            p.g.widthFactor = 0.56f;
            p.baseColor = Theme.Palette[pd.colorIndex % Theme.Palette.Length];
            foreach (var c in pd.cells) p.center += CenterOf(c);
            p.center /= pd.cells.Count;
            p.g.color = p.baseColor;

            p.dirV = new Vector2(pd.dir.x, pd.dir.y);
            p.ext = new List<Vector2>();
            foreach (var c in pd.cells) p.ext.Add(CenterOf(c));
            if (pd.cells.Count == 1) p.ext.Insert(0, p.ext[0] - p.dirV * cell * 0.4f); // stub so a 1-cell arrow has a body

            p.bodyLen = Poly.Length(p.ext);

            // straight path behind the tail, so the arrow can pull back / recoil behind its rest position
            // (the recoil swings back at most ~0.17 x the advance, which is below one board side)
            Vector2 tailDir = (p.ext[0] - p.ext[1]).normalized;
            p.baseOff = cell * Mathf.Max(level.cols, level.rows);
            p.ext.Insert(0, p.ext[0] + tailDir * p.baseOff);

            p.edgeDist = (StepsToEdge(pd.Head, pd.dir) + 0.5f) * cell;
            Vector2 head = p.ext[p.ext.Count - 1];
            p.ext.Add(head + p.dirV * (p.edgeDist + p.bodyLen + cell * 2f));
            p.cum = Poly.Cum(p.ext);

            foreach (var c in pd.cells) owner[c.x, c.y] = i;
            pieces.Add(p);
            Render(p);
        }

        /// <summary>An ArrowGraphic filling the board (pivot = board origin), not a raycast target.</summary>
        ArrowGraphic NewArrow(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var art = (RectTransform)go.transform;
            art.anchorMin = Vector2.zero;
            art.anchorMax = Vector2.one;
            art.offsetMin = art.offsetMax = Vector2.zero;
            art.pivot = Vector2.zero;
            var g = go.AddComponent<ArrowGraphic>();
            g.raycastTarget = false;
            return g;
        }

        int StepsToEdge(Vector2Int head, Vector2Int dir)
        {
            if (dir.x > 0) return level.cols - 1 - head.x;
            if (dir.x < 0) return head.x;
            if (dir.y > 0) return level.rows - 1 - head.y;
            return head.y;
        }

        Vector2 CenterOf(Vector2Int c)
        {
            return new Vector2((c.x + 0.5f) * cell, (c.y + 0.5f) * cell);
        }

        bool InGrid(int x, int y)
        {
            return x >= 0 && y >= 0 && x < level.cols && y < level.rows;
        }

        public void Clear()
        {
            StopAllCoroutines();
            hintFx = null; // a new level / restart starts without the old hint highlight
            nudgeFx = null;
            nudgeList.Clear();
            foreach (var p in pieces) if (p.g != null) Destroy(p.g.gameObject);
            foreach (var g in extras) if (g != null) Destroy(g);
            pieces.Clear();
            extras.Clear();
            level = null;
        }

        // ------------------------------------------------------------------ input

        /// <summary>
        /// A finished tap at a screen point (BoardZoom decides what counts as a tap). Hit tests through the board's
        /// transform, so zoom and pan are taken into account. Returns true if an arrow is under the point (even if it
        /// can't move right now), false for empty space.
        /// </summary>
        public bool TapAt(Vector2 screenPos, Camera cam)
        {
            if (level == null) return false;
            Vector2 lp;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPos, cam, out lp)) return false;
            if (!rt.rect.Contains(lp)) return false; // only on the board, like the old raycast on the board image
            var p = Pick(lp);
            if (p == null) return false;
            if (!Locked) Tap(p);
            return true;
        }

        /// <summary>Nearest arrow (distance to its centre line) within max(0.55 cell, 14 design px) of the tap.</summary>
        Piece Pick(Vector2 lp)
        {
            float reachDist = Mathf.Max(0.55f * cell, Theme.D(14f));
            int reach = Mathf.CeilToInt(reachDist / cell) + 1;
            int cx = Mathf.FloorToInt(lp.x / cell), cy = Mathf.FloorToInt(lp.y / cell);
            pickId++;

            float best = reachDist;
            Piece bp = null;
            for (int dx = -reach; dx <= reach; dx++)
            {
                for (int dy = -reach; dy <= reach; dy++)
                {
                    int nx = cx + dx, ny = cy + dy;
                    if (!InGrid(nx, ny)) continue;
                    int w = owner[nx, ny];
                    if (w < 0 || pickStamp[w] == pickId) continue;
                    pickStamp[w] = pickId;
                    float d = DistanceTo(pieces[w], lp);
                    if (d <= best) { best = d; bp = pieces[w]; }
                }
            }
            return bp;
        }

        /// <summary>Distance from lp to the arrow's centre line (through its cell centres, at rest).</summary>
        float DistanceTo(Piece p, Vector2 lp)
        {
            var cells = p.d.cells;
            Vector2 a = CenterOf(cells[0]);
            float best = Vector2.Distance(lp, a);
            for (int i = 1; i < cells.Count; i++)
            {
                Vector2 b = CenterOf(cells[i]);
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(lp - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                best = Mathf.Min(best, Vector2.Distance(lp, a + ab * t));
                a = b;
            }
            return best;
        }

        /// <summary>
        /// Walks the head ray to the grid edge (the mask is ignored). Any arrow cell on it blocks,
        /// the arrow's own body included.
        /// </summary>
        State Check(Piece p)
        {
            bool waiting = false;
            var c = p.d.Head + p.d.dir;
            while (InGrid(c.x, c.y))
            {
                int w = owner[c.x, c.y];
                if (w >= 0)
                {
                    if (pieces[w].moving) waiting = true;
                    else return State.Blocked;
                }
                c += p.d.dir;
            }
            return waiting ? State.Waiting : State.Free;
        }

        /// <summary>
        /// k = cells from the head to the first blocker (own body included, like Check), or to the first cell
        /// outside the board if free.
        /// </summary>
        int CellsAhead(Piece p, out Piece blocker)
        {
            blocker = null;
            int k = 1;
            var c = p.d.Head + p.d.dir;
            while (InGrid(c.x, c.y))
            {
                int w = owner[c.x, c.y];
                if (w >= 0) { blocker = pieces[w]; return k; }
                c += p.d.dir;
                k++;
            }
            return k;
        }

        void Tap(Piece p)
        {
            if (p.removed || p.moving || p.busy) return; // only the animating arrow ignores taps
            var st = Check(p);
            if (st == State.Waiting) return;

            if (st == State.Free)
            {
                // leaves the blocking map immediately, so arrows behind it are free at once
                p.moving = true;
                p.busy = true;
                foreach (var c in p.d.cells) owner[c.x, c.y] = -1;
                Sfx.Play(Sfx.Id.Slide);
                p.fx = StartCoroutine(ExitRoutine(p));
                RaiseTapped();
                return;
            }

            Piece blocker;
            int k = CellsAhead(p, out blocker);
            p.busy = true;
            p.fx = StartCoroutine(WrongMoveRoutine(p, blocker, k));
            RaiseTapped();
        }

        void RaiseTapped()
        {
            if (Tapped == null) return;
            int left = 0;
            foreach (var q in pieces) if (!q.removed && !q.moving) left++;
            Tapped(left);
        }

        /// <summary>Blocked arrow reached its blocker: lose a life (same rules as before, now at the impact).</summary>
        void LoseLife()
        {
            if (lives <= 0) return;
            lives = Mathf.Max(0, lives - 1);
            Sfx.Play(Sfx.Id.Error);
            if (Save.Haptics) VibrateSafe();
            if (LivesChanged != null) LivesChanged(lives);
            if (Bumped != null) Bumped();
            if (lives <= 0)
            {
                Locked = true;
                if (Lost != null) Lost(); // ScreenGame times the collapse and the lose popup from here
            }
        }

        static void VibrateSafe()
        {
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }

        // ------------------------------------------------------------------ drawing

        void Render(Piece p)
        {
            RenderAt(p, p.g, p.dist);
        }

        /// <summary>Draws piece p's body shifted by off along its path into graphic g.</summary>
        void RenderAt(Piece p, ArrowGraphic g, float off)
        {
            float s0 = p.baseOff + off;
            Poly.Slice(p.ext, p.cum, s0, s0 + p.bodyLen, tmp);
            g.SetPoints(tmp);
        }

        void Finish(Piece p)
        {
            p.removed = true;
            p.moving = false;
            p.busy = false;
            foreach (var c in p.d.cells) if (owner[c.x, c.y] == p.index) owner[c.x, c.y] = -1;
            if (p.g != null) Destroy(p.g.gameObject);
            DestroyTrail(p);
            remaining--;
            if (RemainingChanged != null) RemainingChanged(remaining);
            if (remaining <= 0)
            {
                Locked = true;
                Sfx.Play(Sfx.Id.Win);
                if (Won != null) Won();
            }
        }

        // ------------------------------------------------------------------ exit (free arrow)

        IEnumerator ExitRoutine(Piece p)
        {
            int k = StepsToEdge(p.d.Head, p.d.dir) + 1;
            float back = 0.16f * cell;
            float dist = (k - 0.5f) * cell + p.bodyLen + 0.9f * cell + back;

            // pull back
            const float pullTime = 0.11f;
            float t = 0f;
            while (t < pullTime)
            {
                t += Time.unscaledDeltaTime;
                p.dist = -back * EaseOutCubic(Mathf.Clamp01(t / pullTime));
                Render(p);
                yield return null;
            }

            // launch: ease-in t^2.1, with a speed trail
            MakeTrail(p);
            float launch = 0.24f + (dist / cell) * 0.034f;
            t = 0f;
            while (t < launch)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / launch);
                p.dist = -back + (dist + back) * Mathf.Pow(u, 2.1f);
                float speed = Mathf.Pow(u, 1.1f); // velocity normalised to its maximum at u = 1
                Render(p);
                for (int g = 1; g <= p.trail.Length; g++)
                {
                    var tg = p.trail[g - 1];
                    RenderAt(p, tg, p.dist - g * cell * 0.28f * speed * 2.2f);
                    var c = p.baseColor;
                    c.a = speed > 0.001f ? 0.16f / g : 0f;
                    tg.color = c;
                }
                yield return null;
            }

            p.fx = null;
            Finish(p);
        }

        void MakeTrail(Piece p)
        {
            p.trail = new ArrowGraphic[3];
            for (int g = 0; g < p.trail.Length; g++)
            {
                var tg = NewArrow("Trail" + p.index);
                tg.thickness = p.g.thickness;
                tg.headSize = p.g.headSize;
                tg.widthFactor = p.g.widthFactor;
                tg.color = new Color(p.baseColor.r, p.baseColor.g, p.baseColor.b, 0f);
                tg.transform.SetSiblingIndex(p.rt.GetSiblingIndex()); // behind the arrow
                extras.Add(tg.gameObject);
                p.trail[g] = tg;
            }
        }

        void DestroyTrail(Piece p)
        {
            if (p.trail == null) return;
            foreach (var tg in p.trail) Kill(tg != null ? tg.gameObject : null);
            p.trail = null;
        }

        void Kill(GameObject go)
        {
            if (go == null) return;
            extras.Remove(go);
            Destroy(go);
        }

        // ------------------------------------------------------------------ wrong move (blocked arrow)

        IEnumerator WrongMoveRoutine(Piece p, Piece blocker, int k)
        {
            float adv = ((k - 1) + 0.3f) * cell;
            float slide = 0.11f + (k - 1) * 0.038f;

            float t = 0f;
            while (t < slide)
            {
                t += Time.unscaledDeltaTime;
                p.dist = adv * Mathf.Pow(Mathf.Clamp01(t / slide), 1.8f);
                Render(p);
                yield return null;
            }

            // impact
            LoseLife();
            if (p.tint != null) StopCoroutine(p.tint);
            p.tint = StartCoroutine(TintRoutine(p));
            if (blocker != null && !blocker.removed)
            {
                if (blocker.glow != null) StopCoroutine(blocker.glow);
                blocker.glow = StartCoroutine(GlowRoutine(blocker));
            }
            Vector2 contact = CenterOf(p.d.Head) + p.dirV * ((k - 0.5f) * cell);
            StartCoroutine(RingRoutine(contact));
            StartCoroutine(SparksRoutine(contact));

            // recoil spring: two small wiggles back to rest
            const float recoil = 0.62f;
            t = 0f;
            while (t < recoil)
            {
                t += Time.unscaledDeltaTime;
                float s = Mathf.Min(t, recoil);
                p.dist = adv * Mathf.Exp(-7f * s) * Mathf.Cos(2f * Mathf.PI * 2f * s);
                Render(p);
                yield return null;
            }
            p.dist = 0f;
            Render(p);
            p.busy = false;
            p.fx = null;
        }

        IEnumerator TintRoutine(Piece p)
        {
            const float dur = 0.56f;
            float t = 0f;
            while (t < dur && !p.removed)
            {
                t += Time.unscaledDeltaTime;
                p.g.color = Color.Lerp(Theme.Error, p.baseColor, EaseOutCubic(Mathf.Clamp01(t / dur)));
                yield return null;
            }
            if (!p.removed && p.g != null) p.g.color = p.baseColor;
            p.tint = null;
        }

        IEnumerator GlowRoutine(Piece b)
        {
            if (b.glowG == null)
            {
                b.glowG = NewArrow("Glow" + b.index);
                b.glowG.thickness = b.g.thickness + 0.26f * cell;
                b.glowG.headSize = b.g.headSize + 0.26f * cell;
                b.glowG.widthFactor = b.g.widthFactor;
                b.glowG.transform.SetSiblingIndex(b.rt.GetSiblingIndex()); // behind the blocker
                extras.Add(b.glowG.gameObject);
            }

            const float dur = 0.64f;
            float t = 0f;
            while (t < dur && !b.removed)
            {
                t += Time.unscaledDeltaTime;
                RenderAt(b, b.glowG, b.dist);
                var c = Theme.Error;
                c.a = 0.45f * (1f - Mathf.Clamp01(t / dur));
                b.glowG.color = c;
                yield return null;
            }
            Kill(b.glowG != null ? b.glowG.gameObject : null);
            b.glowG = null;
            b.glow = null;
        }

        IEnumerator RingRoutine(Vector2 center)
        {
            var ring = NewArrow("Ring");
            ring.drawHead = false;
            ring.transform.SetAsLastSibling();
            extras.Add(ring.gameObject);

            const int seg = 32;
            var pts = new List<Vector2>(seg + 1);
            const float dur = 0.42f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / dur);
                float r = cell * (0.18f + 0.6f * EaseOutCubic(u));
                pts.Clear();
                for (int i = 0; i <= seg; i++)
                {
                    float a = i / (float)seg * Mathf.PI * 2f;
                    pts.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
                }
                ring.thickness = Mathf.Max(0.01f, cell * 0.1f * (1f - u));
                var c = Theme.Error;
                c.a = (1f - u) * 0.8f;
                ring.color = c;
                ring.SetPoints(pts);
                yield return null;
            }
            Kill(ring.gameObject);
        }

        IEnumerator SparksRoutine(Vector2 center)
        {
            const int count = 9;
            var imgs = new Image[count];
            var vel = new Vector2[count];
            var life = new float[count];
            for (int i = 0; i < count; i++)
            {
                var img = Ui.Img(transform, "Spark", Theme.Circle, UnityEngine.Random.value < 0.5f ? Theme.Error : Theme.Gold);
                img.raycastTarget = false;
                var srt = img.rectTransform;
                srt.anchorMin = srt.anchorMax = Vector2.zero;
                srt.pivot = new Vector2(0.5f, 0.5f);
                srt.sizeDelta = Vector2.one * cell * 0.055f * 2f;
                srt.anchoredPosition = center;
                extras.Add(img.gameObject);
                imgs[i] = img;

                float a = UnityEngine.Random.value * Mathf.PI * 2f;
                vel[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * cell * UnityEngine.Random.Range(1.4f, 3.0f);
                life[i] = UnityEngine.Random.Range(0.3f, 0.48f);
            }

            float t = 0f;
            while (t < 0.48f)
            {
                t += Time.unscaledDeltaTime;
                for (int i = 0; i < count; i++)
                {
                    var c = imgs[i].color;
                    c.a = 1f - Mathf.Clamp01(t / life[i]);
                    imgs[i].color = c;
                    imgs[i].rectTransform.anchoredPosition = center + vel[i] * t;
                }
                yield return null;
            }
            foreach (var img in imgs) Kill(img.gameObject);
        }

        static float EaseOutCubic(float x)
        {
            float a = 1f - x;
            return 1f - a * a * a;
        }

        // ------------------------------------------------------------------ effects

        // ------------------------------------------------------------------ board collapse (loss)

        Coroutine collapseFx, reviveFx;

        /// <summary>
        /// Loss animation: arrows jitter, turn grey and fall off one after another (tiles and dots stay).
        /// onDone is called 1.4 s in; the last arrows keep falling a little longer. Unscaled time.
        /// </summary>
        public void PlayCollapse(Action onDone)
        {
            if (collapseFx != null) StopCoroutine(collapseFx);
            collapseFx = StartCoroutine(CollapseRoutine(onDone));
        }

        /// <summary>Every remaining arrow back to its normal position, rotation, colour and alpha.</summary>
        public void ResetCollapse()
        {
            if (collapseFx != null) StopCoroutine(collapseFx);
            if (reviveFx != null) StopCoroutine(reviveFx);
            collapseFx = reviveFx = null;
            foreach (var p in pieces)
            {
                if (p.removed || p.g == null) continue;
                if (!p.g.gameObject.activeSelf) p.g.gameObject.SetActive(true);
                Pose(p, 0f, 0f, 0f, 1f);
                p.g.color = p.baseColor;
                p.g.canvasRenderer.SetAlpha(1f);
            }
        }

        IEnumerator CollapseRoutine(Action onDone)
        {
            int n = Mathf.Max(1, pieces.Count);
            float end = FallT0 + FallSpread + FallTime;
            float t = 0f;
            bool done = false, greyDone = false;
            while (t < end || !done)
            {
                t += Time.unscaledDeltaTime;
                float jitter = t < JitterT1 ? Mathf.Clamp01(t / JitterRamp) : 0f;
                bool grey = t >= GreyT0 && !greyDone;                       // colours only change in this window
                float gk = Mathf.Clamp01((t - GreyT0) / (GreyT1 - GreyT0));

                for (int i = 0; i < pieces.Count; i++)
                {
                    var p = pieces[i];
                    if (p.removed || p.moving || p.g == null) continue;
                    float x = (t - (FallT0 + i / (float)n * FallSpread)) / FallTime;
                    if (x >= 1f)
                    {
                        if (p.g.gameObject.activeSelf) p.g.gameObject.SetActive(false);
                        continue;
                    }

                    float dx = jitter > 0f ? Mathf.Sin(t * JitterFreq + i) * JitterAmp * jitter : 0f;
                    float dy = 0f, rot = 0f, alpha = 1f;
                    if (x > 0f)
                    {
                        float g = Mathf.SmoothStep(0f, 1f, x);
                        float g2 = g * g;
                        dy = -g2 * FallDist;
                        rot = g2 * FallSpin * (i % 2 == 0 ? 1f : -1f);
                        dx += Mathf.Sin(i) * FallDrift * g2;
                        if (x > FallFadeFrom) alpha = 1f - (x - FallFadeFrom) / (1f - FallFadeFrom);
                    }
                    Pose(p, Theme.D(dx), Theme.D(dy), rot, 1f);
                    if (grey) p.g.color = Color.Lerp(p.baseColor, CollapseGrey, gk);
                    p.g.canvasRenderer.SetAlpha(alpha); // no mesh rebuild
                }
                if (t >= GreyT1) greyDone = true;

                if (!done && t >= CollapseDoneT)
                {
                    done = true;
                    if (onDone != null) onDone();
                }
                yield return null;
            }
            collapseFx = null;
        }

        // ------------------------------------------------------------------ preview helpers (Home pager)

        const float NudgeAmp = 0.15f, NudgeSpeed = 3.6f, NudgeStagger = 0.7f; // cells, rad/s, rad per arrow
        Coroutine nudgeFx;
        readonly List<Piece> nudgeList = new List<Piece>();

        /// <summary>Idle preview: the arrows that are free right now nudge forward and back (unscaled time).</summary>
        public void SetNudge(bool on)
        {
            if (nudgeFx != null) StopCoroutine(nudgeFx);
            nudgeFx = null;
            foreach (var p in nudgeList)
                if (!p.removed && !p.moving && !p.busy && p.g != null) { p.dist = 0f; Render(p); }
            nudgeList.Clear();
            if (!on || level == null || !isActiveAndEnabled) return;

            foreach (var p in pieces)
                if (!p.removed && !p.moving && !p.busy && Check(p) == State.Free) nudgeList.Add(p);
            if (nudgeList.Count > 0) nudgeFx = StartCoroutine(NudgeRoutine());
        }

        IEnumerator NudgeRoutine()
        {
            float t = 0f;
            while (true)
            {
                t += Time.unscaledDeltaTime;
                for (int i = 0; i < nudgeList.Count; i++)
                {
                    var p = nudgeList[i];
                    if (p.removed || p.g == null) continue;
                    p.dist = Mathf.Max(0f, Mathf.Sin(t * NudgeSpeed - i * NudgeStagger)) * NudgeAmp * cell;
                    Render(p);
                }
                yield return null;
            }
        }

        /// <summary>Preview of a locked mode: every arrow in the collapse grey (or back to its own colour).</summary>
        public void SetGrey(bool on)
        {
            foreach (var p in pieces)
                if (!p.removed && p.g != null) p.g.color = on ? CollapseGrey : p.baseColor;
        }

        /// <summary>Revive: arrows scale in from 0.6 to 1 around their own centres.</summary>
        IEnumerator ReviveRoutine()
        {
            float t = 0f;
            while (t < ReviveScaleTime)
            {
                t += Time.unscaledDeltaTime;
                float s = Mathf.Lerp(ReviveScaleFrom, 1f, EaseOutCubic(Mathf.Clamp01(t / ReviveScaleTime)));
                foreach (var p in pieces) if (!p.removed && p.g != null) Pose(p, 0f, 0f, 0f, s);
                yield return null;
            }
            foreach (var p in pieces) if (!p.removed && p.g != null) Pose(p, 0f, 0f, 0f, 1f);
            reviveFx = null;
        }

        /// <summary>
        /// Moves / rotates / scales an arrow around its own centre. Its RectTransform spans the board with the
        /// pivot at the board origin, so the offset c - R*s*c keeps the centre in place.
        /// </summary>
        static void Pose(Piece p, float offX, float offY, float rotRad, float scale)
        {
            float cs = Mathf.Cos(rotRad), sn = Mathf.Sin(rotRad);
            Vector2 c = p.center;
            Vector2 rc = new Vector2(c.x * cs - c.y * sn, c.x * sn + c.y * cs) * scale;
            p.rt.anchoredPosition = c - rc + new Vector2(offX, offY);
            p.rt.localEulerAngles = new Vector3(0f, 0f, rotRad * Mathf.Rad2Deg);
            p.rt.localScale = new Vector3(scale, scale, 1f);
        }

        IEnumerator HintRoutine(Piece p)
        {
            float t = 0f;
            while (t < 1.8f && !p.removed && !p.moving)
            {
                t += Time.deltaTime;
                float k = (Mathf.Sin(t * 10f) + 1f) * 0.5f;
                p.g.color = Color.Lerp(p.baseColor, Theme.Hint, k);
                yield return null;
            }
            if (!p.removed && p.g != null) p.g.color = p.baseColor;
            hintFx = null;
        }

        // ------------------------------------------------------------------ public actions

        /// <summary>
        /// Highlights an arrow that is safe to tap. Returns false (and shows nothing) if a hint is still playing or
        /// no arrow is free right now, so the caller only takes a hint when one was really shown.
        /// </summary>
        public bool ShowHint()
        {
            if (Locked || level == null || hintFx != null) return false;
            var free = new List<Piece>();
            foreach (var p in pieces)
                if (!p.removed && !p.moving && !p.busy && Check(p) == State.Free) free.Add(p);
            if (free.Count == 0) return false;
            var pick = free[UnityEngine.Random.Range(0, free.Count)];
            hintFx = StartCoroutine(HintRoutine(pick));
            Sfx.Play(Sfx.Id.Hint);
            return true;
        }

        /// <summary>Gives one life back after the lose popup (e.g. after a rewarded ad).</summary>
        public void ReviveLife()
        {
            ResetCollapse();
            if (pieces.Count > 0)
            {
                foreach (var p in pieces) if (!p.removed && p.g != null) Pose(p, 0f, 0f, 0f, ReviveScaleFrom);
                reviveFx = StartCoroutine(ReviveRoutine());
            }
            lives = 1;
            Locked = false;
            if (LivesChanged != null) LivesChanged(lives);
        }
    }
}
