using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

public class DenseGridOverlay : MonoBehaviour
{
    [SerializeField] private PlanetRenderer planetRenderer;
    [SerializeField] private ComputeShader denseGridShader;
    [SerializeField] private Mesh cubeMesh;
    [SerializeField] private Material voxelMaterial;

    [SerializeField] private Color color = new Color(0.7f, 0.7f, 0.7f, 0.3f);
    [SerializeField, Range(0.1f, 1.0f)] private float cubeScale = 1.0f;

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

    [ContextMenu("Generate Dense Grid Overlay")]
    public void Generate()
    {
        if (planetRenderer == null || denseGridShader == null || cubeMesh == null || voxelMaterial == null)
            return;

        Release();

        int vx = planetRenderer.ChunksX * planetRenderer.VoxelPerAxis;
        int vy = planetRenderer.ChunksY * planetRenderer.VoxelPerAxis;
        int vz = planetRenderer.ChunksZ * planetRenderer.VoxelPerAxis;

        int total = vx * vy * vz;

        voxelBuffer = new ComputeBuffer(total, Voxel.Stride, ComputeBufferType.Structured);
        argsBuffer = new ComputeBuffer(1, sizeof(uint) * 5, ComputeBufferType.IndirectArguments);

        int kernel = denseGridShader.FindKernel("GenerateDenseGrid");

        denseGridShader.SetInt("voxelsX", vx);
        denseGridShader.SetInt("voxelsY", vy);
        denseGridShader.SetInt("voxelsZ", vz);
        denseGridShader.SetFloat("voxelSize", planetRenderer.Spacing);
        denseGridShader.SetVector("gridMinCorner", planetRenderer.PlanetGridMinCorner);
        denseGridShader.SetBuffer(kernel, "voxelBuffer", voxelBuffer);

        int gx = Mathf.CeilToInt(vx / 8.0f);
        int gy = Mathf.CeilToInt(vy / 8.0f);
        int gz = Mathf.CeilToInt(vz / 8.0f);

        denseGridShader.Dispatch(kernel, gx, gy, gz);

        uint[] args = new uint[5];
        args[0] = cubeMesh.GetIndexCount(0);
        args[1] = (uint)total;
        args[2] = cubeMesh.GetIndexStart(0);
        args[3] = cubeMesh.GetBaseVertex(0);
        args[4] = 0;
        argsBuffer.SetData(args);

        Vector3 size = new Vector3(
            vx * planetRenderer.Spacing,
            vy * planetRenderer.Spacing,
            vz * planetRenderer.Spacing
        );
        bounds = new Bounds(Vector3.zero, size * 2.0f);
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
        voxelMaterial.SetFloat("_SubsampleStride", 4.0f);

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
