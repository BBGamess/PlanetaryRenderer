using System.Runtime.InteropServices;
using UnityEngine;

public class SVOTestRenderer : MonoBehaviour
{
    [Header("Compute")]
    public ComputeShader svoBuilder;
    public ComputeShader svoMarchingCubes;

    [Header("Rendering")]
    public Material material;

    [Header("Planet")]
    public float radius = 50f;

    [Header("Chunk")]
    public Vector3 chunkMinCorner = new Vector3(-32, -32, -32);
    public int voxelsPerAxis = 64; // fixed
    public float spacing = 1f;     // fixed

    public int THREADS = 64;
    public int MAX_DEPTH = 13; // log2(64)

    [StructLayout(LayoutKind.Sequential)]
    public struct SVONode
    {
        public Vector3 minCorner;  // 12
        public float size;         // 4  -> 16
        public uint level;         // 4
        public uint pad0;          // 4
        public uint pad1;          // 4
        public uint pad2;          // 4  -> 32

        public static int Stride => 32;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Triangle
    {
        public Vector3 a;
        public Vector3 b;
        public Vector3 c;
        public static int Stride => 36;
    }

    ComputeBuffer levelA;
    ComputeBuffer levelB;
    ComputeBuffer leafBuffer;

    ComputeBuffer triBuffer;
    ComputeBuffer countBuffer;

    Mesh mesh;
    MeshFilter mf;
    MeshRenderer mr;

    void Awake()
    {
        mf = gameObject.AddComponent<MeshFilter>();
        mr = gameObject.AddComponent<MeshRenderer>();
        mr.sharedMaterial = material;

        mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mf.sharedMesh = mesh;
    }

    void OnDestroy()
    {
        ReleaseBuffers();
    }

    void ReleaseBuffers()
    {
        levelA?.Release();
        levelB?.Release();
        leafBuffer?.Release();
        triBuffer?.Release();
        countBuffer?.Release();

        levelA = null;
        levelB = null;
        leafBuffer = null;
        triBuffer = null;
        countBuffer = null;
    }

    static int GetAppendCount(ComputeBuffer appendBuffer)
    {
        using var tmp = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
        ComputeBuffer.CopyCount(appendBuffer, tmp, 0);
        int[] c = { 0 };
        tmp.GetData(c);
        return c[0];
    }

    void AllocateBuffers()
    {
        // Worst case node count is huge if your culling is wrong.
        // Start with something safe.
        int maxNodes = 2_500_000;

        levelA = new ComputeBuffer(maxNodes, SVONode.Stride, ComputeBufferType.Append);
        levelB = new ComputeBuffer(maxNodes, SVONode.Stride, ComputeBufferType.Append);
        leafBuffer = new ComputeBuffer(maxNodes, SVONode.Stride, ComputeBufferType.Append);

        // worst-case triangles: leaves * 5
        triBuffer = new ComputeBuffer(maxNodes * 5, Triangle.Stride, ComputeBufferType.Append);

        countBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
    }

    [ContextMenu("Build")]
    public void Build()
    {
        ReleaseBuffers();
        AllocateBuffers();

        BuildSVO();
        MarchSVO();
    }

    void BuildSVO()
    {
        // Reset counters
        levelA.SetCounterValue(0);
        levelB.SetCounterValue(0);
        leafBuffer.SetCounterValue(0);

        // Root node
        float rootSize = voxelsPerAxis * spacing; // 64 * 1.0 = 64

        SVONode root = new SVONode
        {
            minCorner = chunkMinCorner,
            size = rootSize,
            level = 0,
            pad0 = 0,
            pad1 = 0,
            pad2 = 0
        };

        levelA.SetData(new SVONode[] { root });

        // IMPORTANT:
        // SetData does NOT increment the append counter.
        // You MUST set it manually.
        levelA.SetCounterValue(1);

        int kernel = svoBuilder.FindKernel("SVOBuilder");
        svoBuilder.SetFloat("radius", radius);
        svoBuilder.SetInt("maxDepth", MAX_DEPTH);

        ComputeBuffer current = levelA;
        ComputeBuffer next = levelB;

        for (int pass = 0; pass <= MAX_DEPTH; pass++)
        {
            next.SetCounterValue(0);

            svoBuilder.SetBuffer(kernel, "currentLevel", current);
            svoBuilder.SetBuffer(kernel, "nextLevel", next);
            svoBuilder.SetBuffer(kernel, "leafNodes", leafBuffer);

            int nodeCount = GetAppendCount(current);
            Debug.Log($"SVO Pass {pass}: nodes={nodeCount}");

            if (nodeCount == 0)
                break;

            int groups = Mathf.CeilToInt(nodeCount / (float)THREADS);
            svoBuilder.Dispatch(kernel, groups, 1, 1);

            // swap
            (next, current) = (current, next);
        }

        int leafCount = GetAppendCount(leafBuffer);
        Debug.Log($"Leaves = {leafCount}");
    }

    void MarchSVO()
    {
        triBuffer.SetCounterValue(0);

        int leafCount = GetAppendCount(leafBuffer);
        if (leafCount == 0)
        {
            Debug.LogWarning($"{transform.name}: No Leaves");
            mesh.Clear();
            return;
        }

        int kernel = svoMarchingCubes.FindKernel("SVOMarchingCubes");
        svoMarchingCubes.SetFloat("radius", radius);
        svoMarchingCubes.SetBuffer(kernel, "leafNodes", leafBuffer);
        svoMarchingCubes.SetBuffer(kernel, "triangleBuffer", triBuffer);

        int groups = Mathf.CeilToInt(leafCount / (float)THREADS);
        svoMarchingCubes.Dispatch(kernel, groups, 1, 1);

        // Read triangles back
        ComputeBuffer.CopyCount(triBuffer, countBuffer, 0);
        int[] triCountArr = { 0 };
        countBuffer.GetData(triCountArr);
        int triCount = triCountArr[0];

        Debug.Log($"Triangles = {triCount}");

        if (triCount == 0)
        {
            mesh.Clear();
            return;
        }

        Triangle[] tris = new Triangle[triCount];
        triBuffer.GetData(tris);

        BuildMeshFromTriangles(tris);
    }

    void BuildMeshFromTriangles(Triangle[] tris)
    {
        int triCount = tris.Length;

        Vector3[] vertices = new Vector3[triCount * 3];
        int[] indices = new int[triCount * 3];

        for (int i = 0; i < triCount; i++)
        {
            int v = i * 3;
            vertices[v + 0] = tris[i].a;
            vertices[v + 1] = tris[i].b;
            vertices[v + 2] = tris[i].c;

            indices[v + 0] = v + 0;
            indices[v + 1] = v + 1;
            indices[v + 2] = v + 2;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = indices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
}
