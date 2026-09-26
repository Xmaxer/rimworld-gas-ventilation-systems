using NUnit.Framework;

namespace GasVentilation.Core.Tests;

public class GasSimulatorDissipationTests
{
    // An 8x8 field has 64 cells: dissipation visits 1 cell per tick, so one full sweep takes 64 ticks.
    private static GasSimulator Make(float dissipation, out GasField field, out TestEnvironment env, float roofedFactor = 0.5f)
    {
        field = new GasField(8, 8, TestOrders.Shuffled(64, 11));
        env = new TestEnvironment(64);
        return new GasSimulator(field, TestEnvironment.Settings(dissipation, diffuses: false, roofedFactor: roofedFactor), env);
    }

    private static void Run(GasSimulator sim, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }

    [Test]
    public void Tick_WithNoGas_DoesNotMoveCursors()
    {
        GasSimulator sim = Make(3f, out _, out _);
        Run(sim, 10);
        Assert.That(sim.DissipationCursor, Is.EqualTo(0));
        Assert.That(sim.DiffusionCursor, Is.EqualTo(0));
    }

    [Test]
    public void Dissipation_RemovesRateOncePerSweep()
    {
        GasSimulator sim = Make(3f, out GasField field, out _);
        field.Set(10, GasPacking.With(0u, 0, 100));
        Run(sim, 64);
        Assert.That(field.Get(10, 0), Is.EqualTo(97));
    }

    [Test]
    public void Dissipation_IsScaledUnderRoof()
    {
        GasSimulator sim = Make(4f, out GasField field, out TestEnvironment env, roofedFactor: 0.5f);
        env.Roofed[10] = true;
        field.Set(10, GasPacking.With(0u, 2, 100));
        Run(sim, 64);
        Assert.That(field.Get(10, 2), Is.EqualTo(98));
    }

    [Test]
    public void Dissipation_FractionalRateAveragesOut()
    {
        GasSimulator sim = Make(0.25f, out GasField field, out _);
        for (int i = 0; i < 64; i++)
        {
            field.Set(i, GasPacking.With(0u, 1, 200));
        }
        Run(sim, 64 * 40);
        int removed = 0;
        for (int i = 0; i < 64; i++)
        {
            removed += 200 - field.Get(i, 1);
        }
        double meanRemoved = removed / 64.0;
        Assert.That(meanRemoved, Is.InRange(8.0, 12.0), "expected about 40 sweeps x 0.25 = 10 per cell");
    }

    [Test]
    public void Dissipation_EmptiedCellLeavesLiveCount()
    {
        GasSimulator sim = Make(3f, out GasField field, out _);
        field.Set(10, GasPacking.With(0u, 0, 2));
        Run(sim, 64);
        Assert.That(field.Get(10), Is.EqualTo(0u));
        Assert.That(field.LiveCells, Is.EqualTo(0));
    }

    [Test]
    public void Dissipation_VacuumStripsGasFast()
    {
        GasSimulator sim = Make(1f, out GasField field, out TestEnvironment env);
        env.VacuumEverywhere = 1f;
        field.Set(10, GasPacking.With(0u, 0, 255));
        Run(sim, 64 * 10);
        Assert.That(field.LiveCells, Is.EqualTo(0));
    }

    [Test]
    public void LazyClear_RemovesGasFromCellsThatCanNoLongerHoldIt()
    {
        GasSimulator sim = Make(0f, out GasField field, out TestEnvironment env);
        field.Set(10, GasPacking.With(0u, 0, 200));
        env.Blocked[10] = true;
        Run(sim, 64);
        Assert.That(field.Get(10), Is.EqualTo(0u));
    }
}
