using System.Collections.Generic;
using RimWorld;
using Verse;

namespace GasVentilation;

public sealed class HediffCompProperties_PeriodicGasDamage : HediffCompProperties
{
    public DamageDef damageDef;
    public int intervalTicks = 250;

    /// <summary>Damage per pulse at severity 1 (scaled linearly by severity).</summary>
    public float damageAtFullSeverity = 2f;

    public float minSeverity = 0.1f;

    /// <summary>If set, only parts with this tag are hit (for example BreathingSource). No such part, no damage.</summary>
    public BodyPartTagDef partTag;

    /// <summary>If true (and no partTag), hit only outside parts, avoiding one-shot organ kills.</summary>
    public bool outerPartsOnly;

    public HediffCompProperties_PeriodicGasDamage()
    {
        compClass = typeof(HediffComp_PeriodicGasDamage);
    }

    public override IEnumerable<string> ConfigErrors(HediffDef parentDef)
    {
        foreach (string error in base.ConfigErrors(parentDef))
        {
            yield return error;
        }
        if (damageDef == null)
        {
            yield return "damageDef is required";
        }
        if (intervalTicks <= 0)
        {
            yield return "intervalTicks must be positive";
        }
    }
}

public sealed class HediffComp_PeriodicGasDamage : HediffComp
{
    private static readonly List<BodyPartRecord> TmpParts = new List<BodyPartRecord>();

    private int ticksUntilDamage = -1;

    public HediffCompProperties_PeriodicGasDamage Props => (HediffCompProperties_PeriodicGasDamage)props;

    public override void CompPostTickInterval(ref float severityAdjustment, int delta)
    {
        if (ticksUntilDamage < 0)
        {
            ticksUntilDamage = Props.intervalTicks;
        }
        ticksUntilDamage -= delta;
        if (ticksUntilDamage > 0)
        {
            return;
        }
        ticksUntilDamage = Props.intervalTicks;
        float severity = parent.Severity;
        Pawn pawn = Pawn;
        if (severity < Props.minSeverity || pawn.Dead)
        {
            return;
        }
        float amount = Props.damageAtFullSeverity * severity;
        if (amount < 0.1f)
        {
            return;
        }
        BodyPartRecord part = ChoosePart(pawn);
        if (Props.partTag != null && part == null)
        {
            return;
        }
        pawn.TakeDamage(new DamageInfo(Props.damageDef, amount, 0f, -1f, null, part));
    }

    public override void CompExposeData()
    {
        base.CompExposeData();
        Scribe_Values.Look(ref ticksUntilDamage, "gvTicksUntilDamage", -1);
    }

    private BodyPartRecord ChoosePart(Pawn pawn)
    {
        if (Props.partTag != null)
        {
            TmpParts.Clear();
            List<BodyPartRecord> tagged = pawn.RaceProps.body.GetPartsWithTag(Props.partTag);
            for (int i = 0; i < tagged.Count; i++)
            {
                BodyPartRecord part = tagged[i];
                if (!pawn.health.hediffSet.PartIsMissing(part) && !IsVitalMultiRole(part))
                {
                    TmpParts.Add(part);
                }
            }
            BodyPartRecord chosen = TmpParts.Count == 0 ? null : TmpParts[Rand.Range(0, TmpParts.Count)];
            TmpParts.Clear();
            return chosen;
        }
        if (Props.outerPartsOnly)
        {
            return pawn.health.hediffSet.GetRandomNotMissingPart(Props.damageDef, BodyPartHeight.Undefined, BodyPartDepth.Outside);
        }
        return null;
    }

    /// <summary>Insect hearts and similar organs breathe but also pump blood or hold consciousness; do not burn them.</summary>
    private static bool IsVitalMultiRole(BodyPartRecord part)
    {
        List<BodyPartTagDef> tags = part.def.tags;
        return tags.Contains(BodyPartTagDefOf.BloodPumpingSource) || tags.Contains(BodyPartTagDefOf.ConsciousnessSource);
    }
}
