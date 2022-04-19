// could replace with just reading values directly from quad but might need to swap x and y
static const float2 TranslationTable[4] =
{
    float2(0, 0),
        float2(1, 0),
        float2(0, 1),
        float2(1, 1)
};

static const int QuadTable[8] =
{
    //only need quad 0 - 3
    0, 1,
    1, 1,
    0, 1,
    1, 1
};

// could change to float2
static const float3 QuadTriTable[96] =
{
    //always start on point at center of parent quad or furthest if not available
    float3(0, 0, 1), float3(0, 0, 1), float3(0, 0, 1), float3(1, 1, 0), float3(2, 0, 0), float3(0, 0, 0),
    float3(1, 1, 0), float3(0, 0, 0), float3(0, 1, 0), float3(1, 1, 0), float3(2, 0, 0), float3(0, 0, 0),
    float3(0, 0, 1), float3(0, 0, 1), float3(0, 0, 1), float3(1, 1, 0), float3(1, 0, 0), float3(0, 0, 0),
    float3(0, 0, 0), float3(0, 1, 0), float3(1, 0, 0), float3(0, 1, 0), float3(1, 1, 0), float3(1, 0, 0),

    float3(0, 0, 1), float3(0, 0, 1), float3(0, 0, 1), float3(0, 1, 0), float3(1, 2, 0), float3(1, 0, 0),
    float3(0, 1, 0), float3(1, 0, 0), float3(0, 0, 0), float3(0, 1, 0), float3(1, 2, 0), float3(1, 0, 0),
    float3(0, 0, 1), float3(0, 0, 1), float3(0, 0, 1), float3(0, 1, 0), float3(1, 1, 0), float3(1, 0, 0),
    float3(0, 0, 0), float3(0, 1, 0), float3(1, 0, 0), float3(0, 1, 0), float3(1, 1, 0), float3(1, 0, 0),

    float3(0, 0, 1), float3(0, 0, 1), float3(0, 0, 1), float3(1, 0, 0), float3(0, -1, 0), float3(0, 1, 0),
    float3(1, 0, 0), float3(0, 1, 0), float3(1, 1, 0), float3(1, 0, 0), float3(0, -1, 0), float3(0, 1, 0),
    float3(0, 0, 1), float3(0, 0, 1), float3(0, 0, 1), float3(1, 0, 0), float3(0, 0, 0), float3(0, 1, 0),
    float3(0, 0, 0), float3(0, 1, 0), float3(1, 0, 0), float3(0, 1, 0), float3(1, 1, 0), float3(1, 0, 0),

    float3(0, 0, 1), float3(0, 0, 1), float3(0, 0, 1), float3(0, 0, 0), float3(-1, 1, 0), float3(1, 1, 0),
    float3(0, 0, 0), float3(1, 1, 0), float3(1, 0, 0), float3(0, 0, 0), float3(-1, 1, 0), float3(1, 1, 0),
    float3(0, 0, 1), float3(0, 0, 1), float3(0, 0, 1), float3(0, 0, 0), float3(0, 1, 0), float3(1, 1, 0),
    float3(0, 0, 0), float3(0, 1, 0), float3(1, 0, 0), float3(0, 1, 0), float3(1, 1, 0), float3(1, 0, 0),
};

static const float3 EdgeTriTable[6] =
{
    float3(0, 0, 0), float3(1, 0, 0), float3(0, 1, 0), float3(0, 1, 0), float3(1, 0, 0), float3(1, 1, 0),
};

static const float4x4 TransformationTable[4] =
{
                // x + 90
    float4x4(
        1, 0, 0, 0,
        0, 0, -1, 0,
        0, 1, 0, 0,
        0, 0, 0, 1
    ),

    // y - 90
    float4x4(
        0, 0, -1, 0,
        0, 1, 0, 0,
        1, 0, 0, 0,
        0, 0, 0, 1
    ),

    // x - 90, up 1
    float4x4(
        1, 0, 0, 0,
        0, 0, 1, 0,
        0, -1, 0, 1,
        0, 0, 0, 1
    ),

    // y + 90, up 1
    float4x4(
        0, 0, 1, 0,
        0, 1, 0, 0,
        -1, 0, 0, 1,
        0, 0, 0, 1
    ),
};