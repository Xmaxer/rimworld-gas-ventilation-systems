using System;
using GasVentilation.Core;
using Unity.Collections;
using Verse;

namespace GasVentilation;

/// <summary>
/// Per-cell extra path cost for pawns affected by the gases in <see cref="Mask"/>. The grid is only written from
/// <see cref="GasPathSource"/> update calls (or at creation, before any job can see it), never while path jobs could
/// be reading it. Costs stay below 10000, so gas never blocks a cell; pawns already inside can always walk out.
/// </summary>
public sealed class GasPathCustomizer : PathRequest.IPathGridCustomizer, IDisposable
{
    private static readonly ushort[] BandCost = { 0, 120, 300, 600 };
    private const int MaxCost = 9000;

    private NativeArray<ushort> grid;

    public GasPathCustomizer(int cellCount, byte mask)
    {
        Mask = mask;
        grid = new NativeArray<ushort>(cellCount, Allocator.Persistent);
    }

    public byte Mask { get; }

    public NativeArray<ushort> GetOffsetGrid()
    {
        return grid;
    }

    public void RebuildFrom(byte[] packedBands)
    {
        for (int i = 0; i < packedBands.Length; i++)
        {
            grid[i] = Cost(packedBands[i]);
        }
    }

    public void UpdateCell(int cellIndex, byte packedBands)
    {
        grid[cellIndex] = Cost(packedBands);
    }

    public void Dispose()
    {
        if (grid.IsCreated)
        {
            grid.Dispose();
        }
    }

    private ushort Cost(byte packedBands)
    {
        if (packedBands == 0)
        {
            return 0;
        }
        int total = 0;
        for (int channel = 0; channel < GasPacking.Channels; channel++)
        {
            if ((Mask & (1 << channel)) != 0)
            {
                total += BandCost[GasBands.Unpack(packedBands, channel)];
            }
        }
        return (ushort)(total > MaxCost ? MaxCost : total);
    }
}
