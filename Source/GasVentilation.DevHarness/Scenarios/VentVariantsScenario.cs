using System.Collections.Generic;
using Verse;

namespace GasVentilation.DevHarness.Scenarios;

/// <summary>
/// Places each vent shape and checks registration and output cells. Then removes the roof above the ceiling
/// vent and checks that it falls.
/// </summary>
public sealed class VentVariantsScenario : HarnessScenario
{
    private CompGasVentController wall;
    private CompGasVentController mounted;
    private CompGasVentController floor;
    private CompGasVentController ceiling;
    private IntVec3 ceilingCell;
    private bool roofRemoved;

    public override string Name => "vents";

    public override int TimeoutTicks => 1500;

    public override void Setup(Map map, CellRect area)
    {
        CellRect outer = new CellRect(area.minX + 4, area.minZ + 4, 9, 9);
        CellRect inner = HarnessUtil.BuildSealedRoom(map, outer, roofed: true);
        wall = Spawn(map, "GV_VentWall", new IntVec3(outer.CenterCell.x, 0, outer.minZ), Rot4.North);
        mounted = Spawn(map, "GV_VentMounted", new IntVec3(inner.minX + 1, 0, inner.maxZ), Rot4.North);
        floor = Spawn(map, "GV_VentFloor", new IntVec3(inner.minX + 1, 0, inner.minZ + 1), Rot4.North);
        ceilingCell = new IntVec3(inner.maxX - 1, 0, inner.CenterCell.z);
        ceiling = Spawn(map, "GV_VentCeiling", ceilingCell, Rot4.North);
        wall.ToggleGas(VentGasSelection.Haywire);
        mounted.ToggleGas(VentGasSelection.Haywire);
        floor.ToggleGas(VentGasSelection.Haywire);
        ceiling.ToggleGas(VentGasSelection.Haywire);
    }

    public override ScenarioStatus Tick(Map map, int ticksSinceSetup, List<string> failures)
    {
        if (ticksSinceSetup == 150)
        {
            IReadOnlyList<CompGasVentController> controllers = VentGasGrid.For(map).Devices.Controllers;
            Check(controllers, wall, failures);
            Check(controllers, mounted, failures);
            Check(controllers, floor, failures);
            Check(controllers, ceiling, failures);
            if (wall.OutputCell != wall.parent.Position + IntVec3.North)
            {
                failures.Add("wall vent output should be the cell it faces");
            }
            if (mounted.OutputCell != mounted.parent.Position || floor.OutputCell != floor.parent.Position)
            {
                failures.Add("mounted and floor vents should output into their own cell");
            }
            if (!ceiling.parent.Spawned)
            {
                failures.Add("ceiling vent fell although it is under a roof");
            }
            map.roofGrid.SetRoof(ceilingCell, null);
            roofRemoved = true;
            return ScenarioStatus.Running;
        }
        if (roofRemoved && ticksSinceSetup >= 200)
        {
            if (ceiling.parent.Spawned)
            {
                failures.Add("ceiling vent did not fall after its roof was removed");
            }
            return failures.Count == 0 ? ScenarioStatus.Passed : ScenarioStatus.Failed;
        }
        return ScenarioStatus.Running;
    }

    private static CompGasVentController Spawn(Map map, string defName, IntVec3 cell, Rot4 rot)
    {
        return HarnessUtil.SpawnBuilding(DefDatabase<ThingDef>.GetNamed(defName), cell, map, rot).TryGetComp<CompGasVentController>();
    }

    private static void Check(IReadOnlyList<CompGasVentController> controllers, CompGasVentController controller, List<string> failures)
    {
        bool found = false;
        for (int i = 0; i < controllers.Count; i++)
        {
            if (controllers[i] == controller)
            {
                found = true;
            }
        }
        if (!found)
        {
            failures.Add($"{controller.parent.def.defName} is not registered with the device registry");
        }
    }
}
