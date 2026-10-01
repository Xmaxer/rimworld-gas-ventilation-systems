using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace GasVentilation;

/// <summary>
/// Carries a canister (job.targetB) to a manifold (job.targetA) and pours it in. Unlike VEF's stock
/// JobDriver_FillStorage, which always adds a flat amount per carried unit, this reads the canister's actual
/// fill and tops the manifold off with exactly that much -- capped at the manifold's remaining room. A
/// canister that only partially drains (manifold filled up before the canister emptied) comes back out with
/// its fill reduced by however much was actually poured in, rather than being destroyed outright.
/// </summary>
public sealed class JobDriver_RefillManifold : JobDriver
{
    private const int RefillTicks = 150;

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        return pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed)
            && pawn.Reserve(job.targetB, job, 1, job.count, null, errorOnFailed);
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        this.FailOnDestroyedNullOrForbidden(TargetIndex.B);

        yield return Toils_Reserve.Reserve(TargetIndex.B, stackCount: job.count);
        yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch)
            .FailOnSomeonePhysicallyInteracting(TargetIndex.B);
        yield return Toils_Haul.StartCarryThing(TargetIndex.B)
            .FailOnDestroyedNullOrForbidden(TargetIndex.B);
        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

        Toil wait = Toils_General.Wait(RefillTicks).WithProgressBarToilDelay(TargetIndex.A);
        wait.FailOnDestroyedNullOrForbidden(TargetIndex.B);
        wait.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
        yield return wait;

        Toil finalize = new Toil { defaultCompleteMode = ToilCompleteMode.Instant };
        finalize.initAction = () =>
        {
            CompGasManifold manifold = (job.targetA.Thing as ThingWithComps)?.GetComp<CompGasManifold>();
            Thing fuel = job.targetB.Thing;
            if (manifold?.ActiveGas == null || fuel == null)
            {
                return;
            }
            float perItemFill = (fuel as Thing_GasCanister)?.fill ?? 1f;
            float totalAvailable = perItemFill * fuel.stackCount;
            float actualAdded = Mathf.Min(manifold.AmountCanAccept, totalAvailable);
            if (actualAdded <= 0f)
            {
                return;
            }
            manifold.AddResource(actualAdded);
            if (actualAdded >= totalAvailable - 0.0001f)
            {
                fuel.Destroy();
                return;
            }
            int wholeConsumed = Mathf.FloorToInt(actualAdded / perItemFill + 0.0001f);
            float leftoverFraction = actualAdded - wholeConsumed * perItemFill;
            if (wholeConsumed > 0)
            {
                fuel.SplitOff(wholeConsumed).Destroy();
            }
            if (leftoverFraction > 0.001f && fuel is Thing_GasCanister)
            {
                Thing_GasCanister partial = (Thing_GasCanister)fuel.SplitOff(1);
                partial.fill = perItemFill - leftoverFraction;
                GenPlace.TryPlaceThing(partial, pawn.Position, pawn.Map, ThingPlaceMode.Near);
            }
        };
        yield return finalize;
    }
}
