using System;
using System.Runtime.InteropServices;
using UnityEngine;

public sealed class PlanetGpuContext : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    public struct SVONode
    {
        public Vector3 minCorner;
        public float size;
        public uint level;
        public uint pad0, pad1, pad2;

        public static int Stride => 32;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NoiseLayer
    {
        public Vector3 offset;
        public int octaves;
        public float amplitude;
        public float frequency;
        public float lacunarity;
        public float persistence;

        public static int Stride => 32;

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
    }

    [StructLayout(LayoutKind.Sequential)]
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
        public static int Stride => 36;
    }

    // Shaders
    private readonly ComputeShader m_Builder;
    private readonly ComputeShader m_Marcher;

    // Kernels
    private readonly int m_KernelBuilder;
    private readonly int m_KernelMarcher;

    // Property IDs
    private static readonly int PID_radius         = Shader.PropertyToID("radius");
    private static readonly int PID_maxDepth       = Shader.PropertyToID("maxDepth");
    private static readonly int PID_numLayers      = Shader.PropertyToID("numLayers");
    private static readonly int PID_nodeCount      = Shader.PropertyToID("nodeCount");
    private static readonly int PID_leafCount      = Shader.PropertyToID("leafCount");
    private static readonly int PID_chunkMinCorner = Shader.PropertyToID("chunkMinCorner");
    private static readonly int PID_maxNoiseDisp   = Shader.PropertyToID("maxNoiseDisp");

    private static readonly int PID_currentLevel = Shader.PropertyToID("currentLevel");
    private static readonly int PID_nextLevel    = Shader.PropertyToID("nextLevel");
    private static readonly int PID_leafNodes    = Shader.PropertyToID("leafNodes");

    private static readonly int PID_triangleBuffer = Shader.PropertyToID("triangleBuffer");
    private static readonly int PID_noiseLayers    = Shader.PropertyToID("noiseLayers");

    private readonly int THREADS = 64;

    private ComputeBuffer m_SVOA;
    private ComputeBuffer m_SVOB;
    private ComputeBuffer m_Leaf;
    private ComputeBuffer m_Triangles;

    private ComputeBuffer m_Noise;

    private readonly ComputeBuffer m_CountRaw;

    private int m_NoiseCapacity;
    private int m_TriangleCapacity;
    private int m_MaxDepth;

    private float m_Radius;
    private int m_VoxelPerAxis;
    private int m_Spacing;

    private NoiseLayer[] m_NoiseLayers;

    public ComputeBuffer LeafBuffer => m_Leaf;

    public int BuildChunksSvoOnly(Vector3 chunkMinCorner)
    {
        return BuildChunkSvo(chunkMinCorner);
    }

    public PlanetGpuContext(ComputeShader builder,
                            ComputeShader marcher,
                            PlanetShape ps,
                            float radius,
                            int voxelPerAxis,
                            int spacing,
                            int maxNodes,
                            int initialTriangleCapacity = 1024)
    {
        m_Builder = builder;
        m_Marcher = marcher;

        m_KernelBuilder = m_Builder.FindKernel("SVOBuilder");
        m_KernelMarcher = m_Marcher.FindKernel("SVOMarchingCubes");

        m_Radius = radius;
        m_Spacing = spacing;

        m_VoxelPerAxis = voxelPerAxis;
        m_MaxDepth = Mathf.CeilToInt(Mathf.Log(m_VoxelPerAxis, 2));

        m_SVOA = new ComputeBuffer(maxNodes, SVONode.Stride, ComputeBufferType.Append);
        m_SVOB = new ComputeBuffer(maxNodes, SVONode.Stride, ComputeBufferType.Append);
        m_Leaf = new ComputeBuffer(maxNodes, SVONode.Stride, ComputeBufferType.Append);

        SetNoiseLayers(ps);

        m_CountRaw = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);

        AllocateTriangleBuffer(initialTriangleCapacity);

        m_Builder.SetFloat(PID_radius, m_Radius);
        m_Builder.SetInt(PID_numLayers, m_NoiseLayers.Length);
        m_Builder.SetInt(PID_maxDepth, m_MaxDepth);
        m_Builder.SetBuffer(m_KernelBuilder, PID_noiseLayers, m_Noise);

        m_Marcher.SetFloat(PID_radius, m_Radius);
        m_Marcher.SetInt(PID_numLayers, m_NoiseLayers.Length);
        m_Marcher.SetBuffer(m_KernelMarcher, PID_noiseLayers, m_Noise);
    }

    public void Dispose() => Release();

    public void Release()
    {
        m_SVOA?.Release(); m_SVOA = null;
        m_SVOB?.Release(); m_SVOB = null;
        m_Leaf?.Release(); m_Leaf = null;
        m_Triangles?.Release(); m_Triangles = null;
        m_Noise?.Release(); m_Noise = null;
        m_CountRaw?.Release();
        m_TriangleCapacity = 0;
    }
    private NoiseLayer[] PlanetShapeToNoiseLayers(PlanetShape ps)
    {
        var noiseFilters = ps.noiseFilters;
        NoiseLayer[] noiseLayers = new NoiseLayer[noiseFilters.Length];

        for (int i = 0; i < noiseLayers.Length; i++)
        {
            noiseLayers[i] = new NoiseLayer(noiseFilters[i]);
        }

        return noiseLayers;
    }

    private float ComputeMaxNoiseDisp(NoiseLayer[] layers, float radius)
    {
        float sum = 0.0f;
        foreach (var nl in layers)
        {
            float amp = nl.amplitude;
            for (int i = 0; i < nl.octaves; i++)
            {
                sum += Math.Abs(amp);
                amp *= nl.persistence;
            }
        }

        return sum * 0.1f * radius;
    }

    private int GetAppendCount(ComputeBuffer appendBuffer)
    {
        ComputeBuffer.CopyCount(appendBuffer, m_CountRaw, 0);
        int[] temp = { 0 };
        m_CountRaw.GetData(temp);
        return temp[0];
    }

    private void AllocateTriangleBuffer(int needed)
    {
        if (m_Triangles != null && needed <= m_TriangleCapacity) return;

        m_TriangleCapacity = Mathf.Max(needed, 1);
        m_Triangles?.Release();
        m_Triangles = new ComputeBuffer(m_TriangleCapacity, Triangle.Stride, ComputeBufferType.Append);
    }

    private int GroupsFor(int count) => Mathf.CeilToInt(count / (float) THREADS);

    private int BuildChunkSvo(Vector3 chunkMinCorner)
    {
        m_SVOA.SetCounterValue(0);
        m_SVOB.SetCounterValue(0);
        m_Leaf.SetCounterValue(0);

        SVONode root = new SVONode
        {
            minCorner = chunkMinCorner,
            size = m_VoxelPerAxis * m_Spacing,
            level = 0
        };

        m_SVOA.SetData(new[] { root });
        m_SVOA.SetCounterValue(1);

        ComputeBuffer current = m_SVOA;
        ComputeBuffer next    = m_SVOB;

        m_Builder.SetBuffer(m_KernelBuilder, PID_noiseLayers, m_Noise);
        m_Builder.SetInt(PID_numLayers, m_NoiseLayers.Length);

        for (int pass = 0; pass <= m_MaxDepth; pass++)
        {
            next.SetCounterValue(0);

            m_Builder.SetBuffer(m_KernelBuilder, PID_currentLevel, current);
            m_Builder.SetBuffer(m_KernelBuilder, PID_nextLevel, next);
            m_Builder.SetBuffer(m_KernelBuilder, PID_leafNodes, m_Leaf);

            int nodeCount = GetAppendCount(current);
            if (nodeCount == 0)
                break;

            m_Builder.SetInt(PID_nodeCount, nodeCount);

            int groups = GroupsFor(nodeCount);
            m_Builder.Dispatch(m_KernelBuilder, groups, 1, 1);
            (next, current) = (current, next);
        }

        return GetAppendCount(m_Leaf);
    }

    private Triangle[] MarchChunkSvo(Vector3 chunkMinCorner, int leafCount)
    {
        if (leafCount == 0)
            return Array.Empty<Triangle>();

        int neededCapacity = leafCount * 5;

        AllocateTriangleBuffer(neededCapacity);
        m_Triangles.SetCounterValue(0);

        m_Marcher.SetBuffer(m_KernelMarcher, PID_triangleBuffer, m_Triangles);
        m_Marcher.SetBuffer(m_KernelMarcher, PID_leafNodes, m_Leaf);
        m_Marcher.SetBuffer(m_KernelMarcher, PID_noiseLayers, m_Noise);

        m_Marcher.SetVector(PID_chunkMinCorner, chunkMinCorner);
        m_Marcher.SetInt(PID_leafCount, leafCount);
        m_Marcher.SetInt(PID_numLayers, m_NoiseLayers.Length);

        int groups = GroupsFor(leafCount);
        m_Marcher.Dispatch(m_KernelMarcher, groups, 1, 1);

        int triangleCount = GetAppendCount(m_Triangles);
        if (triangleCount == 0) return Array.Empty<Triangle>();

        Triangle[] triangles = new Triangle[triangleCount];
        m_Triangles.GetData(triangles, 0, 0, triangleCount);
        return triangles;
    }

    public Triangle[] ComputeTriangles(Vector3 chunkMinCorner)
    {
        int leafCount = BuildChunkSvo(chunkMinCorner);
        return MarchChunkSvo(chunkMinCorner, leafCount);
    }

    // Setters
    public void SetNoiseLayers(PlanetShape ps)
    {
        m_NoiseLayers = PlanetShapeToNoiseLayers(ps);
        if (m_Noise == null || m_NoiseLayers.Length > m_NoiseCapacity)
        {
            m_NoiseCapacity = Mathf.Max(m_NoiseLayers.Length, 1);
            m_Noise?.Release();
            m_Noise = new ComputeBuffer(m_NoiseCapacity, NoiseLayer.Stride, ComputeBufferType.Structured);

            m_Builder.SetBuffer(m_KernelBuilder, PID_noiseLayers, m_Noise);
            m_Marcher.SetBuffer(m_KernelMarcher, PID_noiseLayers, m_Noise);
        }

        m_Noise.SetData(m_NoiseLayers);

        m_Builder.SetInt(PID_numLayers, m_NoiseLayers.Length);
        m_Marcher.SetInt(PID_numLayers, m_NoiseLayers.Length);

        float maxNoise = ComputeMaxNoiseDisp(m_NoiseLayers, m_Radius);
        m_Builder.SetFloat(PID_maxNoiseDisp, maxNoise);
    }

    public void SetRadius(PlanetShape ps)
    {
        m_Radius = ps.planetRadius;
        m_Builder.SetFloat(PID_radius, m_Radius);
        m_Marcher.SetFloat(PID_radius, m_Radius);
    }

    public void SetVoxelPerAxis(int v)
    {
        m_VoxelPerAxis = v;
        m_MaxDepth = Mathf.CeilToInt(Mathf.Log(m_VoxelPerAxis, 2));
        m_Builder.SetInt(PID_maxDepth, m_MaxDepth);
    }

    public void SetSpacing(int s)
    {
        m_Spacing = s;
    }
}
