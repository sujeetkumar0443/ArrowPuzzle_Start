using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArrowPuzzle
{
    /// <summary>
    /// THEMES: header (back, title, live coin pill), subtitle, a scrolling 2-column grid of the six theme cards. Each card
    /// shows a heart board drawn in that theme (gradient background, its Lines, Zones colours, Night glow), the name and
    /// one button: IN USE (teal, check, teal ring) / USE (Panel) / price (gold, coin) / WATCH AD (purple, play). Buying
    /// or watching unlocks and applies the theme; applying rebuilds every screen in the new colours.
    /// Open with ScreenThemes.Open(); back returns to the screen that opened it.
    /// </summary>
    public class ScreenThemes : AppScreen
    {
        const float CardW = 169f, CardH = 262f, Gap = 12f, CardR = 22f, EdgeD = 5f, Pad = 10f;
        const float PreviewH = 150f, PreviewR = 16f, NameH = 26f, BtnH = 44f, RingW = 3f;
        const int PreviewShape = 0; // the heart's thumbnail
        static readonly Color InUse = Theme.Hex("#2EC4B6"), InUseEdge = Theme.Hex("#1F8E84");
        static readonly Color GoldPill = Theme.Hex("#FFB703"), GoldEdge = Theme.Hex("#C98410"), GoldInk = Theme.Hex("#3A2600");
        static readonly Color AdPurple = Theme.Hex("#9B5DE5"), AdPurpleEdge = Theme.Hex("#6E3BB8");

        static string pendingToast; // shown by the rebuilt screen after a theme switch

        ScreenKit.CoinPill coins;
        ScreenKit.Toast toast;
        ScrollRect scroll;
        RectTransform view;
        readonly List<RectTransform> previewHolders = new List<RectTransform>();
        readonly List<GameObject> previews = new List<GameObject>();
        int adRequest;

        /// <summary>Entry point (Home in Step 8, the dev list for now).</summary>
        public static void Open() { GameApp.I.Show("ScreenThemes"); }

        protected override void Build()
        {
            ScreenKit.Background(Root);
            BannerPlaceholder.Create(Root);
            coins = ScreenKit.Header(Root, "THEMES", "Pick the look of your puzzles", () => ScreenKit.GoBack("ScreenThemes"));

            RectTransform content;
            scroll = ScreenKit.Scroll(Root, out view, out content);
            ScreenKit.Grid(content, 2, new Vector2(CardW, CardH), Gap, 6f, 16f);
            foreach (var t in Themes.All) BuildCard(content, t);

            coins.Rt.SetAsLastSibling();
            toast = new ScreenKit.Toast(Root);
        }

        void BuildCard(RectTransform content, ThemeData t)
        {
            bool owned = Save.IsThemeOwned(t.Id);
            bool inUse = Theme.Current != null && Theme.Current.Id == t.Id;

            var slot = Ui.New(content, "Theme_" + t.Id);
            if (inUse) ScreenKit.Ring(slot, InUse, CardR, RingW);
            var card = ScreenKit.Card(slot, CardR, EdgeD);
            card.raycastTarget = false;
            var crt = card.rectTransform;

            // preview frame: the theme's background (gradient sprite, or solid) in a rounded mask
            var frame = ScreenKit.Rounded(crt, "Frame", Color.white, PreviewR);
            frame.raycastTarget = false;
            Ui.PlaceD(frame.rectTransform, Ui.TC, new Vector2(0.5f, 1f), new Vector2(0f, -Pad), new Vector2(CardW - Pad * 2f, PreviewH));
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var bgSprite = Theme.BgSprite(t);
            var bg = Ui.Img(frame.rectTransform, "Bg", bgSprite, bgSprite != null ? Color.white : t.BgTop);
            bg.raycastTarget = false;
            Ui.Fill(bg.rectTransform);
            var holder = Ui.New(frame.rectTransform, "Holder");
            Ui.Fill(holder);
            previewHolders.Add(holder);

            if (!owned)
            {
                var badge = Ui.Img(frame.rectTransform, "Lock", Theme.CircleHD, Theme.Panel);
                badge.raycastTarget = false;
                Ui.PlaceD(badge.rectTransform, Ui.TR, new Vector2(1f, 1f), new Vector2(-8f, -8f), new Vector2(30f, 30f));
                var li = Icons.Make(badge.rectTransform, Icon.Lock, Theme.PanelText, 16f);
                Ui.PlaceD(li, Ui.C, Vector2.zero, new Vector2(16f, 16f));
            }

            var name = Ui.Label(crt, t.Name, 20f, Theme.Ink, Weight.Bold);
            Ui.PlaceD(name.rectTransform, Ui.TC, new Vector2(0.5f, 1f), new Vector2(0f, -(Pad + PreviewH + 6f)), new Vector2(CardW - Pad * 2f, NameH));

            // the button
            string id = t.Id;
            Color face, edge, ink;
            string label;
            Icon? icon = null;
            System.Action act;
            if (inUse) { face = InUse; edge = InUseEdge; ink = Theme.OnColor; label = "IN USE"; icon = Icon.Check; act = null; }
            else if (owned)
            {
                // Panel pill; on dark themes Panel is close to the card colour, so it is lifted towards the ink a little
                bool dark = Theme.Current != null && Theme.Current.Dark;
                face = dark ? Color.Lerp(Theme.Panel, Theme.Ink, 0.18f) : Theme.Panel;
                edge = Theme.PanelEdge; ink = Theme.PanelText; label = "USE"; act = () => Use(id);
            }
            else if (t.UnlockByAd) { face = AdPurple; edge = AdPurpleEdge; ink = Theme.OnColor; label = "WATCH AD"; icon = Icon.Play; act = () => WatchAd(id); }
            else { face = GoldPill; edge = GoldEdge; ink = GoldInk; label = t.PriceCoins.ToString(); act = () => Buy(id); }

            var btn = Ui.Btn(crt, label, face, ink, 18f, BtnH * 0.5f, edge, 3f, act ?? (() => { }), icon, 18f);
            Ui.PlaceD((RectTransform)btn.transform, Ui.BC, new Vector2(0.5f, 0f), new Vector2(0f, Pad + 3f), new Vector2(CardW - Pad * 2f, BtnH));
            if (!owned && !t.UnlockByAd)
            {
                // coin icon before the price
                var c = ScreenKit.Coin(btn.transform, 22f);
                c.SetAsFirstSibling();
                c.gameObject.AddComponent<LayoutElement>().preferredWidth = Theme.D(22f);
                c.GetComponent<LayoutElement>().preferredHeight = Theme.D(22f);
            }
            if (inUse) btn.interactable = false;
        }

        // ------------------------------------------------------------------ actions

        void Use(string id)
        {
            Save.ThemeId = id;
            AnalyticsService.ThemeUse(id);
            Theme.Apply(id); // rebuilds every screen (this one included) in the new colours
        }

        void Buy(string id)
        {
            var t = Themes.Get(id);
            if (!Save.SpendCoins(t.PriceCoins, "theme " + id))
            {
                coins.Shake();
                toast.Show("Not enough coins", ToastY());
                return;
            }
            Save.UnlockTheme(id);
            AnalyticsService.ThemeUnlock(id, "coins");
            pendingToast = t.Name + " unlocked!";
            Use(id);
        }

        void WatchAd(string id)
        {
            if (Ads.IsRewardedBusy) { toast.Show("Ad not ready, try again", ToastY()); return; }
            int req = ++adRequest;
            Ads.ShowRewarded(() =>
            {
                // the reward is kept even if this screen is gone; it is applied only while the screen is still showing
                Save.UnlockTheme(id);
                AnalyticsService.ThemeUnlock(id, "ad");
                if (req != adRequest || Root == null || !Root.gameObject.activeInHierarchy) return;
                pendingToast = Themes.Get(id).Name + " unlocked!";
                Use(id);
            }, () =>
            {
                if (req == adRequest && Root != null && Root.gameObject.activeInHierarchy) toast.Show("Ad not ready, try again", ToastY());
            });
        }

        static float ToastY() { return ScreenKit.BottomD() + 14f; }

        // ------------------------------------------------------------------ show / hide

        public override void OnShow()
        {
            Save.CoinsChanged -= OnCoins;
            Save.CoinsChanged += OnCoins;
            Ads.BannerHeightChanged -= Layout;
            Ads.BannerHeightChanged += Layout;
            Ads.Banner(true, "ScreenThemes");
            coins.Refresh();
            Layout();

            // previews: drawn once per visit, freed on hide
            var heart = Campaign.Thumb(PreviewShape);
            for (int i = 0; i < previewHolders.Count && i < Themes.All.Length; i++)
            {
                var h = previewHolders[i];
                var size = new Vector2(CardW - Pad * 2f - 16f, PreviewH - 16f);
                previews.Add(LevelPreview.Create(h, heart, Themes.All[i], size, false).gameObject);
            }

            if (pendingToast != null)
            {
                toast.Show(pendingToast, ToastY());
                pendingToast = null;
            }
        }

        public override void OnHide()
        {
            Save.CoinsChanged -= OnCoins;
            Ads.BannerHeightChanged -= Layout;
            Ads.Banner(false, "ScreenThemes");
            adRequest++;
            toast.Hide();
            foreach (var p in previews) if (p != null) Object.Destroy(p);
            previews.Clear();
        }

        public override bool OnBack()
        {
            ScreenKit.GoBack("ScreenThemes");
            return true;
        }

        void OnCoins() { coins.Refresh(); }

        void Layout() { ScreenKit.FitView(view, scroll, 0f); }
    }
}
