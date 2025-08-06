struct Point
{
    float3 position;
    float value;
};

// Linear interpolation of vertices
float3 VertexLerp(float3 p1, float3 p2, float v1, float v2)
{
    float t = (0.0 - v1) / (v2 - v1 + 1e-6);
    return lerp(p1, p2, t);
}