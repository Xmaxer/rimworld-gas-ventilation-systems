using System.Collections.Generic;
using PipeSystem;
using Verse;

namespace GasVentilation;

/// <summary>One of up to four per-gas trader comps on a vent. Shared output cell/roof rule live on the sibling
/// <see cref="CompProperties_GasVentController"/> instead, since those apply to the building, not the gas.</summary>
public sealed class CompProperties_GasVent : CompProperties_Resource
{
    public GasDef gas;

    public CompProperties_GasVent()
    {
        compClass = typeof(CompGasVent);
    }

    public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
    {
        foreach (string error in base.ConfigErrors(parentDef))
        {
            yield return error;
        }
        if (gas == null)
        {
            yield return "gas is required";
        }
    }
}
