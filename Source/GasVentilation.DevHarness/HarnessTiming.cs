using System.Diagnostics;
using HarmonyLib;
using Verse;

namespace GasVentilation.DevHarness;

/// <summary>Accumulates wall-clock time spent in our per-tick entry points. Dev harness only.</summary>
[StaticConstructorOnStartup]
public static class HarnessTiming
{
    public static long GridTicks;
    public static long GridCalls;
    public static long ScannerTicks;
    public static long ScannerCalls;

    static HarnessTiming()
    {
        Harmony harmony = new Harmony("Xmaxer.GasVentilation.DevHarness");
        harmony.Patch(AccessTools.Method(typeof(VentGasGrid), nameof(VentGasGrid.MapComponentTick)),
            prefix: new HarmonyMethod(typeof(HarnessTiming), nameof(Start)),
            postfix: new HarmonyMethod(typeof(HarnessTiming), nameof(StopGrid)));
        harmony.Patch(AccessTools.Method(typeof(GasExposureScanner), nameof(GasExposureScanner.MapComponentTick)),
            prefix: new HarmonyMethod(typeof(HarnessTiming), nameof(Start)),
            postfix: new HarmonyMethod(typeof(HarnessTiming), nameof(StopScanner)));
    }

    public static void Reset()
    {
        GridTicks = GridCalls = ScannerTicks = ScannerCalls = 0;
    }

    public static double MicrosPerCall(long ticks, long calls)
    {
        return calls == 0 ? 0 : ticks * 1000000.0 / Stopwatch.Frequency / calls;
    }

    private static void Start(out long __state)
    {
        __state = Stopwatch.GetTimestamp();
    }

    private static void StopGrid(long __state)
    {
        GridTicks += Stopwatch.GetTimestamp() - __state;
        GridCalls++;
    }

    private static void StopScanner(long __state)
    {
        ScannerTicks += Stopwatch.GetTimestamp() - __state;
        ScannerCalls++;
    }
}
