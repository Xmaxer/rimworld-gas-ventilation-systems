using UnityEngine;
using Verse;

namespace GasVentilation;

public sealed class HediffCompProperties_SedationTrigger : HediffCompProperties
{
    public float threshold = 0.7f;
    public HediffDef latchHediff;
    public int minTicks = 2500;
    public int maxTicks = 15000;
    public float extendFactor = 1.5f;

    public HediffCompProperties_SedationTrigger()
    {
        compClass = typeof(HediffComp_SedationTrigger);
    }
}

public sealed class HediffComp_SedationTrigger : HediffComp
{
    public HediffCompProperties_SedationTrigger Props => (HediffCompProperties_SedationTrigger)props;

    public void OnExposed(int elapsedTicks)
    {
        if (parent.Severity < Props.threshold || Props.latchHediff == null)
        {
            return;
        }
        Pawn pawn = Pawn;
        if (pawn.Dead || (pawn.RaceProps.alwaysAwake && !pawn.RaceProps.Humanlike))
        {
            return;
        }
        Hediff latch = pawn.health.hediffSet.GetFirstHediffOfDef(Props.latchHediff);
        if (latch == null)
        {
            latch = HediffMaker.MakeHediff(Props.latchHediff, pawn);
            pawn.health.AddHediff(latch);
        }
        latch.TryGetComp<HediffComp_GasSedationTimer>()?.Extend(Mathf.RoundToInt(elapsedTicks * Props.extendFactor), Props.minTicks, Props.maxTicks);
    }
}
