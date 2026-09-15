#ifndef MARCHING_TETRAHEDRA_HLSL
#define MARCHING_TETRAHEDRA_HLSL

static const int4 faceCorners[6] =
{
    int4(0, 1, 2, 3), // -Z
    int4(4, 5, 6, 7), // +Z
    int4(0, 1, 5, 4), // -Y
    int4(3, 2, 6, 7), // +Y
    int4(0, 3, 7, 4), // -X
    int4(1, 2, 6, 5)  // +X
};

static const int4 tets[6] =
{
    int4(0, 5, 1, 6),
    int4(0, 1, 2, 6),
    int4(0, 2, 3, 6),
    int4(0, 3, 7, 6),
    int4(0, 7, 4, 6),
    int4(0, 4, 5, 6)
};

// edges inside a tet, as pairs of its 4 corners (0..3)
static const int2 tetEdges[6] =
{
    int2(0, 1), int2(1, 2), int2(2, 0),
    int2(0, 3), int2(1, 3), int2(2, 3)
};

// For 16 tet cases: up to 2 triangles (6 indices), -1 terminated.
// This is standard marching tetrahedra tri table.
static const int tetTriTable[16][7] =
{
    { -1, -1, -1, -1, -1, -1, -1 }, // 0
    { 0, 3, 2, -1, -1, -1, -1 },    // 1
    { 0, 1, 4, -1, -1, -1, -1 },    // 2
    { 1, 4, 2, 2, 4, 3, -1 },       // 3
    { 1, 2, 5, -1, -1, -1, -1 },    // 4
    { 0, 3, 5, 0, 5, 1, -1 },       // 5
    { 0, 2, 5, 0, 5, 4, -1 },       // 6
    { 5, 4, 3, -1, -1, -1, -1 },    // 7
    { 3, 4, 5, -1, -1, -1, -1 },    // 8
    { 4, 5, 0, 5, 2, 0, -1 },       // 9
    { 1, 5, 0, 5, 3, 0, -1 },       // 10
    { 5, 2, 1, -1, -1, -1, -1 },    // 11
    { 3, 4, 2, 2, 4, 1, -1 },       // 12
    { 4, 1, 0, -1, -1, -1, -1 },    // 13
    { 2, 3, 0, -1, -1, -1, -1 },    // 14
    { -1, -1, -1, -1, -1, -1, -1 }  // 15
};


#endif
