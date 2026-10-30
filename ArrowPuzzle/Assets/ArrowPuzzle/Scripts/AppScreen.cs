using UnityEngine;

namespace ArrowPuzzle
{
    /// <summary>
    /// Base class for every screen and popup. To add one: create a new file with a class that
    /// derives from AppScreen and fill Build(). GameApp finds it automatically (no registration).
    /// Show it with GameApp.I.Show&lt;ScreenHome&gt;()  /  popups with GameApp.I.Open&lt;PopupPause&gt;().
    /// </summary>
    public abstract class AppScreen
    {
        public RectTransform Root { get; private set; }
        protected GameApp App { get { return GameApp.I; } }

        /// <summary>Popups are drawn above everything on a dimmed backdrop and don't hide the screen under them.</summary>
        public virtual bool IsPopup { get { return false; } }

        /// <summary>Screens normally live inside the safe area; set false to cover the whole canvas.</summary>
        public virtual bool UseSafeArea { get { return true; } }

        public void Init(RectTransform parent)
        {
            Root = Ui.New(parent, GetType().Name);
            Ui.Fill(Root);
            Build();
        }

        /// <summary>Create all UI under Root here (called once).</summary>
        protected abstract void Build();

        /// <summary>Called every time the screen becomes visible.</summary>
        public virtual void OnShow() { }

        /// <summary>Called when the screen is hidden.</summary>
        public virtual void OnHide() { }

        /// <summary>Android back button / Escape. Return true if handled.</summary>
        public virtual bool OnBack() { return false; }
    }
}
