using System.Collections.Generic;
using RimWorld;
using Verse;

namespace GasVentilation.DevHarness.Scenarios;

/// <summary>
/// Interactive dev convenience, not a test (tools/play.ps1 requests it by default). On a new game: finishes the
/// mod's research (and its prerequisites), drops starter materials next to the colonists, and builds one sealed
/// demo room in the harness corner area: toxin manifold (pre-filled) -> pipes -> wall vent set to On. Then the
/// runner hands control back to the player.
/// </summary>
public sealed class PlaygroundScenario : HarnessScenario
{
    private static readonly (string defName, int count)[] StarterResources =
    {
        ("Steel", 1000),
        ("ComponentIndustrial", 40),
        ("Chemfuel", 300),
        ("Neutroamine", 60),
        ("MedicineHerbal", 30),
        ("GV_CanisterEmpty", 12),
    };

    public override string Name => "playground";

    public override bool IsPersistent => true;

    public override void Setup(Map map, CellRect area)
    {
        ResearchProjectDef research = DefDatabase<ResearchProjectDef>.GetNamed("GV_GasVentilation");
        // FinishProject recursively finishes unfinished prerequisites (Machining) first.
        Find.ResearchManager.FinishProject(research, doCompletionDialog: false, researcher: null, doCompletionLetter: false);

        IntVec3 dropCell = map.Center;
        List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
        if (colonists.Count > 0)
        {
            dropCell = colonists[0].Position;
        }
        for (int i = 0; i < StarterResources.Length; i++)
        {
            DropStacks(DefDatabase<ThingDef>.GetNamed(StarterResources[i].defName), StarterResources[i].count, dropCell, map);
        }

        // area is the harness corner rect (ReserveArea index 0, near map cell 10,10), far from the colonists.
        CellRect outer = new CellRect(area.minX + 6, area.minZ + 8, 9, 9);
        CellRect inner = HarnessUtil.BuildSealedRoom(map, outer, roofed: true);
        int x = outer.CenterCell.x;
        Thing vent = HarnessUtil.SpawnBuilding(DefDatabase<ThingDef>.GetNamed("GV_VentWall"), new IntVec3(x, 0, outer.minZ), map, Rot4.North);
        ThingDef pipe = DefDatabase<ThingDef>.GetNamed("GV_Pipe");
        HarnessUtil.SpawnBuilding(pipe, new IntVec3(x, 0, outer.minZ - 1), map, Rot4.North);
        HarnessUtil.SpawnBuilding(pipe, new IntVec3(x, 0, outer.minZ - 2), map, Rot4.North);
        Thing manifold = HarnessUtil.SpawnBuilding(DefDatabase<ThingDef>.GetNamed("GV_Manifold"), new IntVec3(x, 0, outer.minZ - 3), map, Rot4.North);
        CompGasManifold manifoldComp = manifold.TryGetComp<CompGasManifold>();
        manifoldComp.SetActiveGas(GVDefOf.GV_Gas_Toxin);
        manifoldComp.AddResource(4f);
        CompGasVentController ventController = vent.TryGetComp<CompGasVentController>();
        ventController.ToggleGas(VentGasSelection.Toxin);
        ventController.SetMode(VentMode.On);

        IntVec3 room = inner.CenterCell;
        string text =
            "Gas Ventilation dev playground (DevHarness only):\n\n" +
            "- Gas ventilation research (and its prerequisites) is finished.\n" +
            $"- Starter materials were dropped next to your colonists at {dropCell.x}, {dropCell.z}: steel, components, " +
            "chemfuel, neutroamine, herbal medicine and empty gas canisters.\n" +
            $"- A sealed demo room is at {room.x}, {room.z} (south-west corner of the map): a toxin manifold with 4 canisters " +
            "feeds a wall vent set to On through a short pipe. The room fills with toxic gas; don't open it with colonists nearby.\n\n" +
            "The game is paused and fully yours. Launch with play.ps1 -NoPlayground for an untouched map.";
        Find.LetterStack.ReceiveLetter("Gas ventilation playground ready", text, LetterDefOf.PositiveEvent, new LookTargets(manifold));
        Log.Message($"[GasVentHarness] playground: research finished, resources dropped at {dropCell}, demo room at {room}");
    }

    public override ScenarioStatus Tick(Map map, int ticksSinceSetup, List<string> failures)
    {
        // Never called: the runner hands a persistent scenario to the player right after Setup.
        return ScenarioStatus.Running;
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
