using System.Collections.Generic;
using GasVentilation.Core;
using RimWorld;
using Verse;

namespace GasVentilation;

/// <summary>Per-map storage, simulation and API for the four ventilation gases.</summary>
public sealed class VentGasGrid : MapComponent, IGasFieldListener
{
    private static readonly int[] EqualizeCells = new int[4];
    private static readonly int[] EqualizeSums = new int[GasPacking.Channels];

    private static Map cachedMap;
    private static VentGasGrid cachedGrid;

    private GasField field;
    private GasSimulator simulator;
    private MapGasEnvironment environment;
    private bool eventsHooked;

    // Scribe buffers for the simulator cursors.
    private int savedDissipationCursor;
    private int savedDiffusionCursor;
    private int savedDissipationSweep;

    public VentGasGrid(Map map) : base(map)
    {
        Devices = new GasDeviceRegistry(map);
    }

    public GasDeviceRegistry Devices { get; }

    /// <summary>Fast lookup with a one-entry cache (almost all calls are for the current map).</summary>
    public static VentGasGrid For(Map map)
    {
        if (map == null)
        {
            return null;
        }
        if (map == cachedMap && cachedGrid != null)
        {
            return cachedGrid;
        }
        VentGasGrid grid = map.GetComponent<VentGasGrid>();
        cachedMap = map;
        cachedGrid = grid;
        return grid;
    }

    public int LiveCells => field?.LiveCells ?? 0;

    // ---------------------------------------------------------------- lifecycle

    private void EnsureInitialized()
    {
        if (field != null)
        {
            return;
        }
        List<IntVec3> order = map.cellsInRandomOrder.GetAll();
        CellIndices indices = map.cellIndices;
        int[] orderIndices = new int[order.Count];
        for (int i = 0; i < order.Count; i++)
        {
            orderIndices[i] = indices.CellToIndex(order[i]);
        }
        field = new GasField(map.Size.x, map.Size.z, orderIndices) { Listener = this };
        environment = new MapGasEnvironment(map);
        simulator = new GasSimulator(field, GasDefRegistry.BuildChannelSettings(), environment);
    }

    public override void FinalizeInit()
    {
        EnsureInitialized();
        if (!eventsHooked)
        {
            map.events.BuildingSpawned += OnBuildingSpawned;
            map.events.RoofChanged += Devices.OnRoofChanged;
            eventsHooked = true;
        }
        // [M6] path source registration
    }

    public override void MapRemoved()
    {
        if (eventsHooked)
        {
            map.events.BuildingSpawned -= OnBuildingSpawned;
            map.events.RoofChanged -= Devices.OnRoofChanged;
            eventsHooked = false;
        }
        // [M6] path source disposal
        if (cachedMap == map)
        {
            cachedMap = null;
            cachedGrid = null;
        }
    }

    public override void MapComponentTick()
    {
        Devices.Tick(Find.TickManager.TicksGame);
        if (field == null)
        {
            return;
        }
        simulator.Tick();
    }

    public override void ExposeData()
    {
        EnsureInitialized();
        CellIndices indices = map.cellIndices;
        MapExposeUtility.ExposeUint(map,
            c => field.Get(indices.CellToIndex(c)),
            (c, value) => field.SetSilently(indices.CellToIndex(c), value),
            "gasVentilationDensity");
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            savedDissipationCursor = simulator.DissipationCursor;
            savedDiffusionCursor = simulator.DiffusionCursor;
            savedDissipationSweep = simulator.DissipationSweep;
        }
        Scribe_Values.Look(ref savedDissipationCursor, "gvDissipationCursor");
        Scribe_Values.Look(ref savedDiffusionCursor, "gvDiffusionCursor");
        Scribe_Values.Look(ref savedDissipationSweep, "gvDissipationSweep");
        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            simulator.DissipationCursor = savedDissipationCursor;
            simulator.DiffusionCursor = savedDiffusionCursor;
            simulator.DissipationSweep = savedDissipationSweep;
        }
    }

    // ---------------------------------------------------------------- queries

    public uint PackedAt(int index)
    {
        return field == null ? 0u : field.Get(index);
    }

    public uint PackedAt(IntVec3 cell)
    {
        return PackedAt(map.cellIndices.CellToIndex(cell));
    }

    public int DensityAt(IntVec3 cell, GasDef gas)
    {
        return GasPacking.Get(PackedAt(cell), gas.channel);
    }

    public bool CanHoldGas(IntVec3 cell)
    {
        EnsureInitialized();
        return cell.InBounds(map) && environment.CanHoldGas(map.cellIndices.CellToIndex(cell));
    }

    // ---------------------------------------------------------------- mutation

    /// <summary>Adds gas to one cell, capped at 255. Returns the amount accepted.</summary>
    public int TryAddGas(IntVec3 cell, GasDef gas, int amount)
    {
        if (amount <= 0 || !CanHoldGas(cell))
        {
            return 0;
        }
        return field.TryAdd(map.cellIndices.CellToIndex(cell), gas.channel, amount);
    }

    /// <summary>
    /// Vent emission with back-pressure: fills the centre cell, then spreads what is left over its passable
    /// cardinal neighbours. Never floods further. Returns the amount accepted.
    /// </summary>
    public int EmitAround(IntVec3 center, GasDef gas, int amount)
    {
        if (amount <= 0 || !CanHoldGas(center))
        {
            return 0;
        }
        CellIndices indices = map.cellIndices;
        int accepted = field.TryAdd(indices.CellToIndex(center), gas.channel, amount);
        int remaining = amount - accepted;
        for (int i = 0; i < 4 && remaining > 0; i++)
        {
            IntVec3 cell = center + GenAdj.CardinalDirections[i];
            if (!CanHoldGas(cell))
            {
                continue;
            }
            int share = (remaining + (3 - i)) / (4 - i);
            int added = field.TryAdd(indices.CellToIndex(cell), gas.channel, share);
            accepted += added;
            remaining -= added;
        }
        return accepted;
    }

    /// <summary>Releases a burst (destroyed manifold or canisters) by flood-filling passable cells from the centre.</summary>
    public int ReleaseBurst(IntVec3 center, GasDef gas, int amount, int maxCells = 150)
    {
        if (amount <= 0 || !center.InBounds(map))
        {
            return 0;
        }
        EnsureInitialized();
        CellIndices indices = map.cellIndices;
        int remaining = amount;
        int released = 0;
        map.floodFiller.FloodFill(center, CanHoldGas, c =>
        {
            int added = field.TryAdd(indices.CellToIndex(c), gas.channel, remaining);
            remaining -= added;
            released += added;
            return remaining <= 0;
        }, maxCells);
        return released;
    }

    public void ClearCell(IntVec3 cell)
    {
        if (field != null && cell.InBounds(map))
        {
            field.Clear(map.cellIndices.CellToIndex(cell));
        }
    }

    public void ClearAll()
    {
        field?.ClearAll();
    }

    /// <summary>Called for vanilla vents (and anything else that calls GasGrid.EqualizeGasThroughBuilding).</summary>
    public void EqualizeThrough(Building building, bool twoWay)
    {
        if (field == null || field.LiveCells == 0 || building?.Map != map)
        {
            return;
        }
        int count = 0;
        if (twoWay)
        {
            AddEqualizeCell(building.Position + building.Rotation.FacingCell, ref count);
            AddEqualizeCell(building.Position - building.Rotation.FacingCell, ref count);
        }
        else
        {
            for (int i = 0; i < 4; i++)
            {
                AddEqualizeCell(building.Position + GenAdj.CardinalDirections[i], ref count);
            }
        }
        if (count <= 1)
        {
            return;
        }
        for (int channel = 0; channel < GasPacking.Channels; channel++)
        {
            EqualizeSums[channel] = 0;
        }
        for (int i = 0; i < count; i++)
        {
            uint packed = field.Get(EqualizeCells[i]);
            for (int channel = 0; channel < GasPacking.Channels; channel++)
            {
                EqualizeSums[channel] += GasPacking.Get(packed, channel);
            }
        }
        uint average = 0u;
        for (int channel = 0; channel < GasPacking.Channels; channel++)
        {
            average = GasPacking.With(average, channel, EqualizeSums[channel] / count);
        }
        for (int i = 0; i < count; i++)
        {
            field.Set(EqualizeCells[i], average);
        }
    }

    private void AddEqualizeCell(IntVec3 cell, ref int count)
    {
        if (CanHoldGas(cell))
        {
            EqualizeCells[count++] = map.cellIndices.CellToIndex(cell);
        }
    }

    private void OnBuildingSpawned(Building building)
    {
        if (field == null || field.LiveCells == 0 || building.def.Fillage != FillCategory.Full || building is Building_Door)
        {
            return;
        }
        foreach (IntVec3 cell in building.OccupiedRect())
        {
            ClearCell(cell);
        }
    }

    // ---------------------------------------------------------------- IGasFieldListener

    void IGasFieldListener.OnBandsChanged(int cellIndex, byte oldBands, byte newBands)
    {
        map.mapDrawer.MapMeshDirty(map.cellIndices.IndexToCell(cellIndex), GVDefOf.GV_GasMesh);
        // [M6] path source notification
    }
}
