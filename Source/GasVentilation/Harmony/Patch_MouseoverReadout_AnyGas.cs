using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace GasVentilation;

/// <summary>
/// Vanilla only calls <c>DrawGas</c> inside <c>if (gasGrid.AnyGasAt(cell))</c>, so a cell holding only our gas
/// would never reach <see cref="Patch_MouseoverReadout_DrawGas"/>. Swap that one call for a check that also
/// looks at <see cref="VentGasGrid"/>. Vanilla <c>DrawGas</c> skips zero densities, so nothing extra is drawn.
/// </summary>
[HarmonyPatch(typeof(MouseoverReadout), nameof(MouseoverReadout.MouseoverReadoutOnGUI))]
internal static class Patch_MouseoverReadout_AnyGas
{
    private static readonly MethodInfo VanillaAnyGasAt =
        AccessTools.Method(typeof(GasGrid), nameof(GasGrid.AnyGasAt), new[] { typeof(IntVec3) });

    private static readonly MethodInfo Replacement =
        AccessTools.Method(typeof(Patch_MouseoverReadout_AnyGas), nameof(AnyGasIncludingVentGas));

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        int replaced = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.Calls(VanillaAnyGasAt))
            {
                instruction.opcode = System.Reflection.Emit.OpCodes.Call;
                instruction.operand = Replacement;
                replaced++;
            }
            yield return instruction;
        }
        if (replaced == 0)
        {
            Log.Warning("[GasVentilation] Could not find GasGrid.AnyGasAt in MouseoverReadoutOnGUI; ventilation gas only shows in the readout where vanilla gas is present.");
        }
    }

    private static bool AnyGasIncludingVentGas(GasGrid gasGrid, IntVec3 cell)
    {
        if (gasGrid.AnyGasAt(cell))
        {
            return true;
        }
        VentGasGrid grid = VentGasGrid.For(Find.CurrentMap);
        return grid != null && grid.LiveCells > 0 && cell.InBounds(Find.CurrentMap) && grid.PackedAt(cell) != 0u;
    }
}
