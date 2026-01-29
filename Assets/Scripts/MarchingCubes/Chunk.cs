using System.Runtime.InteropServices;
using UnityEngine;

[RequireComponent (typeof(MeshFilter), typeof(MeshRenderer))]
public class Chunk : MonoBehaviour
{
    private MeshFilter m_MeshFilter;
    private MeshRenderer m_Renderer;
    public Mesh m_Mesh;
    private Material m_Material;

    private Vector2 m_MinMax;

    private bool m_BuffersReleased = true;
    private bool m_ChunkInitialized = false;

    public float Max
    {
        get { return m_MinMax[1]; }
    }

    public float Min
    {
        get { return m_MinMax[0]; }
    }
    
    public bool Initialized
    {
        get { return m_ChunkInitialized; }
    }

    public struct Triangle
    {
        public Vector3 a, b, c;

        public readonly Vector3 this[int index]
        {
            get
            {
                return index switch
                {
                    0 => a,
                    1 => b,
                    2 => c,
                    _ => Vector3.zero,
                };
            }
        }
    }

    public void InitChunk(Material mat)
    {
        m_Material = mat;
        m_MeshFilter = GetComponent<MeshFilter>();
        m_Renderer = GetComponent<MeshRenderer>();
        m_Renderer.sharedMaterial = m_Material;

        if (m_Mesh == null)
        {
            m_Mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m_Mesh.MarkDynamic();
            m_MeshFilter.sharedMesh = m_Mesh;
        }

        m_ChunkInitialized = true;
    }

    public void ComputeNoise(ComputeShader noiseShader, ComputeBuffer noiseBuffer, ComputeBuffer noiseLayersBuffer, Vector3 center, float radius,
        int resolution, int spacing, int numLayers)
    {
        int threadGroups = Mathf.CeilToInt((resolution+1) / 8.0f);
        int kernelID = noiseShader.FindKernel("FractalNoise");

        noiseShader.SetBuffer(kernelID, "noise", noiseBuffer);
        noiseShader.SetBuffer(kernelID, "noiseLayers", noiseLayersBuffer);

        noiseShader.SetVector("chunkOffset", transform.localPosition);
        noiseShader.SetVector("center", center);
        noiseShader.SetFloat("radius", radius);
        noiseShader.SetInt("resolution", resolution);
        noiseShader.SetInt("spacing", spacing);
        noiseShader.SetInt("numLayers", numLayers);

        noiseShader.Dispatch(kernelID, threadGroups, threadGroups, threadGroups);
    }

    public void ComputeElevation(ComputeShader isosurfaceShader, ComputeBuffer pointsBuffer, ComputeBuffer noiseBuffer,
        Vector3 center, float radius, int resolution, int spacing)
    {
        int threadGroups = Mathf.CeilToInt((resolution+1) / 8.0f);
        int kernelID = isosurfaceShader.FindKernel("SphereIsosurface");

        Vector2[] minMaxArray = { new Vector2(float.MaxValue, float.MinValue) };
        ComputeBuffer minMaxBuffer = new ComputeBuffer(1, sizeof(float) * 2, ComputeBufferType.Default);
        minMaxBuffer.SetData(minMaxArray);

        isosurfaceShader.SetBuffer(kernelID, "minMax", minMaxBuffer);
        isosurfaceShader.SetBuffer(kernelID, "points", pointsBuffer);
        isosurfaceShader.SetBuffer(kernelID, "noise", noiseBuffer);

        isosurfaceShader.SetVector("chunkOffset", transform.localPosition);
        isosurfaceShader.SetVector("center", center);
        isosurfaceShader.SetFloat("radius", radius);
        isosurfaceShader.SetInt("resolution", resolution);
        isosurfaceShader.SetInt("spacing", spacing);

        isosurfaceShader.Dispatch(kernelID, threadGroups, threadGroups, threadGroups);
        
        minMaxBuffer.GetData(minMaxArray);
        m_MinMax = minMaxArray[0];
        
        minMaxBuffer.Release();
    }

    public void MarchChunk(
        ComputeShader marchingShader, ComputeBuffer triangleBuffer, ComputeBuffer triangleCountBuffer, ComputeBuffer pointsBuffer, int resolution)
    {
        if (!m_ChunkInitialized) return;

        triangleBuffer.SetCounterValue(0);

        int threadGroups = Mathf.CeilToInt(resolution / 8.0f);
        int kernelID = marchingShader.FindKernel("MarchChunk");

        marchingShader.SetBuffer(kernelID, "triangleBuffer", triangleBuffer);
        marchingShader.SetBuffer(kernelID, "points", pointsBuffer);

        marchingShader.SetInt("resolution", resolution);
        marchingShader.SetFloat("isovalue", 0.0f);

        marchingShader.Dispatch(kernelID, threadGroups, threadGroups, threadGroups);

        ComputeBuffer.CopyCount(triangleBuffer, triangleCountBuffer, 0);
        int[] triCountArr = { 0 };
        triangleCountBuffer.GetData(triCountArr);
        int numTriangles = triCountArr[0];

        Triangle[] triangles = new Triangle[numTriangles];
        triangleBuffer.GetData(triangles, 0, 0, numTriangles);

        SetMesh(triangles);
    }

    private void SetMesh(Triangle[] triangles)
    {
        m_Mesh.Clear();
        int numTriangles = triangles.Length;

        Vector3[] meshVertices = new Vector3[numTriangles * 3];
        int[] meshTriangles = new int[numTriangles * 3];

        for (int i = 0; i < numTriangles; i++)
            for (int j = 0; j < 3; j++)
            {
                meshTriangles[i * 3 + j] = i * 3 + j;
                meshVertices[i * 3 + j] = triangles[i][j];
            }

        m_Mesh.SetVertices(meshVertices);
        m_Mesh.SetTriangles(meshTriangles, 0);

        m_Mesh.RecalculateBounds();
        m_Mesh.RecalculateNormals();

        m_MeshFilter.mesh = m_Mesh;
    }
}