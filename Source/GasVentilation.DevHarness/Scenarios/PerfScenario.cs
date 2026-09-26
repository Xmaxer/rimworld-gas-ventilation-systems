using System.Collections.Generic;
using RimWorld;
using Verse;

namespace GasVentilation.DevHarness.Scenarios;

/// <summary>
/// A 22x22 sealed room full of two gases, with 12 pawns and 16 emitting vents. Measures average microseconds per
/// tick for VentGasGrid and GasExposureScanner, then measures the idle cost after clearing all gas.
/// </summary>
public sealed class PerfScenario : HarnessScenario
{
    private const double GridBudgetMicros = 400;
    private const double ScannerBudgetMicros = 100;
    private const double IdleBudgetMicros = 10;

    private CellRect inner;
    private int phase;

    public override string Name => "perf";

    public override int TimeoutTicks => 4000;

    public override void Setup(Map map, CellRect area)
    {
        inner = HarnessUtil.BuildSealedRoom(map, new CellRect(area.minX + 1, area.minZ + 1, 22, 22), roofed: true);
        VentGasGrid grid = VentGasGrid.For(map);
        foreach (IntVec3 cell in inner)
        {
            grid.TryAddGas(cell, GVDefOf.GV_Gas_Toxin, 200);
            grid.TryAddGas(cell, GVDefOf.GV_Gas_Sedative, 120);
        }
        PawnKindDef muffalo = DefDatabase<PawnKindDef>.GetNamed("Muffalo");
        for (int i = 0; i < 12; i++)
        {
            Pawn animal = HarnessUtil.SpawnPawn(muffalo, null, inner.RandomCell, map);
            animal.SetFaction(Faction.OfPlayer);
        }
        ThingDef floorVent = DefDatabase<ThingDef>.GetNamed("GV_VentFloor_Haywire");
        for (int i = 0; i < 16; i++)
        {
            IntVec3 cell = new IntVec3(inner.minX + 1 + (i % 8) * 2, 0, inner.minZ + 2 + (i / 8) * 10);
            HarnessUtil.SpawnBuilding(floorVent, cell, map, Rot4.North).TryGetComp<CompGasVent>().SetMode(VentMode.On);
        }
        HarnessTiming.Reset();
    }

    public override ScenarioStatus Tick(Map map, int ticksSinceSetup, List<string> failures)
    {
        if (phase == 0 && ticksSinceSetup >= 2000)
        {
            double grid = HarnessTiming.MicrosPerCall(HarnessTiming.GridTicks, HarnessTiming.GridCalls);
            double scanner = HarnessTiming.MicrosPerCall(HarnessTiming.ScannerTicks, HarnessTiming.ScannerCalls);
            Metrics.Add($"active_grid_us_per_tick={grid:F1} (budget {GridBudgetMicros})");
            Metrics.Add($"active_scanner_us_per_tick={scanner:F1} (budget {ScannerBudgetMicros})");
            if (grid > GridBudgetMicros)
            {
                failures.Add($"VentGasGrid tick too slow: {grid:F1} us");
            }
            if (scanner > ScannerBudgetMicros)
            {
                failures.Add($"GasExposureScanner tick too slow: {scanner:F1} us");
            }
            List<Thing> vents = new List<Thing>(map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed("GV_VentFloor_Haywire")));
            for (int i = 0; i < vents.Count; i++)
            {
                vents[i].Destroy(DestroyMode.Vanish);
            }
            // Earlier scenarios in the same run may still have vents switched on; silence them so "idle" is idle.
            List<Building> buildings = map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < buildings.Count; i++)
            {
                buildings[i].TryGetComp<CompGasVent>()?.SetMode(VentMode.Off);
            }
            VentGasGrid.For(map).ClearAll();
            HarnessTiming.Reset();
            phase = 1;
            return ScenarioStatus.Running;
        }
        if (phase == 1 && ticksSinceSetup >= 2600)
        {
            double idle = HarnessTiming.MicrosPerCall(HarnessTiming.GridTicks, HarnessTiming.GridCalls);
            Metrics.Add($"idle_grid_us_per_tick={idle:F2} (budget {IdleBudgetMicros})");
            if (idle > IdleBudgetMicros)
            {
                failures.Add($"idle VentGasGrid tick too slow: {idle:F2} us");
            }
            return failures.Count == 0 ? ScenarioStatus.Passed : ScenarioStatus.Failed;
        }
        return ScenarioStatus.Running;
    }
}
