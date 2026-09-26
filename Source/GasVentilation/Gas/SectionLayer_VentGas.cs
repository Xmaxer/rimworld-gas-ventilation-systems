using GasVentilation.Core;
using UnityEngine;
using Verse;

namespace GasVentilation;

/// <summary>
/// Draws ventilation gas clouds. Created automatically by the game for every map section (public constructor
/// taking a Section). Uses its own MapMeshFlagDef, so vanilla gas changes never rebuild it and vice versa.
/// </summary>
public sealed class SectionLayer_VentGas : SectionLayer
{
    // Vertex colour selects the shader's first ("smoke") channel; the tint comes from the material colour.
    private static readonly Color32 VertexColor = new Color32(255, 0, 0, 0);

    private readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

    public SectionLayer_VentGas(Section section) : base(section)
    {
        relevantChangeTypes = GVDefOf.GV_GasMesh;
    }

    public override bool Visible => DebugViewSettings.drawGas;

    public override void Regenerate()
    {
        ClearSubMeshes(MeshParts.All);
        VentGasGrid grid = VentGasGrid.For(Map);
        if (grid == null || grid.LiveCells == 0)
        {
            return;
        }
        float altitude = AltitudeLayer.Gas.AltitudeFor();
        CellIndices indices = Map.cellIndices;
        CellRect rect = section.CellRect;
        for (int z = rect.minZ; z <= rect.maxZ; z++)
        {
            for (int x = rect.minX; x <= rect.maxX; x++)
            {
                int index = indices.CellToIndex(x, z);
                uint packed = grid.PackedAt(index);
                if (packed == 0u)
                {
                    continue;
                }
                for (int channel = 0; channel < GasPacking.Channels; channel++)
                {
                    int band = GasBands.Band(GasPacking.Get(packed, channel));
                    if (band == GasBands.None)
                    {
                        continue;
                    }
                    Material material = GasMaterials.Get(channel, band);
                    if (material != null)
                    {
                        AddQuad(GetSubMesh(material), x, z, index, altitude);
                    }
                }
            }
        }
        FinalizeMesh(MeshParts.All);
    }

    public override void DrawLayer()
    {
        if (!Visible)
        {
            return;
        }
        propertyBlock.SetFloat(ShaderPropertyIDs.AgeSecsPausable, RealTime.UnpausedRealTime);
        for (int i = 0; i < subMeshes.Count; i++)
        {
            LayerSubMesh subMesh = subMeshes[i];
            if (subMesh.finalized && !subMesh.disabled)
            {
                Graphics.DrawMesh(subMesh.mesh, Vector3.zero, Quaternion.identity, subMesh.material, 0, null, 0, propertyBlock);
            }
        }
    }

    // Same jittered, oversized quad recipe as vanilla SectionLayer_Gas, seeded by cell index so it is stable.
    private static void AddQuad(LayerSubMesh subMesh, int x, int z, int index, float altitude)
    {
        Rand.PushState(index);
        float grow = Rand.Range(0.4f, 0.6f);
        float offsetX = Rand.Range(-0.2f, 0.2f);
        float offsetZ = Rand.Range(-0.2f, 0.2f);
        float y = altitude + Rand.Range(-0.01f, 0.01f);
        Rand.PopState();

        float x0 = x - grow + offsetX;
        float x1 = x + 1 + grow + offsetX;
        float z0 = z - grow + offsetZ;
        float z1 = z + 1 + grow + offsetZ;
        int start = subMesh.verts.Count;
        subMesh.verts.Add(new Vector3(x0, y, z0));
        subMesh.verts.Add(new Vector3(x0, y, z1));
        subMesh.verts.Add(new Vector3(x1, y, z1));
        subMesh.verts.Add(new Vector3(x1, y, z0));
        subMesh.uvs.Add(new Vector3(0f, 0f, index));
        subMesh.uvs.Add(new Vector3(0f, 1f, index));
        subMesh.uvs.Add(new Vector3(1f, 1f, index));
        subMesh.uvs.Add(new Vector3(1f, 0f, index));
        subMesh.colors.Add(VertexColor);
        subMesh.colors.Add(VertexColor);
        subMesh.colors.Add(VertexColor);
        subMesh.colors.Add(VertexColor);
        subMesh.tris.Add(start);
        subMesh.tris.Add(start + 1);
        subMesh.tris.Add(start + 2);
        subMesh.tris.Add(start);
        subMesh.tris.Add(start + 2);
        subMesh.tris.Add(start + 3);
    }
}
