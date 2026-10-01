using HarmonyLib;
using RimWorld;
using Verse;

namespace GasVentilation;

/// <summary>A door locked by a triggered, linked intruder sensor (CompDoorLock) blocks exactly the pawns that
/// sensor is configured to detect -- see CompIntruderSensor.MatchesTargets. Everyone else opens it normally.</summary>
[HarmonyPatch(typeof(Building_Door), nameof(Building_Door.PawnCanOpen))]
internal static class Patch_Door_PawnCanOpen
{
    [HarmonyPostfix]
    private static void Postfix(Building_Door __instance, Pawn p, ref bool __result)
    {
        if (!__result)
        {
            return;
        }
        CompDoorLock doorLock = __instance.TryGetComp<CompDoorLock>();
        if (doorLock != null && doorLock.Blocks(p))
        {
            __result = false;
        }
    }
}
