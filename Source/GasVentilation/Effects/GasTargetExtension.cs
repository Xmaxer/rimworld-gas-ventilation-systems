using System.Collections.Generic;
using Verse;

namespace GasVentilation;

/// <summary>
/// Opt-in or opt-out for other mods and patches. Attach it to a race ThingDef, FleshTypeDef, PawnKindDef,
/// GeneDef (active genes only) or HediffDef. The most specific source wins: hediff, then gene, then pawn kind,
/// then race, then flesh type.
/// </summary>
public sealed class GasTargetExtension : DefModExtension
{
    public GasTargetCategory category = GasTargetCategory.Auto;
    public GasBreathing breathing = GasBreathing.Auto;
    public List<GasDef> immuneTo;

    /// <summary>Multiplies the dose from every gas.</summary>
    public float exposureFactor = 1f;
}
