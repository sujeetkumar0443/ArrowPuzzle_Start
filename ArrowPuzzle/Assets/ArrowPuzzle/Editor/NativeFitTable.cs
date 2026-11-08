using UnityEditor;
using UnityEngine;

namespace ArrowPuzzle
{
    /// <summary>"ArrowPuzzle/Native ad placement table": the native ad placement maths (NativeFit) for the test phones.</summary>
    static class NativeFitTable
    {
        [MenuItem("ArrowPuzzle/Native ad placement table")]
        static void Print()
        {
            Debug.Log("[Ads] native placement table (dp; medium planned at " + NativeFit.MediumPlanDp + " = template " +
                      NativeFit.MediumNominalDp + " + " + NativeFit.SafetyDp + " safety; the real height is measured on device)");
            foreach (var line in NativeFit.Table()) Debug.Log("[Ads] " + line);
        }
    }
}
