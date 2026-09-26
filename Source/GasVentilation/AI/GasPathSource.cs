using System.Collections.Generic;
using GasVentilation.Core;
using Verse;

namespace GasVentilation;

/// <summary>
/// Registered with the map's PathFinderMapData, which owns it and disposes it after the pathfinder has completed
/// all jobs. Band changes from the gas grid are queued on the main thread, and applied to the customizer grids at
/// most every 60 ticks inside UpdateIncrementally. The game calls that (on a pool thread, while the main thread
/// waits and after all scheduled path jobs have completed) before scheduling new path jobs.
/// </summary>
public sealed class GasPathSource : IPathFinderDataSource
{
    public const int ApplyInterval = 60;

    private readonly VentGasGrid grid;
    private readonly int cellCount;
    private readonly byte[] appliedBands;
    private readonly bool[] pendingFlag;
    private readonly List<int> pending = new List<int>();
    private readonly GasPathCustomizer[] customizers = new GasPathCustomizer[16];
    private int lastApplyTick = -99999;
    private bool disposed;

    public GasPathSource(Map map, VentGasGrid grid)
    {
        this.grid = grid;
        cellCount = map.cellIndices.NumGridCells;
        appliedBands = new byte[cellCount];
        pendingFlag = new bool[cellCount];
        for (int i = 0; i < cellCount; i++)
        {
            appliedBands[i] = GasBands.Pack(grid.PackedAt(i));
        }
    }

    public bool Disposed => disposed;

    /// <summary>Main thread (grid listener).</summary>
    public void MarkDirty(int cellIndex)
    {
        if (disposed || pendingFlag[cellIndex])
        {
            return;
        }
        pendingFlag[cellIndex] = true;
        pending.Add(cellIndex);
    }

    /// <summary>Main thread. Returns null for an empty mask.</summary>
    public GasPathCustomizer CustomizerFor(byte mask)
    {
        mask &= 0x0F;
        if (mask == 0 || disposed)
        {
            return null;
        }
        GasPathCustomizer customizer = customizers[mask];
        if (customizer == null)
        {
            // A new grid is invisible to jobs until a request carrying it is scheduled, so writing it here is safe.
            customizer = new GasPathCustomizer(cellCount, mask);
            customizer.RebuildFrom(appliedBands);
            customizers[mask] = customizer;
        }
        return customizer;
    }

    public void ComputeAll(IEnumerable<PathRequest> requests)
    {
        if (disposed)
        {
            return;
        }
        for (int i = 0; i < cellCount; i++)
        {
            appliedBands[i] = GasBands.Pack(grid.PackedAt(i));
        }
        for (int i = 0; i < pending.Count; i++)
        {
            pendingFlag[pending[i]] = false;
        }
        pending.Clear();
        for (int m = 1; m < customizers.Length; m++)
        {
            customizers[m]?.RebuildFrom(appliedBands);
        }
    }

    public bool UpdateIncrementally(IEnumerable<PathRequest> requests, List<IntVec3> cellDeltas)
    {
        if (disposed || pending.Count == 0)
        {
            return false;
        }
        int now = GenTicks.TicksGame;
        if (now - lastApplyTick < ApplyInterval && now >= lastApplyTick)
        {
            return false;
        }
        lastApplyTick = now;
        bool changed = false;
        for (int i = 0; i < pending.Count; i++)
        {
            int cell = pending[i];
            pendingFlag[cell] = false;
            byte bands = GasBands.Pack(grid.PackedAt(cell));
            if (bands == appliedBands[cell])
            {
                continue;
            }
            appliedBands[cell] = bands;
            changed = true;
            for (int m = 1; m < customizers.Length; m++)
            {
                customizers[m]?.UpdateCell(cell, bands);
            }
        }
        pending.Clear();
        return changed;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        for (int m = 1; m < customizers.Length; m++)
        {
            customizers[m]?.Dispose();
            customizers[m] = null;
        }
    }
}
