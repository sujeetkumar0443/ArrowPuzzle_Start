using UnityEditor;
using UnityEngine;

namespace ArrowPuzzle
{
    /// <summary>"ArrowPuzzle/MREC placement table": where the 300x250 dp MREC goes on each result popup (MrecFit), per test phone.</summary>
    static class MrecFitTable
    {
        [MenuItem("ArrowPuzzle/MREC placement table")]
        static void Print()
        {
            Debug.Log("[Ads] MREC placement table (design px from the safe-area top; ad = 300x250 dp; frame = header " +
                      MrecFit.HeaderD + " + ad + " + MrecFit.PadD + " padding; " + MrecFit.BottomMarginD + " above the bottom limit)");
            foreach (var line in MrecFit.Table()) Debug.Log("[Ads] " + line);
        }
    }
}
