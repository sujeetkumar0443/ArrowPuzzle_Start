using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace ArrowPuzzle
{
    /// <summary>"ArrowPuzzle/Check Firebase setup": google-services.json, its package name, the Analytics and Crashlytics assemblies.</summary>
    public static class FirebaseEditorTools
    {
        const string JsonPath = "Assets/google-services.json";
        // assembly -> a type it must contain
        static readonly string[,] Assemblies =
        {
            { "Firebase.Analytics", "Firebase.Analytics.FirebaseAnalytics" },
            { "Firebase.Crashlytics", "Firebase.Crashlytics.Crashlytics" },
        };

        [MenuItem("ArrowPuzzle/Check Firebase setup")]
        static void CheckFirebaseSetup()
        {
            var sb = new StringBuilder("Firebase setup:");
            bool ok = true;

            // google-services.json and its package name(s)
            string package = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            if (!File.Exists(JsonPath))
            {
                sb.Append("\n  google-services.json: MISSING at ").Append(JsonPath);
                ok = false;
            }
            else
            {
                var names = Regex.Matches(File.ReadAllText(JsonPath), "\"package_name\"\\s*:\\s*\"([^\"]+)\"")
                                 .Cast<Match>().Select(m => m.Groups[1].Value).Distinct().ToArray();
                bool match = names.Contains(package);
                sb.Append("\n  google-services.json: found, package_name ").Append(names.Length > 0 ? string.Join(", ", names) : "none");
                sb.Append("\n  Player Settings package: ").Append(package).Append(match ? "  (matches)" : "  MISMATCH");
                ok &= match;
            }

            // the Firebase assemblies
            for (int i = 0; i < Assemblies.GetLength(0); i++)
            {
                bool present = Type.GetType(Assemblies[i, 1] + ", " + Assemblies[i, 0]) != null;
                sb.Append("\n  ").Append(Assemblies[i, 0]).Append(": ").Append(present ? "present" : "MISSING");
                ok &= present;
            }

            sb.Append("\n  Ads.TestMode = ").Append(Ads.TestMode);

            if (ok) Debug.Log(sb.ToString());
            else Debug.LogWarning(sb.Append("\nProblems found: see the lines marked MISSING / MISMATCH.").ToString());
        }
    }
}
