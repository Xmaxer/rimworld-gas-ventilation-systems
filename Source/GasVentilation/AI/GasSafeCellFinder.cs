using GasVentilation.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace GasVentilation;

/// <summary>
/// Region breadth-first search (capped) from the pawn to the nearest reachable cell without gas that affects it.
/// Also tracks the least-gassed cell seen, as a fallback. Main thread only (static search state). The delegates are
/// cached statics, so a search allocates nothing.
/// </summary>
public static class GasSafeCellFinder
{
    public const int MaxRegions = 40;
    private const int SamplesPerRegion = 12;
    private const int MeaningfulImprovement = 20;

    private static readonly RegionEntryPredicate EntryCondition = AllowsEntry;
    private static readonly RegionProcessor Processor = ProcessRegion;

    private static Pawn searcher;
    private static VentGasGrid searchGrid;
    private static int searchMask;
    private static TraverseParms traverseParms;
    private static IntVec3 foundSafe;
    private static IntVec3 bestCell;
    private static int bestDensity;

    public static IntVec3 FindSafeCell(Pawn pawn, VentGasGrid grid, byte mask, out IntVec3 leastGassed)
    {
        leastGassed = IntVec3.Invalid;
        Region root = pawn.Position.GetRegion(pawn.Map);
        if (root == null)
        {
            return IntVec3.Invalid;
        }
        searcher = pawn;
        searchGrid = grid;
        searchMask = mask;
        traverseParms = TraverseParms.For(pawn);
        foundSafe = IntVec3.Invalid;
        bestCell = IntVec3.Invalid;
        bestDensity = GasPacking.MaxMaskedDensity(grid.PackedAt(pawn.Position), mask);
        try
        {
            RegionTraverser.BreadthFirstTraverse(root, EntryCondition, Processor, MaxRegions);
            leastGassed = bestCell;
            return foundSafe;
        }
        finally
        {
            searcher = null;
            searchGrid = null;
        }
    }

    private static bool AllowsEntry(Region from, Region to)
    {
        return to.Allows(traverseParms, false);
    }

    private static bool ProcessRegion(Region region)
    {
        if (region.IsDoorway)
        {
            return false;
        }
        Map map = searcher.Map;
        bool restrictToArea = searcher.Faction == Faction.OfPlayer;
        for (int i = 0; i < SamplesPerRegion; i++)
        {
            IntVec3 cell = region.RandomCell;
            if (!cell.Standable(map) || cell.Fogged(map) || (restrictToArea && !cell.InAllowedArea(searcher)))
            {
                continue;
            }
            int density = GasPacking.MaxMaskedDensity(searchGrid.PackedAt(cell), searchMask);
            if (density == 0)
            {
                foundSafe = cell;
                return true;
            }
            if (density < bestDensity - MeaningfulImprovement)
            {
                bestDensity = density;
                bestCell = cell;
            }
        }
        return false;
    }
}
