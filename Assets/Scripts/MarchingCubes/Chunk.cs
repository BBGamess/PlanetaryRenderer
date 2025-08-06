using System.Runtime.InteropServices;
using UnityEngine;

[RequireComponent (typeof(MeshFilter), typeof(MeshRenderer))]
public class Chunk : MonoBehaviour
{
    private MeshFilter m_MeshFilter;
    private MeshRenderer m_Renderer;
    private Mesh m_Mesh;
    private Material m_Material;

    private ComputeBuffer m_TriangleBuffer;
    private ComputeBuffer m_TriangleCountBuffer;
    private ComputeBuffer m_PointsBuffer;
    private ComputeBuffer m_ChunkOffsetBuffer;

    private bool m_BuffersReleased = true;
    private bool m_ChunkInitialized = false;

    struct Triangle
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

        m_ChunkInitialized = true;
    }

    private void CreateBuffers(int maxTriangleCount, int numPoints)
    {
        if (!m_BuffersReleased) ReleaseBuffers();

        m_TriangleBuffer = new ComputeBuffer(maxTriangleCount, sizeof(float) * 3 * 3, ComputeBufferType.Append);
        m_TriangleCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);

        m_PointsBuffer = new ComputeBuffer(numPoints, sizeof(float) * 4, ComputeBufferType.Raw);
        m_ChunkOffsetBuffer = new ComputeBuffer(1, sizeof(float) * 3, ComputeBufferType.Raw);
        m_ChunkOffsetBuffer.SetData(new Vector3[] { transform.localPosition });

        m_BuffersReleased = false;
    }

    private void ReleaseBuffers()
    {
        if (m_BuffersReleased) return;

        m_TriangleBuffer.Release();
        m_TriangleCountBuffer.Release();
        m_PointsBuffer.Release();
        m_ChunkOffsetBuffer.Release();

        m_TriangleBuffer = null;
        m_TriangleCountBuffer = null;
        m_PointsBuffer = null;
        m_ChunkOffsetBuffer = null;

        m_BuffersReleased = true;
    }

    private void ComputeChunk(ComputeShader isosurfaceShader, Vector3 center, float radius, int resolution)
    {
        int threadGroups = Mathf.CeilToInt((resolution+1) / 8.0f);
        int kernelID = isosurfaceShader.FindKernel("SphereIsosurface");

        isosurfaceShader.SetBuffer(kernelID, "chunkOffset", m_ChunkOffsetBuffer);
        isosurfaceShader.SetBuffer(kernelID, "points", m_PointsBuffer);
        isosurfaceShader.SetVector("center", center);
        isosurfaceShader.SetFloat("radius", radius);
        isosurfaceShader.SetInt("resolution", resolution);

        isosurfaceShader.Dispatch(kernelID, threadGroups, threadGroups, threadGroups);
    }

    public void UpdateChunkPipeline(
        ComputeShader marchingShader, ComputeShader isosurfaceShader, Vector3 surfaceCenter, 
        float radius, int resolution, int maxTriangleCount, int numPoints)
    {
        if (!m_ChunkInitialized) return;

        CreateBuffers(maxTriangleCount, numPoints);
        m_TriangleBuffer.SetCounterValue(0);

        ComputeChunk(isosurfaceShader, surfaceCenter, radius, resolution);


        int threadGroups = Mathf.CeilToInt(resolution / 8.0f);
        int kernelID = marchingShader.FindKernel("MarchChunk");

        marchingShader.SetBuffer(kernelID, "triangleBuffer", m_TriangleBuffer);
        marchingShader.SetBuffer(kernelID, "points", m_PointsBuffer);
        marchingShader.SetBuffer(kernelID, "chunkOffset", m_ChunkOffsetBuffer);
        marchingShader.SetInt("resolution", resolution);
        marchingShader.SetFloat("isovalue", 0.0f);

        marchingShader.Dispatch(kernelID, threadGroups, threadGroups, threadGroups);

        ComputeBuffer.CopyCount(m_TriangleBuffer, m_TriangleCountBuffer, 0);
        int[] triCountArr = { 0 };
        m_TriangleCountBuffer.GetData(triCountArr);
        int numTriangles = triCountArr[0];

        Triangle[] triangles = new Triangle[numTriangles];
        m_TriangleBuffer.GetData(triangles, 0, 0, numTriangles);

        SetMesh(triangles);

        ReleaseBuffers();
    }

    public void UpdateChunk(ComputeShader marchingShader, Vector3 center, float radius, int resolution, int maxTriangleCount, int numPoints)
    {
        if (!m_ChunkInitialized) return;

        CreateBuffers(maxTriangleCount, numPoints);
        m_TriangleBuffer.SetCounterValue(0);

        int threadGroups = Mathf.CeilToInt(resolution / 8.0f);
        int kernelID = marchingShader.FindKernel("March");

        ComputeBuffer offBuffer = new ComputeBuffer(1, sizeof(float) * 3, ComputeBufferType.Raw);
        offBuffer.SetData(new Vector3[] { transform.localPosition });

        marchingShader.SetInt("resolution", resolution);
        marchingShader.SetFloat("radius", radius);
        marchingShader.SetBuffer(kernelID, "off", offBuffer);
        marchingShader.SetBuffer(kernelID, "triangleBuffer", m_TriangleBuffer);

        marchingShader.Dispatch(kernelID, threadGroups, threadGroups, threadGroups);

        ComputeBuffer.CopyCount(m_TriangleBuffer, m_TriangleCountBuffer, 0);
        int[] triCountArr = { 0 };
        m_TriangleCountBuffer.GetData(triCountArr);
        int numTriangles = triCountArr[0];

        Triangle[] triangles = new Triangle[numTriangles];
        m_TriangleBuffer.GetData(triangles, 0, 0, numTriangles);

        SetMesh(triangles);

        ReleaseBuffers();
        offBuffer.Release();
    }

    private void SetMesh(Triangle[] triangles)
    {
        if (m_Mesh != null) m_Mesh.Clear();
        m_Mesh = new Mesh()
        {
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
        };
        int numTriangles = triangles.Length;

        Vector3[] meshVertices = new Vector3[numTriangles * 3];
        int[] meshTriangles = new int[numTriangles * 3];

        for (int i = 0; i < numTriangles; i++)
            for (int j = 0; j < 3; j++)
            {
                meshTriangles[i * 3 + j] = i * 3 + j;
                meshVertices[i * 3 + j] = triangles[i][j];
            }

        m_Mesh.Clear();

        m_Mesh.vertices = meshVertices;
        m_Mesh.triangles = meshTriangles;

        m_Mesh.RecalculateBounds();
        m_Mesh.RecalculateNormals();
        m_Mesh.RecalculateTangents();

        m_MeshFilter.mesh = m_Mesh;
    }

    /*private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Vector3 corner = transform.position;
        Gizmos.DrawSphere(corner, 1.0f);

        Gizmos.color = Color.gray;
        Vector3 vec = new Vector3(20.0f, 20.0f, 20.0f);
        Gizmos.DrawWireCube(corner + vec, Vector3.one * 41);
    }*/
}