using HarmonyLib;
using Verse;

namespace GasVentilation;

/// <summary>Vanilla vents (and other callers) equalise vanilla gas through a building; do the same for ours.</summary>
[HarmonyPatch(typeof(GasGrid), nameof(GasGrid.EqualizeGasThroughBuilding))]
internal static class Patch_GasGrid_Equalize
{
    [HarmonyPostfix]
    private static void Postfix(Building b, bool twoWay)
    {
        if (b?.Map == null)
        {
            return;
        }
        VentGasGrid.For(b.Map)?.EqualizeThrough(b, twoWay);
    }
}
