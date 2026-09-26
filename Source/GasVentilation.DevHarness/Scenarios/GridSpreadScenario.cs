using System.Collections.Generic;
using GasVentilation.Core;
using Verse;

namespace GasVentilation.DevHarness.Scenarios;

/// <summary>
/// Sealed, roofed 9x9 room. Toxin in the middle and the other three gases in corners.
/// Checks spread, containment by walls, and takes a screenshot for the tint check.
/// </summary>
public sealed class GridSpreadScenario : HarnessScenario
{
    private CellRect inner;
    private CellRect outer;

    public override string Name => "grid";

    public override int TimeoutTicks => 1000;

    public override void Setup(Map map, CellRect area)
    {
        outer = new CellRect(area.minX + 2, area.minZ + 2, 11, 11);
        inner = HarnessUtil.BuildSealedRoom(map, outer, roofed: true);
        VentGasGrid grid = VentGasGrid.For(map);
        IntVec3 center = inner.CenterCell;
        foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, 1.5f, true))
        {
            grid.TryAddGas(cell, GVDefOf.GV_Gas_Toxin, 255);
        }
        grid.TryAddGas(new IntVec3(inner.minX, 0, inner.minZ), GVDefOf.GV_Gas_Sedative, 255);
        grid.TryAddGas(new IntVec3(inner.maxX, 0, inner.minZ), GVDefOf.GV_Gas_Haywire, 255);
        grid.TryAddGas(new IntVec3(inner.minX, 0, inner.maxZ), GVDefOf.GV_Gas_Insecticide, 255);
        HarnessUtil.JumpCamera(center);
    }

    public override ScenarioStatus Tick(Map map, int ticksSinceSetup, List<string> failures)
    {
        if (ticksSinceSetup == 90)
        {
            HarnessUtil.Screenshot("grid-t90");        }
        if (ticksSinceSetup < 300)
        {
            return ScenarioStatus.Running;
        }
        VentGasGrid grid = VentGasGrid.For(map);
        int toxinInside = 0;
        foreach (IntVec3 cell in inner)
        {
            toxinInside += grid.DensityAt(cell, GVDefOf.GV_Gas_Toxin);
        }
        if (toxinInside == 0)
        {
            failures.Add("no toxin left inside the room after 300 ticks");
        }
        if (grid.DensityAt(inner.CenterCell + new IntVec3(2, 0, 0), GVDefOf.GV_Gas_Toxin) == 0)
        {
            failures.Add("toxin did not spread two cells from the centre");
        }
        foreach (IntVec3 cell in outer.EdgeCells)
        {
            if (grid.PackedAt(cell) != 0u)
            {
                failures.Add($"gas inside a wall at {cell}");
            }
        }
        foreach (IntVec3 cell in outer.ExpandedBy(1).EdgeCells)
        {
            if (cell.InBounds(map) && grid.PackedAt(cell) != 0u)
            {
                failures.Add($"gas leaked outside the room at {cell}");
            }
        }
        return failures.Count == 0 ? ScenarioStatus.Passed : ScenarioStatus.Failed;
    }
}
