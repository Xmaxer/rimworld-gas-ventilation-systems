using System.Collections.Generic;
using PipeSystem;
using UnityEngine;
using Verse;

namespace GasVentilation;

/// <summary>Owned by <see cref="VentGasGrid"/>. Pulses vents, evaluates sensors, computes per-network throughput
/// shares, and handles ceiling-vent roof checks.</summary>
public sealed class GasDeviceRegistry
{
    public const int PulseInterval = 25;
    public const int SensorInterval = 60;
    private const int RoofCheckDelay = 60;

    private readonly Map map;
    private readonly List<CompGasVent> vents = new List<CompGasVent>();
    private readonly List<CompGasVentController> controllers = new List<CompGasVentController>();
    private readonly List<CompIntruderSensor> sensors = new List<CompIntruderSensor>();
    private readonly List<CompGasVentController> roofCheckVents = new List<CompGasVentController>();
    private readonly List<int> roofCheckTicks = new List<int>();
    private readonly List<Thing> tmpThings = new List<Thing>();
    private readonly Dictionary<PipeNet, float> netShares = new Dictionary<PipeNet, float>();
    private readonly Dictionary<PipeNet, int> netEmittingCount = new Dictionary<PipeNet, int>();

    public GasDeviceRegistry(Map map)
    {
        this.map = map;
    }

    public IReadOnlyList<CompGasVent> Vents => vents;

    public IReadOnlyList<CompGasVentController> Controllers => controllers;

    public void Register(CompGasVent vent)
    {
        if (!vents.Contains(vent))
        {
            vents.Add(vent);
        }
    }

    public void Deregister(CompGasVent vent)
    {
        vents.Remove(vent);
    }

    public void RegisterController(CompGasVentController controller)
    {
        if (!controllers.Contains(controller))
        {
            controllers.Add(controller);
        }
    }

    public void DeregisterController(CompGasVentController controller)
    {
        controllers.Remove(controller);
        for (int i = roofCheckVents.Count - 1; i >= 0; i--)
        {
            if (roofCheckVents[i] == controller)
            {
                roofCheckVents.RemoveAt(i);
                roofCheckTicks.RemoveAt(i);
            }
        }
    }

    public IReadOnlyList<CompIntruderSensor> Sensors => sensors;

    public void Register(CompIntruderSensor sensor)
    {
        if (!sensors.Contains(sensor))
        {
            sensors.Add(sensor);
        }
    }

    public void Deregister(CompIntruderSensor sensor)
    {
        sensors.Remove(sensor);
    }

    public void ScheduleRoofCheck(CompGasVentController controller)
    {
        roofCheckVents.Add(controller);
        roofCheckTicks.Add(Find.TickManager.TicksGame + RoofCheckDelay);
    }

    public void Tick(int ticksGame)
    {
        if (vents.Count > 0 && ticksGame % PulseInterval == 0)
        {
            ComputeThroughputShares();
            for (int i = vents.Count - 1; i >= 0; i--)
            {
                CompGasVent vent = vents[i];
                if (vent.Emitting)
                {
                    vent.Pulse();
                }
            }
        }
        if ((sensors.Count > 0 || controllers.Count > 0) && ticksGame % SensorInterval == 0)
        {
            EvaluateSensors(ticksGame);
        }
        if (roofCheckVents.Count > 0)
        {
            ProcessRoofChecks(ticksGame);
        }
    }

    /// <summary>
    /// Per gas network: slots = number of canisters currently holding any gas, summed across every manifold on
    /// that connected PipeNet (a partially-full canister counts the same as a full one). Each currently-emitting
    /// vent trader on that same net gets an equal proportional share of those slots, capped at 100%. No leftover
    /// redistribution -- an unused share (e.g. a saturated room) is simply unused that pulse, by design.
    /// </summary>
    private void ComputeThroughputShares()
    {
        netShares.Clear();
        netEmittingCount.Clear();
        for (int i = 0; i < vents.Count; i++)
        {
            CompGasVent vent = vents[i];
            if (!vent.Emitting)
            {
                continue;
            }
            PipeNet net = vent.PipeNet;
            if (net == null)
            {
                continue;
            }
            netEmittingCount.TryGetValue(net, out int count);
            netEmittingCount[net] = count + 1;
        }
        foreach (KeyValuePair<PipeNet, int> pair in netEmittingCount)
        {
            PipeNet net = pair.Key;
            int slots = 0;
            List<CompResourceStorage> storages = net.storages;
            for (int i = 0; i < storages.Count; i++)
            {
                slots += Mathf.CeilToInt(storages[i].AmountStored - 0.0001f);
            }
            netShares[net] = Mathf.Min(1f, slots / (float)pair.Value);
        }
        for (int i = 0; i < vents.Count; i++)
        {
            CompGasVent vent = vents[i];
            if (!vent.Emitting || vent.PipeNet == null)
            {
                continue;
            }
            vent.LastShare = netShares.TryGetValue(vent.PipeNet, out float share) ? share : 0f;
        }
    }

    public void OnRoofChanged(IntVec3 cell)
    {
        if (!cell.InBounds(map) || cell.Roofed(map))
        {
            return;
        }
        tmpThings.Clear();
        tmpThings.AddRange(cell.GetThingList(map));
        for (int i = 0; i < tmpThings.Count; i++)
        {
            // Gravship launch/landing moves roofs and buildings together; never drop vents mid-transport.
            if (tmpThings[i] is ThingWithComps thing && !thing.Destroyed && !thing.BeingTransportedOnGravship)
            {
                CompGasVentController controller = thing.GetComp<CompGasVentController>();
                if (controller != null && controller.Props.requiresRoof)
                {
                    controller.FallFromCeiling();
                }
            }
        }
        tmpThings.Clear();
    }

    private void EvaluateSensors(int ticksGame)
    {
        for (int i = 0; i < sensors.Count; i++)
        {
            sensors[i].Evaluate(ticksGame);
        }
        for (int i = 0; i < controllers.Count; i++)
        {
            CompGasVentController controller = controllers[i];
            if (controller.Mode != VentMode.Sensor)
            {
                continue;
            }
            bool trigger = false;
            if (sensors.Count > 0)
            {
                Room room = controller.OutputCell.GetRoom(map);
                if (room != null)
                {
                    for (int s = 0; s < sensors.Count; s++)
                    {
                        if (sensors[s].Triggered && sensors[s].Room == room)
                        {
                            trigger = true;
                            break;
                        }
                    }
                }
            }
            controller.SensorTriggered = trigger;
        }
    }

    private void ProcessRoofChecks(int ticksGame)
    {
        for (int i = roofCheckVents.Count - 1; i >= 0; i--)
        {
            if (ticksGame < roofCheckTicks[i])
            {
                continue;
            }
            CompGasVentController controller = roofCheckVents[i];
            roofCheckVents.RemoveAt(i);
            roofCheckTicks.RemoveAt(i);
            if (controller.parent.Spawned && !controller.parent.BeingTransportedOnGravship && !controller.parent.Position.Roofed(map))
            {
                controller.FallFromCeiling();
            }
        }
    }
}
