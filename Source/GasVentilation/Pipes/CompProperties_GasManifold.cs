using System.Collections.Generic;
using PipeSystem;
using Verse;

namespace GasVentilation;

public sealed class CompProperties_GasManifold : CompProperties_ResourceStorage
{
    public GasDef gas;

    public CompProperties_GasManifold()
    {
        compClass = typeof(CompGasManifold);
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
        if (refillOptions == null || refillOptions.thing == null || refillOptions.ratio != 1f)
        {
            yield return "refillOptions with a canister thing and ratio 1 is required (1 resource unit = 1 canister)";
        }
    }
}
