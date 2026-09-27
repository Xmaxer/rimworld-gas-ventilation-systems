using System.Collections.Generic;
using PipeSystem;
using Verse;

namespace GasVentilation;

/// <summary>
/// No fixed gas or pipeNet: <see cref="CompGasManifold"/> clones this into an instance-owned copy and fills
/// in <c>gas</c>/<c>pipeNet</c>/<c>refillOptions.thing</c> at runtime, since every spawned manifold can be
/// independently configured for a different gas.
/// </summary>
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
        if (refillOptions == null || refillOptions.ratio != 1f)
        {
            yield return "refillOptions with ratio 1 is required (1 resource unit = 1 canister)";
        }
    }
}
