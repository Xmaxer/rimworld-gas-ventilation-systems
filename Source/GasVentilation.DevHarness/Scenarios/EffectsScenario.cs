using System.Collections.Generic;
using RimWorld;
using Verse;

namespace GasVentilation.DevHarness.Scenarios;

/// <summary>
/// Six sealed rooms kept saturated with gas. Checks that each gas affects its targets and spares non-targets.
/// A: human + toxin. B: human + sedative. C: scyther + haywire. D: megaspider + insecticide.
/// E1: scyther + sedative (immune). E2: human + haywire (immune). Separate rooms so nobody fights.
/// </summary>
public sealed class EffectsScenario : HarnessScenario
{
    private readonly List<(CellRect room, GasDef gas)> fills = new List<(CellRect, GasDef)>();
    private Pawn humanToxin;
    private Pawn humanSedative;
    private Pawn mechHaywire;
    private Pawn spider;
    private Pawn mechImmune;
    private Pawn humanImmune;

    public override string Name => "effects";

    public override int TimeoutTicks => 2000;

    public override void Setup(Map map, CellRect area)
    {
        PawnKindDef scyther = DefDatabase<PawnKindDef>.GetNamed("Mech_Scyther");
        PawnKindDef megaspider = DefDatabase<PawnKindDef>.GetNamed("Megaspider");

        CellRect a = Room(map, area, 0, 0);
        CellRect b = Room(map, area, 1, 0);
        CellRect c = Room(map, area, 2, 0);
        CellRect d = Room(map, area, 0, 1);
        CellRect e1 = Room(map, area, 1, 1);
        CellRect e2 = Room(map, area, 2, 1);

        humanToxin = HarnessUtil.SpawnPawn(PawnKindDefOf.Colonist, Faction.OfPlayer, a.CenterCell, map);
        humanSedative = HarnessUtil.SpawnPawn(PawnKindDefOf.Colonist, Faction.OfPlayer, b.CenterCell, map);
        mechHaywire = HarnessUtil.SpawnPawn(scyther, Faction.OfMechanoids, c.CenterCell, map);
        spider = HarnessUtil.SpawnPawn(megaspider, null, d.CenterCell, map);
        spider.SetFaction(Faction.OfPlayer);
        mechImmune = HarnessUtil.SpawnPawn(scyther, Faction.OfMechanoids, e1.CenterCell, map);
        humanImmune = HarnessUtil.SpawnPawn(PawnKindDefOf.Colonist, Faction.OfPlayer, e2.CenterCell, map);

        fills.Add((a, GVDefOf.GV_Gas_Toxin));
        fills.Add((b, GVDefOf.GV_Gas_Sedative));
        fills.Add((c, GVDefOf.GV_Gas_Haywire));
        fills.Add((d, GVDefOf.GV_Gas_Insecticide));
        fills.Add((e1, GVDefOf.GV_Gas_Sedative));
        fills.Add((e2, GVDefOf.GV_Gas_Haywire));
        HarnessUtil.JumpCamera(b.CenterCell);
    }

    public override ScenarioStatus Tick(Map map, int ticksSinceSetup, List<string> failures)
    {
        if (ticksSinceSetup % 30 == 0)
        {
            VentGasGrid grid = VentGasGrid.For(map);
            for (int i = 0; i < fills.Count; i++)
            {
                foreach (IntVec3 cell in fills[i].room)
                {
                    grid.TryAddGas(cell, fills[i].gas, 255);
                }
            }
        }
        if (ticksSinceSetup < 900)
        {
            return ScenarioStatus.Running;
        }
        LogState(humanToxin, "GV_ToxinExposure");
        LogState(humanSedative, "GV_SedativeExposure");
        LogState(mechHaywire, "GV_Haywire");
        LogState(spider, "GV_InsecticideExposure");
        LogState(mechImmune, "GV_SedativeExposure");
        LogState(humanImmune, "GV_Haywire");
        ExpectSeverity(humanToxin, "GV_ToxinExposure", 0.3f, failures);
        ExpectHediff(humanSedative, "GV_GasSedation", failures);
        if (!humanSedative.Downed)
        {
            failures.Add("sedated human is not downed");
        }
        ExpectSeverity(mechHaywire, "GV_Haywire", 0.3f, failures);
        ExpectSeverity(spider, "GV_InsecticideExposure", 0.2f, failures);
        ExpectNoHediff(mechImmune, "GV_SedativeExposure", failures);
        ExpectNoHediff(humanImmune, "GV_Haywire", failures);
        HarnessUtil.Screenshot("effects-t900");
        return failures.Count == 0 ? ScenarioStatus.Passed : ScenarioStatus.Failed;
    }

    private static CellRect Room(Map map, CellRect area, int col, int row)
    {
        CellRect outer = new CellRect(area.minX + 1 + col * 7, area.minZ + 1 + row * 7, 6, 6);
        return HarnessUtil.BuildSealedRoom(map, outer, roofed: true);
    }

    /// <summary>Logs the state at the check tick, so dead pawns (skipped by ExpectSeverity) and doses are visible for balancing.</summary>
    private static void LogState(Pawn pawn, string hediff)
    {
        Hediff h = pawn.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed(hediff));
        Log.Message($"[GasVentHarness] effects: {pawn.LabelShort} ({pawn.def.defName}) dead={pawn.Dead} downed={pawn.Downed} {hediff}={(h == null ? "none" : h.Severity.ToString("F2"))} hediffs=[{string.Join(", ", pawn.health.hediffSet.hediffs.ConvertAll(x => x.def.defName + (x.Part != null ? "@" + x.Part.def.defName : string.Empty)))}]");
    }

    private static void ExpectSeverity(Pawn pawn, string hediff, float min, List<string> failures)
    {
        if (pawn.Dead)
        {
            return;
        }
        Hediff h = pawn.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed(hediff));
        if (h == null || h.Severity < min)
        {
            failures.Add($"{pawn.LabelShort}: expected {hediff} >= {min}, got {(h == null ? "none" : h.Severity.ToString("F2"))}");
        }
    }

    private static void ExpectHediff(Pawn pawn, string hediff, List<string> failures)
    {
        if (pawn.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed(hediff)) == null)
        {
            failures.Add($"{pawn.LabelShort}: expected {hediff}");
        }
    }

    private static void ExpectNoHediff(Pawn pawn, string hediff, List<string> failures)
    {
        if (pawn.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed(hediff)) != null)
        {
            failures.Add($"{pawn.LabelShort}: should be immune to {hediff}");
        }
    }
}
