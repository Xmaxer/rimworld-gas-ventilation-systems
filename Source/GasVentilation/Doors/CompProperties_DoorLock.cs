using Verse;

namespace GasVentilation;

/// <summary>Added to every powered door at startup by <see cref="DoorLockInjector"/> -- never written to XML
/// directly, since eligibility (any Building_Door with a power comp, from any mod) is computed at runtime.</summary>
public sealed class CompProperties_DoorLock : CompProperties
{
    public CompProperties_DoorLock()
    {
        compClass = typeof(CompDoorLock);
    }
}
