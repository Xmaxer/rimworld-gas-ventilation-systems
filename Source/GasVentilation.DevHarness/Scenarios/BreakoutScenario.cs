using System.Collections.Generic;
using RimWorld;
using Verse;

namespace GasVentilation.DevHarness.Scenarios;

/// <summary>A hostile pawn sealed in a wooden room full of toxin damages the walls to escape.</summary>
public sealed class BreakoutScenario : HarnessScenario
{
    private CellRect outer;
    private Pawn raider;

    public override string Name => "breakout";

    public override int TimeoutTicks => 5000;

    public override void Setup(Map map, CellRect area)
    {
        outer = new CellRect(area.minX + 4, area.minZ + 4, 7, 7);
        CellRect inner = HarnessUtil.BuildSealedRoom(map, outer, roofed: true, wallStuff: ThingDefOf.WoodLog);
        Faction enemy = Find.FactionManager.RandomEnemyFaction(false, false, false) ?? Faction.OfPirates;
        raider = HarnessUtil.SpawnPawn(PawnKindDefOf.Villager, enemy, inner.CenterCell, map);
        VentGasGrid grid = VentGasGrid.For(map);
        foreach (IntVec3 cell in inner)
        {
            grid.TryAddGas(cell, GVDefOf.GV_Gas_Toxin, 255);
        }
        HarnessUtil.JumpCamera(outer.CenterCell);
    }

    public override ScenarioStatus Tick(Map map, int ticksSinceSetup, List<string> failures)
    {
        if (ticksSinceSetup % 60 != 0)
        {
            return ScenarioStatus.Running;
        }
        if (ticksSinceSetup <= 1200 && !raider.Dead)
        {
            Log.Message($"[GasVentHarness] breakout t{ticksSinceSetup}: pos={raider.Position} job={raider.CurJobDef?.defName} " +
                $"target={raider.CurJob?.targetA} downed={raider.Downed} " +
                $"toxin={VentGasGrid.For(map).DensityAt(raider.Position, GVDefOf.GV_Gas_Toxin)}");
        }
        if (ticksSinceSetup < 600)
        {
            return ScenarioStatus.Running;
        }
        foreach (IntVec3 cell in outer.EdgeCells)
        {
            Building wall = cell.GetEdifice(map);
            if (wall == null || wall.HitPoints < wall.MaxHitPoints)
            {
                Log.Message($"[GasVentHarness] breakout: wall at {cell} damaged or gone after {ticksSinceSetup} ticks");
                return ScenarioStatus.Passed;
            }
        }
        if (raider.Dead || raider.Downed)
        {
            failures.Add("raider was downed or killed before damaging a wall");
            return ScenarioStatus.Failed;
        }
        return ScenarioStatus.Running;
    }
}
