using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace ArrowPuzzle
{
    /// <summary>
    /// The "Set app icon" menu item: builds the Android adaptive icon pair and the Play Store icon from app_icon.png,
    /// sets every icon slot and the Android identity. Safe to re-run after the icon changes.
    /// </summary>
    public static class AppIconEditorTools
    {
        const string IconPath = "Assets/ArrowPuzzle/Icon/app_icon.png";
        const string BgPath = "Assets/ArrowPuzzle/Icon/icon_bg.png";
        const string FgPath = "Assets/ArrowPuzzle/Icon/icon_fg.png";
        const string StoreFolder = "PlayStore";     // next to Assets: not shipped in the build
        const string StoreFile = "icon_512.png";

        const int AdaptiveSize = 432, StoreSize = 512, MaxSize = 1024;
        const float ArtScale = 0.62f;               // foreground art size as a share of the 432 canvas: inside the adaptive safe zone
        const float WhiteCutoff = 0.94f;            // an edge colour this light (every channel) counts as a white background

        const string ProductName = "Arrow Puzzle";
        const string PackageName = "arrowpuzzle.puzzlegame.arrowsescape";
        const string Version = "1.0";
        const int VersionCode = 1;

        [MenuItem("ArrowPuzzle/Set app icon")]
        static void SetAppIcon()
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath) == null)
            {
                Debug.LogError("Set app icon: " + IconPath + " not found.");
                return;
            }

            string storePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", StoreFolder, StoreFile));
            Color[] px;
            int w, h;
            ConfigureImport(IconPath, true);
            try
            {
                var src = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
                px = src.GetPixels();
                w = src.width;
                h = src.height;
            }
            finally
            {
                ConfigureImport(IconPath, false);
            }
            if (w != h) Debug.LogError("Set app icon: app_icon.png is " + w + "x" + h + ", it should be square.");

            // background: the colour of the icon's edge pixels, white when the icon has a white (or transparent) background
            Color bgColor = EdgeColor(px, w, h, out bool whiteBg);
            var bg = new Color[AdaptiveSize * AdaptiveSize];
            for (int i = 0; i < bg.Length; i++) bg[i] = bgColor;
            WritePng(BgPath, bg, AdaptiveSize);

            // foreground: transparent 432x432 canvas, the whole icon at 62% centred, so launcher masks never cut the artwork
            int artSize = Mathf.RoundToInt(AdaptiveSize * ArtScale);
            var fg = new Color[AdaptiveSize * AdaptiveSize]; // Color.clear everywhere
            var art = Downscale(px, w, h, artSize);
            int off = (AdaptiveSize - artSize) / 2;
            for (int y = 0; y < artSize; y++)
                for (int x = 0; x < artSize; x++)
                    fg[(y + off) * AdaptiveSize + x + off] = art[y * artSize + x];
            WritePng(FgPath, fg, AdaptiveSize);

            // Play Store: the full icon edge to edge (no safe-zone shrink), only resampled to 512
            Directory.CreateDirectory(Path.GetDirectoryName(storePath));
            WritePng(storePath, w == StoreSize && h == StoreSize ? px : Downscale(px, w, h, StoreSize), StoreSize);

            AssetDatabase.ImportAsset(BgPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(FgPath, ImportAssetOptions.ForceSynchronousImport);
            ConfigureImport(BgPath, false);
            ConfigureImport(FgPath, false);

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            var bgTex = AssetDatabase.LoadAssetAtPath<Texture2D>(BgPath);
            var fgTex = AssetDatabase.LoadAssetAtPath<Texture2D>(FgPath);

            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            // layer 0 background, layer 1 foreground. Round (API 25) and Legacy are obsolete in Unity 6.6 (min SDK 26): adaptive only
            SetAndroidIcons(AndroidPlatformIconKind.Adaptive, bgTex, fgTex);

            SetIdentity();
            AssetDatabase.SaveAssets();
            Report(storePath, bgColor, whiteBg, artSize, off, w, h);
        }

        // ------------------------------------------------------------------ icons

        /// <summary>Most common colour among the border pixels; white when that colour is near white or mostly transparent.</summary>
        static Color EdgeColor(Color[] px, int w, int h, out bool white)
        {
            var counts = new System.Collections.Generic.Dictionary<Color32, int>();
            Color32 best = default;
            int bestN = 0;
            for (int i = 0; i < 2 * (w + h); i++)
            {
                int x, y;
                if (i < w) { x = i; y = 0; }
                else if (i < 2 * w) { x = i - w; y = h - 1; }
                else if (i < 2 * w + h) { x = 0; y = i - 2 * w; }
                else { x = w - 1; y = i - 2 * w - h; }
                Color32 c = px[y * w + x];
                counts.TryGetValue(c, out int n);
                counts[c] = ++n;
                if (n > bestN) { bestN = n; best = c; }
            }
            Color col = best;
            white = col.a < 0.5f || (col.r >= WhiteCutoff && col.g >= WhiteCutoff && col.b >= WhiteCutoff);
            if (white) return Color.white;
            col.a = 1f;
            return col;
        }

        static void ConfigureImport(string path, bool readable)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;
            imp.textureType = TextureImporterType.Default;
            imp.maxTextureSize = MaxSize;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.npotScale = TextureImporterNPOTScale.None; // keep 432x432 as drawn
            imp.isReadable = readable;
            imp.SaveAndReimport();
        }

        static void SetAndroidIcons(PlatformIconKind kind, params Texture2D[] layers)
        {
            var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
            foreach (var icon in icons) icon.SetTextures(layers);
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
        }

        static void WritePng(string path, Color[] px, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        /// <summary>Area-average resize to size x size, alpha weighted so transparent pixels do not darken edges.</summary>
        static Color[] Downscale(Color[] src, int sw, int sh, int size)
        {
            var dst = new Color[size * size];
            float sx = (float)sw / size, sy = (float)sh / size;
            for (int dy = 0; dy < size; dy++)
            {
                float y0 = dy * sy, y1 = y0 + sy;
                for (int dx = 0; dx < size; dx++)
                {
                    float x0 = dx * sx, x1 = x0 + sx;
                    float r = 0f, g = 0f, b = 0f, a = 0f, area = 0f;
                    for (int iy = (int)y0; iy < Mathf.Min(Mathf.CeilToInt(y1), sh); iy++)
                    {
                        float wy = Mathf.Min(iy + 1, y1) - Mathf.Max(iy, y0);
                        for (int ix = (int)x0; ix < Mathf.Min(Mathf.CeilToInt(x1), sw); ix++)
                        {
                            float wgt = wy * (Mathf.Min(ix + 1, x1) - Mathf.Max(ix, x0));
                            var c = src[iy * sw + ix];
                            r += c.r * c.a * wgt;
                            g += c.g * c.a * wgt;
                            b += c.b * c.a * wgt;
                            a += c.a * wgt;
                            area += wgt;
                        }
                    }
                    dst[dy * size + dx] = a > 0f ? new Color(r / a, g / a, b / a, a / area) : Color.clear;
                }
            }
            return dst;
        }

        // ------------------------------------------------------------------ identity

        static void SetIdentity()
        {
            // "Override Default Package Name" has no public setter: set it on the PlayerSettings asset
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settings.Length > 0)
            {
                var so = new SerializedObject(settings[0]);
                var overrideId = so.FindProperty("overrideDefaultApplicationIdentifier");
                if (overrideId != null)
                {
                    overrideId.boolValue = true;
                    so.ApplyModifiedProperties();
                }
            }

            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageName);
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.Android.bundleVersionCode = VersionCode;
        }

        static bool OverrideOn()
        {
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settings.Length == 0) return false;
            var p = new SerializedObject(settings[0]).FindProperty("overrideDefaultApplicationIdentifier");
            return p != null && p.boolValue;
        }

        static void Report(string storePath, Color bgColor, bool whiteBg, int artSize, int off, int w, int h)
        {
            var adaptive = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Adaptive);
            var defaults = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
            string company = PlayerSettings.companyName;

            Debug.Log("Set app icon: done"
                      + "\n  Default Icon: " + (defaults.Length > 0 && defaults[0] != null ? defaults[0].name : "none") + " (" + w + "x" + h + ", full icon)"
                      + "\n  Android Adaptive: " + Slots(adaptive)
                      + "\n    background icon_bg: " + AdaptiveSize + "x" + AdaptiveSize + " #" + ColorUtility.ToHtmlStringRGB(bgColor) + (whiteBg ? " (white background)" : " (edge colour)")
                      + "\n    foreground icon_fg: " + AdaptiveSize + "x" + AdaptiveSize + " transparent, art " + artSize + "x" + artSize + " (" + Mathf.RoundToInt(ArtScale * 100) + "%) at offset " + off
                      + "\n  Company Name: " + company + (company == "DefaultCompany" ? "  (still the Unity default: set it by hand)" : "")
                      + "\n  Product Name: " + PlayerSettings.productName
                      + "\n  Package Name: " + PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android) + ", override " + (OverrideOn() ? "ON" : "OFF")
                      + "\n  Version: " + PlayerSettings.bundleVersion + ", Bundle Version Code " + PlayerSettings.Android.bundleVersionCode
                      + "\n  Play Store icon: " + storePath + ", " + StoreSize + "x" + StoreSize + " full icon, " + (new FileInfo(storePath).Length / 1024f).ToString("0.0") + " KB");
        }

        /// <summary>"n of n slots set".</summary>
        static string Slots(PlatformIcon[] icons)
        {
            int set = 0;
            foreach (var icon in icons)
            {
                var t = icon.GetTextures();
                bool full = t.Length > 0;
                for (int i = 0; i < Mathf.Min(t.Length, icon.minLayerCount > 0 ? icon.minLayerCount : t.Length); i++) full &= t[i] != null;
                if (full) set++;
            }
            return set + " of " + icons.Length + " slots set";
        }
    }
}
