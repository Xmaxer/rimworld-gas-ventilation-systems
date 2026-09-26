using System.Collections.Generic;
using System.Text;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace GasVentilation;

/// <summary>Watches its room for targets and triggers Sensor-mode vents in that room (see GasDeviceRegistry).</summary>
public sealed class CompIntruderSensor : ThingComp
{
    private const int MaxLingerSeconds = 120;

    private static readonly SensorTargets[] TargetOptions =
    {
        SensorTargets.Humanlikes, SensorTargets.Mechanoids, SensorTargets.Insectoids, SensorTargets.Animals, SensorTargets.IncludeNonHostile
    };

    private static bool clipboardSet;
    private static bool clipArmed;
    private static SensorTargets clipTargets;
    private static int clipLinger;
    private static bool clipStopWhenDowned;

    private bool armed = true;
    private SensorTargets targets = SensorTargets.Default;
    private int lingerTicks = 600;
    private bool stopWhenAllDowned;
    private bool triggered;
    private int lastSeenTick = -99999;
    private CompPowerTrader power;

    public bool Triggered => triggered;

    public bool Armed => armed;

    public SensorTargets Targets => targets;

    public int LingerTicks => lingerTicks;

    public bool StopWhenAllDowned => stopWhenAllDowned;

    public Room Room => parent.Spawned ? parent.GetRoom() : null;

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        power = parent.GetComp<CompPowerTrader>();
        VentGasGrid.For(parent.Map)?.Devices.Register(this);
    }

    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
    {
        base.PostDeSpawn(map, mode);
        triggered = false;
        VentGasGrid.For(map)?.Devices.Deregister(this);
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref armed, "gvArmed", true);
        Scribe_Values.Look(ref targets, "gvTargets", SensorTargets.Default);
        Scribe_Values.Look(ref lingerTicks, "gvLingerTicks", 600);
        Scribe_Values.Look(ref stopWhenAllDowned, "gvStopWhenAllDowned");
        Scribe_Values.Look(ref triggered, "gvTriggered");
        Scribe_Values.Look(ref lastSeenTick, "gvLastSeenTick", -99999);
    }

    /// <summary>Called by GasDeviceRegistry every SensorInterval ticks.</summary>
    public void Evaluate(int now)
    {
        Room room = Room;
        bool powered = power == null || power.PowerOn;
        if (!armed || !powered || room == null || room.TouchesMapEdge)
        {
            triggered = false;
            return;
        }
        bool anyTarget = false;
        bool anyStanding = false;
        List<Region> regions = room.Regions;
        for (int r = 0; r < regions.Count; r++)
        {
            List<Thing> pawns = regions[r].ListerThings.ThingsInGroup(ThingRequestGroup.Pawn);
            for (int i = 0; i < pawns.Count; i++)
            {
                if (pawns[i] is Pawn pawn && Matches(pawn))
                {
                    anyTarget = true;
                    if (!pawn.Downed)
                    {
                        anyStanding = true;
                    }
                }
            }
        }
        bool allDowned = anyTarget && !anyStanding;
        if (anyTarget && !(stopWhenAllDowned && allDowned))
        {
            lastSeenTick = now;
        }
        triggered = now - lastSeenTick <= lingerTicks && !(stopWhenAllDowned && allDowned);
    }

    private bool Matches(Pawn pawn)
    {
        if (pawn.Dead || pawn.Faction == Faction.OfPlayer || pawn.IsPrisonerOfColony || pawn.IsSlaveOfColony)
        {
            return false;
        }
        if ((targets & SensorTargets.IncludeNonHostile) == 0 && !pawn.HostileTo(Faction.OfPlayer))
        {
            return false;
        }
        RaceProperties race = pawn.RaceProps;
        if (race.Humanlike)
        {
            return (targets & SensorTargets.Humanlikes) != 0;
        }
        if (race.IsMechanoid || race.IsDrone)
        {
            return (targets & SensorTargets.Mechanoids) != 0;
        }
        if (race.Insect)
        {
            return (targets & SensorTargets.Insectoids) != 0;
        }
        if (race.Animal)
        {
            return (targets & SensorTargets.Animals) != 0;
        }
        return false;
    }

    // ------------------------------------------------------------ synced mutations (Multiplayer)

    [SyncMethod]
    public void SetArmed(bool value)
    {
        armed = value;
        if (!value)
        {
            triggered = false;
        }
    }

    [SyncMethod]
    public void SetTargets(SensorTargets value)
    {
        targets = value;
    }

    [SyncMethod]
    public void SetLinger(int ticks)
    {
        lingerTicks = ticks < 0 ? 0 : ticks;
    }

    [SyncMethod]
    public void SetStopWhenAllDowned(bool value)
    {
        stopWhenAllDowned = value;
    }

    [SyncMethod]
    public void ApplySettings(bool newArmed, SensorTargets newTargets, int newLinger, bool newStopWhenAllDowned)
    {
        armed = newArmed;
        targets = newTargets;
        lingerTicks = newLinger < 0 ? 0 : newLinger;
        stopWhenAllDowned = newStopWhenAllDowned;
        if (!armed)
        {
            triggered = false;
        }
    }

    // ------------------------------------------------------------ UI

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        foreach (Gizmo gizmo in base.CompGetGizmosExtra())
        {
            yield return gizmo;
        }
        if (parent.Faction != Faction.OfPlayer)
        {
            yield break;
        }
        yield return new Command_Toggle
        {
            defaultLabel = "GV_SensorArmed".Translate(),
            defaultDesc = "GV_SensorArmedDesc".Translate(),
            icon = GasVentTextures.SensorArmed,
            isActive = () => armed,
            toggleAction = () => SetArmed(!armed)
        };
        yield return new Command_Action
        {
            defaultLabel = "GV_SensorTargets".Translate(),
            defaultDesc = "GV_SensorTargetsDesc".Translate(),
            icon = GasVentTextures.SensorTargets,
            action = () => Find.WindowStack.Add(new FloatMenu(TargetMenuOptions()))
        };
        yield return new Command_Action
        {
            defaultLabel = "GV_SensorLinger".Translate(LingerLabel(lingerTicks)),
            defaultDesc = "GV_SensorLingerDesc".Translate(),
            icon = GasVentTextures.SensorLinger,
            action = () => Find.WindowStack.Add(new Dialog_Slider(
                seconds => "GV_SensorLinger".Translate(LingerLabel(seconds * GenTicks.TicksPerRealSecond)).ToString(),
                0,
                MaxLingerSeconds,
                seconds => SetLinger(seconds * GenTicks.TicksPerRealSecond),
                lingerTicks / GenTicks.TicksPerRealSecond))
        };
        yield return new Command_Toggle
        {
            defaultLabel = "GV_SensorStopWhenDowned".Translate(),
            defaultDesc = "GV_SensorStopWhenDownedDesc".Translate(),
            icon = GasVentTextures.SensorStopWhenDowned,
            isActive = () => stopWhenAllDowned,
            toggleAction = () => SetStopWhenAllDowned(!stopWhenAllDowned)
        };
        yield return new Command_Action
        {
            defaultLabel = "GV_SensorCopy".Translate(),
            defaultDesc = "GV_SensorCopyDesc".Translate(),
            icon = GasVentTextures.Copy,
            action = () =>
            {
                clipboardSet = true;
                clipArmed = armed;
                clipTargets = targets;
                clipLinger = lingerTicks;
                clipStopWhenDowned = stopWhenAllDowned;
            }
        };
        if (clipboardSet)
        {
            yield return new Command_Action
            {
                defaultLabel = "GV_SensorPaste".Translate(),
                defaultDesc = "GV_SensorPasteDesc".Translate(),
                icon = GasVentTextures.Paste,
                action = () => ApplySettings(clipArmed, clipTargets, clipLinger, clipStopWhenDowned)
            };
        }
    }

    public override string CompInspectStringExtra()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append((armed ? "GV_SensorInspectArmed" : "GV_SensorInspectDisarmed").Translate());
        if (armed)
        {
            sb.Append(", ").Append((triggered ? "GV_SensorInspectTriggered" : "GV_SensorInspectIdle").Translate());
        }
        sb.AppendLine().Append("GV_SensorInspectTargets".Translate(TargetsLabel()));
        Room room = Room;
        if (room != null && room.TouchesMapEdge)
        {
            sb.AppendLine().Append("GV_SensorInspectOpenRoom".Translate());
        }
        if (power != null && !power.PowerOn)
        {
            sb.AppendLine().Append("GV_SensorInspectUnpowered".Translate());
        }
        return sb.ToString();
    }

    private List<FloatMenuOption> TargetMenuOptions()
    {
        List<FloatMenuOption> options = new List<FloatMenuOption>();
        for (int i = 0; i < TargetOptions.Length; i++)
        {
            SensorTargets flag = TargetOptions[i];
            bool on = (targets & flag) != 0;
            string label = (on ? "[x] " : "[ ] ") + ("GV_SensorTarget_" + flag).Translate();
            options.Add(new FloatMenuOption(label, () => SetTargets(targets ^ flag)));
        }
        return options;
    }

    private static string LingerLabel(int ticks)
    {
        return ticks <= 0 ? "GV_SensorLingerNone".Translate().ToString() : ticks.ToStringSecondsFromTicks();
    }

    private string TargetsLabel()
    {
        List<string> parts = new List<string>();
        for (int i = 0; i < TargetOptions.Length; i++)
        {
            if ((targets & TargetOptions[i]) != 0)
            {
                parts.Add(("GV_SensorTarget_" + TargetOptions[i]).Translate());
            }
        }
        return parts.Count == 0 ? "GV_SensorTarget_None".Translate().ToString() : string.Join(", ", parts);
    }
}
