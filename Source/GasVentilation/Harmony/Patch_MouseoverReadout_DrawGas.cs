using GasVentilation.Core;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace GasVentilation;

/// <summary>
/// Vanilla <c>MouseoverReadout.MouseoverReadoutOnGUI</c> calls the private <c>DrawGas</c> for each vanilla gas,
/// with DeadlifeDust last. This postfix appends our lines after that last call, advancing the same offset.
/// <see cref="Patch_MouseoverReadout_AnyGas"/> makes sure the calls happen when only our gas is present.
/// </summary>
[HarmonyPatch(typeof(MouseoverReadout), "DrawGas")]
internal static class Patch_MouseoverReadout_DrawGas
{
    private const float LeftX = 15f;
    private const float BottomY = 65f;
    private const float LineHeight = 19f;

    [HarmonyPostfix]
    private static void Postfix(GasType gasType, ref float curYOffset)
    {
        if (gasType != GasType.DeadlifeDust)
        {
            return;
        }
        Map map = Find.CurrentMap;
        if (map == null)
        {
            return;
        }
        VentGasGrid grid = VentGasGrid.For(map);
        if (grid == null || grid.LiveCells == 0)
        {
            return;
        }
        IntVec3 cell = UI.MouseCell();
        if (!cell.InBounds(map))
        {
            return;
        }
        uint packed = grid.PackedAt(cell);
        if (packed == 0u)
        {
            return;
        }
        for (int channel = 0; channel < GasPacking.Channels; channel++)
        {
            int density = GasPacking.Get(packed, channel);
            GasDef def = GasDefRegistry.ForChannel(channel);
            if (density == 0 || def == null)
            {
                continue;
            }
            Rect rect = new Rect(LeftX, UI.screenHeight - BottomY - curYOffset, 999f, 999f);
            Widgets.Label(rect, "GV_GasReadout".Translate(def.LabelCap, (density / 255f).ToStringPercent("F0")));
            curYOffset += LineHeight;
        }
    }
}
