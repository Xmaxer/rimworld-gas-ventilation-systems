using System.Collections.Generic;
using Verse;

namespace GasVentilation;

public sealed class CompProperties_GasReleaseOnDestroy : CompProperties
{
    public GasDef gas;
    public float canistersPerItem = 1f;

    public CompProperties_GasReleaseOnDestroy()
    {
        compClass = typeof(CompGasReleaseOnDestroy);
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
