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

    static GVDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(GVDefOf));
    }
}
