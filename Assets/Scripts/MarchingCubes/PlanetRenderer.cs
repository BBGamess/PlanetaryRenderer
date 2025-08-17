using System;
using System.Threading.Tasks;
using UnityEngine;

public class PlanetRenderer : MonoBehaviour
{
    const int COLOR_TEXTURE_RESOLUTION = 200;

    public readonly struct NoiseLayer
    {
        public readonly Vector3 offset;
        public readonly int     octaves;
        public readonly float   amplitude;
        public readonly float   frequency;
        public readonly float   lacunarity;
        public readonly float   persistence;

        public NoiseLayer(Vector3 offset, int octaves, float amplitude, float frequency, float lacunarity, float persistence)
        {
            this.offset      = offset;
            this.octaves     = octaves;
            this.amplitude   = amplitude;
            this.frequency   = frequency;
            this.lacunarity  = lacunarity;
            this.persistence = persistence;
        }

        public NoiseLayer(PlanetShape.NoiseFilter filter)
        {
            offset      = filter.offset;
            octaves     = filter.octaves;
            amplitude   = filter.amplitude;
            frequency   = filter.frequency;
            lacunarity  = filter.lacunarity;
            persistence = filter.persistence;
        }

        public static int Size
        {
            get { return 32; } // float3 + int + floatx5
        }
    }

    // CAUTION: Increase the resolution alongside with chunks per axes irresponsibly, you will quickly find yourself out of GPU memory
    // Your system may CRASH
    [Header("Properties")]
    [SerializeField, Range(20, 50)] private int halfResolution = 40;
    [SerializeField] private int spacing = 1;

    [Header("Chunks")]
    [SerializeField] private int chunksXAxis = 6;
    [SerializeField] private int chunksYAxis = 6;
    [SerializeField] private int chunksZAxis = 6;

    [Header("Compute")]
    [SerializeField, Range(1, 10)] private int chunksPerIteration = 5;
    [SerializeField] private Material mat;
    [SerializeField] private ComputeShader marchingShader;
    [SerializeField] private ComputeShader isosurfaceShader;
    [SerializeField] private ComputeShader noiseShader;

    [HideInInspector] public PlanetColors planetColors;
    [HideInInspector] public PlanetShape  planetShape;

    Chunk[] m_Chunks;

    private int resolution;
    private int numChunks;

    private ComputeBuffer m_TriangleBuffer;
    private ComputeBuffer m_TriangleCountBuffer;
    private ComputeBuffer m_PointsBuffer;
    private ComputeBuffer m_NoiseBuffer;
    private ComputeBuffer m_NoiseLayersBuffer;

    private bool m_BuffersReleased = true;
    private bool m_ChunksInitialized = false;
    [HideInInspector] public bool startRender = false;

    private float m_Radius = 100f;
    private int m_NoiseCount = 1;

    public int chunkInd = 0;

    [HideInInspector] public Vector2 minMaxVec;

    public float Radius
    {
        get { return m_Radius; }
    }

    private void OnValidate()
    {
        if (planetColors == null) planetColors = new PlanetColors(COLOR_TEXTURE_RESOLUTION);
        else planetColors.colorResolution = COLOR_TEXTURE_RESOLUTION;

        if (planetShape == null) planetShape = new PlanetShape();
        
        UpdateShapeSettings();
        UpdatePlanetColors();
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
        return (resolution - 1) * spacing * pos;
    }

    private void CreateBuffers(int maxTriangleCount, int numPoints)
    {
        if (!m_BuffersReleased) ReleaseBuffers();

        m_TriangleBuffer = new ComputeBuffer(maxTriangleCount, sizeof(float) * 3 * 3, ComputeBufferType.Append);
        m_TriangleCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);

        m_PointsBuffer = new ComputeBuffer(numPoints, sizeof(float) * 4, ComputeBufferType.Raw);
        m_NoiseBuffer  = new ComputeBuffer(numPoints, sizeof(float), ComputeBufferType.Raw);

        NoiseLayer[] noiseLayers = GetNoiseLayers();    // Call this before creating Noise Layers ComputeBuffer: It sets the m_NoiseCount field
        m_NoiseLayersBuffer = new ComputeBuffer(m_NoiseCount, NoiseLayer.Size, ComputeBufferType.Raw);
        
        m_NoiseLayersBuffer.SetData(noiseLayers);

        m_BuffersReleased = false;

        Debug.Log("Allocated Compute Buffers");
    }

    private void ResetBuffers()
    {
        m_TriangleBuffer.SetData(new Chunk.Triangle[0]);                        // Append Structured Buffer
        m_TriangleCountBuffer.SetData(new int[] { 0 });                         // Raw
        m_PointsBuffer.SetData(new Vector4[] { Vector4.zero });                 // Raw
        m_NoiseBuffer.SetData(new float[] { 0.0f });                            // Raw

        m_TriangleBuffer.SetCounterValue(0);                                    // Reset append counter
    }

    private void ReleaseBuffers()
    {
        if (m_BuffersReleased) return;

        m_TriangleBuffer?.Release();
        m_TriangleCountBuffer?.Release();
        m_PointsBuffer?.Release();
        m_NoiseBuffer?.Release();
        m_NoiseLayersBuffer?.Release();

        m_TriangleBuffer = null;
        m_TriangleCountBuffer = null;
        m_PointsBuffer = null;
        m_NoiseBuffer = null;
        m_NoiseLayersBuffer = null;

        m_BuffersReleased = true;

        Debug.Log("Freed VRAM Allocation");
    }

    private void UpdateChunks()
    {
        int voxelPerAxis = resolution - 1;
        int numVoxels = voxelPerAxis * voxelPerAxis * voxelPerAxis;
        int maxTriangles = 5 * numVoxels;

        int pointsPerAxis = resolution + 1;
        int numPoints = pointsPerAxis * pointsPerAxis * pointsPerAxis;

        CreateBuffers(maxTriangles, numPoints);

        for (int i = 0; i < numChunks; i++)
        {
            ResetBuffers();
            Chunk chunk = m_Chunks[i];
            chunk.ComputeNoise(noiseShader, m_NoiseBuffer, m_NoiseLayersBuffer, Vector3.zero, m_Radius, resolution, spacing, m_NoiseCount);
            chunk.ComputeElevation(isosurfaceShader, m_PointsBuffer, m_NoiseBuffer, Vector3.zero, m_Radius, resolution, spacing);
            chunk.MarchChunk(marchingShader, m_TriangleBuffer, m_TriangleCountBuffer, m_PointsBuffer, resolution);
        }

        ReleaseBuffers();
    }

    private NoiseLayer[] GetNoiseLayers()
    {
        var noiseFilters = planetShape.noiseFilters;
        NoiseLayer[] noiseLayers = new NoiseLayer[noiseFilters.Length];

        m_NoiseCount = noiseLayers.Length;
        m_Radius = planetShape.planetRadius;

        for (int i = 0; i < noiseLayers.Length; i++)
        {
            noiseLayers[i] = new NoiseLayer(noiseFilters[i]);
        }

        return noiseLayers;
    }

    private void FindMinMax()
    {
        float min = float.MaxValue;
        float max = float.MinValue;

        foreach(Chunk chunk in m_Chunks)
        {
            if (chunk.Min < min) min = chunk.Min;
            else if (chunk.Max > max) max = chunk.Max;
        }

        minMaxVec = new Vector2(min, max);
        Debug.Log(minMaxVec);
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

    public void RenderPlanet()
    {
        InitChunks();
        UpdateChunks();
        FindMinMax();
        UpdatePlanetColors();
    }
    private async Task UpdateChunkAsync(Chunk chunk)
    {
        ResetBuffers();
        chunk.ComputeNoise(noiseShader, m_NoiseBuffer, m_NoiseLayersBuffer, Vector3.zero, m_Radius, resolution, spacing, m_NoiseCount);
        chunk.ComputeElevation(isosurfaceShader, m_PointsBuffer, m_NoiseBuffer, Vector3.zero, m_Radius, resolution, spacing);
        chunk.MarchChunk(marchingShader, m_TriangleBuffer, m_TriangleCountBuffer, m_PointsBuffer, resolution);
        await Task.Yield();
    }

    private async Task UpdateChunksAsync()
    {
        int voxelPerAxis = resolution - 1;
        int numVoxels = voxelPerAxis * voxelPerAxis * voxelPerAxis;
        int maxTriangles = 5 * numVoxels;

        int pointsPerAxis = resolution + 1;
        int numPoints = pointsPerAxis * pointsPerAxis * pointsPerAxis;

        CreateBuffers(maxTriangles, numPoints);

        Task[] chunkTasks = new Task[chunksPerIteration];
        int taskIndex = 0;
        for (int i = 0; i < numChunks; i++)
        {
            taskIndex = i % chunksPerIteration;

            Chunk chunk = m_Chunks[i];
            chunkTasks[taskIndex] = UpdateChunkAsync(chunk);

            if (i != 0 && taskIndex == 0 || i == numChunks - 1)
            {
                await Task.WhenAll(chunkTasks);
            }
        }

        ReleaseBuffers();
    }

    public async void RenderPlanetAsync()
    {
        InitChunks();

        await UpdateChunksAsync();

        FindMinMax();
        UpdatePlanetColors();
    }
}