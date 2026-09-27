using RimWorld;
using Verse;
using Verse.AI;

namespace GasVentilation;

/// <summary>Any free colonist picks up a manifold with a pending gas-switch request, the same way refuelling or
/// repair work is auto-scanned -- no skill requirement, "basic" work per the design.</summary>
public sealed class WorkGiver_ReconfigureManifold : WorkGiver_Scanner
{
    public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(GVDefOf.GV_Manifold);

    public override PathEndMode PathEndMode => PathEndMode.Touch;

    public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        CompGasManifold comp = (t as ThingWithComps)?.GetComp<CompGasManifold>();
        if (comp == null || comp.PendingGas == null)
        {
            return false;
        }
        return pawn.CanReserve(t, 1, -1, null, forced);
    }

    public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        return JobMaker.MakeJob(GVDefOf.GV_ReconfigureManifold, t);
    }
}
