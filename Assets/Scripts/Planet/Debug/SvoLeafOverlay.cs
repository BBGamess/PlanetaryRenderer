using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

public class SvoOverleay : MonoBehaviour
{
    [SerializeField] private PlanetRenderer planetRenderer;
    [SerializeField] private ComputeShader appendLeavesShader;
    [SerializeField] private Mesh cubeMesh;
    [SerializeField] private Material voxelMaterial;

    [SerializeField] private Color color = new Color(1f, 0.45f, 0.1f, 0.3f);
    [SerializeField, Range(0.1f, 1.0f)] private float cubeScale = 0.92f;
    [SerializeField] private int maxLeafVoxels = 8_000_000;

    private ComputeBuffer voxelBuffer;
    private ComputeBuffer argsBuffer;
    private Bounds bounds;

    [StructLayout(LayoutKind.Sequential)]
    private struct Voxel
    {
        public Vector3 center;
        public float size;
        public static int Stride => sizeof(float) * 4;
    }

    [ContextMenu("Generate SVO Leaf Overlay")]
    public void Generate()
    {
        if (planetRenderer == null || appendLeavesShader == null || cubeMesh == null || voxelMaterial == null)
            return;

        Release();

        voxelBuffer = new ComputeBuffer(maxLeafVoxels, Voxel.Stride, ComputeBufferType.Append);
        voxelBuffer.SetCounterValue(0);

        argsBuffer = new ComputeBuffer(1, sizeof(uint) * 5, ComputeBufferType.IndirectArguments);
        uint[] args = new uint[5];
        args[0] = cubeMesh.GetIndexCount(0);
        args[1] = 0;
        args[2] = cubeMesh.GetIndexStart(0);
        args[3] = cubeMesh.GetBaseVertex(0);
        args[4] = 0;
        argsBuffer.SetData(args);

        var ctx = new PlanetGpuContext(
            planetRenderer.SvoBuilderShader,
            planetRenderer.MarchingShader,
            planetRenderer.planetShape,
            planetRenderer.Radius,
            planetRenderer.VoxelPerAxis,
            planetRenderer.Spacing,
            planetRenderer.DebugMaxNodes
        );

        ctx.SetRadius(planetRenderer.planetShape);
        ctx.SetNoiseLayers(planetRenderer.planetShape);
        ctx.SetVoxelPerAxis(planetRenderer.VoxelPerAxis);
        ctx.SetSpacing(planetRenderer.Spacing);

        int kernel = appendLeavesShader.FindKernel("AppendLeafNodesToVoxelInstances");

        foreach (Vector3 chunkCorner in planetRenderer.AllChunkCorners())
        {
            int leafCount = ctx.BuildChunksSvoOnly(chunkCorner);
            if (leafCount == 0) continue;

            appendLeavesShader.SetInt("leafCount", leafCount);
            appendLeavesShader.SetBuffer(kernel, "leafNodes", ctx.LeafBuffer);
            appendLeavesShader.SetBuffer(kernel, "voxelBuffer", voxelBuffer);

            int groups = Mathf.CeilToInt(leafCount / 64.0f);
            appendLeavesShader.Dispatch(kernel, groups, 1, 1);
        }

        ComputeBuffer.CopyCount(voxelBuffer, argsBuffer, sizeof(uint));

        int vx = planetRenderer.ChunksX * planetRenderer.VoxelPerAxis;
        int vy = planetRenderer.ChunksY * planetRenderer.VoxelPerAxis;
        int vz = planetRenderer.ChunksZ * planetRenderer.VoxelPerAxis;

        Vector3 size = new Vector3(
            vx * planetRenderer.Spacing,
            vy * planetRenderer.Spacing,
            vz * planetRenderer.Spacing
        );

        bounds = new Bounds(Vector3.zero, size * 2.0f);

        ctx.Dispose();
    }

    private void LateUpdate()
    {
        Draw();
    }

    private void Draw()
    {
        if (voxelBuffer == null || argsBuffer == null || cubeMesh == null || voxelMaterial == null)
            return;

        voxelMaterial.SetBuffer("_Voxels", voxelBuffer);
        voxelMaterial.SetVector("_WorldOffset", planetRenderer.transform.position);
        voxelMaterial.SetColor("_Color", color);
        voxelMaterial.SetFloat("_Scale", cubeScale);

        voxelMaterial.SetFloat("_ClipMode", 1.0f);
        voxelMaterial.SetVector("_ClipPlane", new Vector4(0, 0, 1, 0));
        voxelMaterial.SetFloat("_SubsampleStride", 1.0f);

        Graphics.DrawMeshInstancedIndirect(
            cubeMesh,
            0,
            voxelMaterial,
            bounds,
            argsBuffer,
            0,
            null,
            ShadowCastingMode.Off,
            false,
            gameObject.layer
        );
    }

    private void OnDisable() => Release();

    private void Release()
    {
        voxelBuffer?.Release();
        voxelBuffer = null;

        argsBuffer?.Release();
        argsBuffer = null;
    }
}
