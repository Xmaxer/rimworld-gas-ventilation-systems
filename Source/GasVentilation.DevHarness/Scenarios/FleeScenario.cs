using System.Collections.Generic;
using RimWorld;
using Verse;

namespace GasVentilation.DevHarness.Scenarios;

/// <summary>A colonist in a toxin-filled room with an open doorway walks out of the gas.</summary>
public sealed class FleeScenario : HarnessScenario
{
    private CellRect inner;
    private Pawn colonist;

    public override string Name => "flee";

    public override int TimeoutTicks => 1500;

    public override void Setup(Map map, CellRect area)
    {
        CellRect outer = new CellRect(area.minX + 4, area.minZ + 4, 9, 9);
        inner = HarnessUtil.BuildSealedRoom(map, outer, roofed: true);
        // Knock a doorway in the east wall so there is somewhere to go.
        IntVec3 doorway = new IntVec3(outer.maxX, 0, outer.CenterCell.z);
        doorway.GetEdifice(map)?.Destroy(DestroyMode.Vanish);
        colonist = HarnessUtil.SpawnPawn(PawnKindDefOf.Colonist, Faction.OfPlayer, new IntVec3(inner.minX + 1, 0, inner.CenterCell.z), map);
        VentGasGrid grid = VentGasGrid.For(map);
        foreach (IntVec3 cell in inner)
        {
            grid.TryAddGas(cell, GVDefOf.GV_Gas_Toxin, 255);
        }
        HarnessUtil.JumpCamera(outer.CenterCell);
    }

    public override ScenarioStatus Tick(Map map, int ticksSinceSetup, List<string> failures)
    {
        if (ticksSinceSetup % 60 == 0 && ticksSinceSetup <= 900 && !colonist.Dead)
        {
            Log.Message($"[GasVentHarness] flee t{ticksSinceSetup}: pos={colonist.Position} job={colonist.CurJobDef?.defName} " +
                $"toxin={VentGasGrid.For(map).DensityAt(colonist.Position, GVDefOf.GV_Gas_Toxin)} downed={colonist.Downed}");
        }
        if (ticksSinceSetup == 120)
        {
            HarnessUtil.Screenshot("flee-t120");
        }
        if (ticksSinceSetup < 900)
        {
            return ScenarioStatus.Running;
        }
        if (colonist.Dead)
        {
            failures.Add("colonist died instead of escaping");
        }
        else if (VentGasGrid.For(map).DensityAt(colonist.Position, GVDefOf.GV_Gas_Toxin) > 40)
        {
            failures.Add($"colonist still in dense toxin at {colonist.Position}");
        }
        return failures.Count == 0 ? ScenarioStatus.Passed : ScenarioStatus.Failed;
    }
}
