using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace GasVentilation;

/// <summary>Greys out a locked door's vanilla "Hold Open" toggle with an explanatory reason, so the player sees
/// why it won't stay on instead of it silently reverting (CompDoorLock re-suppresses Hold Open every evaluation
/// pass while locked).</summary>
[HarmonyPatch(typeof(Building_Door), nameof(Building_Door.GetGizmos))]
internal static class Patch_Door_GetGizmos
{
    private static readonly AccessTools.FieldRef<Gizmo, bool> DisabledField = AccessTools.FieldRefAccess<Gizmo, bool>("disabled");

    [HarmonyPostfix]
    private static void Postfix(Building_Door __instance, ref IEnumerable<Gizmo> __result)
    {
        CompDoorLock doorLock = __instance.TryGetComp<CompDoorLock>();
        if (doorLock != null && doorLock.Locked)
        {
            __result = DisableHoldOpen(__result);
        }
    }

    private static IEnumerable<Gizmo> DisableHoldOpen(IEnumerable<Gizmo> gizmos)
    {
        foreach (Gizmo gizmo in gizmos)
        {
            if (gizmo is Command_Toggle toggle && toggle.icon == TexCommand.HoldOpen)
            {
                DisabledField(toggle) = true;
                toggle.disabledReason = "GV_DoorLockedDisablesHoldOpen".Translate();
            }
            yield return gizmo;
        }
    }
}
