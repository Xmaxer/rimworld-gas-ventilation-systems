using NUnit.Framework;

namespace GasVentilation.Core.Tests;

public class GasSimulatorDiffusionTests
{
    private const int Size = 9;
    private const int Count = Size * Size;

    private static int Index(int x, int z) => z * Size + x;

    private static GasSimulator Make(out GasField field, out TestEnvironment env, int seed = 5)
    {
        field = new GasField(Size, Size, TestOrders.Shuffled(Count, 13));
        env = new TestEnvironment(Count, seed);
        return new GasSimulator(field, TestEnvironment.Settings(dissipation: 0f, diffuses: true), env);
    }

    private static int Total(GasField field, int channel)
    {
        int total = 0;
        for (int i = 0; i < Count; i++)
        {
            total += field.Get(i, channel);
        }
        return total;
    }

    [Test]
    public void Diffusion_SpreadsToNeighboursAndConservesGas()
    {
        GasSimulator sim = Make(out GasField field, out _);
        field.Set(Index(4, 4), GasPacking.With(0u, 3, 255));
        for (int i = 0; i < 300; i++)
        {
            sim.Tick();
        }
        Assert.That(Total(field, 3), Is.EqualTo(255));
        Assert.That(field.Get(Index(5, 4), 3), Is.GreaterThan(0));
        Assert.That(field.Get(Index(4, 4), 3), Is.LessThan(255));
    }

    [Test]
    public void Diffusion_IsBlockedByCellsThatCannotHoldGas()
    {
        GasSimulator sim = Make(out GasField field, out TestEnvironment env);
        for (int z = 0; z < Size; z++)
        {
            env.Blocked[Index(4, z)] = true;
        }
        field.Set(Index(1, 4), GasPacking.With(0u, 0, 255));
        for (int i = 0; i < 2000; i++)
        {
            sim.Tick();
        }
        for (int z = 0; z < Size; z++)
        {
            for (int x = 4; x < Size; x++)
            {
                Assert.That(field.Get(Index(x, z)), Is.EqualTo(0u), $"gas crossed the wall at ({x},{z})");
            }
        }
    }

    [Test]
    public void Diffusion_IgnoresSmallGradients()
    {
        GasSimulator sim = Make(out GasField field, out _);
        field.Set(Index(4, 4), GasPacking.With(0u, 1, 40));
        field.Set(Index(5, 4), GasPacking.With(0u, 1, 10));
        field.Set(Index(3, 4), GasPacking.With(0u, 1, 10));
        field.Set(Index(4, 5), GasPacking.With(0u, 1, 10));
        field.Set(Index(4, 3), GasPacking.With(0u, 1, 10));
        for (int i = 0; i < 100; i++)
        {
            sim.Tick();
        }
        Assert.That(field.Get(Index(4, 4), 1), Is.EqualTo(40));
    }

    [Test]
    public void Diffusion_MovesEveryChannelIndependently()
    {
        GasSimulator sim = Make(out GasField field, out _);
        uint packed = GasPacking.With(GasPacking.With(0u, 0, 255), 2, 200);
        field.Set(Index(4, 4), packed);
        for (int i = 0; i < 200; i++)
        {
            sim.Tick();
        }
        Assert.That(Total(field, 0), Is.EqualTo(255));
        Assert.That(Total(field, 2), Is.EqualTo(200));
        Assert.That(field.LiveCells, Is.GreaterThan(1));
    }

    [Test]
    public void Simulation_IsDeterministicForTheSameSeed()
    {
        GasSimulator a = Make(out GasField fa, out _, seed: 99);
        GasSimulator b = Make(out GasField fb, out _, seed: 99);
        fa.Set(Index(2, 2), GasPacking.With(0u, 1, 255));
        fb.Set(Index(2, 2), GasPacking.With(0u, 1, 255));
        for (int i = 0; i < 300; i++)
        {
            a.Tick();
            b.Tick();
        }
        Assert.That(fa.Snapshot(), Is.EqualTo(fb.Snapshot()));
    }
}
