using System.Collections.Generic;
using GasVentilation.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace GasVentilation;

/// <summary>Who is affected by which gas, how strongly, and applying doses. Main thread only (shared scratch list).</summary>
public static class GasExposure
{
    /// <summary>Densities below this are ignored for exposure.</summary>
    public const int NoiseFloor = 3;

    private static readonly List<GasTargetExtension> TmpExtensions = new List<GasTargetExtension>();
    private static StatDef empResistance;
    private static bool empResistanceLooked;

    private static StatDef EmpResistance
    {
        get
        {
            if (!empResistanceLooked)
            {
                empResistance = DefDatabase<StatDef>.GetNamedSilentFail("EMPResistance");
                empResistanceLooked = true;
            }
            return empResistance;
        }
    }

    public static bool Affects(GasDef gas, GasTargetFlags flags)
    {
        if ((flags & GasTargetFlags.Excluded) != 0)
        {
            return false;
        }
        switch (gas.targets)
        {
            case GasTargetRule.OrganicBreathers:
                return (flags & GasTargetFlags.Organic) != 0 && (flags & GasTargetFlags.Breathes) != 0 && (flags & GasTargetFlags.Synthetic) == 0;
            case GasTargetRule.Machines:
                return (flags & (GasTargetFlags.Mechanical | GasTargetFlags.Synthetic)) != 0;
            case GasTargetRule.Insectoids:
                return (flags & GasTargetFlags.Insectoid) != 0;
            default:
                return false;
        }
    }

    /// <summary>Dose multiplier; 0 means immune. <paramref name="extensions"/> comes from GasTargetClassifier.Classify.</summary>
    public static float ProtectionFactor(Pawn pawn, GasDef gas, List<GasTargetExtension> extensions)
    {
        float factor = 1f;
        for (int i = 0; i < extensions.Count; i++)
        {
            GasTargetExtension ext = extensions[i];
            if (ext.immuneTo != null && ext.immuneTo.Contains(gas))
            {
                return 0f;
            }
            factor *= ext.exposureFactor;
        }
        List<Apparel> worn = pawn.apparel?.WornApparel;
        if (worn != null)
        {
            for (int i = 0; i < worn.Count; i++)
            {
                ThingDef def = worn[i].def;
                if (gas.protection == GasProtectionKind.Respiratory && def.apparel != null && def.apparel.immuneToToxGasExposure)
                {
                    return 0f;
                }
                GasProtectionExtension protection = def.GetModExtension<GasProtectionExtension>();
                if (protection != null)
                {
                    if (protection.immuneTo != null && protection.immuneTo.Contains(gas))
                    {
                        return 0f;
                    }
                    factor *= protection.doseFactor;
                }
            }
        }
        // Humanlikes are protected by gear only. Xenotype genes (sanguophage tox immunity and the like) and other
        // body sources of toxic resistance don't stop respiratory gas, so every humanlike race and xenotype is affected.
        bool gearOnly = pawn.RaceProps.Humanlike;
        if (gas.protection == GasProtectionKind.Respiratory && !gearOnly && ModsConfig.BiotechActive && pawn.genes != null)
        {
            List<Gene> genes = pawn.genes.GenesListForReading;
            for (int i = 0; i < genes.Count; i++)
            {
                if (genes[i].Active && genes[i].def.immuneToToxGasExposure)
                {
                    return 0f;
                }
            }
        }
        switch (gas.protection)
        {
            case GasProtectionKind.Respiratory:
                float resistance = gearOnly ? GearToxicResistance(pawn) : pawn.GetStatValue(StatDefOf.ToxicEnvironmentResistance, true, 250);
                factor *= Mathf.Max(0f, 1f - resistance);
                break;
            case GasProtectionKind.Electromagnetic:
                StatDef emp = EmpResistance;
                if (emp != null)
                {
                    factor *= Mathf.Max(0f, 1f - pawn.GetStatValue(emp, true, 250));
                }
                break;
        }
        if (gas.scaleDoseByBodySize)
        {
            factor /= Mathf.Clamp(pawn.BodySize, 0.5f, 3f);
        }
        return factor;
    }

    /// <summary>Toxic environment resistance from worn apparel and equipment only, clamped like the stat.</summary>
    private static float GearToxicResistance(Pawn pawn)
    {
        StatDef stat = StatDefOf.ToxicEnvironmentResistance;
        float total = 0f;
        List<Apparel> worn = pawn.apparel?.WornApparel;
        if (worn != null)
        {
            for (int i = 0; i < worn.Count; i++)
            {
                total += StatWorker.StatOffsetFromGear(worn[i], stat);
            }
        }
        List<ThingWithComps> equipment = pawn.equipment?.AllEquipmentListForReading;
        if (equipment != null)
        {
            for (int i = 0; i < equipment.Count; i++)
            {
                total += StatWorker.StatOffsetFromGear(equipment[i], stat);
            }
        }
        return Mathf.Clamp(total, stat.minValue, stat.maxValue);
    }

    /// <summary>Applies the doses for one pawn standing in <paramref name="packed"/> gas for <paramref name="elapsedTicks"/>.</summary>
    public static void ExposeTo(Pawn pawn, uint packed, int elapsedTicks)
    {
        GasTargetFlags flags = GasTargetClassifier.Classify(pawn, TmpExtensions);
        int now = Find.TickManager.TicksGame;
        for (int channel = 0; channel < GasPacking.Channels; channel++)
        {
            int density = GasPacking.Get(packed, channel);
            if (density < NoiseFloor)
            {
                continue;
            }
            GasDef gas = GasDefRegistry.ForChannel(channel);
            if (gas?.exposureHediff == null || !Affects(gas, flags))
            {
                continue;
            }
            float protection = ProtectionFactor(pawn, gas, TmpExtensions);
            if (protection <= 0f)
            {
                continue;
            }
            float dose = gas.dosePerTickAtFullDensity * (density / 255f) * elapsedTicks * protection;
            if (dose <= 0f)
            {
                continue;
            }
            HealthUtility.AdjustSeverity(pawn, gas.exposureHediff, dose);
            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(gas.exposureHediff);
            if (hediff == null)
            {
                continue;
            }
            hediff.TryGetComp<HediffComp_GasExposure>()?.Notify_Exposed(now);
            hediff.TryGetComp<HediffComp_SedationTrigger>()?.OnExposed(elapsedTicks);
            if (pawn.Dead)
            {
                return;
            }
        }
    }

    /// <summary>Bit per channel: gases that would affect this pawn meaningfully. Used by AI avoidance.</summary>
    public static byte RelevantMask(Pawn pawn)
    {
        GasTargetFlags flags = GasTargetClassifier.Classify(pawn, TmpExtensions);
        int mask = 0;
        for (int channel = 0; channel < GasPacking.Channels; channel++)
        {
            GasDef gas = GasDefRegistry.ForChannel(channel);
            if (gas != null && Affects(gas, flags) && ProtectionFactor(pawn, gas, TmpExtensions) > 0.05f)
            {
                mask |= 1 << channel;
            }
        }
        return (byte)mask;
    }
}
