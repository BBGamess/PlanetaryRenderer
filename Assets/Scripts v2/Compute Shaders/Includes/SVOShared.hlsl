#ifndef SVO_SHARED_INCLUDED
#define SVO_SHARED_INCLUDED

struct SVONode
{
    float3 minCorner; // planet-local
    float size;       // world units
    uint level;
    uint pad0;
    uint pad1;
    uint pad2;
};

#ifndef CORNER_OFFSETS
#define CORNER_OFFSETS
static const float3 cornerOffsets[8] =
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
#endif

#endif
