using Verse;

namespace GasVentilation;

public sealed class HediffCompProperties_GasExposure : HediffCompProperties
{
    /// <summary>Severity lost per day once the pawn has been out of gas for decayDelayTicks.</summary>
    public float decayPerDay = 2f;

    public int decayDelayTicks = 90;

    public HediffCompProperties_GasExposure()
    {
        compClass = typeof(HediffComp_GasExposure);
    }
}

/// <summary>Exposure severity is added by the scanner; this comp only decays it when exposure stops.</summary>
public sealed class HediffComp_GasExposure : HediffComp
{
    private int lastExposedTick = -99999;

    public HediffCompProperties_GasExposure Props => (HediffCompProperties_GasExposure)props;

    public void Notify_Exposed(int tick)
    {
        lastExposedTick = tick;
    }

    public override void CompPostPostAdd(DamageInfo? dinfo)
    {
        base.CompPostPostAdd(dinfo);
        lastExposedTick = Find.TickManager.TicksGame;
    }

    public override void CompPostTickInterval(ref float severityAdjustment, int delta)
    {
        if (Find.TickManager.TicksGame - lastExposedTick > Props.decayDelayTicks)
        {
            severityAdjustment -= Props.decayPerDay * delta / 60000f;
        }
    }

    public override void CompExposeData()
    {
        base.CompExposeData();
        Scribe_Values.Look(ref lastExposedTick, "gvLastExposedTick", -99999);
    }
}
