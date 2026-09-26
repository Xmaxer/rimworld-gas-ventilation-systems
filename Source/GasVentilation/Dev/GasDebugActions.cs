using System.Collections.Generic;
using GasVentilation.Core;
using LudeonTK;
using RimWorld;
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

    [DebugAction(Category, "Remove all Gas Ventilation content (before uninstalling)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void RemoveAllContent()
    {
        ModContentPack pack = GasVentilationMod.ContentPack;
        int things = 0;
        int hediffs = 0;
        List<Thing> doomed = new List<Thing>();
        foreach (Map map in Find.Maps)
        {
            doomed.Clear();
            List<Thing> all = map.listerThings.AllThings;
            for (int i = 0; i < all.Count; i++)
            {
                Thing thing = all[i];
                ThingDef def = thing is MinifiedThing minified ? minified.InnerThing?.def : thing.def;
                BuildableDef built = def?.entityDefToBuild;
                if (def?.modContentPack == pack || built?.modContentPack == pack)
                {
                    doomed.Add(thing);
                }
            }
            for (int i = 0; i < doomed.Count; i++)
            {
                if (!doomed[i].Destroyed)
                {
                    doomed[i].Destroy(DestroyMode.Vanish);
                    things++;
                }
            }
            VentGasGrid.For(map)?.ClearAll();
        }
        foreach (Pawn pawn in PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead)
        {
            List<Hediff> list = pawn.health?.hediffSet?.hediffs;
            if (list == null)
            {
                continue;
            }
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].def.modContentPack == pack)
                {
                    pawn.health.RemoveHediff(list[i]);
                    hediffs++;
                }
            }
        }
        Messages.Message("GV_RemovedAllContent".Translate(things, hediffs), MessageTypeDefOf.TaskCompletion, false);
    }
}
