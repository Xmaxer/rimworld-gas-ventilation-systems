using Verse;

namespace GasVentilation;

/// <summary>Ceiling vents need a roof to hang from.</summary>
public sealed class PlaceWorker_UnderRoof : PlaceWorker
{
    public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
    {
        return loc.Roofed(map) ? AcceptanceReport.WasAccepted : new AcceptanceReport("GV_MustPlaceUnderRoof".Translate());
    }
}
