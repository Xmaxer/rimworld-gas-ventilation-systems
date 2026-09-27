using System.Collections.Generic;
using Verse;

namespace GasVentilation.DevHarness.Scenarios;

/// <summary>
/// Manifold (4 sedative canisters) -> visible pipes -> wall vent (mode On) in the south wall of a sealed room.
/// Expects gas in the room, canisters drained, at least one empty shell ejected, and no gas outside the room.
/// </summary>
public sealed class PipeNetworkScenario : HarnessScenario
{
    private CellRect inner;
    private CellRect outer;
    private CellRect area;
    private CompGasManifold manifold;

    public override string Name => "pipes";

    public override int TimeoutTicks => 4000;

    public override void Setup(Map map, CellRect area)
    {
        this.area = area;
        outer = new CellRect(area.minX + 6, area.minZ + 8, 9, 9);
        inner = HarnessUtil.BuildSealedRoom(map, outer, roofed: true);
        int x = outer.CenterCell.x;
        Thing vent = HarnessUtil.SpawnBuilding(DefDatabase<ThingDef>.GetNamed("GV_VentWall"), new IntVec3(x, 0, outer.minZ), map, Rot4.North);
        ThingDef pipe = DefDatabase<ThingDef>.GetNamed("GV_Pipe");
        HarnessUtil.SpawnBuilding(pipe, new IntVec3(x, 0, outer.minZ - 1), map, Rot4.North);
        HarnessUtil.SpawnBuilding(pipe, new IntVec3(x, 0, outer.minZ - 2), map, Rot4.North);
        Thing manifoldThing = HarnessUtil.SpawnBuilding(DefDatabase<ThingDef>.GetNamed("GV_Manifold"), new IntVec3(x, 0, outer.minZ - 3), map, Rot4.North);
        manifold = manifoldThing.TryGetComp<CompGasManifold>();
        manifold.SetActiveGas(GVDefOf.GV_Gas_Sedative);
        manifold.AddResource(4f);
        CompGasVentController controller = vent.TryGetComp<CompGasVentController>();
        controller.SetMode(VentMode.On);
        controller.ToggleGas(VentGasSelection.Sedative);
        HarnessUtil.JumpCamera(inner.CenterCell);
    }

    public override ScenarioStatus Tick(Map map, int ticksSinceSetup, List<string> failures)
    {
        if (ticksSinceSetup == 600)
        {
            HarnessUtil.Screenshot("pipes-t600");
        }
        if (ticksSinceSetup < 2400)
        {
            return ScenarioStatus.Running;
        }
        VentGasGrid grid = VentGasGrid.For(map);
        int inside = 0;
        foreach (IntVec3 cell in inner)
        {
            inside += grid.DensityAt(cell, GVDefOf.GV_Gas_Sedative);
        }
        if (inside < 1000)
        {
            failures.Add($"expected sedative gas in the room, total density {inside}");
        }
        if (manifold.AmountStored > 3.01f)
        {
            failures.Add($"manifold should have drained at least one canister, stored {manifold.AmountStored:F2}");
        }
        int shells = 0;
        foreach (IntVec3 cell in area)
        {
            List<Thing> things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i].def == GVDefOf.GV_CanisterEmpty)
                {
                    shells += things[i].stackCount;
                }
            }
        }
        if (shells < 1)
        {
            failures.Add("no empty canister shell was ejected");
        }
        foreach (IntVec3 cell in outer.ExpandedBy(1).EdgeCells)
        {
            if (cell.InBounds(map) && grid.PackedAt(cell) != 0u)
            {
                failures.Add($"gas outside the room at {cell}");
                break;
            }
        }
        return failures.Count == 0 ? ScenarioStatus.Passed : ScenarioStatus.Failed;
    }
}
