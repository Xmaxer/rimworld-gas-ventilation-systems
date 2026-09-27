using System.Collections.Generic;
using Verse;

namespace GasVentilation.DevHarness.Scenarios;

/// <summary>
/// Manifold (4 sedative canisters) -> visible pipes -> wall vent (mode On) in the south wall of a sealed room.
/// Expects gas in the room, canisters drained, at least one empty shell ejected, and no gas outside the room.
/// The manifold is switched Toxin -> Haywire -> Sedative before filling, through the same RequestGasChange
/// entry point the player's gizmo uses (under God mode, so it applies instantly) -- regression coverage for a
/// real bug where repeated gas switching left a manifold simultaneously registered in multiple gas networks
/// (see docs/implementation-notes.md, "PipeNet cross-registration bug").
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
        // Real player entry point (RequestGasChange under God mode), not a direct SetActiveGas call, and
        // switched twice before settling on Sedative -- see the class doc comment.
        bool priorGodMode = Verse.DebugSettings.godMode;
        Verse.DebugSettings.godMode = true;
        manifold.RequestGasChange(GVDefOf.GV_Gas_Toxin);
        manifold.RequestGasChange(GVDefOf.GV_Gas_Haywire);
        manifold.RequestGasChange(GVDefOf.GV_Gas_Sedative);
        Verse.DebugSettings.godMode = priorGodMode;
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
        // 2400 previously left a razor-thin margin: AmountStored crosses below 3 canisters only shortly
        // before this point, and the manifold's own CompTickRare (which ejects the empty shell once bodies
        // drops) fires on its own ~250-tick offset -- so the check could run before that catch-up tick, even
        // though nothing was actually wrong (confirmed against the prior commit: same failure, unrelated to
        // this change). 2650 gives at least one full rare-tick cycle of headroom after the crossing.
        if (ticksSinceSetup < 2650)
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
        int toxinInside = 0;
        foreach (IntVec3 cell in inner)
        {
            toxinInside += grid.DensityAt(cell, GVDefOf.GV_Gas_Toxin);
        }
        if (toxinInside > 0)
        {
            failures.Add($"cross-network leak: toxin density {toxinInside} in a sedative-only setup");
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
