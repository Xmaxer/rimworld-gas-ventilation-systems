using System.Diagnostics;
using NUnit.Framework;

namespace GasVentilation.Core.Tests;

public class GasSimulatorBenchmarkTests
{
    [Test]
    [Explicit("Benchmark; run manually with --filter")]
    public void Benchmark_250x250_With30x30Cloud()
    {
        const int size = 250;
        GasField field = new GasField(size, size, TestOrders.Shuffled(size * size, 1));
        TestEnvironment env = new TestEnvironment(size * size);
        GasSimulator sim = new GasSimulator(field, TestEnvironment.Settings(dissipation: 0.5f, diffuses: true), env);
        for (int z = 100; z < 130; z++)
        {
            for (int x = 100; x < 130; x++)
            {
                field.Set(z * size + x, GasPacking.With(0u, 0, 200));
            }
        }
        Stopwatch sw = Stopwatch.StartNew();
        for (int i = 0; i < 2000; i++)
        {
            sim.Tick();
        }
        sw.Stop();
        TestContext.Out.WriteLine($"2000 ticks: {sw.Elapsed.TotalMilliseconds:F1} ms ({sw.Elapsed.TotalMilliseconds / 2000.0 * 1000.0:F1} us/tick), live cells {field.LiveCells}");

        GasField empty = new GasField(size, size, TestOrders.Identity(size * size));
        GasSimulator idle = new GasSimulator(empty, TestEnvironment.Settings(1f, true), env);
        sw.Restart();
        for (int i = 0; i < 100000; i++)
        {
            idle.Tick();
        }
        sw.Stop();
        TestContext.Out.WriteLine($"100k idle ticks: {sw.Elapsed.TotalMilliseconds:F2} ms");
    }
}
