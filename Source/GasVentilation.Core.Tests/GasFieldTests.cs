using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace GasVentilation.Core.Tests;

public class GasFieldTests
{
    private sealed class RecordingListener : IGasFieldListener
    {
        public readonly List<int> Cells = new List<int>();

        public void OnBandsChanged(int cellIndex, byte oldBands, byte newBands)
        {
            Cells.Add(cellIndex);
        }
    }

    [Test]
    public void Constructor_RejectsOrderThatIsNotAPermutation()
    {
        // NUnit 4.6 has both TestDelegate and Action overloads, so a bare lambda is ambiguous; pick Action.
        Assert.Throws<ArgumentException>(new Action(() => new GasField(2, 2, new[] { 0, 1, 1, 3 })));
        Assert.Throws<ArgumentException>(new Action(() => new GasField(2, 2, new[] { 0, 1, 2 })));
    }

    [Test]
    public void Set_TracksLiveCells()
    {
        GasField field = new GasField(4, 4, TestOrders.Identity(16));
        field.Set(5, 5u);
        Assert.That(field.LiveCells, Is.EqualTo(1));
        field.Set(5, 7u);
        Assert.That(field.LiveCells, Is.EqualTo(1));
        field.Set(6, 1u);
        Assert.That(field.LiveCells, Is.EqualTo(2));
        field.Set(5, 0u);
        Assert.That(field.LiveCells, Is.EqualTo(1));
    }

    [Test]
    public void NextOccupied_FindsPositionsAcrossWordBoundaries()
    {
        GasField field = new GasField(20, 10, TestOrders.Identity(200));
        field.Set(3, 1u);
        field.Set(63, 1u);
        field.Set(64, 1u);
        field.Set(130, 1u);
        Assert.That(field.NextOccupied(0, 200), Is.EqualTo(3));
        Assert.That(field.NextOccupied(4, 200), Is.EqualTo(63));
        Assert.That(field.NextOccupied(64, 200), Is.EqualTo(64));
        Assert.That(field.NextOccupied(65, 200), Is.EqualTo(130));
        Assert.That(field.NextOccupied(131, 200), Is.EqualTo(200));
        Assert.That(field.NextOccupied(0, 3), Is.EqualTo(3));
    }

    [Test]
    public void NextOccupied_UsesRandomOrderPositions()
    {
        int[] order = TestOrders.Shuffled(100, 7);
        GasField field = new GasField(10, 10, order);
        field.Set(42, 9u);
        int position = field.NextOccupied(0, 100);
        Assert.That(field.OrderToCell(position), Is.EqualTo(42));
        Assert.That(field.NextOccupied(position + 1, 100), Is.EqualTo(100));
    }

    [Test]
    public void TryAdd_CapsAtMaxAndReturnsAccepted()
    {
        GasField field = new GasField(2, 2, TestOrders.Identity(4));
        Assert.That(field.TryAdd(0, 1, 200), Is.EqualTo(200));
        Assert.That(field.TryAdd(0, 1, 200), Is.EqualTo(55));
        Assert.That(field.Get(0, 1), Is.EqualTo(255));
        Assert.That(field.TryAdd(0, 1, 10), Is.EqualTo(0));
        Assert.That(field.TryAdd(0, 2, 0), Is.EqualTo(0));
        Assert.That(field.TryAdd(0, 2, -4), Is.EqualTo(0));
    }

    [Test]
    public void Listener_IsCalledOnlyWhenABandChanges()
    {
        RecordingListener listener = new RecordingListener();
        GasField field = new GasField(2, 2, TestOrders.Identity(4)) { Listener = listener };
        field.Set(1, 10u);
        field.Set(1, 20u);
        field.Set(1, 100u);
        Assert.That(listener.Cells, Is.EqualTo(new[] { 1, 1 }));
    }

    [Test]
    public void SetSilently_UpdatesCountsWithoutNotifying()
    {
        RecordingListener listener = new RecordingListener();
        GasField field = new GasField(2, 2, TestOrders.Identity(4)) { Listener = listener };
        field.SetSilently(2, 200u);
        Assert.That(field.LiveCells, Is.EqualTo(1));
        Assert.That(field.NextOccupied(0, 4), Is.EqualTo(2));
        Assert.That(listener.Cells, Is.Empty);
    }

    [Test]
    public void SnapshotAndRestore_RoundTripAndRebuildDerivedState()
    {
        GasField a = new GasField(8, 8, TestOrders.Shuffled(64, 3));
        a.Set(10, 0x00FF0011u);
        a.Set(33, 0x01000000u);
        uint[] snapshot = a.Snapshot();

        GasField b = new GasField(8, 8, TestOrders.Shuffled(64, 3));
        b.Restore(snapshot);
        Assert.That(b.Snapshot(), Is.EqualTo(snapshot));
        Assert.That(b.LiveCells, Is.EqualTo(2));
        Assert.Throws<ArgumentException>(new Action(() => b.Restore(new uint[5])));
    }

    [Test]
    public void ClearAll_EmptiesEverything()
    {
        GasField field = new GasField(4, 4, TestOrders.Identity(16));
        field.Set(1, 1u);
        field.Set(15, 1u);
        field.ClearAll();
        Assert.That(field.LiveCells, Is.EqualTo(0));
        Assert.That(field.NextOccupied(0, 16), Is.EqualTo(16));
    }
}
