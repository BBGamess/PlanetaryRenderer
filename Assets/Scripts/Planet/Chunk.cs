using System;
using System.Runtime.InteropServices;
using UnityEngine;

[RequireComponent (typeof(MeshFilter), typeof(MeshRenderer))]
public class Chunk : MonoBehaviour
{
    private MeshFilter m_MeshFilter;
    private MeshRenderer m_Renderer;
    public  Mesh m_Mesh;
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

    public void SetMesh(PlanetGpuContext.Triangle[] triangles)
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

    public void SetMinMax(float radius)
    {
        m_MinMax = new Vector2 (int.MaxValue, int.MinValue);
        foreach(Vector3 vert in m_Mesh.vertices)
        {
            Vector3 planetLocal = transform.localPosition + vert;
            float elevation = planetLocal.magnitude - radius;
            if (elevation < m_MinMax[0]) m_MinMax[0] = elevation;
            if (elevation > m_MinMax[1]) m_MinMax[1] = elevation;
        }
    }
}