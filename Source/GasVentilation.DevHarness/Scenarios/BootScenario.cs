using System.Collections.Generic;
using Verse;

namespace GasVentilation.DevHarness.Scenarios;

/// <summary>Proves the game loads a map with the mod active and ticks without errors.</summary>
public sealed class BootScenario : HarnessScenario
{
    public override string Name => "boot";

    public override void Setup(Map map, CellRect area)
    {
    }

    public override ScenarioStatus Tick(Map map, int ticksSinceSetup, List<string> failures)
    {
        return ticksSinceSetup >= 120 ? ScenarioStatus.Passed : ScenarioStatus.Running;
    }
}
