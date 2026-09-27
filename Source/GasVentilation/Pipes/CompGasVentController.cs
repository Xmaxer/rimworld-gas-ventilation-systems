using System.Collections.Generic;
using System.Text;
using Multiplayer.API;
using RimWorld;
using Verse;

namespace GasVentilation;

/// <summary>
/// One per vent building. A vent is a shape (wall/mounted/ceiling/floor) with up to four <see cref="CompGasVent"/>
/// trader comps sitting alongside this controller, one per gas. The controller owns the parts that are shared
/// across whichever gases are selected: mode (Off/On/Sensor), which gas(es) currently output, the output cell,
/// gizmos, the combined inspect string, and roof-fall handling. Each trader comp reads <see cref="Mode"/> and
/// <see cref="Selection"/> from here to decide whether it personally emits this pulse.
/// </summary>
public sealed class CompGasVentController : ThingComp
{
    private VentMode mode = VentMode.Off;
    private VentGasSelection selection = VentGasSelection.None;
    private bool sensorTriggered;
    private List<CompGasVent> traders;

    public CompProperties_GasVentController Props => (CompProperties_GasVentController)props;

    public VentMode Mode => mode;

    public VentGasSelection Selection => selection;

    public IntVec3 OutputCell => parent.Position + Props.outputOffset.RotatedBy(parent.Rotation);

    public bool SensorTriggered
    {
        get => sensorTriggered;
        set => sensorTriggered = value;
    }

    public bool Emitting => mode == VentMode.On || (mode == VentMode.Sensor && sensorTriggered);

    public List<CompGasVent> Traders => traders ??= parent.AllComps.FindAll(c => c is CompGasVent).ConvertAll(c => (CompGasVent)c);

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        GasDeviceRegistry devices = VentGasGrid.For(parent.Map)?.Devices;
        devices?.RegisterController(this);
        if (!respawningAfterLoad && Props.requiresRoof)
        {
            devices?.ScheduleRoofCheck(this);
        }
    }

    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
    {
        base.PostDeSpawn(map, mode);
        VentGasGrid.For(map)?.Devices.DeregisterController(this);
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref mode, "gvVentMode", VentMode.Off);
        Scribe_Values.Look(ref selection, "gvVentSelection", VentGasSelection.None);
        Scribe_Values.Look(ref sensorTriggered, "gvSensorTriggered");
    }

    [SyncMethod]
    public void SetMode(VentMode newMode)
    {
        mode = newMode;
        if (newMode != VentMode.Sensor)
        {
            sensorTriggered = false;
        }
    }

    [SyncMethod]
    public void ToggleGas(VentGasSelection flag)
    {
        selection ^= flag;
    }

    public void FallFromCeiling()
    {
        Map map = parent.Map;
        IntVec3 cell = parent.Position;
        bool bridging = IsBridgingPipes(map, cell);
        Messages.Message("GV_CeilingVentFell".Translate(parent.LabelCap), new TargetInfo(cell, map), MessageTypeDefOf.NegativeEvent);
        parent.Destroy(DestroyMode.Deconstruct);
        if (bridging && GenConstruct.CanPlaceBlueprintAt(GVDefOf.GV_HiddenPipe, cell, Rot4.North, map).Accepted)
        {
            GenConstruct.PlaceBlueprintForBuild(GVDefOf.GV_HiddenPipe, cell, map, Rot4.North, Faction.OfPlayer, null);
        }
    }

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
        yield return new Command_Action
        {
            defaultLabel = ("GV_VentMode_" + mode).Translate(),
            defaultDesc = "GV_VentModeDesc".Translate(),
            icon = GasVentTextures.ForMode(mode),
            action = () => SetMode(NextMode(mode))
        };
        foreach (CompGasVent trader in Traders)
        {
            GasDef gas = trader.Gas;
            VentGasSelection flag = gas.ventFlag;
            yield return new Command_Toggle
            {
                defaultLabel = gas.LabelCap,
                defaultDesc = "GV_VentGasToggleDesc".Translate(gas.label),
                icon = GasVentTextures.GasSwatch,
                defaultIconColor = gas.color,
                isActive = () => (selection & flag) != 0,
                toggleAction = () => ToggleGas(flag)
            };
        }
    }

    public override string CompInspectStringExtra()
    {
        StringBuilder sb = new StringBuilder();
        string baseText = base.CompInspectStringExtra();
        if (!baseText.NullOrEmpty())
        {
            sb.Append(baseText);
        }
        sb.AppendLineIfNotEmpty();
        sb.Append("GV_VentInspectMode".Translate(("GV_VentMode_" + mode).Translate()));
        if (mode == VentMode.Sensor)
        {
            sb.Append(' ').Append((sensorTriggered ? "GV_SensorTriggered" : "GV_SensorIdle").Translate());
        }
        if (selection == VentGasSelection.None)
        {
            sb.AppendLine().Append("GV_VentNoGasSelected".Translate());
        }
        else
        {
            foreach (CompGasVent trader in Traders)
            {
                if ((selection & trader.Gas.ventFlag) == 0)
                {
                    continue;
                }
                sb.AppendLine().Append("GV_VentInspectOutput".Translate(trader.Gas.LabelCap, trader.LastSharePercent));
            }
        }
        VentGasGrid grid = VentGasGrid.For(parent.Map);
        if (grid != null && !grid.CanHoldGas(OutputCell))
        {
            sb.AppendLine().Append("GV_VentOutputBlocked".Translate());
        }
        return sb.ToString();
    }

    private static VentMode NextMode(VentMode m)
    {
        switch (m)
        {
            case VentMode.Off:
                return VentMode.On;
            case VentMode.On:
                return VentMode.Sensor;
            default:
                return VentMode.Off;
        }
    }

    private bool IsBridgingPipes(Map map, IntVec3 cell)
    {
        int connectedSides = 0;
        for (int i = 0; i < 4; i++)
        {
            IntVec3 neighbour = cell + GenAdj.CardinalDirections[i];
            if (!neighbour.InBounds(map))
            {
                continue;
            }
            List<Thing> things = neighbour.GetThingList(map);
            for (int j = 0; j < things.Count; j++)
            {
                if (things[j] is ThingWithComps other && other != parent && SharesAnyNet(other))
                {
                    connectedSides++;
                    break;
                }
            }
        }
        return connectedSides >= 2;
    }

    private bool SharesAnyNet(ThingWithComps other)
    {
        List<ThingComp> otherComps = other.AllComps;
        foreach (CompGasVent trader in Traders)
        {
            for (int i = 0; i < otherComps.Count; i++)
            {
                if (otherComps[i] is PipeSystem.CompResource resource && resource.Props.pipeNet == trader.Props.pipeNet)
                {
                    return true;
                }
            }
        }
        return false;
    }
}
