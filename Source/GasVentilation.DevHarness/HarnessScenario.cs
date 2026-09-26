using System.Collections.Generic;
using Verse;

namespace GasVentilation.DevHarness;

public enum ScenarioStatus
{
    Running,
    Passed,
    Failed
}

public abstract class HarnessScenario
{
    public abstract string Name { get; }

    public virtual int TimeoutTicks => 5000;

    /// <summary>Name/value measurements written to results.jsonl (not failures).</summary>
    public readonly List<string> Metrics = new List<string>();

    /// <summary>Called once. <paramref name="area"/> is a cleared, unroofed, unfogged 24x24 rectangle.</summary>
    public abstract void Setup(Map map, CellRect area);

    /// <summary>Called every game tick after Setup. Add readable messages to <paramref name="failures"/>.</summary>
    public abstract ScenarioStatus Tick(Map map, int ticksSinceSetup, List<string> failures);
}
