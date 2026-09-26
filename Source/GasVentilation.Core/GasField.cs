using System;

namespace GasVentilation.Core;

/// <summary>
/// Per-cell gas storage. Four byte channels are packed in one uint per cell (the same layout as vanilla
/// GasGrid). The field keeps an exact count of non-empty cells, and an occupancy bitset indexed by position in a
/// fixed random visiting order, so the simulator can skip empty stretches 64 cells at a time.
/// All writes go through <see cref="SetInternal"/>.
/// </summary>
public sealed class GasField
{
    private static readonly int[] DeBruijnIndex =
    {
        0, 1, 48, 2, 57, 49, 28, 3, 61, 58, 50, 42, 38, 29, 17, 4,
        62, 55, 59, 36, 53, 51, 43, 22, 45, 39, 33, 30, 24, 18, 12, 5,
        63, 47, 56, 27, 60, 41, 37, 16, 54, 35, 52, 21, 44, 32, 23, 11,
        46, 26, 40, 15, 34, 20, 31, 10, 25, 14, 19, 9, 13, 8, 7, 6
    };

    private readonly uint[] cells;
    private readonly int[] orderToCell;
    private readonly int[] cellToOrder;
    private readonly ulong[] occupied;

    public GasField(int width, int height, int[] randomOrder)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }
        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }
        Width = width;
        Height = height;
        CellCount = width * height;
        if (randomOrder == null || randomOrder.Length != CellCount)
        {
            throw new ArgumentException("randomOrder must contain every cell index exactly once", nameof(randomOrder));
        }
        cells = new uint[CellCount];
        orderToCell = (int[])randomOrder.Clone();
        cellToOrder = new int[CellCount];
        for (int i = 0; i < CellCount; i++)
        {
            cellToOrder[i] = -1;
        }
        for (int position = 0; position < CellCount; position++)
        {
            int cell = orderToCell[position];
            if (cell < 0 || cell >= CellCount || cellToOrder[cell] != -1)
            {
                throw new ArgumentException("randomOrder is not a permutation of the cell indices", nameof(randomOrder));
            }
            cellToOrder[cell] = position;
        }
        occupied = new ulong[(CellCount + 63) >> 6];
    }

    public int Width { get; }

    public int Height { get; }

    public int CellCount { get; }

    public int LiveCells { get; private set; }

    public IGasFieldListener Listener { get; set; }

    public uint Get(int index)
    {
        return cells[index];
    }

    public int Get(int index, int channel)
    {
        return GasPacking.Get(cells[index], channel);
    }

    public int OrderToCell(int position)
    {
        return orderToCell[position];
    }

    public void Set(int index, uint value)
    {
        SetInternal(index, value, notify: true);
    }

    /// <summary>Write without notifying the listener (loading, bulk restores).</summary>
    public void SetSilently(int index, uint value)
    {
        SetInternal(index, value, notify: false);
    }

    /// <summary>Adds up to <paramref name="amount"/> to one channel, capped at 255. Returns the amount accepted.</summary>
    public int TryAdd(int index, int channel, int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }
        uint packed = cells[index];
        int density = GasPacking.Get(packed, channel);
        int accepted = Math.Min(amount, GasPacking.MaxDensity - density);
        if (accepted <= 0)
        {
            return 0;
        }
        Set(index, GasPacking.With(packed, channel, density + accepted));
        return accepted;
    }

    public void Clear(int index)
    {
        Set(index, 0u);
    }

    public void ClearAll()
    {
        for (int i = 0; i < CellCount; i++)
        {
            if (cells[i] != 0u)
            {
                Set(i, 0u);
            }
        }
    }

    /// <summary>
    /// Returns the first visiting-order position in [from, toExclusive) whose cell holds gas,
    /// or toExclusive when there is none.
    /// </summary>
    public int NextOccupied(int from, int toExclusive)
    {
        if (toExclusive > CellCount)
        {
            toExclusive = CellCount;
        }
        int position = from;
        while (position < toExclusive)
        {
            int word = position >> 6;
            ulong bits = occupied[word] >> (position & 63);
            if (bits != 0UL)
            {
                int found = position + TrailingZeroCount(bits);
                return found < toExclusive ? found : toExclusive;
            }
            position = (word + 1) << 6;
        }
        return toExclusive;
    }

    public uint[] Snapshot()
    {
        return (uint[])cells.Clone();
    }

    public void Restore(uint[] data)
    {
        if (data == null || data.Length != CellCount)
        {
            throw new ArgumentException("snapshot length does not match the field", nameof(data));
        }
        for (int i = 0; i < CellCount; i++)
        {
            SetSilently(i, data[i]);
        }
    }

    private void SetInternal(int index, uint value, bool notify)
    {
        uint old = cells[index];
        if (old == value)
        {
            return;
        }
        cells[index] = value;
        if (old == 0u)
        {
            LiveCells++;
            int position = cellToOrder[index];
            occupied[position >> 6] |= 1UL << (position & 63);
        }
        else if (value == 0u)
        {
            LiveCells--;
            int position = cellToOrder[index];
            occupied[position >> 6] &= ~(1UL << (position & 63));
        }
        if (notify && Listener != null)
        {
            byte oldBands = GasBands.Pack(old);
            byte newBands = GasBands.Pack(value);
            if (oldBands != newBands)
            {
                Listener.OnBandsChanged(index, oldBands, newBands);
            }
        }
    }

    private static int TrailingZeroCount(ulong value)
    {
        ulong lowest = value & (ulong)(-(long)value);
        return DeBruijnIndex[(lowest * 0x03F79D71B4CB0A89UL) >> 58];
    }
}
