using GasVentilation.Core;
using RimWorld;
using Verse;

namespace GasVentilation;

/// <summary>Adapts a RimWorld map to the core simulator. Passability mirrors vanilla GasGrid.GasCanMoveTo.</summary>
internal sealed class MapGasEnvironment : IGasEnvironment
{
    private readonly Map map;
    private readonly EdificeGrid edifices;
    private readonly RoofGrid roofs;
    private readonly CellIndices indices;
    private readonly bool vacuumPossible;

    public MapGasEnvironment(Map map)
    {
        this.map = map;
        edifices = map.edificeGrid;
        roofs = map.roofGrid;
        indices = map.cellIndices;
        vacuumPossible = ModsConfig.OdysseyActive;
    }

    public bool CanHoldGas(int cellIndex)
    {
        Building building = edifices[cellIndex];
        if (building == null || building.def.Fillage != FillCategory.Full)
        {
            return true;
        }
        return building is Building_Door door && door.Open;
    }

    public bool IsRoofed(int cellIndex)
    {
        return roofs.Roofed(cellIndex);
    }

    public float Vacuum(int cellIndex)
    {
        return vacuumPossible ? indices.IndexToCell(cellIndex).GetVacuum(map) : 0f;
    }

    public int RandomInt(int exclusiveMax)
    {
        return Rand.Range(0, exclusiveMax);
    }
}
