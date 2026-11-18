using UnityEngine;

namespace ArrowPuzzle
{
    /// <summary>
    /// A small, non-interactive picture of a long-arrow level (Themes cards, Shapes gallery) drawn with the long-arrow
    /// mesh code (TileGraphic + StrokeGraphic with a fixed style) in an explicit theme, without touching the global one.
    /// The meshes are built once by the canvas; destroy the returned object to free them.
    /// </summary>
    public static class LevelPreview
    {
        /// <summary>
        /// Builds the preview centred in parent, fitted into sizeD (design px). faded = locked look: grey lines and tiles,
        /// no glow. Returns the preview root (destroy it to free the meshes).
        /// </summary>
        public static RectTransform Create(RectTransform parent, LevelData lv, ThemeData theme, Vector2 sizeD, bool faded)
        {
            var root = Ui.New(parent, "Preview");
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = sizeD * Theme.K;
            if (lv == null || theme == null || lv.cols <= 0 || lv.rows <= 0) return root;

            float cell = Mathf.Min(root.sizeDelta.x / lv.cols, root.sizeDelta.y / lv.rows);
            var board = Ui.New(root, "Board");
            board.anchorMin = board.anchorMax = board.pivot = new Vector2(0.5f, 0.5f);
            board.sizeDelta = new Vector2(cell * lv.cols, cell * lv.rows);

            Color tileC = theme.Tile, lineFade = Mix(theme.Card, theme.Ink, 0.28f);
            if (faded) tileC = Mix(theme.Card, theme.Tile, 0.5f);

            if (lv.mask != null)
            {
                var tg = Layer(board, "Tiles").gameObject.AddComponent<TileGraphic>();
                tg.raycastTarget = false;
                tg.color = tileC;
                tg.Set(lv.mask, lv.cols, lv.rows, cell);
            }

            var sg = Layer(board, "Arrows").gameObject.AddComponent<StrokeGraphic>();
            sg.raycastTarget = false;
            float line = Mathf.Max(cell * LongStyle.LineCells, Theme.D(0.9f)); // same shape as the game board
            float feather = 1f / Mathf.Max(0.1f, Screen.width / 1080f); // 1 screen px in canvas units (canvas = 1080 wide)
            sg.fixedStyle = (mul, head) => Style(cell, line, feather, mul, head);

            int n = lv.pieces.Count;
            var pts = new System.Collections.Generic.List<Vector2>[n];
            var cols = new Color[n];
            for (int i = 0; i < n; i++)
            {
                var cells = lv.pieces[i].cells;
                var p = new System.Collections.Generic.List<Vector2>(cells.Count);
                Vector2 c = Vector2.zero;
                foreach (var cc in cells)
                {
                    var v = new Vector2((cc.x + 0.5f) * cell, (cc.y + 0.5f) * cell);
                    p.Add(v);
                    c += v;
                }
                c /= Mathf.Max(1, cells.Count);
                pts[i] = p;
                cols[i] = faded ? lineFade : Themes.ArrowColor(theme, lv.number, i, c.x / (lv.cols * cell), 1f - c.y / (lv.rows * cell));
            }
            if (theme.Glow && !faded)
                for (int i = 0; i < n; i++)
                {
                    var g = cols[i];
                    g.a *= LongStyle.GlowAlpha;
                    sg.items.Add(new StrokeGraphic.Item { pts = pts[i], col = g, mul = LongStyle.GlowMul, head = true });
                }
            for (int i = 0; i < n; i++)
                sg.items.Add(new StrokeGraphic.Item { pts = pts[i], col = cols[i], mul = 1f, head = true });
            sg.Dirty();
            return root;
        }

        static StrokeStyle Style(float cell, float line, float feather, float mul, bool head)
        {
            return StrokeStyle.Make(cell, line, feather, mul, head);
        }

        static RectTransform Layer(Transform parent, string name)
        {
            var r = Ui.New(parent, name);
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
            r.pivot = Vector2.zero;
            return r;
        }

        static Color Mix(Color a, Color b, float t) { var c = Color.Lerp(a, b, t); c.a = 1f; return c; }
    }
}
