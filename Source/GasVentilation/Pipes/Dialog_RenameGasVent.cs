using Verse;

namespace GasVentilation;

public sealed class Dialog_RenameGasVent : Dialog_Rename<Building_GasVent>
{
    public Dialog_RenameGasVent(Building_GasVent renaming) : base(renaming)
    {
    }
}
