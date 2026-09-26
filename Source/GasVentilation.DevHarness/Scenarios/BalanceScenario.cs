using System.Collections.Generic;
using RimWorld;
using Verse;

namespace GasVentilation.DevHarness.Scenarios;

/// <summary>
/// Measures the balance targets from the spec (section 13). It always passes; the values are reported as metrics
/// and compared in M7 Task 4.
/// Rooms: S sedative + colonist, T toxin + colonist, H haywire + scyther, I insecticide + megaspider,
/// F empty 7x7 room fed by a toxin wall vent (fill cost and linger).
/// </summary>
public sealed class BalanceScenario : HarnessScenario
{
    private readonly List<(CellRect room, GasDef gas)> saturated = new List<(CellRect, GasDef)>();
    private Pawn sedated;
    private Pawn toxic;
    private Pawn mech;
    private Pawn spider;
    private CellRect fillRoom;
    private CompGasVent fillVent;
    private CompGasManifold fillManifold;
    private int sedationTick = -1;
    private int lungTick = -1;
    private int criticalTick = -1;
    private int mechDeathTick = -1;
    private bool spiderMeasured;
    private int fillTick = -1;
    private float fillCanisters;
    private int lingerTick = -1;

    /// <summary>
    /// Room average that counts as "full". One wall vent cannot push a 7x7 room's average much past 80% against
    /// dissipation (vanilla-style diffusion needs a gradient to carry gas away from the vent), so 75% is used.
    /// </summary>
    private const float FillFraction = 0.75f;

    public override string Name => "balance";

    public override int TimeoutTicks => 26000;

    public override void Setup(Map map, CellRect area)
    {
        CellRect s = Room(map, area.minX, area.minZ, 7);
        CellRect t = Room(map, area.minX + 8, area.minZ, 7);
        CellRect h = Room(map, area.minX + 16, area.minZ, 7);
        CellRect i = Room(map, area.minX, area.minZ + 8, 7);
        CellRect fOuter = new CellRect(area.minX + 8, area.minZ + 8, 9, 9);
        fillRoom = HarnessUtil.BuildSealedRoom(map, fOuter, roofed: true);

        sedated = HarnessUtil.SpawnPawn(PawnKindDefOf.Colonist, Faction.OfPlayer, s.CenterCell, map);
        toxic = HarnessUtil.SpawnPawn(PawnKindDefOf.Colonist, Faction.OfPlayer, t.CenterCell, map);
        mech = HarnessUtil.SpawnPawn(DefDatabase<PawnKindDef>.GetNamed("Mech_Scyther"), Faction.OfMechanoids, h.CenterCell, map);
        spider = HarnessUtil.SpawnPawn(DefDatabase<PawnKindDef>.GetNamed("Megaspider"), null, i.CenterCell, map);
        spider.SetFaction(Faction.OfPlayer);
        saturated.Add((s, GVDefOf.GV_Gas_Sedative));
        saturated.Add((t, GVDefOf.GV_Gas_Toxin));
        saturated.Add((h, GVDefOf.GV_Gas_Haywire));
        saturated.Add((i, GVDefOf.GV_Gas_Insecticide));

        IntVec3 ventCell = new IntVec3(fOuter.maxX, 0, fOuter.CenterCell.z);
        fillVent = HarnessUtil.SpawnBuilding(DefDatabase<ThingDef>.GetNamed("GV_VentWall_Toxin"), ventCell, map, Rot4.West).TryGetComp<CompGasVent>();
        ThingDef pipe = DefDatabase<ThingDef>.GetNamed("GV_Pipe_Toxin");
        HarnessUtil.SpawnBuilding(pipe, ventCell + IntVec3.East, map, Rot4.North);
        HarnessUtil.SpawnBuilding(pipe, ventCell + IntVec3.East * 2, map, Rot4.North);
        fillManifold = HarnessUtil.SpawnBuilding(DefDatabase<ThingDef>.GetNamed("GV_Manifold_Toxin"), ventCell + IntVec3.East * 3, map, Rot4.North).TryGetComp<CompGasManifold>();
        fillManifold.AddResource(4f);
        fillVent.SetMode(VentMode.On);
    }

    public override ScenarioStatus Tick(Map map, int ticksSinceSetup, List<string> failures)
    {
        VentGasGrid grid = VentGasGrid.For(map);
        if (ticksSinceSetup % 30 == 0)
        {
            for (int r = 0; r < saturated.Count; r++)
            {
                foreach (IntVec3 cell in saturated[r].room)
                {
                    grid.TryAddGas(cell, saturated[r].gas, 255);
                }
            }
        }
        if (sedationTick < 0 && sedated.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed("GV_GasSedation")) != null)
        {
            sedationTick = ticksSinceSetup;
            Metrics.Add($"sedative_knockout_ticks={sedationTick} (target 600-1200)");
        }
        if (lungTick < 0 && (toxic.Dead || LungMissing(toxic)))
        {
            lungTick = ticksSinceSetup;
            Metrics.Add($"toxin_first_lung_destroyed_ticks={lungTick} (target 2000-4000){(toxic.Dead ? " [pawn died]" : "")}");
        }
        if (criticalTick < 0 && Severity(mech, "GV_Haywire") >= 0.85f)
        {
            criticalTick = ticksSinceSetup;
            Metrics.Add($"haywire_scyther_critical_ticks={criticalTick} (target 1200-2400)");
        }
        if (mechDeathTick < 0 && mech.Dead)
        {
            mechDeathTick = ticksSinceSetup;
            Metrics.Add($"haywire_scyther_destroyed_ticks={mechDeathTick}");
        }
        if (!spiderMeasured && ticksSinceSetup >= 5000)
        {
            spiderMeasured = true;
            float health = spider.Dead ? 0f : spider.health.summaryHealth.SummaryHealthPercent;
            Metrics.Add($"insecticide_megaspider_health_at_5000={health:F2} alive={!spider.Dead} (target alive, health <= 0.70)");
        }
        if (ticksSinceSetup % 60 == 0)
        {
            float average = AverageDensity(grid, fillRoom, GVDefOf.GV_Gas_Toxin);
            if (fillTick < 0 && average >= FillFraction * 255f)
            {
                fillTick = ticksSinceSetup;
                fillCanisters = 4f - fillManifold.AmountStored;
                Metrics.Add($"fill_7x7_ticks={fillTick} canisters_used={fillCanisters:F2} (target canisters 0.8-1.3)");
                fillVent.SetMode(VentMode.Off);
            }
            else if (fillTick >= 0 && lingerTick < 0 && average < 0.1f * 255f)
            {
                lingerTick = ticksSinceSetup - fillTick;
                Metrics.Add($"linger_7x7_to_10pct_ticks={lingerTick} (target 1800-4000)");
            }
        }
        if (ticksSinceSetup % 1000 == 0)
        {
            LogState(ticksSinceSetup, grid);
        }
        bool done = sedationTick >= 0 && lungTick >= 0 && criticalTick >= 0 && spiderMeasured && lingerTick >= 0;
        if (done)
        {
            return ScenarioStatus.Passed;
        }
        if (ticksSinceSetup >= TimeoutTicks - 1)
        {
            Metrics.Add("incomplete: some targets were not reached within the timeout");
            return ScenarioStatus.Passed;
        }
        return ScenarioStatus.Running;
    }

    /// <summary>Progress log for tuning: exposure severities, fill level and positions (mechs may break out).</summary>
    private void LogState(int ticks, VentGasGrid grid)
    {
        Log.Message($"[GasVentHarness] balance t{ticks}: sedExp={Severity(sedated, "GV_SedativeExposure"):F2} "
            + $"toxExp={Severity(toxic, "GV_ToxinExposure"):F2} toxicHealth={(toxic.Dead ? 0f : toxic.health.summaryHealth.SummaryHealthPercent):F2} "
            + $"haywire={Severity(mech, "GV_Haywire"):F2} mechDead={mech.Dead} mechPos={(mech.Spawned ? mech.Position.ToString() : "-")} "
            + $"insect={Severity(spider, "GV_InsecticideExposure"):F2} spiderHealth={(spider.Dead ? 0f : spider.health.summaryHealth.SummaryHealthPercent):F2} "
            + $"toxicHediffs=[{string.Join(", ", toxic.health.hediffSet.hediffs.ConvertAll(x => x.def.defName + (x.Part != null ? "@" + x.Part.def.defName : string.Empty)))}] "
            + $"fillAvg={AverageDensity(grid, fillRoom, GVDefOf.GV_Gas_Toxin):F0} stored={fillManifold.AmountStored:F2}");
    }

    private static CellRect Room(Map map, int x, int z, int size)
    {
        return HarnessUtil.BuildSealedRoom(map, new CellRect(x, z, size, size), roofed: true);
    }

    private static float Severity(Pawn pawn, string hediff)
    {
        Hediff h = pawn.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed(hediff));
        return h?.Severity ?? 0f;
    }

    private static bool LungMissing(Pawn pawn)
    {
        List<BodyPartRecord> parts = pawn.RaceProps.body.GetPartsWithTag(BodyPartTagDefOf.BreathingSource);
        for (int i = 0; i < parts.Count; i++)
        {
            if (pawn.health.hediffSet.PartIsMissing(parts[i]))
            {
                return true;
            }
        }
        return false;
    }

    private static float AverageDensity(VentGasGrid grid, CellRect room, GasDef gas)
    {
        int total = 0;
        int count = 0;
        foreach (IntVec3 cell in room)
        {
            total += grid.DensityAt(cell, gas);
            count++;
        }
        return count == 0 ? 0f : (float)total / count;
    }
}
