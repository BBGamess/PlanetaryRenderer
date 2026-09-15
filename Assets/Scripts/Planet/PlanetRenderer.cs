using System;
using UnityEngine;

public class PlanetRenderer : MonoBehaviour
{
    const int COLOR_TEXTURE_RESOLUTION = 200;

    // CAUTION: Increase the resolution alongside with chunks per axes irresponsibly, you will quickly find yourself out of GPU memory
    // Your system may CRASH
    [Header("Properties")]
    [SerializeField, Range(16, 1024)] private int voxelPerAxis = 64;
    [SerializeField] private int spacing = 1;

    [Header("Chunks")]
    [SerializeField] private int chunksXAxis = 6;
    [SerializeField] private int chunksYAxis = 6;
    [SerializeField] private int chunksZAxis = 6;

    [Header("Compute")]
    [SerializeField, Range(1, 10)] private int chunksPerIteration = 5;
    [SerializeField] private Material mat;
    [SerializeField] private ComputeShader marchingShader;
    [SerializeField] private ComputeShader svoBuilderShader;

    [HideInInspector] public PlanetColors planetColors;
    [HideInInspector] public PlanetShape  planetShape;

    private Chunk[] m_Chunks;

    private int numChunks;

    private bool m_ChunksInitialized = false;
    [HideInInspector] public bool startRender = false;

    private float m_Radius = 100f;
    private int m_NoiseCount = 1;

    [HideInInspector] public Vector2 minMaxVec;

    private PlanetGpuContext m_GpuContext;

    public float Radius => m_Radius;
    public int VoxelPerAxis => voxelPerAxis;
    public int Spacing => spacing;
    public int ChunksX => chunksXAxis;
    public int ChunksY => chunksYAxis;
    public int ChunksZ => chunksZAxis;

    public ComputeShader SvoBuilderShader => svoBuilderShader;
    public ComputeShader MarchingShader => marchingShader;
    public int DebugMaxNodes => 2 << 20;

    public Vector3 PlanetGridMinCorner => new(
    -Mathf.CeilToInt(chunksXAxis / 2f) * voxelPerAxis * spacing,
    -Mathf.CeilToInt(chunksYAxis / 2f) * voxelPerAxis * spacing,
    -Mathf.CeilToInt(chunksZAxis / 2f) * voxelPerAxis * spacing
    );

    public System.Collections.Generic.IEnumerable<Vector3> AllChunkCorners()
    {
        for (int z = 0; z < chunksZAxis; z++)
            for (int y = 0; y < chunksYAxis; y++)
                for (int x = 0; x < chunksXAxis; x++)
                    yield return GetChunkCorner(x, y, z);
    }

    private void OnValidate()
    {
        if (planetColors == null) planetColors = new PlanetColors(COLOR_TEXTURE_RESOLUTION);
        else planetColors.colorResolution = COLOR_TEXTURE_RESOLUTION;

        if (planetShape == null) planetShape = new PlanetShape();
        
        UpdateShapeSettings();
        UpdatePlanetColors();
    }

    public void ApplyGpuSettings()
    {
        if (m_GpuContext == null) return;

        m_GpuContext.SetRadius(planetShape);
        m_GpuContext.SetNoiseLayers(planetShape);
        m_GpuContext.SetVoxelPerAxis(voxelPerAxis);
        m_GpuContext.SetSpacing(spacing);

        mat.SetFloat("_radius", planetShape.planetRadius);
    }

    public void EnsureGpuContext()
    {
        m_GpuContext ??= new PlanetGpuContext(
            svoBuilderShader, marchingShader, planetShape,
            planetShape.planetRadius, voxelPerAxis, spacing,
            maxNodes: 2 << 20
        );
    }

    private void DestroyChunks()
    {
        Chunk[] chunkComponents = GetComponentsInChildren<Chunk>();
        if (Application.isPlaying)
        {
            foreach (Chunk chunk in chunkComponents)
            {
                if (chunk.TryGetComponent(out MeshFilter mf) && mf.sharedMesh != null)
                    Destroy(mf.sharedMesh);
                Destroy(chunk.gameObject);
            }
        } else
        {
            foreach (Chunk chunk in chunkComponents)
            {
                if (chunk.TryGetComponent(out MeshFilter mf) && mf.sharedMesh != null)
                    DestroyImmediate(mf.sharedMesh);
                DestroyImmediate(chunk.gameObject);
            }
        }
    }

    private void InitChunks()
    {
        DestroyChunks();

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
        Vector3 pos = new Vector3(x, y, z) - new Vector3(Mathf.CeilToInt(chunksXAxis / 2),
                                                         Mathf.CeilToInt(chunksYAxis / 2),
                                                         Mathf.CeilToInt(chunksZAxis / 2));
        return voxelPerAxis * spacing * pos;
    }
    private void FindMinMax()
    {
        float min = float.MaxValue;
        float max = float.MinValue;

        foreach(Chunk chunk in m_Chunks)
        {
            if (chunk.Min < min) min = chunk.Min;
            if (chunk.Max > max) max = chunk.Max;
        }

        minMaxVec = new Vector2(min, max);
        mat.SetFloat("_min", min);
        mat.SetFloat("_max", max);
        mat.SetFloat("_radius", m_Radius);
        mat.SetVector("_center", transform.position);
    }

    public void UpdatePlanetColors()
    {
        Texture2D texture = planetColors.GetColorTexture();
        mat.SetTexture("_planetTexture", texture);
    }

    public void UpdateShapeSettings()
    {
        m_Radius = planetShape.planetRadius;
        m_NoiseCount = planetShape.noiseFilters.Length;
    }

    public void RenderPlanetSequential()
    {
        EnsureGpuContext();
        ApplyGpuSettings();
        InitChunks();

        foreach (Chunk chunk in m_Chunks)
        {
            var triangles = m_GpuContext.ComputeTriangles(chunk.transform.localPosition);
            chunk.SetMesh(triangles);
            chunk.SetMinMax(m_Radius);
        }

        FindMinMax();
        UpdatePlanetColors();

        m_GpuContext.Dispose();
        m_GpuContext = null;
    }
}