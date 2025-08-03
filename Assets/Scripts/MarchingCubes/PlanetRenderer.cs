using System.Text;
using UnityEngine;

public class PlanetRenderer : MonoBehaviour
{
    [Header("Properties")]
    [SerializeField, Range(30, 100)] private int resolutionPerChunk = 40;
    [SerializeField] private float radius = 20.0f;

    [Header("Chunks")]
    [SerializeField] private int chunksXAxis = 10;
    [SerializeField] private int chunksYAxis = 10;
    [SerializeField] private int chunksZAxis = 10;

    [Header("Marching Shader")]
    [SerializeField] private ComputeShader marchingCubesShader;
    [SerializeField] private Material marchingMaterial;

    private Chunk[] m_Chunks;
    private int m_NumChunks;
    private int m_MaxTrianglesPerChunk;
    private int m_Resolution2x;
    struct Triangle
    {
        public Vector3 a; public Vector3 b; public Vector3 c;

        public Vector3 this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0:
                        return a;
                    case 1:
                        return b;
                    case 2:
                        return c;
                    default:
                        return Vector3.zero;
                }
            }
        }
    }

    private void Start()
    {
        DestroyChunks();

        m_Resolution2x = resolutionPerChunk * 2;

        m_NumChunks = chunksXAxis * chunksYAxis * chunksZAxis;
        m_Chunks = new Chunk[m_NumChunks];
        for (int i = 0;  i < m_NumChunks; i++)
        {
            string name = "Chunk" + (i + 1).ToString();
            GameObject chunkObject = new GameObject(name);
            chunkObject.transform.parent = transform;
            chunkObject.transform.localPosition = Vector3.zero;

            Chunk chunk = Chunk.InitializeChunk(chunkObject, i);
            m_Chunks[i] = chunk;
        }

        // Minus 1 because resolution is inclusive
        int voxelPerAxis = m_Resolution2x - 1;
        int voxelPerChunk = voxelPerAxis * voxelPerAxis * voxelPerAxis;
        // In marching cubes algorithm a voxel can have a maximum of 5 triangles
        m_MaxTrianglesPerChunk = voxelPerChunk * 5;
    }

    private Chunk GetChunk(int x, int y, int z)
    {
        int index = x + y * chunksXAxis + z * chunksXAxis * chunksYAxis;
        return m_Chunks[index];
    }

    private void UpdateChunk(Chunk chunk)
    {
        chunk.CreateBuffers(m_MaxTrianglesPerChunk);

        chunk.TriangleBuffer.SetCounterValue(0);

        int threadGroups = Mathf.CeilToInt(m_Resolution2x / 8.0f);
        int kernelID = marchingCubesShader.FindKernel("March");

        marchingCubesShader.SetBuffer(kernelID, "triangleBuffer", chunk.TriangleBuffer);

        marchingCubesShader.Dispatch(kernelID, threadGroups, threadGroups, threadGroups / 2);

        ComputeBuffer.CopyCount(chunk.TriangleBuffer, chunk.TriangleCountBuffer, 0);

        // Use a buffer for a single integer because the result is always dependent on the group dispatched
        int[] triCountArr = { 0 };
        chunk.TriangleCountBuffer.GetData(triCountArr);
        int numTriangles = triCountArr[0];

        Triangle[] triangles = new Triangle[numTriangles];
        chunk.TriangleBuffer.GetData(triangles, 0, 0, numTriangles);

        chunk.ReleaseBuffers();

        Vector3[] meshVertices = new Vector3[numTriangles * 3];
        int[] meshTriangles = new int[numTriangles * 3];

        // Turn the flattened float3 array into vertex and triangle index arrays
        for (int y = 0; y < numTriangles; y++)
            for (int x = 0; x < 3; x++)
            {
                meshTriangles[x + y * 3] = x + y * 3;
                meshVertices[x + y * 3] = triangles[y][x];
            }

        chunk.SetMesh(meshVertices, meshTriangles, marchingMaterial);
    }

    private void DestroyChunks()
    {
        foreach(Chunk chunk in m_Chunks)
        {
            if (chunk == null) continue;
            Destroy(chunk.gameObject);
        }
    }

    public void UpdateChunks()
    {
        Start();
        marchingCubesShader.SetFloat("radius", radius);
        marchingCubesShader.SetInt("resolution", m_Resolution2x);
        foreach(Chunk chunk in m_Chunks)
        {
            UpdateChunk(chunk);
        }
    }
}
