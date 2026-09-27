using System.Collections.Generic;
using RimWorld;
using Verse;

namespace GasVentilation.DevHarness.Scenarios;

/// <summary>
/// Interactive dev convenience, not a test (tools/play.ps1 requests it by default). On a new game: finishes the
/// mod's research (and its prerequisites), flattens the whole map to concrete, teleports the colonists and their
/// starter materials onto a central platform, then builds a gallery of a dozen small sealed rooms nearby
/// covering the various vent/manifold/network combinations someone would otherwise have to build by hand to
/// playtest. Control is then handed back to the player.
/// </summary>
public sealed class PlaygroundScenario : HarnessScenario
{
    private static readonly (string defName, int count)[] StarterResources =
    {
        ("Steel", 2000),
        ("ComponentIndustrial", 80),
        ("Chemfuel", 300),
        ("Neutroamine", 60),
        ("MedicineHerbal", 30),
        ("GV_CanisterEmpty", 20),
    };

    private const int RoomSize = 9;
    private const int RoomGap = 3;
    private const int GalleryCols = 4;

    public override string Name => "playground";

    public override bool IsPersistent => true;

    public override void Setup(Map map, CellRect area)
    {
        ResearchProjectDef research = DefDatabase<ResearchProjectDef>.GetNamed("GV_GasVentilation");
        // FinishProject recursively finishes unfinished prerequisites (Machining) first.
        Find.ResearchManager.FinishProject(research, doCompletionDialog: false, researcher: null, doCompletionLetter: false);

        Log.Message("[GasVentHarness] playground: flattening map to concrete...");
        HarnessUtil.FlattenMap(map);

        IntVec3 platform = map.Center;
        List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
        for (int i = 0; i < colonists.Count; i++)
        {
            HarnessUtil.Teleport(colonists[i], platform + new IntVec3(i - colonists.Count / 2, 0, -2), map);
        }
        for (int i = 0; i < StarterResources.Length; i++)
        {
            DropStacks(DefDatabase<ThingDef>.GetNamed(StarterResources[i].defName), StarterResources[i].count, platform, map);
        }

        List<string> rooms = BuildGallery(map, platform);

        HarnessUtil.JumpCamera(platform);
        string text =
            "Gas Ventilation dev playground (DevHarness only):\n\n" +
            "- Gas ventilation research (and its prerequisites) is finished.\n" +
            "- The whole map is flattened to concrete, unroofed and unfogged; every natural obstruction was removed.\n" +
            $"- Your colonists and a stack of starter materials (steel, components, chemfuel, neutroamine, herbal " +
            $"medicine, empty canisters) are on the platform at {platform.x}, {platform.z}.\n" +
            "- A gallery of test rooms sits just north of the platform, one per combination:\n" +
            string.Join("\n", rooms) +
            "\n\nThe game is paused and fully yours. Launch with play.ps1 -NoPlayground for an untouched map.";
        Find.LetterStack.ReceiveLetter("Gas ventilation playground ready", text, LetterDefOf.PositiveEvent, new LookTargets(new TargetInfo(platform, map)));
        Log.Message($"[GasVentHarness] playground: research finished, platform at {platform}, {rooms.Count} test rooms built");
    }

    public override ScenarioStatus Tick(Map map, int ticksSinceSetup, List<string> failures)
    {
        // Never called: the runner hands a persistent scenario to the player right after Setup.
        return ScenarioStatus.Running;
    }

    /// <summary>Builds the room grid north of <paramref name="platform"/> and returns one description line per room.</summary>
    private static List<string> BuildGallery(Map map, IntVec3 platform)
    {
        GasDef toxin = GVDefOf.GV_Gas_Toxin;
        GasDef sedative = GVDefOf.GV_Gas_Sedative;
        GasDef haywire = GVDefOf.GV_Gas_Haywire;
        GasDef insecticide = GVDefOf.GV_Gas_Insecticide;

        int span = GalleryCols * (RoomSize + RoomGap);
        IntVec3 galleryOrigin = new IntVec3(platform.x - span / 2, 0, platform.z + 6);
        List<string> descriptions = new List<string>();
        int index = 0;

        descriptions.Add(Describe(BuildBasicRoom(map, RoomOuter(galleryOrigin, index++), toxin, 4f, VentMode.On, VentGasSelection.Toxin, "GV_VentWall"),
            "toxin manifold (full) -> wall vent, On"));
        descriptions.Add(Describe(BuildBasicRoom(map, RoomOuter(galleryOrigin, index++), sedative, 4f, VentMode.On, VentGasSelection.Sedative, "GV_VentWall"),
            "sedative manifold (full) -> wall vent, On"));
        descriptions.Add(Describe(BuildBasicRoom(map, RoomOuter(galleryOrigin, index++), haywire, 4f, VentMode.On, VentGasSelection.Haywire, "GV_VentWall"),
            "haywire manifold (full) -> wall vent, On"));
        descriptions.Add(Describe(BuildBasicRoom(map, RoomOuter(galleryOrigin, index++), insecticide, 4f, VentMode.On, VentGasSelection.Insecticide, "GV_VentWall"),
            "insecticide manifold (full) -> wall vent, On"));

        descriptions.Add(Describe(BuildBasicRoom(map, RoomOuter(galleryOrigin, index++), toxin, 4f, VentMode.On, VentGasSelection.Toxin, "GV_VentCeiling"),
            "toxin manifold -> ceiling vent, On"));
        descriptions.Add(Describe(BuildBasicRoom(map, RoomOuter(galleryOrigin, index++), toxin, 4f, VentMode.On, VentGasSelection.Toxin, "GV_VentFloor"),
            "toxin manifold -> floor vent, On"));
        descriptions.Add(Describe(BuildBasicRoom(map, RoomOuter(galleryOrigin, index++), toxin, 4f, VentMode.On, VentGasSelection.Toxin, "GV_VentMounted"),
            "toxin manifold -> wall-mounted vent, On"));
        descriptions.Add(Describe(BuildBasicRoom(map, RoomOuter(galleryOrigin, index++), sedative, 2.5f, VentMode.Off, VentGasSelection.None, "GV_VentWall"),
            "sedative manifold, partially filled (2.5/4), vent Off -- switch gas or deconstruct to see a real partial canister eject"));

        descriptions.Add(BuildMultiGasRoom(map, RoomOuter(galleryOrigin, index++), toxin, haywire));
        descriptions.Add(BuildContentionRoom(map, RoomOuter(galleryOrigin, index++), toxin));
        descriptions.Add(BuildSensorRoom(map, RoomOuter(galleryOrigin, index++), sedative));
        descriptions.Add(BuildRestockRoom(map, RoomOuter(galleryOrigin, index++), toxin, sedative, haywire, insecticide));

        return descriptions;
    }

    private static CellRect RoomOuter(IntVec3 origin, int index)
    {
        int col = index % GalleryCols;
        int row = index / GalleryCols;
        int step = RoomSize + RoomGap;
        return new CellRect(origin.x + col * step, origin.z + row * step, RoomSize, RoomSize);
    }

    /// <summary>
    /// Sealed room fed by one manifold through a short pipe run south of the room. GV_VentWall replaces the
    /// south wall cell directly, same as the pipe run's other regression scenarios. Every other vent shape
    /// (ceiling/floor/mounted) doesn't sit on the boundary -- it goes one cell inside instead, bridged across
    /// the wall by a hidden pipe on the wall cell itself, the same connection method their own flavour text
    /// describes ("hidden pipes under the neighbouring walls/floor").
    /// </summary>
    private static Thing BuildBasicRoom(Map map, CellRect outer, GasDef gas, float fill, VentMode mode, VentGasSelection selection, string ventDefName)
    {
        HarnessUtil.BuildSealedRoom(map, outer, roofed: true);
        int x = outer.CenterCell.x;
        ThingDef pipe = DefDatabase<ThingDef>.GetNamed("GV_Pipe");
        HarnessUtil.SpawnBuilding(pipe, new IntVec3(x, 0, outer.minZ - 1), map, Rot4.North);
        HarnessUtil.SpawnBuilding(pipe, new IntVec3(x, 0, outer.minZ - 2), map, Rot4.North);
        Thing ventThing;
        if (ventDefName == "GV_VentWall")
        {
            ventThing = HarnessUtil.SpawnBuilding(DefDatabase<ThingDef>.GetNamed(ventDefName), new IntVec3(x, 0, outer.minZ), map, Rot4.North);
        }
        else
        {
            HarnessUtil.SpawnBuilding(GVDefOf.GV_HiddenPipe, new IntVec3(x, 0, outer.minZ), map, Rot4.North);
            ventThing = HarnessUtil.SpawnBuilding(DefDatabase<ThingDef>.GetNamed(ventDefName), new IntVec3(x, 0, outer.minZ + 1), map, Rot4.North);
        }
        Thing manifoldThing = HarnessUtil.SpawnBuilding(DefDatabase<ThingDef>.GetNamed("GV_Manifold"), new IntVec3(x, 0, outer.minZ - 3), map, Rot4.North);
        CompGasManifold manifold = manifoldThing.TryGetComp<CompGasManifold>();
        manifold.SetActiveGas(gas);
        if (fill > 0f)
        {
            manifold.AddResource(fill);
        }
        CompGasVentController controller = ventThing.TryGetComp<CompGasVentController>();
        controller.ToggleGas(selection);
        controller.SetMode(mode);
        return manifoldThing;
    }

    /// <summary>Two manifolds (different gases), one vent with both toggled on -- confirms independent draw per network.</summary>
    private static string BuildMultiGasRoom(Map map, CellRect outer, GasDef gasA, GasDef gasB)
    {
        HarnessUtil.BuildSealedRoom(map, outer, roofed: true);
        int x = outer.CenterCell.x;
        ThingDef pipe = DefDatabase<ThingDef>.GetNamed("GV_Pipe");
        Thing ventThing = HarnessUtil.SpawnBuilding(DefDatabase<ThingDef>.GetNamed("GV_VentWall"), new IntVec3(x, 0, outer.minZ), map, Rot4.North);
        HarnessUtil.SpawnBuilding(pipe, new IntVec3(x - 1, 0, outer.minZ - 1), map, Rot4.North);
        HarnessUtil.SpawnBuilding(pipe, new IntVec3(x + 1, 0, outer.minZ - 1), map, Rot4.North);
        CompGasManifold a = HarnessUtil.SpawnBuilding(DefDatabase<ThingDef>.GetNamed("GV_Manifold"), new IntVec3(x - 1, 0, outer.minZ - 2), map, Rot4.North).TryGetComp<CompGasManifold>();
        CompGasManifold b = HarnessUtil.SpawnBuilding(DefDatabase<ThingDef>.GetNamed("GV_Manifold"), new IntVec3(x + 1, 0, outer.minZ - 2), map, Rot4.North).TryGetComp<CompGasManifold>();
        a.SetActiveGas(gasA);
        a.AddResource(4f);
        b.SetActiveGas(gasB);
        b.AddResource(4f);
        CompGasVentController controller = ventThing.TryGetComp<CompGasVentController>();
        controller.ToggleGas(gasA.ventFlag);
        controller.ToggleGas(gasB.ventFlag);
        controller.SetMode(VentMode.On);
        return Describe(ventThing, $"one vent outputting both {gasA.label} and {gasB.label} at once, fed by two manifolds");
    }

    /// <summary>One manifold, two vents on the same network -- confirms proportional throughput sharing under contention.</summary>
    private static string BuildContentionRoom(Map map, CellRect outer, GasDef gas)
    {
        HarnessUtil.BuildSealedRoom(map, outer, roofed: true);
        int x = outer.CenterCell.x;
        ThingDef pipe = DefDatabase<ThingDef>.GetNamed("GV_Pipe");
        Thing ventA = HarnessUtil.SpawnBuilding(DefDatabase<ThingDef>.GetNamed("GV_VentWall"), new IntVec3(x, 0, outer.minZ), map, Rot4.North);
        Thing ventB = HarnessUtil.SpawnBuilding(DefDatabase<ThingDef>.GetNamed("GV_VentMounted"), new IntVec3(outer.minX, 0, outer.CenterCell.z), map, Rot4.East);
        HarnessUtil.SpawnBuilding(pipe, new IntVec3(x, 0, outer.minZ - 1), map, Rot4.North);
        HarnessUtil.SpawnBuilding(pipe, new IntVec3(x, 0, outer.minZ - 2), map, Rot4.North);
        CompGasManifold manifold = HarnessUtil.SpawnBuilding(DefDatabase<ThingDef>.GetNamed("GV_Manifold"), new IntVec3(x, 0, outer.minZ - 3), map, Rot4.North).TryGetComp<CompGasManifold>();
        manifold.SetActiveGas(gas);
        manifold.AddResource(2f);
        foreach (Thing vent in new[] { ventA, ventB })
        {
            CompGasVentController controller = vent.TryGetComp<CompGasVentController>();
            controller.ToggleGas(gas.ventFlag);
            controller.SetMode(VentMode.On);
        }
        return Describe(manifold.parent, $"one {gas.label} manifold (only 2/4 canisters) feeding two vents at once -- throughput contention");
    }

    /// <summary>Sensor-controlled vent plus an armed intruder sensor, for manually walking a hostile in and watching it trigger.</summary>
    private static string BuildSensorRoom(Map map, CellRect outer, GasDef gas)
    {
        CellRect inner = HarnessUtil.BuildSealedRoom(map, outer, roofed: true);
        int x = outer.CenterCell.x;
        Thing ventThing = HarnessUtil.SpawnBuilding(DefDatabase<ThingDef>.GetNamed("GV_VentWall"), new IntVec3(x, 0, outer.minZ), map, Rot4.North);
        ThingDef pipe = DefDatabase<ThingDef>.GetNamed("GV_Pipe");
        HarnessUtil.SpawnBuilding(pipe, new IntVec3(x, 0, outer.minZ - 1), map, Rot4.North);
        HarnessUtil.SpawnBuilding(pipe, new IntVec3(x, 0, outer.minZ - 2), map, Rot4.North);
        CompGasManifold manifold = HarnessUtil.SpawnBuilding(DefDatabase<ThingDef>.GetNamed("GV_Manifold"), new IntVec3(x, 0, outer.minZ - 3), map, Rot4.North).TryGetComp<CompGasManifold>();
        manifold.SetActiveGas(gas);
        manifold.AddResource(4f);
        HarnessUtil.SpawnBuilding(DefDatabase<ThingDef>.GetNamed("GV_IntruderSensor"), new IntVec3(x, 0, inner.maxZ), map, Rot4.North);
        CompGasVentController controller = ventThing.TryGetComp<CompGasVentController>();
        controller.ToggleGas(gas.ventFlag);
        controller.SetMode(VentMode.Sensor);
        return Describe(ventThing, $"{gas.label} sensor room -- vent stays idle until a hostile walks in");
    }

    /// <summary>Unconfigured manifold plus a stack of full canisters for every gas, for testing reconfigure/refill jobs by hand.</summary>
    private static string BuildRestockRoom(Map map, CellRect outer, params GasDef[] gases)
    {
        HarnessUtil.BuildSealedRoom(map, outer, roofed: true);
        Thing manifoldThing = HarnessUtil.SpawnBuilding(DefDatabase<ThingDef>.GetNamed("GV_Manifold"), outer.CenterCell, map, Rot4.North);
        IntVec3 stockCell = outer.CenterCell + new IntVec3(0, 0, -2);
        for (int i = 0; i < gases.Length; i++)
        {
            Thing canister = ThingMaker.MakeThing(gases[i].canister);
            canister.stackCount = 4;
            GenPlace.TryPlaceThing(canister, stockCell, map, ThingPlaceMode.Near);
        }
        return Describe(manifoldThing, "unconfigured manifold plus 4 full canisters of every gas -- pick a gas, then let a colonist reconfigure and refill it");
    }

    private static string Describe(Thing thing, string text)
    {
        return $"  - {thing.Position.x}, {thing.Position.z}: {text}";
    }

    private static void DropStacks(ThingDef def, int total, IntVec3 near, Map map)
    {
        while (total > 0)
        {
            Thing thing = ThingMaker.MakeThing(def);
            thing.stackCount = total < def.stackLimit ? total : def.stackLimit;
            total -= thing.stackCount;
            GenPlace.TryPlaceThing(thing, near, map, ThingPlaceMode.Near);
        }
    }
}
