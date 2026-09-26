using RimWorld;
using Verse;

namespace GasVentilation;

public sealed class HediffCompProperties_HaywireCritical : HediffCompProperties
{
    public float minSeverity = 0.85f;
    public int stunIntervalTicks = 600;
    public float stunAmountMechanical = 15f;
    public float stunAmountSynthetic = 6f;
    public int berserkCheckIntervalTicks = 600;
    public float berserkChance = 0.5f;

    public HediffCompProperties_HaywireCritical()
    {
        compClass = typeof(HediffComp_HaywireCritical);
    }
}

/// <summary>
/// At critical haywire: EMP stun pulses routed through the vanilla stun handler (so EMP adaptation and VRE's EMP
/// vulnerability apply), and berserk chances (BerserkMechanoid for mechs, Berserk for humanlike androids).
/// </summary>
public sealed class HediffComp_HaywireCritical : HediffComp
{
    private int ticksUntilStun;
    private int ticksUntilBerserkCheck;

    public HediffCompProperties_HaywireCritical Props => (HediffCompProperties_HaywireCritical)props;

    public override void CompPostTickInterval(ref float severityAdjustment, int delta)
    {
        if (parent.Severity < Props.minSeverity)
        {
            ticksUntilStun = 0;
            ticksUntilBerserkCheck = Props.berserkCheckIntervalTicks;
            return;
        }
        Pawn pawn = Pawn;
        if (pawn.Dead || !pawn.Spawned)
        {
            return;
        }
        ticksUntilStun -= delta;
        if (ticksUntilStun <= 0)
        {
            ticksUntilStun = Props.stunIntervalTicks;
            bool machine = pawn.RaceProps.IsMechanoid || pawn.RaceProps.IsDrone;
            float amount = machine ? Props.stunAmountMechanical : Props.stunAmountSynthetic;
            pawn.stances?.stunner?.Notify_DamageApplied(new DamageInfo(DamageDefOf.EMP, amount));
        }
        ticksUntilBerserkCheck -= delta;
        if (ticksUntilBerserkCheck <= 0)
        {
            ticksUntilBerserkCheck = Props.berserkCheckIntervalTicks;
            if (!pawn.InMentalState && !pawn.Downed && Rand.Chance(Props.berserkChance))
            {
                TryBerserk(pawn);
            }
        }
    }

    public override void CompExposeData()
    {
        base.CompExposeData();
        Scribe_Values.Look(ref ticksUntilStun, "gvTicksUntilStun");
        Scribe_Values.Look(ref ticksUntilBerserkCheck, "gvTicksUntilBerserkCheck");
    }

    private static void TryBerserk(Pawn pawn)
    {
        MentalStateDef state = pawn.RaceProps.IsMechanoid
            ? MentalStateDefOf.BerserkMechanoid
            : (pawn.RaceProps.Humanlike ? MentalStateDefOf.Berserk : null);
        if (state == null || pawn.mindState?.mentalStateHandler == null)
        {
            return;
        }
        pawn.mindState.mentalStateHandler.TryStartMentalState(state, "GV_HaywireBerserkReason".Translate(), forced: true);
    }
}
