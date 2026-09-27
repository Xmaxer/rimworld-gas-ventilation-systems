using System.Collections.Generic;
using RimWorld;
using Verse;

namespace GasVentilation.DevHarness.Scenarios;

/// <summary>
/// A sealed room with a sensor on the inside wall and a Sensor-mode wall vent fed by a manifold.
/// No target: vent idle. Hostile pawn spawned: vent triggers and gas appears. Pawn removed: after the 600-tick
/// linger, the vent goes idle again. There is no power grid in the harness; HarnessUtil.SpawnBuilding/
/// ReapplyForcedPower keep every power-requiring building spawned here forced on.
/// </summary>
public sealed class SensorScenario : HarnessScenario
{
    private CellRect inner;
    private CompGasVentController vent;
    private CompIntruderSensor sensor;
    private Pawn intruder;

    public override string Name => "sensor";

    public override int TimeoutTicks => 2000;

    public override void Setup(Map map, CellRect area)
    {
        CellRect outer = new CellRect(area.minX + 6, area.minZ + 8, 9, 9);
        inner = HarnessUtil.BuildSealedRoom(map, outer, roofed: true);
        int x = outer.CenterCell.x;
        vent = HarnessUtil.SpawnBuilding(DefDatabase<ThingDef>.GetNamed("GV_VentWall"), new IntVec3(x, 0, outer.minZ), map, Rot4.North).TryGetComp<CompGasVentController>();
        ThingDef pipe = DefDatabase<ThingDef>.GetNamed("GV_Pipe");
        HarnessUtil.SpawnBuilding(pipe, new IntVec3(x, 0, outer.minZ - 1), map, Rot4.North);
        HarnessUtil.SpawnBuilding(pipe, new IntVec3(x, 0, outer.minZ - 2), map, Rot4.North);
        CompGasManifold manifold = HarnessUtil.SpawnBuilding(DefDatabase<ThingDef>.GetNamed("GV_Manifold"), new IntVec3(x, 0, outer.minZ - 3), map, Rot4.North)
            .TryGetComp<CompGasManifold>();
        manifold.SetActiveGas(GVDefOf.GV_Gas_Toxin);
        manifold.AddResource(4f);
        Thing sensorThing = HarnessUtil.SpawnBuilding(DefDatabase<ThingDef>.GetNamed("GV_IntruderSensor"), new IntVec3(x, 0, inner.maxZ), map, Rot4.North);
        sensor = sensorThing.TryGetComp<CompIntruderSensor>();
        vent.ToggleGas(VentGasSelection.Toxin);
        vent.SetMode(VentMode.Sensor);
        HarnessUtil.JumpCamera(inner.CenterCell);
    }

    public override ScenarioStatus Tick(Map map, int ticksSinceSetup, List<string> failures)
    {
        if (ticksSinceSetup == 70)
        {
            if (sensor == null)
            {
                failures.Add("sensor has no CompIntruderSensor");
                return ScenarioStatus.Failed;
            }
            if (vent.SensorTriggered)
            {
                failures.Add("vent triggered with no target present");
            }
            Faction enemy = Find.FactionManager.RandomEnemyFaction(false, false, false) ?? Faction.OfPirates;
            intruder = HarnessUtil.SpawnPawn(PawnKindDefOf.Villager, enemy, inner.CenterCell, map);
        }
        if (ticksSinceSetup == 200)
        {
            if (!sensor.Triggered)
            {
                failures.Add("sensor did not trigger with a hostile pawn in the room");
            }
            if (!vent.SensorTriggered)
            {
                failures.Add("vent did not trigger with a hostile pawn in the room");
            }
        }
        if (ticksSinceSetup == 390)
        {
            HarnessUtil.Screenshot("sensor-t390");
        }
        if (ticksSinceSetup == 400)
        {
            int inside = 0;
            foreach (IntVec3 cell in inner)
            {
                inside += VentGasGrid.For(map).DensityAt(cell, GVDefOf.GV_Gas_Toxin);
            }
            if (inside == 0)
            {
                failures.Add("triggered vent released no gas");
            }
            Log.Message($"[GasVentHarness] sensor: toxin in room at t400 = {inside}, intruder dead={intruder?.Dead} downed={intruder?.Downed}");
            RemoveIntruder();
        }
        if (ticksSinceSetup < 1150)
        {
            return ScenarioStatus.Running;
        }
        if (sensor.Triggered || vent.SensorTriggered)
        {
            failures.Add("vent still triggered after the linger time with no target");
        }
        return failures.Count == 0 ? ScenarioStatus.Passed : ScenarioStatus.Failed;
    }

    private void RemoveIntruder()
    {
        if (intruder == null)
        {
            return;
        }
        if (intruder.Dead)
        {
            Corpse corpse = intruder.Corpse;
            if (corpse != null && !corpse.Destroyed)
            {
                corpse.Destroy(DestroyMode.Vanish);
            }
        }
        else if (!intruder.Destroyed)
        {
            intruder.Destroy(DestroyMode.Vanish);
        }
    }
}
