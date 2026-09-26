using System;

namespace GasVentilation.Core;

/// <summary>
/// Rolling random-order sweep, mirroring vanilla GasGrid: every tick, ceil(N/64) visiting positions are
/// dissipated and ceil(N/32) are diffused. Empty stretches are skipped using the field's occupancy bitset.
/// </summary>
public sealed class GasSimulator
{
    private static readonly int[][] Permutations = BuildPermutations();

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
        uint a = field.Get(cell);
        if (!CanDiffuse(a))
        {
            return;
        }
        int[] order = Permutations[env.RandomInt(Permutations.Length)];
        bool sourceChanged = false;
        for (int i = 0; i < 4; i++)
        {
            int neighbour = Neighbour(cell, order[i]);
            if (neighbour < 0 || !env.CanHoldGas(neighbour))
            {
                continue;
            }
            uint b = field.Get(neighbour);
            uint newA = a;
            uint newB = b;
            bool moved = false;
            for (int channel = 0; channel < GasPacking.Channels; channel++)
            {
                GasChannelSettings s = channels[channel];
                if (!s.Diffuses)
                {
                    continue;
                }
                int da = GasPacking.Get(newA, channel);
                if (da < s.MinDiffusion)
                {
                    continue;
                }
                int db = GasPacking.Get(newB, channel);
                if (da <= db)
                {
                    continue;
                }
                int half = (da - db) / 2;
                if (half < s.MinDiffusion)
                {
                    continue;
                }
                newA = GasPacking.With(newA, channel, da - half);
                newB = GasPacking.With(newB, channel, db + half);
                moved = true;
            }
            if (!moved)
            {
                continue;
            }
            field.Set(neighbour, newB);
            a = newA;
            sourceChanged = true;
            if (!CanDiffuse(a))
            {
                break;
            }
        }
        if (sourceChanged)
        {
            field.Set(cell, a);
        }
    }

    private bool CanDiffuse(uint packed)
    {
        for (int channel = 0; channel < GasPacking.Channels; channel++)
        {
            GasChannelSettings s = channels[channel];
            if (s.Diffuses && GasPacking.Get(packed, channel) >= s.MinDiffusion)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Directions: 0 = north (+z), 1 = east (+x), 2 = south (-z), 3 = west (-x). Returns -1 off-grid.</summary>
    private int Neighbour(int cell, int direction)
    {
        int width = field.Width;
        int x = cell % width;
        int z = cell / width;
        switch (direction)
        {
            case 0:
                return z + 1 < field.Height ? cell + width : -1;
            case 1:
                return x + 1 < width ? cell + 1 : -1;
            case 2:
                return z > 0 ? cell - width : -1;
            default:
                return x > 0 ? cell - 1 : -1;
        }
    }

    private static int[][] BuildPermutations()
    {
        int[][] result = new int[24][];
        int n = 0;
        for (int a = 0; a < 4; a++)
        {
            for (int b = 0; b < 4; b++)
            {
                if (b == a)
                {
                    continue;
                }
                for (int c = 0; c < 4; c++)
                {
                    if (c == a || c == b)
                    {
                        continue;
                    }
                    int d = 6 - a - b - c;
                    result[n++] = new[] { a, b, c, d };
                }
            }
        }
        return result;
    }
}
