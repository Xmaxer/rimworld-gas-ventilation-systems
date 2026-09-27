using Verse;

namespace GasVentilation;

public sealed class CompProperties_GasVentController : CompProperties
{
    /// <summary>Output cell relative to the building, rotated with it. (0,0,1) for wall vents, zero otherwise.</summary>
    public IntVec3 outputOffset = IntVec3.Zero;

    /// <summary>Ceiling vents: must stay under a roof, and fall when it is removed.</summary>
    public bool requiresRoof;

    public CompProperties_GasVentController()
    {
        compClass = typeof(CompGasVentController);
    }
}
