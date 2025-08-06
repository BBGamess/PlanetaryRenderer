using UnityEngine;

public class PlanetRenderer : MonoBehaviour
{
    [Header("Properties")]
    [SerializeField, Range(20, 50)] private int halfResolution = 40;
    [SerializeField] private int spacing = 1;
    [SerializeField] private float radius = 1.0f;

    [Header("Chunks")]
    [SerializeField] private int chunksXAxis = 6;
    [SerializeField] private int chunksYAxis = 6;
    [SerializeField] private int chunksZAxis = 6;

    [Header("Compute")]
    public Material mat;
    public ComputeShader marchingShader;
    public ComputeShader isosurfaceShader;

    Chunk _chunk;
    Chunk[] m_Chunks;

    private int resolution;
    private int numChunks;

    private bool m_ChunksInitialized;

    private void DestroyChunks()
    {
        Chunk[] chunkComponents = GetComponentsInChildren<Chunk>();
        if (Application.isPlaying)
        {
            foreach (Chunk chunk in chunkComponents)
            {
                Destroy(chunk.gameObject);
            }
        } else
        {
            foreach (Chunk chunk in chunkComponents)
            {
                DestroyImmediate(chunk.gameObject);
            }
        }
    }

    private void InitChunks()
    {
        if (halfResolution * 2 != resolution || numChunks != chunksXAxis * chunksYAxis * chunksZAxis)
        {
            DestroyChunks();
            m_ChunksInitialized = false;
        }

        if (m_ChunksInitialized) return;

        resolution = 2 * halfResolution;
        numChunks = chunksXAxis * chunksYAxis * chunksZAxis;
        m_Chunks = new Chunk[numChunks];

        int chunkIndex = 0;
        for (int z = 0; z < chunksZAxis; z++)
            for (int y = 0; y < chunksYAxis; y++)
                for (int x = 0; x < chunksXAxis; x++)
                {
                    Vector3 chunkPos = GetChunkCorner(x, y, z);
                    GameObject go = new GameObject($"Chunk{chunkIndex}");
                    go.transform.parent = transform;
                    go.transform.localPosition = chunkPos;
                    Chunk chunk = go.AddComponent<Chunk>();
                    chunk.InitChunk(mat);
                    m_Chunks[chunkIndex] = chunk; 

                    chunkIndex++;
                }

        m_ChunksInitialized = true;
    }

    private Vector3 GetChunkCorner(int x, int y, int z)
    {
        Vector3 pos = new Vector3(x, y, z) - new Vector3(Mathf.CeilToInt(chunksXAxis/2),
            Mathf.CeilToInt(chunksYAxis / 2), Mathf.CeilToInt(chunksZAxis / 2));
        return pos * (resolution - 1) * spacing;
    }

    private void UpdateChunks()
    {
        int voxelPerAxis = resolution - 1;
        int numVoxels = voxelPerAxis * voxelPerAxis * voxelPerAxis;
        int maxTriangles = 5 * numVoxels;

        int pointsPerAxis = resolution + 1;
        int numPoints = pointsPerAxis * pointsPerAxis * pointsPerAxis;

        for (int i = 0; i < numChunks; i++)
        {
            m_Chunks[i].UpdateChunkPipeline(marchingShader, isosurfaceShader, Vector3.zero, radius, resolution, maxTriangles, numPoints);
        }
    }

    public void RenderPlanet()
    {
        InitChunks();
        UpdateChunks();
    }
}
