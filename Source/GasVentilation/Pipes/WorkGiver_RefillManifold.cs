using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace GasVentilation;

/// <summary>
/// Replaces VEF's own WorkGiver_RefillStorage for manifolds: that one assumes a flat ratio (every carried
/// unit is worth exactly one resource), which breaks down once a canister can be partially full. This scans
/// manifolds directly (there is no markedForRefill bookkeeping to maintain) and hands off to
/// JobDriver_RefillManifold, which reads the found canister's actual fill.
/// </summary>
public sealed class WorkGiver_RefillManifold : WorkGiver_Scanner
{
    public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(GVDefOf.GV_Manifold);

    public override PathEndMode PathEndMode => PathEndMode.ClosestTouch;

    public override Danger MaxPathDanger(Pawn pawn) => Danger.Deadly;

    public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        CompGasManifold manifold = (t as ThingWithComps)?.GetComp<CompGasManifold>();
        if (manifold?.ActiveGas == null || manifold.AmountCanAccept < 1f)
        {
            return false;
        }
        if (t.IsForbidden(pawn) || t.Faction != pawn.Faction || !pawn.CanReserve(t, 1, -1, null, forced))
        {
            return false;
        }
        if (FindBestCanister(pawn, manifold.ActiveGas.canister) == null)
        {
            JobFailReason.Is("GV_NothingToRefill".Translate(manifold.ActiveGas.label));
            return false;
        }
        return true;
    }

    public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        CompGasManifold manifold = (t as ThingWithComps)?.GetComp<CompGasManifold>();
        Thing best = FindBestCanister(pawn, manifold.ActiveGas.canister);
        if (best == null)
        {
            return null;
        }
        float fillEach = (best as Thing_GasCanister)?.fill ?? 1f;
        Job job = JobMaker.MakeJob(GVDefOf.GV_RefillManifold, t, best);
        job.count = Mathf.Max(Mathf.CeilToInt(manifold.AmountCanAccept / fillEach), 1);
        return job;
    }

    private static Thing FindBestCanister(Pawn pawn, ThingDef canisterDef)
    {
        bool Validator(Thing x) => !x.IsForbidden(pawn) && pawn.CanReserve(x);
        return GenClosest.ClosestThingReachable(
            pawn.Position, pawn.Map, ThingRequest.ForDef(canisterDef), PathEndMode.ClosestTouch,
            TraverseParms.For(pawn), validator: Validator);
    }
}
