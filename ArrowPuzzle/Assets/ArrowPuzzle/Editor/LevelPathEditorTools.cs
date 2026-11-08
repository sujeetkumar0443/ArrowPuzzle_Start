using System.Text;
using UnityEditor;
using UnityEngine;

namespace ArrowPuzzle
{
    /// <summary>The "Check level path" menu item: every global level, its type, type level and difficulty, and that it loads.</summary>
    public static class LevelPathEditorTools
    {
        [MenuItem("ArrowPuzzle/Check level path")]
        static void CheckPath()
        {
            var list = new StringBuilder();
            int failed = 0, hard = 0;
            for (int g = 1; g <= LevelPath.Count; g++)
            {
                var type = LevelPath.TypeOf(g);
                int typeLevel = LevelPath.TypeLevelOf(g);
                var data = Levels.For(type, typeLevel);
                bool ok = data != null && data.pieces.Count > 0 && LevelPath.Global(type, typeLevel) == g;
                if (!ok) failed++;
                string difficulty = LevelPath.Difficulty(g);
                if (difficulty == "Hard") hard++;

                list.Append("\n  ").Append(g).Append(" -> ").Append(type).Append(' ').Append(typeLevel)
                    .Append(" (").Append(LevelPath.TypeName(g)).Append("), ").Append(difficulty);
                if (data != null) list.Append(", ").Append(data.pieces.Count).Append(" arrows");
                if (!ok) list.Append("  FAILED");
            }

            string head = "Level path: " + LevelPath.Count + " levels, " + (LevelPath.Count - failed) + " load OK, "
                          + (LevelPath.Count - hard) + " Easy / " + hard + " Hard";
            if (failed == 0) Debug.Log(head + list);
            else Debug.LogError(head + ", " + failed + " FAILED" + list);
        }
    }
}
