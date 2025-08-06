using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MarchingCubes : MonoBehaviour
{
    [SerializeField, Range(20, 150)] private int resolution = 100;
    [SerializeField] private float radius = 1.0f;

    [Space]
    [SerializeField] private ComputeShader marchingCubesShader;

    private List<Vector3> _vertices = new List<Vector3>();
    private List<int> _triangles = new List<int>();

    private ComputeBuffer triangleBuffer;
    private ComputeBuffer triangleCountBuffer;

    private int _resolution2x;

    private MeshFilter meshFilter;
    private MeshCollider meshCollider;

    struct Triangle
    {
        public Vector3 a; public Vector3 b; public Vector3 c;

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

    public void Generate()
    {
        _resolution2x = resolution * 2;
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        if (meshCollider == null) meshCollider = GetComponent<MeshCollider>();
        MarchCubes();
        SetMesh();

        if (meshCollider != null) meshCollider.sharedMesh = meshFilter.sharedMesh;
    }

    public void GenerateGPU()
    {
        _resolution2x = resolution * 2;
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        if (meshCollider == null) meshCollider = GetComponent<MeshCollider>();
        MarchCubesGPU();

        if (meshCollider != null) meshCollider.sharedMesh = meshFilter.sharedMesh;
    }

    private void SetMesh()
    {
        Mesh mesh = new Mesh()
        {
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
        };

        mesh.vertices  = _vertices.ToArray();
        mesh.triangles = _triangles.ToArray();
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        
        meshFilter.mesh = mesh;
    }

    private void SetMesh(Vector3[] vertices, int[] triangles)
    {
        Mesh mesh = new Mesh()
        {
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
        };

        mesh.vertices  = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        meshFilter.mesh = mesh;
    }

    private int GetConfigIndex(float[] cubeCornerHeights)
    {
        int configIndex = 0;

        for (int i = 0; i < 8; i++)
        {
            if (cubeCornerHeights[i] > 0)
            {
                configIndex |= 1 << i;
            }
        }

        return configIndex;
    }

    private float ScalarField(float x, float y, float z)
    {
        Vector3 vec = new Vector3(x, y, z) + transform.position;
        return radius*radius - vec.sqrMagnitude;
    }

    private void MarchCubes()
    {
        _vertices.Clear();
        _triangles.Clear();

        for (int z = 0; z < _resolution2x; z++)
            for (int y = 0; y < _resolution2x; y++)
                for (int x = 0; x < _resolution2x; x++)
                {
                    float[] cubeCornerHeights = new float[8];

                    for (int i = 0; i < 8; i++)
                    {
                        Vector3Int corner = new Vector3Int(x, y, z) + MarchingTable.Corners[i];
                        cubeCornerHeights[i] = ScalarField(corner.x, corner.y, corner.z);
                    }

                    MarchCube(new Vector3(x, y, z), cubeCornerHeights);
                }
    }

    private void CreateBuffers()
    {
        int numVoxelsPerAxis = _resolution2x - 1;
        int numVoxels = numVoxelsPerAxis * numVoxelsPerAxis * numVoxelsPerAxis;
        int maxTriangleCount = numVoxels * 5;

        if (triangleBuffer != null)
        {
            triangleBuffer.Release();
            triangleCountBuffer.Release();
        }

        triangleBuffer = new ComputeBuffer(maxTriangleCount, sizeof(float) * 3 * 3, ComputeBufferType.Append);
        triangleCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
    }

    private void MarchCubesGPU()
    {
        CreateBuffers();
        triangleBuffer.SetCounterValue(0);

        int threadGroups = Mathf.CeilToInt(_resolution2x / 8.0f);
        int kernelID = marchingCubesShader.FindKernel("March");

        ComputeBuffer offBuffer = new ComputeBuffer(1, sizeof(float) * 3, ComputeBufferType.Raw);
        offBuffer.SetData(new Vector3[] { transform.position });

        marchingCubesShader.SetInt("resolution", resolution);
        marchingCubesShader.SetFloat("radius", radius);
        marchingCubesShader.SetBuffer(kernelID, "off", offBuffer);
        marchingCubesShader.SetBuffer(kernelID, "triangleBuffer", triangleBuffer);

        marchingCubesShader.Dispatch(kernelID, threadGroups, threadGroups, threadGroups);

        ComputeBuffer.CopyCount(triangleBuffer, triangleCountBuffer, 0);
        int[] triCountArr = { 0 };
        triangleCountBuffer.GetData(triCountArr);
        int numTriangles = triCountArr[0];

        Triangle[] triangles = new Triangle[numTriangles];
        triangleBuffer.GetData(triangles, 0, 0, numTriangles);

        Vector3[] vertices = new Vector3[numTriangles * 3];
        int[] meshTriangles = new int[numTriangles * 3];

        for (int i = 0; i < numTriangles; i++)
            for (int j = 0; j < 3; j++)
            {
                meshTriangles[i * 3 + j] = i * 3 + j;
                vertices[i * 3 + j] = triangles[i][j];
            }

        SetMesh(vertices, meshTriangles);

        triangleBuffer.Release();
        triangleCountBuffer.Release();
        offBuffer.Release();
    }

    private void MarchCube(Vector3 position, float[] cubeCornerHeights)
    {
        int configIndex = GetConfigIndex(cubeCornerHeights);

        if (configIndex == 0 || configIndex == 255) return;

        int edgeIndex = 0;
        for (int tri = 0; tri < 5; tri++)
        {
            for (int vert = 0; vert < 3; vert++)
            {
                int edgeConfig = MarchingTable.Triangles[configIndex, edgeIndex];

                if (edgeConfig == -1) break;

                Vector3 start = position + MarchingTable.Edges[edgeConfig].start;
                Vector3 end = position + MarchingTable.Edges[edgeConfig].end;

                float startHeight = ScalarField(start.x, start.y, start.z);
                float endHeight = ScalarField(end.x, end.y, end.z);

                float t = - startHeight / (endHeight - startHeight);
                Vector3 vertex = Vector3.Lerp(start, end, t);

                _vertices.Add(vertex);

                edgeIndex++;
            }

            int count = _vertices.Count;
            _triangles.Add(count - 1);
            _triangles.Add(count - 2);
            _triangles.Add(count - 3);
        }
    }
}