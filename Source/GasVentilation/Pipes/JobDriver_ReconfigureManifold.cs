using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace GasVentilation;

/// <summary>Walk to the manifold, spend a short basic task, then apply its pending gas switch.</summary>
public sealed class JobDriver_ReconfigureManifold : JobDriver
{
    private const int WorkTicks = 200;

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        return pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed);
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        this.FailOnBurningImmobile(TargetIndex.A);
        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
        Toil wait = Toils_General.Wait(WorkTicks).WithProgressBarToilDelay(TargetIndex.A);
        wait.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
        yield return wait;
        yield return new Toil
        {
            initAction = () => (job.targetA.Thing as ThingWithComps)?.GetComp<CompGasManifold>()?.CompleteReconfigure(),
            defaultCompleteMode = ToilCompleteMode.Instant
        };
    }
}
