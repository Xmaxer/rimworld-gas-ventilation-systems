using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Verse;
using Verse.AI;

namespace GasVentilation.DevHarness.Scenarios;

/// <summary>
/// Interactive dev convenience (not a test), built for recording Workshop trailer footage: four independent,
/// clearly-separated set-pieces on one flattened map, each behind its own closed door. Persistent: control is
/// handed back to the player paused right after Setup, exactly like PlaygroundScenario, so nothing moves until
/// you press play -- re-launch the scenario (tools/play.ps1 -Scenario showcase) for a clean take each time.
///
/// 1. Toxin trap: a corridor leads through an open, held-open Autodoor into a sealed 5x5 room with a floor vent
///    (Sensor mode) fed by a full toxin manifold, and an intruder sensor linked to both the vent and the door.
///    A hostile pawn is given a one-shot walk-in job; the moment you unpause, it walks in, the sensor trips, the
///    door locks (overriding its own Hold Open, same fix as the general door-lock feature), and the vent opens.
/// 2. Three mechanoids sealed in a room with haywire gas already flowing.
/// 3. Two insectoids sealed in a room with insecticide gas already flowing.
/// 4. Three hostile humanlikes sealed in a room with sedative gas already flowing.
/// </summary>
public sealed class ShowcaseScenario : HarnessScenario
{
    private const int Gap = 20;

    private static readonly FieldInfo OpenField = typeof(Building_Door).GetField("openInt", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo HoldOpenField = typeof(Building_Door).GetField("holdOpenInt", BindingFlags.NonPublic | BindingFlags.Instance);

    public override string Name => "showcase";

    public override bool IsPersistent => true;

    public override void Setup(Map map, CellRect area)
    {
        Log.Message("[GasVentHarness] showcase: flattening map to concrete...");
        HarnessUtil.FlattenMap(map);
        RemoveAllPlayerPawns(map);

        IntVec3 origin = map.Center;
        List<string> descriptions = new List<string>();

        IntVec3 p1 = origin + new IntVec3(-Gap, 0, 0);
        IntVec3 p2 = origin + new IntVec3(0, 0, 0);
        IntVec3 p3 = origin + new IntVec3(Gap, 0, 0);
        IntVec3 p4 = origin + new IntVec3(Gap * 2, 0, 0);

        descriptions.Add(BuildToxinTrapRoom(map, p1));
        descriptions.Add(BuildMechanoidRoom(map, p2));
        descriptions.Add(BuildInsectoidRoom(map, p3));
        descriptions.Add(BuildHumanRoom(map, p4));

        HarnessUtil.JumpCamera(p1);
        string text =
            "Gas Ventilation showcase (DevHarness only), for recording trailer footage:\n\n" +
            string.Join("\n\n", descriptions) +
            "\n\nThe game is paused. Press play when you're ready to record each beat; " +
            "re-launch (tools/play.ps1 -Scenario showcase) for a clean take.";
        Find.LetterStack.ReceiveLetter("Gas ventilation showcase ready", text, LetterDefOf.PositiveEvent, new LookTargets(new TargetInfo(p1, map)));
        Log.Message("[GasVentHarness] showcase: 4 set-pieces built");
    }

    public override ScenarioStatus Tick(Map map, int ticksSinceSetup, List<string> failures)
    {
        // Never called: the runner hands a persistent scenario to the player right after Setup.
        return ScenarioStatus.Running;
    }

    /// <summary>Corridor -> open, held-open Autodoor -> sealed 5x5 room: floor vent (Sensor mode) fed by a full
    /// toxin manifold behind the far wall, intruder sensor linked to both the vent and the door. A hostile pawn
    /// starts at the far end of the corridor with a one-shot walk-in job.</summary>
    private static string BuildToxinTrapRoom(Map map, IntVec3 origin)
    {
        const int roomSize = 5;
        const int corridorLength = 7;

        CellRect outer = new CellRect(origin.x - roomSize / 2, origin.z, roomSize, roomSize);
        CellRect inner = HarnessUtil.BuildSealedRoom(map, outer, roofed: true);
        int x = outer.CenterCell.x;

        IntVec3 doorCell = new IntVec3(x, 0, outer.minZ);
        doorCell.GetFirstThing(map, ThingDefOf.Wall)?.Destroy(DestroyMode.Vanish);
        Thing doorThing = HarnessUtil.SpawnBuilding(DefDatabase<ThingDef>.GetNamed("Autodoor"), doorCell, map, Rot4.North, ThingDefOf.Plasteel);
        ForceDoorOpenAndHeld(doorThing);

        // Corridor south of the door: 1-wide, flanked and roofed, open at the far (spawn) end.
        for (int i = 1; i <= corridorLength; i++)
        {
            IntVec3 corridorCell = new IntVec3(x, 0, doorCell.z - i);
            HarnessUtil.SpawnBuilding(ThingDefOf.Wall, corridorCell + IntVec3.West, map, Rot4.North, ThingDefOf.Plasteel);
            HarnessUtil.SpawnBuilding(ThingDefOf.Wall, corridorCell + IntVec3.East, map, Rot4.North, ThingDefOf.Plasteel);
            map.roofGrid.SetRoof(corridorCell, RoofDefOf.RoofConstructed);
            map.roofGrid.SetRoof(corridorCell + IntVec3.West, RoofDefOf.RoofConstructed);
            map.roofGrid.SetRoof(corridorCell + IntVec3.East, RoofDefOf.RoofConstructed);
        }

        // Floor vent dead centre; a visible pipe carries it to a hidden pipe under the north wall, then out to a
        // full toxin manifold behind the room (same convention as PlaygroundScenario's non-wall-vent rooms).
        IntVec3 ventCell = inner.CenterCell;
        Thing ventThing = HarnessUtil.SpawnBuilding(DefDatabase<ThingDef>.GetNamed("GV_VentFloor"), ventCell, map, Rot4.North);
        HarnessUtil.SpawnBuilding(DefDatabase<ThingDef>.GetNamed("GV_Pipe"), ventCell + IntVec3.North, map, Rot4.North);
        HarnessUtil.SpawnBuilding(GVDefOf.GV_HiddenPipe, new IntVec3(x, 0, outer.maxZ), map, Rot4.North);
        HarnessUtil.SpawnBuilding(DefDatabase<ThingDef>.GetNamed("GV_Pipe"), new IntVec3(x, 0, outer.maxZ + 1), map, Rot4.North);
        CompGasManifold manifold = HarnessUtil.SpawnBuilding(DefDatabase<ThingDef>.GetNamed("GV_Manifold"), new IntVec3(x, 0, outer.maxZ + 2), map, Rot4.South)
            .TryGetComp<CompGasManifold>();
        manifold.SetActiveGas(GVDefOf.GV_Gas_Toxin);
        manifold.AddResource(4f);

        CompGasVentController vent = ventThing.TryGetComp<CompGasVentController>();
        vent.ToggleGas(VentGasSelection.Toxin);
        vent.SetMode(VentMode.Sensor);

        // Sensor on the inner west wall, clear of the vent/pipe column and the doorway. West-facing: its wall
        // attachment rotation points AT the wall it's mounted on, which is to the west of this inner cell.
        Thing sensorThing = HarnessUtil.SpawnBuilding(DefDatabase<ThingDef>.GetNamed("GV_IntruderSensor"), new IntVec3(inner.minX, 0, inner.CenterCell.z), map, Rot4.West);
        CompIntruderSensor sensor = sensorThing.TryGetComp<CompIntruderSensor>();
        sensor.ToggleLink(ventThing);
        sensor.ToggleDoorLink(doorThing);

        Faction enemy = Find.FactionManager.RandomEnemyFaction(false, false, false) ?? Faction.OfPirates;
        IntVec3 spawnCell = new IntVec3(x, 0, doorCell.z - corridorLength);
        Pawn intruder = HarnessUtil.SpawnPawn(PawnKindDefOf.Villager, enemy, spawnCell, map);
        Job walkIn = JobMaker.MakeJob(JobDefOf.Goto, inner.CenterCell);
        intruder.jobs.StartJob(walkIn, JobCondition.InterruptForced);

        return Describe(doorThing, "toxin trap: walk the pawn down the corridor into the 5x5 room -- the sensor " +
            "locks the door (overriding Hold Open) and opens the vent the moment it enters");
    }

    /// <summary>Three mechanoids sealed in a room, haywire gas already flowing.</summary>
    private static string BuildMechanoidRoom(Map map, IntVec3 origin)
    {
        CellRect outer = new CellRect(origin.x - 3, origin.z - 3, 6, 6);
        CellRect inner = HarnessUtil.BuildSealedRoom(map, outer, roofed: true);
        AddClosedDoor(map, outer);
        BuildGasSupply(map, outer, GVDefOf.GV_Gas_Haywire, VentGasSelection.Haywire);

        PawnKindDef scyther = DefDatabase<PawnKindDef>.GetNamed("Mech_Scyther");
        IntVec3 c = inner.CenterCell;
        HarnessUtil.SpawnPawn(scyther, Faction.OfMechanoids, c + new IntVec3(-1, 0, 0), map);
        HarnessUtil.SpawnPawn(scyther, Faction.OfMechanoids, c, map);
        HarnessUtil.SpawnPawn(scyther, Faction.OfMechanoids, c + new IntVec3(1, 0, 0), map);

        return Describe(outer.CenterCell.ToString(), "mechanoids: 3 Scythers sealed in with haywire gas, already flowing");
    }

    /// <summary>Two insectoids sealed in a room, insecticide gas already flowing.</summary>
    private static string BuildInsectoidRoom(Map map, IntVec3 origin)
    {
        CellRect outer = new CellRect(origin.x - 2, origin.z - 2, 5, 5);
        CellRect inner = HarnessUtil.BuildSealedRoom(map, outer, roofed: true);
        AddClosedDoor(map, outer);
        BuildGasSupply(map, outer, GVDefOf.GV_Gas_Insecticide, VentGasSelection.Insecticide);

        PawnKindDef megaspider = DefDatabase<PawnKindDef>.GetNamed("Megaspider");
        IntVec3 c = inner.CenterCell;
        Pawn a = HarnessUtil.SpawnPawn(megaspider, null, c + new IntVec3(-1, 0, 0), map);
        Pawn b = HarnessUtil.SpawnPawn(megaspider, null, c + new IntVec3(1, 0, 0), map);
        a.SetFaction(null);
        b.SetFaction(null);

        return Describe(outer.CenterCell.ToString(), "insectoids: 2 Megaspiders sealed in with insecticide gas, already flowing");
    }

    /// <summary>Three hostile humanlikes sealed in a room, sedative gas already flowing.</summary>
    private static string BuildHumanRoom(Map map, IntVec3 origin)
    {
        CellRect outer = new CellRect(origin.x - 3, origin.z - 3, 6, 6);
        CellRect inner = HarnessUtil.BuildSealedRoom(map, outer, roofed: true);
        AddClosedDoor(map, outer);
        BuildGasSupply(map, outer, GVDefOf.GV_Gas_Sedative, VentGasSelection.Sedative);

        Faction enemy = Find.FactionManager.RandomEnemyFaction(false, false, false) ?? Faction.OfPirates;
        IntVec3 c = inner.CenterCell;
        HarnessUtil.SpawnPawn(PawnKindDefOf.Villager, enemy, c + new IntVec3(-1, 0, 0), map);
        HarnessUtil.SpawnPawn(PawnKindDefOf.Villager, enemy, c, map);
        HarnessUtil.SpawnPawn(PawnKindDefOf.Villager, enemy, c + new IntVec3(1, 0, 0), map);

        return Describe(outer.CenterCell.ToString(), "humans: 3 hostiles sealed in with sedative gas, already flowing");
    }

    /// <summary>Knocks a closed Autodoor into the room's south wall, purely for visual realism -- these three
    /// rooms are already triggered and don't use the sensor-lock feature.</summary>
    private static void AddClosedDoor(Map map, CellRect outer)
    {
        IntVec3 doorCell = new IntVec3(outer.CenterCell.x, 0, outer.minZ);
        doorCell.GetFirstThing(map, ThingDefOf.Wall)?.Destroy(DestroyMode.Vanish);
        HarnessUtil.SpawnBuilding(DefDatabase<ThingDef>.GetNamed("Autodoor"), doorCell, map, Rot4.North, ThingDefOf.Plasteel);
    }

    /// <summary>Wall vent on the room's north edge (On, not Sensor), fed by a full manifold just behind it.</summary>
    private static void BuildGasSupply(Map map, CellRect outer, GasDef gas, VentGasSelection selection)
    {
        int x = outer.CenterCell.x;
        Thing ventThing = HarnessUtil.SpawnBuilding(DefDatabase<ThingDef>.GetNamed("GV_VentWall"), new IntVec3(x, 0, outer.maxZ), map, Rot4.South);
        HarnessUtil.SpawnBuilding(DefDatabase<ThingDef>.GetNamed("GV_Pipe"), new IntVec3(x, 0, outer.maxZ + 1), map, Rot4.North);
        CompGasManifold manifold = HarnessUtil.SpawnBuilding(DefDatabase<ThingDef>.GetNamed("GV_Manifold"), new IntVec3(x, 0, outer.maxZ + 2), map, Rot4.South)
            .TryGetComp<CompGasManifold>();
        manifold.SetActiveGas(gas);
        manifold.AddResource(4f);
        CompGasVentController vent = ventThing.TryGetComp<CompGasVentController>();
        vent.ToggleGas(selection);
        vent.SetMode(VentMode.On);
    }

    /// <summary>
    /// A new-game map-gen start drops the player's 3 starting colonists (plus any starting pet/animal) on the
    /// map before the harness ever runs; HarnessUtil.FlattenMap deliberately spares player-faction pawns (it's
    /// shared with PlaygroundScenario, which needs the real colonists to survive). This recording backdrop needs
    /// none of them -- remove every player-faction pawn outright, on top of the blanket item/building wipe
    /// FlattenMap already did for everything else.
    /// </summary>
    private static void RemoveAllPlayerPawns(Map map)
    {
        List<Pawn> pawns = new List<Pawn>(map.mapPawns.AllPawnsSpawned);
        for (int i = 0; i < pawns.Count; i++)
        {
            Pawn pawn = pawns[i];
            if (pawn.Faction == Faction.OfPlayer && !pawn.Destroyed)
            {
                pawn.Destroy(DestroyMode.Vanish);
            }
        }
    }

    private static void ForceDoorOpenAndHeld(Thing doorThing)
    {
        if (doorThing is Building_Door door)
        {
            OpenField.SetValue(door, true);
            HoldOpenField.SetValue(door, true);
        }
    }

    private static string Describe(string where, string text)
    {
        return $"  - {where}: {text}";
    }

    private static string Describe(Thing thing, string text)
    {
        return Describe($"{thing.Position.x}, {thing.Position.z}", text);
    }
}
