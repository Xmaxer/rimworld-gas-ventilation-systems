using RimWorld;
using UnityEngine;
using Verse;

namespace GasVentilation;

public sealed class HediffCompProperties_GasSedationTimer : HediffCompProperties
{
    public HediffCompProperties_GasSedationTimer()
    {
        compClass = typeof(HediffComp_GasSedationTimer);
    }
}

/// <summary>Keeps the sedation latch for a minimum time (no flicker at the threshold); extended while exposed.</summary>
public sealed class HediffComp_GasSedationTimer : HediffComp
{
    private int ticksRemaining;

    public override bool CompShouldRemove => ticksRemaining <= 0;

    public override string CompLabelInBracketsExtra => ticksRemaining > 0 ? ticksRemaining.ToStringTicksToPeriod() : null;

    public void Extend(int ticks, int minTicks, int maxTicks)
    {
        ticksRemaining = Mathf.Clamp(Mathf.Max(ticksRemaining, minTicks) + ticks, minTicks, maxTicks);
    }

    public override void CompPostTickInterval(ref float severityAdjustment, int delta)
    {
        ticksRemaining -= delta;
    }

    public override void CompExposeData()
    {
        base.CompExposeData();
        Scribe_Values.Look(ref ticksRemaining, "gvTicksRemaining");
    }
}
