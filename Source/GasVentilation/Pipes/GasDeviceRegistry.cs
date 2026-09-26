using System.Collections.Generic;
using Verse;

namespace GasVentilation;

/// <summary>Owned by <see cref="VentGasGrid"/>. Pulses vents, evaluates sensors and handles ceiling-vent roof checks.</summary>
public sealed class GasDeviceRegistry
{
    public const int PulseInterval = 25;
    private const int RoofCheckDelay = 60;

    private readonly Map map;
    private readonly List<CompGasVent> vents = new List<CompGasVent>();
    private readonly List<CompGasVent> roofCheckVents = new List<CompGasVent>();
    private readonly List<int> roofCheckTicks = new List<int>();
    private readonly List<Thing> tmpThings = new List<Thing>();

    public GasDeviceRegistry(Map map)
    {
        this.map = map;
    }

    public IReadOnlyList<CompGasVent> Vents => vents;

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
        for (int i = roofCheckVents.Count - 1; i >= 0; i--)
        {
            if (roofCheckVents[i] == vent)
            {
                roofCheckVents.RemoveAt(i);
                roofCheckTicks.RemoveAt(i);
            }
        }
    }

    public void ScheduleRoofCheck(CompGasVent vent)
    {
        roofCheckVents.Add(vent);
        roofCheckTicks.Add(Find.TickManager.TicksGame + RoofCheckDelay);
    }

    public void Tick(int ticksGame)
    {
        if (vents.Count > 0 && ticksGame % PulseInterval == 0)
        {
            for (int i = vents.Count - 1; i >= 0; i--)
            {
                CompGasVent vent = vents[i];
                if (vent.Emitting)
                {
                    vent.Pulse();
                }
            }
        }
        // [M5] sensor evaluation
        if (roofCheckVents.Count > 0)
        {
            ProcessRoofChecks(ticksGame);
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
                CompGasVent vent = thing.GetComp<CompGasVent>();
                if (vent != null && vent.Props.requiresRoof)
                {
                    vent.FallFromCeiling();
                }
            }
        }
        tmpThings.Clear();
    }

    private void ProcessRoofChecks(int ticksGame)
    {
        for (int i = roofCheckVents.Count - 1; i >= 0; i--)
        {
            if (ticksGame < roofCheckTicks[i])
            {
                continue;
            }
            CompGasVent vent = roofCheckVents[i];
            roofCheckVents.RemoveAt(i);
            roofCheckTicks.RemoveAt(i);
            if (vent.parent.Spawned && !vent.parent.BeingTransportedOnGravship && !vent.parent.Position.Roofed(map))
            {
                vent.FallFromCeiling();
            }
        }
    }
}
