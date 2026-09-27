using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Verse;

namespace GasVentilation.DevHarness;

public sealed class HarnessRunner : GameComponent
{
    /// <summary>Scenario factories by name. Later milestones add entries here.</summary>
    private static readonly Dictionary<string, Func<HarnessScenario>> Registry =
        new Dictionary<string, Func<HarnessScenario>>(StringComparer.OrdinalIgnoreCase)
        {
            ["boot"] = () => new Scenarios.BootScenario(),
            ["grid"] = () => new Scenarios.GridSpreadScenario(),
            ["pipes"] = () => new Scenarios.PipeNetworkScenario(),
            ["vents"] = () => new Scenarios.VentVariantsScenario(),
            ["effects"] = () => new Scenarios.EffectsScenario(),
            ["sensor"] = () => new Scenarios.SensorScenario(),
            ["flee"] = () => new Scenarios.FleeScenario(),
            ["breakout"] = () => new Scenarios.BreakoutScenario(),
            ["balance"] = () => new Scenarios.BalanceScenario(),
            ["perf"] = () => new Scenarios.PerfScenario(),
            ["playground"] = () => new Scenarios.PlaygroundScenario(),
        };

    private readonly List<HarnessScenario> queue = new List<HarnessScenario>();
    private readonly List<string> failures = new List<string>();
    private HarnessScenario current;
    private bool active;
    private bool finished;
    private int setupTick;
    private int areaIndex;
    private int warmupTicks = 180;

    public HarnessRunner(Game game)
    {
    }

    public override void FinalizeInit()
    {
        string request = Path.Combine(HarnessUtil.HarnessDir, "request.txt");
        if (!File.Exists(request))
        {
            return;
        }
        foreach (string raw in File.ReadAllLines(request))
        {
            string name = raw.Trim();
            if (name.Length == 0)
            {
                continue;
            }
            if (Registry.TryGetValue(name, out Func<HarnessScenario> factory))
            {
                queue.Add(factory());
            }
            else
            {
                WriteLine($"{{\"scenario\":\"{HarnessUtil.JsonEscape(name)}\",\"status\":\"Failed\",\"failures\":[\"unknown scenario\"]}}");
            }
        }
        active = true;
        Log.Message($"[GasVentHarness] active with {queue.Count} scenario(s)");
    }

    /// <summary>
    /// Runs after FinalizeInit when a save is loaded. Persistent (interactive) scenarios only make sense on a new
    /// game: tools/play.ps1 writes request.txt on every launch, and re-running the playground on a loaded colony
    /// would clear its corner and drop duplicate resources.
    /// </summary>
    public override void LoadedGame()
    {
        int before = queue.Count;
        for (int i = queue.Count - 1; i >= 0; i--)
        {
            if (queue[i].IsPersistent)
            {
                queue.RemoveAt(i);
            }
        }
        if (before > 0 && queue.Count == 0)
        {
            active = false;
            Log.Message("[GasVentHarness] loaded save: skipping persistent scenario(s); harness inactive");
        }
    }

    public override void GameComponentUpdate()
    {
        if (!active || finished)
        {
            return;
        }
        HarnessUtil.CloseBlockingWindows();
        if (Find.TickManager.CurTimeSpeed != TimeSpeed.Ultrafast)
        {
            Find.TickManager.CurTimeSpeed = TimeSpeed.Ultrafast;
        }
    }

    public override void GameComponentTick()
    {
        if (!active || finished)
        {
            return;
        }
        Map map = Find.CurrentMap;
        if (map == null)
        {
            return;
        }
        HarnessUtil.ReapplyForcedPower();
        if (warmupTicks > 0)
        {
            warmupTicks--;
            return;
        }
        if (current == null)
        {
            if (queue.Count == 0)
            {
                Finish();
                return;
            }
            current = queue[0];
            queue.RemoveAt(0);
            failures.Clear();
            try
            {
                current.Setup(map, HarnessUtil.ReserveArea(map, areaIndex++));
            }
            catch (Exception e)
            {
                failures.Add("Setup threw: " + e);
                Record(ScenarioStatus.Failed, 0);
                current = null;
                return;
            }
            if (current.IsPersistent)
            {
                HandOffToPlayer();
                return;
            }
            setupTick = Find.TickManager.TicksGame;
            return;
        }

        int elapsed = Find.TickManager.TicksGame - setupTick;
        ScenarioStatus status;
        try
        {
            status = current.Tick(map, elapsed, failures);
        }
        catch (Exception e)
        {
            failures.Add("Tick threw: " + e);
            status = ScenarioStatus.Failed;
        }
        if (status == ScenarioStatus.Running && elapsed >= current.TimeoutTicks)
        {
            failures.Add($"Timed out after {elapsed} ticks");
            status = ScenarioStatus.Failed;
        }
        if (status != ScenarioStatus.Running)
        {
            Record(status, elapsed);
            current = null;
        }
    }

    /// <summary>
    /// A persistent scenario set up successfully: stop driving the game (no forced Ultrafast, no window closing,
    /// no timeout, no results, no shutdown) and leave it paused for the player.
    /// </summary>
    private void HandOffToPlayer()
    {
        Log.Message($"[GasVentHarness] {current.Name}: persistent setup complete, control handed back to the player");
        current = null;
        queue.Clear();
        active = false;
        Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
    }

    private void Record(ScenarioStatus status, int ticks)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("{\"scenario\":\"").Append(HarnessUtil.JsonEscape(current?.Name ?? "?")).Append("\",");
        sb.Append("\"status\":\"").Append(status).Append("\",\"ticks\":").Append(ticks).Append(",\"failures\":[");
        for (int i = 0; i < failures.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }
            sb.Append('"').Append(HarnessUtil.JsonEscape(failures[i])).Append('"');
        }
        sb.Append("],\"metrics\":[");
        List<string> metrics = current?.Metrics ?? new List<string>();
        for (int i = 0; i < metrics.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }
            sb.Append('"').Append(HarnessUtil.JsonEscape(metrics[i])).Append('"');
        }
        sb.Append("]}");
        WriteLine(sb.ToString());
        Log.Message($"[GasVentHarness] {current?.Name}: {status} after {ticks} ticks");
    }

    private void Finish()
    {
        finished = true;
        List<string> errors = HarnessUtil.UnexpectedLoggedErrors();
        StringBuilder sb = new StringBuilder("{\"summary\":true,\"unexpectedErrors\":[");
        for (int i = 0; i < errors.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }
            sb.Append('"').Append(HarnessUtil.JsonEscape(errors[i])).Append('"');
        }
        sb.Append("]}");
        WriteLine(sb.ToString());
        File.WriteAllText(Path.Combine(HarnessUtil.HarnessDir, "done.txt"), "done");
        Root.Shutdown();
    }

    private static void WriteLine(string line)
    {
        Directory.CreateDirectory(HarnessUtil.HarnessDir);
        File.AppendAllText(Path.Combine(HarnessUtil.HarnessDir, "results.jsonl"), line + "\n");
    }
}
