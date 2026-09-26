using HarmonyLib;
using Verse;
using Verse.AI;

namespace GasVentilation;

/// <summary>
/// Every pawn path request passes through this overload. Attach the gas customizer for the pawn's relevance mask
/// when the request has none (breach raids and other mods bring their own customizer; they are left alone, and the
/// low priority lets them run first). Drafted pawns follow orders, so they get no gas costs.
/// </summary>
[HarmonyPatch(typeof(PathFinder), nameof(PathFinder.CreateRequest), new[]
{
    typeof(IntVec3), typeof(LocalTargetInfo), typeof(IntVec3?), typeof(TraverseParms),
    typeof(PathFinderCostTuning?), typeof(PathEndMode), typeof(Pawn), typeof(PathRequest.IPathGridCustomizer)
})]
internal static class Patch_PathFinder_CreateRequest
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(PathRequest __result, Pawn pawn)
    {
        if (__result == null || __result.customizer != null || pawn == null || !pawn.Spawned || pawn.Drafted)
        {
            return;
        }
        VentGasGrid grid = VentGasGrid.For(pawn.Map);
        if (grid?.PathSource == null || grid.LiveCells == 0)
        {
            return;
        }
        byte mask = GasPawnMaskCache.MaskFor(pawn);
        if (mask != 0)
        {
            __result.customizer = grid.PathSource.CustomizerFor(mask);
        }
    }
}
