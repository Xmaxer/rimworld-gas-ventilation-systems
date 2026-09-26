using System.Collections.Generic;
using System.Text;
using Multiplayer.API;
using PipeSystem;
using RimWorld;
using UnityEngine;
using Verse;

namespace GasVentilation;

/// <summary>
/// A pipe-network connector that releases its network's gas into the world. It never ticks itself:
/// <see cref="GasDeviceRegistry"/> pulses it every 25 ticks while it is emitting. Each pulse draws exactly what
/// the grid accepted, so vents start instantly, stall under back-pressure, and stop when the manifolds are empty.
/// </summary>
public sealed class CompGasVent : CompResource
{
    private VentMode ventMode = VentMode.Off;
    private bool sensorTriggered;

    public new CompProperties_GasVent Props => (CompProperties_GasVent)props;

    public GasDef Gas => Props.gas;

    public VentMode Mode => ventMode;

    public IntVec3 OutputCell => parent.Position + Props.outputOffset.RotatedBy(parent.Rotation);

    public bool SensorTriggered
    {
        get => sensorTriggered;
        set => sensorTriggered = value;
    }

    public bool Emitting => ventMode == VentMode.On || (ventMode == VentMode.Sensor && sensorTriggered);

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        GasDeviceRegistry devices = VentGasGrid.For(parent.Map)?.Devices;
        devices?.Register(this);
        if (!respawningAfterLoad && Props.requiresRoof)
        {
            devices?.ScheduleRoofCheck(this);
        }
    }

    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
    {
        base.PostDeSpawn(map, mode);
        VentGasGrid.For(map)?.Devices.Deregister(this);
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref ventMode, "gvVentMode", VentMode.Off);
        Scribe_Values.Look(ref sensorTriggered, "gvSensorTriggered");
    }

    public void Pulse()
    {
        PipeNet net = PipeNet;
        if (net == null)
        {
            return;
        }
        float stored = net.CurrentStored();
        if (stored <= 0f)
        {
            return;
        }
        GasDef gas = Gas;
        int budget = Mathf.Min(gas.emitPerPulse, Mathf.FloorToInt(stored * gas.densityPerCanister));
        if (budget <= 0)
        {
            return;
        }
        int accepted = VentGasGrid.For(parent.Map).EmitAround(OutputCell, gas, budget);
        if (accepted > 0)
        {
            net.DrawAmongStorage(accepted / gas.densityPerCanister, out _, null);
        }
    }

    [SyncMethod]
    public void SetMode(VentMode newMode)
    {
        ventMode = newMode;
        if (newMode != VentMode.Sensor)
        {
            sensorTriggered = false;
        }
    }

    public void FallFromCeiling()
    {
        Map map = parent.Map;
        IntVec3 cell = parent.Position;
        bool bridging = IsBridgingPipes(map, cell);
        GasDef gas = Gas;
        Messages.Message("GV_CeilingVentFell".Translate(parent.LabelCap), new TargetInfo(cell, map), MessageTypeDefOf.NegativeEvent);
        parent.Destroy(DestroyMode.Deconstruct);
        if (bridging && gas.hiddenPipe != null && GenConstruct.CanPlaceBlueprintAt(gas.hiddenPipe, cell, Rot4.North, map).Accepted)
        {
            GenConstruct.PlaceBlueprintForBuild(gas.hiddenPipe, cell, map, Rot4.North, Faction.OfPlayer, null);
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
            defaultLabel = ("GV_VentMode_" + ventMode).Translate(),
            defaultDesc = "GV_VentModeDesc".Translate(),
            icon = GasVentTextures.ForMode(ventMode),
            action = () => SetMode(NextMode(ventMode))
        };
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
        sb.Append("GV_VentInspectMode".Translate(("GV_VentMode_" + ventMode).Translate()));
        if (ventMode == VentMode.Sensor)
        {
            sb.Append(' ').Append((sensorTriggered ? "GV_SensorTriggered" : "GV_SensorIdle").Translate());
        }
        VentGasGrid grid = VentGasGrid.For(parent.Map);
        if (grid != null && !grid.CanHoldGas(OutputCell))
        {
            sb.AppendLine().Append("GV_VentOutputBlocked".Translate());
        }
        return sb.ToString();
    }

    private static VentMode NextMode(VentMode mode)
    {
        switch (mode)
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
                if (things[j] is ThingWithComps other && other != parent && SharesNet(other))
                {
                    connectedSides++;
                    break;
                }
            }
        }
        return connectedSides >= 2;
    }

    private bool SharesNet(ThingWithComps other)
    {
        List<ThingComp> comps = other.AllComps;
        for (int i = 0; i < comps.Count; i++)
        {
            if (comps[i] is CompResource resource && resource.Props.pipeNet == Props.pipeNet)
            {
                return true;
            }
        }
        return false;
    }
}
