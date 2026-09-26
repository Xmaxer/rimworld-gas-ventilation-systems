using HarmonyLib;
using Verse;

namespace GasVentilation;

/// <summary>The vanilla dev-mode "clear all gas" also clears ventilation gas.</summary>
[HarmonyPatch(typeof(GasGrid), nameof(GasGrid.Debug_ClearAll))]
internal static class Patch_GasGrid_DebugClearAll
{
    [HarmonyPostfix]
    private static void Postfix(Map ___map)
    {
        VentGasGrid.For(___map)?.ClearAll();
    }
}
