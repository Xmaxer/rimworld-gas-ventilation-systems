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

    internal static readonly SensorTargets[] TargetOptions =
    {
        SensorTargets.Humanlikes, SensorTargets.Mechanoids, SensorTargets.Insectoids, SensorTargets.Animals,
        SensorTargets.IncludeNonHostile, SensorTargets.Colonists
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

    /// <summary>Explicitly linked vents (see ITab_GasSensor). Empty by default: <see cref="GasDeviceRegistry"/>
    /// falls back to triggering every Sensor-mode vent in the same room, same as before this existed. Once any
    /// vent is linked, the sensor only ever triggers its links -- room membership stops mattering for it.</summary>
    private List<Thing> linkedVents = new List<Thing>();

    /// <summary>Explicitly linked powered doors (see ITab_GasSensor). Unlike vents, doors have no room-based
    /// fallback -- a door only ever locks because it's linked to a sensor that is currently triggered.</summary>
    private List<Thing> linkedDoors = new List<Thing>();

    public bool Triggered => triggered;

    public bool Armed => armed;

    public SensorTargets Targets => targets;

    public int LingerTicks => lingerTicks;

    public bool StopWhenAllDowned => stopWhenAllDowned;

    public Room Room => parent.Spawned ? parent.GetRoom() : null;

    public bool HasExplicitLinks => linkedVents.Count > 0;

    public IReadOnlyList<Thing> LinkedVents => linkedVents;

    public bool IsLinkedTo(CompGasVentController controller)
    {
        return controller != null && linkedVents.Contains(controller.parent);
    }

    public bool HasExplicitDoorLinks => linkedDoors.Count > 0;

    public IReadOnlyList<Thing> LinkedDoors => linkedDoors;

    public bool IsLinkedToDoor(Thing door)
    {
        return door != null && linkedDoors.Contains(door);
    }

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
        Scribe_Collections.Look(ref linkedVents, "gvLinkedVents", LookMode.Reference);
        Scribe_Collections.Look(ref linkedDoors, "gvLinkedDoors", LookMode.Reference);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            linkedVents ??= new List<Thing>();
            linkedVents.RemoveAll(t => t == null);
            linkedDoors ??= new List<Thing>();
            linkedDoors.RemoveAll(t => t == null);
        }
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
        return MatchesTargets(pawn, targets);
    }

    /// <summary>Shared by sensor detection and <see cref="CompDoorLock"/> (a locked door blocks exactly the
    /// pawns its linking sensor(s) would detect). Colonists are excluded unless <see cref="SensorTargets.Colonists"/>
    /// is set; prisoners and slaves are always excluded regardless -- they're never "the colony" for this purpose.</summary>
    internal static bool MatchesTargets(Pawn pawn, SensorTargets targets)
    {
        if (pawn.Dead || pawn.IsPrisonerOfColony || pawn.IsSlaveOfColony)
        {
            return false;
        }
        if (pawn.Faction == Faction.OfPlayer)
        {
            if ((targets & SensorTargets.Colonists) == 0)
            {
                return false;
            }
        }
        else if ((targets & SensorTargets.IncludeNonHostile) == 0 && !pawn.HostileTo(Faction.OfPlayer))
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

    /// <summary>Called from ITab_GasSensor's vent checklist.</summary>
    [SyncMethod]
    public void ToggleLink(Thing vent)
    {
        if (!linkedVents.Remove(vent))
        {
            linkedVents.Add(vent);
        }
    }

    /// <summary>Called from ITab_GasSensor's door checklist.</summary>
    [SyncMethod]
    public void ToggleDoorLink(Thing door)
    {
        if (!linkedDoors.Remove(door))
        {
            linkedDoors.Add(door);
        }
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
        sb.AppendLine().Append(linkedVents.Count > 0
            ? "GV_SensorInspectLinked".Translate(linkedVents.Count)
            : "GV_SensorInspectRoomFallback".Translate());
        if (linkedDoors.Count > 0)
        {
            sb.AppendLine().Append("GV_SensorInspectLinkedDoors".Translate(linkedDoors.Count));
        }
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
