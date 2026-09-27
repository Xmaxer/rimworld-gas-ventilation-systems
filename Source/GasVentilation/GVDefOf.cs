using RimWorld;
using Verse;

namespace GasVentilation;

[DefOf]
public static class GVDefOf
{
    public static GasDef GV_Gas_Toxin;
    public static GasDef GV_Gas_Sedative;
    public static GasDef GV_Gas_Haywire;
    public static GasDef GV_Gas_Insecticide;

    public static MapMeshFlagDef GV_GasMesh;

    public static ThingDef GV_CanisterEmpty;
    public static ThingDef GV_HiddenPipe;
    public static ThingDef GV_Manifold;

    public static JobDef GV_FleeGas;
    public static JobDef GV_ReconfigureManifold;

    static GVDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(GVDefOf));
    }
}
