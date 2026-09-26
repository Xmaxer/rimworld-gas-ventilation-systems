using System.Collections.Generic;
using PipeSystem;
using Verse;

namespace GasVentilation;

public sealed class CompProperties_GasVent : CompProperties_Resource
{
    public GasDef gas;

    /// <summary>Output cell relative to the building, rotated with it. (0,0,1) for wall vents, zero otherwise.</summary>
    public IntVec3 outputOffset = IntVec3.Zero;

    /// <summary>Ceiling vents: must stay under a roof, and fall when it is removed.</summary>
    public bool requiresRoof;

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
