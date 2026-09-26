using GasVentilation.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace GasVentilation;

/// <summary>
/// Inserted into the constant think trees (evaluated every 30 ticks, and able to interrupt jobs). The wrapping
/// vanilla node already skips drafted, mentally broken, downed, burning and sleeping pawns.
/// </summary>
public sealed class JobGiver_FleeGas : ThinkNode_JobGiver
{
    private const int FleeExpiryTicks = 300;

    protected override Job TryGiveJob(Pawn pawn)
    {
        if (!pawn.Spawned || pawn.Downed || pawn.Drafted)
        {
            return null;
        }
        VentGasGrid grid = VentGasGrid.For(pawn.Map);
        if (grid == null || grid.LiveCells == 0)
        {
            return null;
        }
        uint here = grid.PackedAt(pawn.Position);
        if (here == 0u)
        {
            return null;
        }
        // Keep running our own flee or breakout job instead of restarting it every 30 ticks.
        Job current = pawn.CurJob;
        if (current != null && (current.jobGiver == this || current.def == GVDefOf.GV_FleeGas))
        {
            return null;
        }
        if (pawn.IsDeactivated() || pawn.IsSelfShutdown() || PawnUtility.PlayerForcedJobNowOrSoon(pawn))
        {
            return null;
        }
        byte mask = GasPawnMaskCache.MaskFor(pawn);
        if (mask == 0 || !GasBands.AnyBandAtLeast(here, mask, GasBands.Low))
        {
            return null;
        }
        IntVec3 safe = GasSafeCellFinder.FindSafeCell(pawn, grid, mask, out IntVec3 leastGassed);
        if (safe.IsValid)
        {
            return MakeFleeJob(safe);
        }
        if (GasBreakout.CanBreakOut(pawn))
        {
            Job breakout = GasBreakout.TryMakeJob(pawn, grid, mask);
            if (breakout != null)
            {
                return breakout;
            }
        }
        if (leastGassed.IsValid && leastGassed != pawn.Position)
        {
            return MakeFleeJob(leastGassed);
        }
        return null;
    }

    private static Job MakeFleeJob(IntVec3 cell)
    {
        Job job = JobMaker.MakeJob(GVDefOf.GV_FleeGas, cell);
        job.locomotionUrgency = LocomotionUrgency.Sprint;
        job.expiryInterval = FleeExpiryTicks;
        job.checkOverrideOnExpire = true;
        return job;
    }
}
