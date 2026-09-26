using System;
using System.Collections.Generic;
using Verse;

namespace GasVentilation;

/// <summary>
/// Every 30 ticks, and only while the map has gas, reads the cell under each spawned pawn and applies doses for the
/// real elapsed time. Independent of 1.6's camera-dependent pawn tick rates. No pawn-tick patch is needed.
/// </summary>
public sealed class GasExposureScanner : MapComponent
{
    public const int ScanInterval = 30;
    private const int MaxElapsed = 250;

    private readonly List<Pawn> hits = new List<Pawn>();
    private readonly List<uint> hitPacked = new List<uint>();
    private int lastScanTick = -1;

    public GasExposureScanner(Map map) : base(map)
    {
    }

    public override void MapComponentTick()
    {
        int now = Find.TickManager.TicksGame;
        VentGasGrid grid = VentGasGrid.For(map);
        if (grid == null || grid.LiveCells == 0)
        {
            lastScanTick = now;
            return;
        }
        if (lastScanTick < 0)
        {
            lastScanTick = now - ScanInterval;
        }
        if (now - lastScanTick < ScanInterval)
        {
            return;
        }
        int elapsed = Math.Min(now - lastScanTick, MaxElapsed);
        lastScanTick = now;

        IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
        CellIndices indices = map.cellIndices;
        for (int i = 0; i < pawns.Count; i++)
        {
            Pawn pawn = pawns[i];
            if (pawn.Dead)
            {
                continue;
            }
            uint packed = grid.PackedAt(indices.CellToIndex(pawn.Position));
            if (packed == 0u)
            {
                continue;
            }
            hits.Add(pawn);
            hitPacked.Add(packed);
        }
        // Apply after the loop: effects can kill, down or despawn pawns, which mutates the spawned list.
        for (int i = 0; i < hits.Count; i++)
        {
            Pawn pawn = hits[i];
            if (pawn.Spawned && !pawn.Dead)
            {
                GasExposure.ExposeTo(pawn, hitPacked[i], elapsed);
            }
        }
        hits.Clear();
        hitPacked.Clear();
    }
}
