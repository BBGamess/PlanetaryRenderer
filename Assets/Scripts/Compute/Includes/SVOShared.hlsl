#ifndef SVO_SHARED_INCLUDED
#define SVO_SHARED_INCLUDED

#include "Noise.hlsl"

struct SVONode
{
    float3 minCorner; // planet-local
    float size;       // world units
    uint level;
    uint pad0;
    uint pad1;
    uint pad2;
};

static const float3 cornerOffsetsFloat[8] =
{
    float3(0, 0, 0),
    float3(1, 0, 0),
    float3(1, 1, 0),
    float3(0, 1, 0),
    float3(0, 0, 1),
    float3(1, 0, 1),
    float3(1, 1, 1),
    float3(0, 1, 1)
};

struct NoiseLayer
{
    float3 offset;
    int octaves;
    float amplitude;
    float frequency;
    float lacunarity;
    float persistence;
};

StructuredBuffer<NoiseLayer> noiseLayers;
int numLayers;

float Noise(float3 worldPos, float radius)
{
    float dist = radius - length(worldPos);
    
    float3 noisePos = worldPos / radius;
    float value = 0;
    
    for (int n = 0; n < numLayers; n++)
    {
        NoiseLayer nl = noiseLayers[n];
        float3 offset = nl.offset;
        float freq = nl.frequency;
        float amp = nl.amplitude;
        for (int i = 0; i < nl.octaves; i++)
        {
            value += snoise(noisePos * freq + offset) * amp;
            freq *= nl.lacunarity;
            amp *= nl.persistence;
        }
    }
    
    return value * 0.1 * radius;
}

float SampleDensity(float3 worldPos, float radius)
{
    float r = length(worldPos);
    float noise = Noise(worldPos, radius);
    
    noise = max(noise, 0.0);
    
    return r - (radius + noise);
}

float SampleSphereDensity(float3 worldPos, float radius)
{
    return length(worldPos) - radius;
}

#endif
