using PipeSystem;

namespace GasVentilation;

/// <summary>
/// No fixed gas or pipeNet: <see cref="CompGasManifold"/> clones this into an instance-owned copy and fills
/// in <c>gas</c>/<c>pipeNet</c> at runtime, since every spawned manifold can be independently configured for
/// a different gas. Leave <c>refillOptions</c> unset in XML -- refilling is a custom job
/// (WorkGiver_RefillManifold/JobDriver_RefillManifold), not VEF's stock ratio-based one, since a canister can
/// be partially full.
/// </summary>
public sealed class CompProperties_GasManifold : CompProperties_ResourceStorage
{
    public GasDef gas;

    public CompProperties_GasManifold()
    {
        compClass = typeof(CompGasManifold);
    }
}
