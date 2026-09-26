using System.Collections.Generic;
using GasVentilation.Core;
using LudeonTK;
using Verse;

namespace GasVentilation;

public static class GasDebugActions
{
    private const string Category = "Gas Ventilation";

    [DebugAction(Category, "Add vent gas...", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static List<DebugActionNode> AddGas()
    {
        List<DebugActionNode> nodes = new List<DebugActionNode>();
        for (int channel = 0; channel < GasPacking.Channels; channel++)
        {
            GasDef def = GasDefRegistry.ForChannel(channel);
            if (def == null)
            {
                continue;
            }
            nodes.Add(new DebugActionNode(def.LabelCap, DebugActionType.ToolMap, () =>
            {
                Map map = Find.CurrentMap;
                VentGasGrid grid = VentGasGrid.For(map);
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(UI.MouseCell(), 2.9f, true))
                {
                    if (cell.InBounds(map))
                    {
                        grid.TryAddGas(cell, def, 255);
                    }
                }
            }));
        }
        return nodes;
    }

    [DebugAction(Category, "Clear all vent gas", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void ClearAll()
    {
        VentGasGrid.For(Find.CurrentMap)?.ClearAll();
    }

    [DebugAction(Category, "Log vent gas at cell", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void LogCell()
    {
        Map map = Find.CurrentMap;
        IntVec3 cell = UI.MouseCell();
        VentGasGrid grid = VentGasGrid.For(map);
        if (grid == null || !cell.InBounds(map))
        {
            return;
        }
        uint packed = grid.PackedAt(cell);
        Log.Message($"[GasVentilation] {cell}: packed=0x{packed:X8} live={grid.LiveCells} canHold={grid.CanHoldGas(cell)}");
    }
}
