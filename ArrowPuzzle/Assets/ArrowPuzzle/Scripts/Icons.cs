using System.Collections.Generic;
using UnityEngine;

namespace ArrowPuzzle
{
    public enum Icon { Bulb, Star, Heart, Gear, Back, Pause, Restart, Home, Play, Sound, Vibrate, Lock, ArrowRight, Share, Shield, Chevron, Music }

    /// <summary>
    /// Icons drawn in code (stroke polylines or generated sprites), copied from the design's 24x24 icon set.
    /// Make() returns a square RectTransform of sizeD design px; keep that size when placing it.
    /// </summary>
    public static class Icons
    {
        public static RectTransform Make(Transform parent, Icon icon, Color color, float sizeD, float stroke = 2.2f)
        {
            var rt = Ui.New(parent, "Icon_" + icon);
            float s = sizeD * Theme.K;
            rt.sizeDelta = new Vector2(s, s);

            switch (icon)
            {
                case Icon.Star: FillSprite(rt, Theme.Star, color); break;
                case Icon.Heart: FillSprite(rt, Theme.Heart, color); break;
                case Icon.Play: FillSprite(rt, Theme.Triangle, color); break;

                case Icon.Pause:
                    Rect(rt, 5, 4, 5, 16, 2, color, sizeD);
                    Rect(rt, 14, 4, 5, 16, 2, color, sizeD);
                    break;

                case Icon.Back:
                    Stroke(rt, color, 2.6f, s, P(15, 5), P(8, 12), P(15, 19));
                    break;

                case Icon.ArrowRight:
                    Stroke(rt, color, 3f, s, P(5, 12), P(19, 12));
                    Stroke(rt, color, 3f, s, P(13, 6), P(19, 12), P(13, 18));
                    break;

                case Icon.Home:
                    Stroke(rt, color, stroke, s, P(4, 11), P(12, 4), P(20, 11));
                    Stroke(rt, color, stroke, s, P(6, 10), P(6, 20), P(18, 20), P(18, 10));
                    break;

                case Icon.Restart:
                    Stroke(rt, color, stroke, s, Arc(12, 12, 8, 0f, -312.5f, 26));
                    Stroke(rt, color, stroke, s, P(20, 4), P(20, 9), P(15, 9));
                    break;

                case Icon.Bulb:
                {
                    Stroke(rt, color, 2f, s, P(9, 18), P(15, 18));
                    Stroke(rt, color, 2f, s, P(10, 21), P(14, 21));
                    var body = new List<Vector2>();
                    body.AddRange(Arc(12, 9, 6, 90f, 234.5f, 14));
                    body.Add(P(9.5f, 16f));
                    body.Add(P(14.5f, 16f));
                    body.AddRange(Arc(12, 9, 6, -54.5f, 90f, 14));
                    Stroke(rt, color, 2f, s, body);
                    break;
                }

                case Icon.Gear:
                    Stroke(rt, color, stroke, s, Arc(12, 12, 3.2f, 0f, 360f, 20));
                    Stroke(rt, color, stroke, s, Arc(12, 12, 7.2f, 0f, 360f, 28));
                    Stroke(rt, color, stroke, s, P(12, 2), P(12, 4.6f));
                    Stroke(rt, color, stroke, s, P(12, 19.4f), P(12, 22));
                    Stroke(rt, color, stroke, s, P(2, 12), P(4.6f, 12));
                    Stroke(rt, color, stroke, s, P(19.4f, 12), P(22, 12));
                    Stroke(rt, color, stroke, s, P(4.9f, 4.9f), P(6.7f, 6.7f));
                    Stroke(rt, color, stroke, s, P(17.3f, 17.3f), P(19.1f, 19.1f));
                    Stroke(rt, color, stroke, s, P(4.9f, 19.1f), P(6.7f, 17.3f));
                    Stroke(rt, color, stroke, s, P(17.3f, 6.7f), P(19.1f, 4.9f));
                    break;

                case Icon.Sound:
                    Stroke(rt, color, stroke, s, P(4, 9.5f), P(4, 14.5f), P(7.5f, 14.5f), P(12, 18.5f), P(12, 5.5f), P(7.5f, 9.5f), P(4, 9.5f));
                    Stroke(rt, color, stroke, s, Arc(12.85f, 12, 4, 48.6f, -48.6f, 10));
                    Stroke(rt, color, stroke, s, Arc(12.19f, 12, 8, 43.4f, -43.4f, 14));
                    break;

                case Icon.Vibrate:
                    Stroke(rt, color, stroke, s, RoundRect(8, 3, 8, 18, 2.5f));
                    Stroke(rt, color, stroke, s, P(4, 8), P(4, 16));
                    Stroke(rt, color, stroke, s, P(20, 8), P(20, 16));
                    break;

                case Icon.Lock:
                {
                    Rect(rt, 5, 11, 14, 10, 3, color, sizeD);
                    var sh = new List<Vector2> { P(8, 11), P(8, 8) };
                    sh.AddRange(Arc(12, 8, 4, 180f, 0f, 10));
                    sh.Add(P(16, 11));
                    Stroke(rt, color, 2.4f, s, sh);
                    break;
                }

                case Icon.Share:
                    Stroke(rt, color, 2f, s, P(18, 5), P(6, 12), P(18, 19));
                    Rect(rt, 15, 2, 6, 6, 3, color, sizeD);
                    Rect(rt, 3, 9, 6, 6, 3, color, sizeD);
                    Rect(rt, 15, 16, 6, 6, 3, color, sizeD);
                    break;

                case Icon.Shield:
                    Stroke(rt, color, stroke, s, P(12, 3), P(19, 6), P(19, 11), P(18.7f, 13.8f), P(17.6f, 16.4f), P(15.4f, 18.8f),
                           P(12, 21), P(8.6f, 18.8f), P(6.4f, 16.4f), P(5.3f, 13.8f), P(5, 11), P(5, 6), P(12, 3));
                    Stroke(rt, color, 2.4f, s, P(9, 12.2f), P(11.2f, 14.4f), P(15.2f, 10));
                    break;

                case Icon.Chevron:
                    Stroke(rt, color, 2.6f, s, P(9, 5), P(16, 12), P(9, 19));
                    break;

                case Icon.Music:
                    Stroke(rt, color, 2.2f, s, P(9.5f, 18), P(9.5f, 6), P(19.5f, 4), P(19.5f, 16));
                    Rect(rt, 4, 15, 6.5f, 6, 3, color, sizeD);
                    Rect(rt, 14, 13, 6.5f, 6, 3, color, sizeD);
                    break;
            }
            return rt;
        }

        // ------------------------------------------------------------------ helpers

        static Vector2 P(float x, float y) { return new Vector2(x, y); }

        /// <summary>Arc in svg space (y down). Angles in degrees, counter-clockwise on screen, 0 = right.</summary>
        static List<Vector2> Arc(float cx, float cy, float r, float a0, float a1, int steps)
        {
            var l = new List<Vector2>();
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)steps) * Mathf.Deg2Rad;
                l.Add(new Vector2(cx + r * Mathf.Cos(a), cy - r * Mathf.Sin(a)));
            }
            return l;
        }

        static List<Vector2> RoundRect(float x, float y, float w, float h, float r)
        {
            var l = new List<Vector2>();
            l.AddRange(Arc(x + r, y + r, r, 180f, 90f, 5));
            l.AddRange(Arc(x + w - r, y + r, r, 90f, 0f, 5));
            l.AddRange(Arc(x + w - r, y + h - r, r, 0f, -90f, 5));
            l.AddRange(Arc(x + r, y + h - r, r, -90f, -180f, 5));
            l.Add(l[0]);
            return l;
        }

        static void Stroke(RectTransform root, Color color, float widthSvg, float size, params Vector2[] pts)
        {
            Stroke(root, color, widthSvg, size, new List<Vector2>(pts));
        }

        static void Stroke(RectTransform root, Color color, float widthSvg, float size, List<Vector2> pts)
        {
            var rt = Ui.New(root, "stroke");
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            var g = rt.gameObject.AddComponent<ArrowGraphic>();
            g.raycastTarget = false;
            g.drawHead = false;
            g.color = color;
            g.thickness = widthSvg / 24f * size;

            var local = new List<Vector2>(pts.Count);
            foreach (var p in pts) local.Add(new Vector2((p.x / 24f - 0.5f) * size, (0.5f - p.y / 24f) * size));
            g.SetPoints(local);
        }

        static void FillSprite(RectTransform root, Sprite sprite, Color color)
        {
            var img = Ui.Img(root, "fill", sprite, color);
            img.raycastTarget = false;
            Ui.Fill(img.rectTransform);
        }

        static void Rect(RectTransform root, float x, float y, float w, float h, float r, Color color, float sizeD)
        {
            float u = sizeD / 24f; // design px per svg unit
            var img = Ui.Img(root, "rect", Theme.Rounded, color, Mathf.Max(0.5f, r * u));
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w * u * Theme.K, h * u * Theme.K);
            rt.anchoredPosition = new Vector2(((x + w * 0.5f) / 24f - 0.5f) * sizeD * Theme.K, (0.5f - (y + h * 0.5f) / 24f) * sizeD * Theme.K);
        }
    }
}
