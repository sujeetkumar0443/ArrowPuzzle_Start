using System;
using UnityEngine;

namespace ArrowPuzzle
{
    /// <summary>
    /// Store / share links and the actions behind the Settings "MORE" rows. Every action tells Ads first, so
    /// the trip to the other app doesn't bring an app open ad on the way back. In the Editor they only log.
    /// </summary>
    public static class AppLinks
    {
        public static string PlayStoreWebUrl { get { return "https://play.google.com/store/apps/details?id=" + Application.identifier; } }
        public static string PlayStoreMarketUrl { get { return "market://details?id=" + Application.identifier; } }

        public static string ShareMessage
        {
            get { return "Try Arrow Puzzle! Tap the arrows, slide them out and solve every picture. " + PlayStoreWebUrl; }
        }

        const float AppOpenSkipSeconds = 5f; // the player is just coming back from another app

        /// <summary>Android share sheet with ShareMessage (plain text, ACTION_SEND through a chooser).</summary>
        public static void Share()
        {
            Ads.SuppressAppOpenFor(AppOpenSkipSeconds);
#if UNITY_EDITOR
            Debug.Log("[AppLinks] share: " + ShareMessage);
#elif UNITY_ANDROID
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var intentClass = new AndroidJavaClass("android.content.Intent"))
                using (var intent = new AndroidJavaObject("android.content.Intent"))
                {
                    intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
                    intent.Call<AndroidJavaObject>("setType", "text/plain");
                    intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_SUBJECT"), "Arrow Puzzle");
                    intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), ShareMessage);
                    using (var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "Share with"))
                        activity.Call("startActivity", chooser);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AppLinks] share failed: " + e);
            }
#else
            Debug.Log("[AppLinks] share is Android only: " + ShareMessage);
#endif
        }

        /// <summary>Play Store app page (market://); the web page when the Play Store app can't open it.</summary>
        public static void Rate()
        {
            Ads.SuppressAppOpenFor(AppOpenSkipSeconds);
#if UNITY_EDITOR
            Debug.Log("[AppLinks] rate: " + PlayStoreMarketUrl + " (fallback " + PlayStoreWebUrl + ")");
#elif UNITY_ANDROID
            // Application.OpenURL never throws on Android (a missing Play Store is only logged by Unity), so the
            // market:// intent is started directly: no Play Store = ActivityNotFoundException = web fallback.
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var intentClass = new AndroidJavaClass("android.content.Intent"))
                using (var uriClass = new AndroidJavaClass("android.net.Uri"))
                using (var uri = uriClass.CallStatic<AndroidJavaObject>("parse", PlayStoreMarketUrl))
                using (var intent = new AndroidJavaObject("android.content.Intent", intentClass.GetStatic<string>("ACTION_VIEW"), uri))
                {
                    activity.Call("startActivity", intent);
                }
            }
            catch (Exception e)
            {
                Debug.Log("[AppLinks] market:// failed (" + e.Message + "), opening " + PlayStoreWebUrl);
                Application.OpenURL(PlayStoreWebUrl);
            }
#else
            Application.OpenURL(PlayStoreWebUrl);
#endif
        }
    }
}
