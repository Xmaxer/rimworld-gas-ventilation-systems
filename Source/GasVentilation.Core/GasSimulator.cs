using System;

namespace GasVentilation.Core;

/// <summary>
/// Rolling random-order sweep, mirroring vanilla GasGrid: every tick, ceil(N/64) visiting positions are
/// dissipated and ceil(N/32) are diffused. Empty stretches are skipped using the field's occupancy bitset.
/// </summary>
public sealed class GasSimulator
{
    private readonly GasField field;
    private readonly GasChannelSettings[] channels;
    private readonly IGasEnvironment env;

    /// <summary>Saved with the map.</summary>
    public int DissipationCursor;

    /// <summary>Saved with the map.</summary>
    public int DiffusionCursor;

    /// <summary>Number of completed dissipation sweeps; seeds fractional dissipation. Saved with the map.</summary>
    public int DissipationSweep;

    public GasSimulator(GasField field, GasChannelSettings[] channels, IGasEnvironment env)
    {
        this.field = field ?? throw new ArgumentNullException(nameof(field));
        if (channels == null || channels.Length != GasPacking.Channels)
        {
            throw new ArgumentException("exactly four channel settings are required", nameof(channels));
        }
        this.channels = channels;
        this.env = env ?? throw new ArgumentNullException(nameof(env));
    }

    public void Tick()
    {
        if (field.LiveCells == 0)
        {
            return;
        }
        int n = field.CellCount;
        RunWindow(ref DissipationCursor, (n + 63) / 64, dissipate: true);
        if (field.LiveCells == 0)
        {
            return;
        }
        RunWindow(ref DiffusionCursor, (n + 31) / 32, dissipate: false);
    }

    private void RunWindow(ref int cursor, int count, bool dissipate)
    {
        int n = field.CellCount;
        if (cursor < 0 || cursor >= n)
        {
            cursor = 0;
        }
        if (count > n)
        {
            count = n;
        }
        int end = cursor + count;
        if (end < n)
        {
            ProcessRange(cursor, end, dissipate);
            cursor = end;
            return;
        }
        ProcessRange(cursor, n, dissipate);
        if (dissipate)
        {
            DissipationSweep++;
        }
        int rest = end - n;
        if (rest > 0)
        {
            ProcessRange(0, rest, dissipate);
        }
        cursor = rest;
    }

    private void ProcessRange(int from, int to, bool dissipate)
    {
        for (int position = field.NextOccupied(from, to); position < to; position = field.NextOccupied(position + 1, to))
        {
            int cell = field.OrderToCell(position);
            if (!env.CanHoldGas(cell))
            {
                field.Set(cell, 0u);
                continue;
            }
            if (dissipate)
            {
                Dissipate(cell);
            }
            else
            {
                Diffuse(cell);
            }
        }
    }

    private void Dissipate(int cell)
    {
        uint packed = field.Get(cell);
        uint result = packed;
        bool roofed = env.IsRoofed(cell);
        float vacuum = env.Vacuum(cell);
        for (int channel = 0; channel < GasPacking.Channels; channel++)
        {
            int density = GasPacking.Get(packed, channel);
            if (density == 0)
            {
                continue;
            }
            GasChannelSettings s = channels[channel];
            float rate = s.DissipationPerVisit * (roofed ? s.RoofedFactor : 1f) + vacuum * s.VacuumFactor;
            if (rate <= 0f)
            {
                continue;
            }
            int whole = (int)rate;
            float fraction = rate - whole;
            int amount = whole;
            if (fraction > 0f && (GasHash.Mix(cell, DissipationSweep, channel) & 0xFFFFu) < (uint)(fraction * 65536f))
            {
                amount++;
            }
            if (amount > 0)
            {
                result = GasPacking.With(result, channel, density - amount);
            }
        }
        if (result != packed)
        {
            field.Set(cell, result);
        }
    }

    private void Diffuse(int cell)
    {
    }
}
