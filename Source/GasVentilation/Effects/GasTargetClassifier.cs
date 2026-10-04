using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace GasVentilation;

/// <summary>
/// Decides what kind of target a pawn is. Race results are cached per ThingDef index. Per-pawn work (genes,
/// hediffs, mutants) only happens for pawns standing in gas.
/// </summary>
[StaticConstructorOnStartup]
public static class GasTargetClassifier
{
    /// <summary>Type names of extensions or comps that other android and robot mods put on their races.</summary>
    private static readonly string[] KnownRobotTypeNames =
    {
        "Androids.MechanicalPawnProperties",
        "Androids.CompProperties_EnergyTracker",
        "ArtificialBeings.ABF_ArtificialPawnExtension",
        "ArtificialBeings.CompArtificialPawn"
    };

    private static readonly Dictionary<GeneDef, GasTargetExtension> GeneExtensions = new Dictionary<GeneDef, GasTargetExtension>();
    private static readonly Dictionary<PawnKindDef, GasTargetExtension> KindExtensions = new Dictionary<PawnKindDef, GasTargetExtension>();
    private static readonly GasTargetFlags[] RaceCache;
    private static readonly bool[] RaceComputed;
    private static readonly bool AnyHediffExtensions;

    static GasTargetClassifier()
    {
        int count = DefDatabase<ThingDef>.DefCount;
        RaceCache = new GasTargetFlags[count];
        RaceComputed = new bool[count];
        foreach (GeneDef gene in DefDatabase<GeneDef>.AllDefsListForReading)
        {
            GasTargetExtension ext = gene.GetModExtension<GasTargetExtension>();
            if (ext != null)
            {
                GeneExtensions[gene] = ext;
            }
        }
        foreach (PawnKindDef kind in DefDatabase<PawnKindDef>.AllDefsListForReading)
        {
            GasTargetExtension ext = kind.GetModExtension<GasTargetExtension>();
            if (ext != null)
            {
                KindExtensions[kind] = ext;
            }
        }
        foreach (HediffDef hediff in DefDatabase<HediffDef>.AllDefsListForReading)
        {
            if (hediff.HasModExtension<GasTargetExtension>())
            {
                AnyHediffExtensions = true;
                break;
            }
        }
    }

    /// <summary>
    /// Classifies <paramref name="pawn"/> and fills <paramref name="extensions"/> (cleared first) with every
    /// extension that applies, so callers can read immunities and exposure factors without a second scan.
    /// </summary>
    public static GasTargetFlags Classify(Pawn pawn, List<GasTargetExtension> extensions)
    {
        extensions.Clear();
        ThingDef race = pawn.def;
        GasTargetFlags flags = RaceFlags(race);

        AddIfPresent(race.race.FleshType.GetModExtension<GasTargetExtension>(), extensions);
        AddIfPresent(race.GetModExtension<GasTargetExtension>(), extensions);
        if (pawn.kindDef != null && KindExtensions.TryGetValue(pawn.kindDef, out GasTargetExtension kindExt))
        {
            extensions.Add(kindExt);
            flags = Apply(flags, kindExt, race);
        }
        if (GeneExtensions.Count > 0 && ModsConfig.BiotechActive && pawn.genes != null)
        {
            List<Gene> genes = pawn.genes.GenesListForReading;
            for (int i = 0; i < genes.Count; i++)
            {
                Gene gene = genes[i];
                if (gene.Active && GeneExtensions.TryGetValue(gene.def, out GasTargetExtension geneExt))
                {
                    extensions.Add(geneExt);
                    flags = Apply(flags, geneExt, race);
                }
            }
        }
        if (AnyHediffExtensions)
        {
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                GasTargetExtension hediffExt = hediffs[i].def.GetModExtension<GasTargetExtension>();
                if (hediffExt != null)
                {
                    extensions.Add(hediffExt);
                    flags = Apply(flags, hediffExt, race);
                }
            }
        }
        if (pawn.IsMutant && !pawn.mutant.Def.breathesAir)
        {
            flags &= ~GasTargetFlags.Breathes;
        }
        return flags;
    }

    public static GasTargetFlags RaceFlags(ThingDef race)
    {
        int index = race.index;
        if (index >= RaceComputed.Length)
        {
            return ComputeRace(race);
        }
        if (!RaceComputed[index])
        {
            RaceCache[index] = ComputeRace(race);
            RaceComputed[index] = true;
        }
        return RaceCache[index];
    }

    private static GasTargetFlags ComputeRace(ThingDef race)
    {
        GasTargetExtension ext = race.GetModExtension<GasTargetExtension>() ?? race.race.FleshType.GetModExtension<GasTargetExtension>();
        if (ext != null && ext.category != GasTargetCategory.Auto)
        {
            return Apply(GasTargetFlags.None, ext, race);
        }
        if (IsKnownRobotRace(race))
        {
            return GasTargetFlags.Synthetic;
        }
        RaceProperties props = race.race;
        if (props.IsAnomalyEntity)
        {
            return GasTargetFlags.Excluded;
        }
        if (props.IsMechanoid || props.IsDrone)
        {
            return GasTargetFlags.Mechanical;
        }
        GasTargetFlags flags;
        if (props.Insect)
        {
            flags = GasTargetFlags.Insectoid | GasTargetFlags.Organic;
        }
        else if (props.IsFlesh)
        {
            flags = GasTargetFlags.Organic;
        }
        else
        {
            return GasTargetFlags.Excluded;
        }
        if (HasBreathingPart(race))
        {
            flags |= GasTargetFlags.Breathes;
        }
        return ext != null ? ApplyBreathing(flags, ext) : flags;
    }

    private static GasTargetFlags Apply(GasTargetFlags current, GasTargetExtension ext, ThingDef race)
    {
        GasTargetFlags flags;
        switch (ext.category)
        {
            case GasTargetCategory.Organic:
                flags = GasTargetFlags.Organic | (HasBreathingPart(race) ? GasTargetFlags.Breathes : GasTargetFlags.None);
                break;
            case GasTargetCategory.Insectoid:
                flags = GasTargetFlags.Insectoid | GasTargetFlags.Organic | (HasBreathingPart(race) ? GasTargetFlags.Breathes : GasTargetFlags.None);
                break;
            case GasTargetCategory.Mechanical:
                flags = GasTargetFlags.Mechanical;
                break;
            case GasTargetCategory.Synthetic:
                flags = GasTargetFlags.Synthetic;
                break;
            case GasTargetCategory.Excluded:
                flags = GasTargetFlags.Excluded;
                break;
            default:
                flags = current;
                break;
        }
        return ApplyBreathing(flags, ext);
    }

    private static GasTargetFlags ApplyBreathing(GasTargetFlags flags, GasTargetExtension ext)
    {
        switch (ext.breathing)
        {
            case GasBreathing.Yes:
                return flags | GasTargetFlags.Breathes;
            case GasBreathing.No:
                return flags & ~GasTargetFlags.Breathes;
            default:
                return flags;
        }
    }

    /// <summary>Humanlike races always count as breathing, even with a custom body that has no lung-tagged part.</summary>
    private static bool HasBreathingPart(ThingDef race)
    {
        if (race.race.Humanlike)
        {
            return true;
        }
        return race.race.body != null && race.race.body.HasPartWithTag(BodyPartTagDefOf.BreathingSource);
    }

    private static void AddIfPresent(GasTargetExtension ext, List<GasTargetExtension> list)
    {
        if (ext != null)
        {
            list.Add(ext);
        }
    }

    private static bool IsKnownRobotRace(ThingDef race)
    {
        if (HarSaysNotFlesh(race))
        {
            return true;
        }
        if (race.modExtensions != null)
        {
            for (int i = 0; i < race.modExtensions.Count; i++)
            {
                if (IsKnownRobotType(race.modExtensions[i].GetType()))
                {
                    return true;
                }
            }
        }
        if (race.comps != null)
        {
            for (int i = 0; i < race.comps.Count; i++)
            {
                CompProperties comp = race.comps[i];
                if (IsKnownRobotType(comp.GetType()) || (comp.compClass != null && IsKnownRobotType(comp.compClass)))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool IsKnownRobotType(Type type)
    {
        string name = type.FullName;
        for (int i = 0; i < KnownRobotTypeNames.Length; i++)
        {
            if (name == KnownRobotTypeNames[i])
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Humanoid Alien Races: ThingDef_AlienRace.alienRace.compatibility.IsFlesh == false. Read once per race.</summary>
    private static bool HarSaysNotFlesh(ThingDef race)
    {
        Type type = race.GetType();
        if (type.FullName != "AlienRace.ThingDef_AlienRace")
        {
            return false;
        }
        try
        {
            object settings = AccessTools.Field(type, "alienRace")?.GetValue(race);
            object compatibility = settings == null ? null : AccessTools.Field(settings.GetType(), "compatibility")?.GetValue(settings);
            object isFlesh = compatibility == null ? null : AccessTools.Property(compatibility.GetType(), "IsFlesh")?.GetValue(compatibility, null);
            return isFlesh is bool flesh && !flesh;
        }
        catch (Exception e)
        {
            Log.Warning($"[GasVentilation] Could not read HAR compatibility for {race.defName}: {e.Message}");
            return false;
        }
    }
}
