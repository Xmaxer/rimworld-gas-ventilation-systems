using System.Collections.Generic;
using Verse;

namespace GasVentilation;

/// <summary>Put on apparel ThingDefs for protection beyond the vanilla gas-mask flag and stats.</summary>
public sealed class GasProtectionExtension : DefModExtension
{
    public List<GasDef> immuneTo;

    /// <summary>Multiplies the dose from every gas this apparel does not make the wearer immune to.</summary>
    public float doseFactor = 1f;
}
