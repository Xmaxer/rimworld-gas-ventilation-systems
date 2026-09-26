using System.Collections.Generic;
using GasVentilation.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace GasVentilation;

/// <summary>
/// Trapped hostiles and wild animals path to the nearest gas-free cell outside their room, treating walls and
/// doors as destroyable. They then use the vanilla sapper helper to attack or mine the first blocker.
/// </summary>
public static class GasBreakout
{
    private const float SearchRadius = 20f;
    private const int CooldownTicks = 300;
    private const int MaxTracked = 1024;
    private const int GotoExpiryTicks = 500;

    private static readonly Dictionary<int, int> LastAttempt = new Dictionary<int, int>();
    private static Game cachedGame;

    public static bool CanBreakOut(Pawn pawn)
    {
        if (pawn.IsPrisoner || pawn.IsSlave || pawn.Faction == Faction.OfPlayer)
        {
            return false;
        }
        if (pawn.HostileTo(Faction.OfPlayer))
        {
            return true;
        }
        return pawn.Faction == null && pawn.RaceProps.Animal;
    }

    public static Job TryMakeJob(Pawn pawn, VentGasGrid grid, byte mask)
    {
        if (Current.Game != cachedGame)
        {
            LastAttempt.Clear();
            cachedGame = Current.Game;
        }
        int now = Find.TickManager.TicksGame;
        if (LastAttempt.TryGetValue(pawn.thingIDNumber, out int last) && now - last < CooldownTicks && now >= last)
        {
            return null;
        }
        if (LastAttempt.Count >= MaxTracked)
        {
            LastAttempt.Clear();
        }
        LastAttempt[pawn.thingIDNumber] = now;

        Map map = pawn.Map;
        Room room = pawn.GetRoom();
        IntVec3 target = IntVec3.Invalid;
        int cells = GenRadial.NumCellsInRadius(SearchRadius);
        for (int i = 1; i < cells; i++)
        {
            IntVec3 cell = pawn.Position + GenRadial.RadialPattern[i];
            if (!cell.InBounds(map) || !cell.Standable(map) || GasPacking.MaxMaskedDensity(grid.PackedAt(cell), mask) > 0)
            {
                continue;
            }
            if (room != null && cell.GetRoom(map) == room)
            {
                continue;
            }
            target = cell;
            break;
        }
        if (!target.IsValid)
        {
            return null;
        }
        if (!pawn.CanReach(target, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.PassAllDestroyableThings))
        {
            return null;
        }
        TraverseParms parms = TraverseParms.For(pawn, Danger.Deadly, TraverseMode.PassAllDestroyableThings);
        using (PawnPath path = map.pathFinder.FindPathNow(pawn.Position, target, parms))
        {
            if (path == null || !path.Found)
            {
                return null;
            }
            Thing blocker = path.FirstBlockingBuilding(out IntVec3 cellBefore, pawn);
            if (blocker != null)
            {
                Job pass = DigUtility.PassBlockerJob(pawn, blocker, cellBefore, true, true);
                if (pass != null)
                {
                    return pass;
                }
            }
        }
        // Nothing to break: walk out, bashing doors or fences on the way (door-only traps).
        Job job = JobMaker.MakeJob(JobDefOf.Goto, target, GotoExpiryTicks, true);
        job.canBashDoors = true;
        job.canBashFences = true;
        job.locomotionUrgency = LocomotionUrgency.Sprint;
        return job;
    }
}
