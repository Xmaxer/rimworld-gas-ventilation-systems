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
