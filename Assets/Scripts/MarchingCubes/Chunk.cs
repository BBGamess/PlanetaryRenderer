using System.Collections.Generic;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Chunk : MonoBehaviour {
    private MeshFilter m_MeshFilter;
    private MeshRenderer m_Renderer;
    private Mesh m_Mesh;

    private ComputeBuffer m_TriangleBuffer;
    private ComputeBuffer m_TriangleCountBuffer;

    private int m_ChunkIndex;
    private bool m_BuffersReleased = true;

    public ComputeBuffer TriangleBuffer
    {
        get { return m_TriangleBuffer; }
    }

    public ComputeBuffer TriangleCountBuffer
    {
        get { return m_TriangleCountBuffer; }
    }

    public int ChunkIndex
    {
        get { return m_ChunkIndex; }
        set { m_ChunkIndex = value; }
    }

    public static Chunk InitializeChunk(GameObject gameObject, int chunkIndex)
    {
        Chunk chunk = gameObject.AddComponent<Chunk>();
        chunk.ChunkIndex = chunkIndex;

        // Mesh filter and renderer are added by unity since Chunk has a require component attribute
        chunk.m_MeshFilter = gameObject.GetComponent<MeshFilter>();
        chunk.m_Renderer = gameObject.GetComponent<MeshRenderer>();

        return chunk;
    }

    public void CreateBuffers(int maxTriangles)
    {
        if (!m_BuffersReleased)
        {
            ReleaseBuffers();
        }

        m_TriangleBuffer = new ComputeBuffer(maxTriangles, sizeof(float) * 3 * 3, ComputeBufferType.Append);
        m_TriangleCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
        m_BuffersReleased = false;
    }

    public void ReleaseBuffers()
    {
        m_TriangleCountBuffer.Release();
        m_TriangleCountBuffer.Release();
        m_BuffersReleased = true;
    }

    public void SetMesh(Vector3[] vertices, int[] triangles, Material mat)
    {
        if (m_Mesh == null)
        {
            // Manually set indexing format to 32 bit unsigned integers because Unity has it set to 16 bit by default for some reason
            // 16 bit indices are not enough for most planetary meshes
            m_Mesh = new Mesh() { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        }

        m_MeshFilter.mesh = null;

        m_Mesh.Clear();
        m_Mesh.vertices = vertices;
        m_Mesh.triangles = triangles;

        m_Mesh.RecalculateBounds();
        m_Mesh.RecalculateNormals();

        if (mat != null)
            m_Renderer.material = mat;

        m_MeshFilter.mesh = m_Mesh;
    }

}
