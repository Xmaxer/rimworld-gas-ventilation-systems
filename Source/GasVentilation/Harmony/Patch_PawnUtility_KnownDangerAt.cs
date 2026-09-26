using GasVentilation.Core;
using HarmonyLib;
using RimWorld;
using Verse;

namespace GasVentilation;

/// <summary>
/// Medium or dense gas that affects the pawn counts as a known danger. Vanilla then re-paths around gas that
/// drifts onto a route, and keeps wander, flee and cover spots out of it.
/// </summary>
[HarmonyPatch(typeof(PawnUtility), nameof(PawnUtility.KnownDangerAt))]
internal static class Patch_PawnUtility_KnownDangerAt
{
    [HarmonyPostfix]
    private static void Postfix(IntVec3 c, Map map, Pawn forPawn, ref bool __result)
    {
        if (__result || forPawn == null || map == null || forPawn.Drafted)
        {
            return;
        }
        VentGasGrid grid = VentGasGrid.For(map);
        if (grid == null || grid.LiveCells == 0 || !c.InBounds(map))
        {
            return;
        }
        uint packed = grid.PackedAt(c);
        if (packed == 0u)
        {
            return;
        }
        byte mask = GasPawnMaskCache.MaskFor(forPawn);
        if (mask != 0 && GasBands.AnyBandAtLeast(packed, mask, GasBands.Medium))
        {
            __result = true;
        }
    }
}
