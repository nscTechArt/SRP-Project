#pragma once

// ---------------------------------------MACROS--------------------------------------
#if defined(FXAA_QUALITY_LOW)
    #define EXTRA_EDGE_STEPS 3
    #define EDGE_STEP_SIZES 1.5, 2.0, 2.0
    #define LAST_EDGE_STEP_GUESS 8.0
#elif defined(FXAA_QUALITY_MEDIUM)
    #define EXTRA_EDGE_STEPS 8
    #define EDGE_STEP_SIZES 1.5, 2.0, 2.0, 2.0, 2.0, 2.0, 2.0, 4.0
    #define LAST_EDGE_STEP_GUESS 8.0
#else
    #define EXTRA_EDGE_STEPS 10
    #define EDGE_STEP_SIZES 1.0, 1.0, 1.0, 1.0, 1.5, 2.0, 2.0, 2.0, 2.0, 4.0
    #define LAST_EDGE_STEP_GUESS 8.0
#endif

// --------------------------------TEXTURES AND SAMPLERS------------------------------

// --------------------------------------GLOBALS--------------------------------------
float4 _FXAAConfig;
static const float edgeStepSizes[EXTRA_EDGE_STEPS] = { EDGE_STEP_SIZES };

// --------------------------------------STRUCTS--------------------------------------
struct LumaNeighborhood
{
    float m, n, e, s, w, ne, se, sw, nw;
    float highest, lowest, range;
};

struct FXAAEdge
{
    bool isHorizontal;
    float pixelStep;
    float lumaGradient, otherLuma;
};

// -------------------------------------FUNCTIONS-------------------------------------
float GetLuma(float2 uv, float uOffset = 0.0, float vOffset = 0.0)
{
    // calculate sampling uv
    // ---------------------
    uv += float2(uOffset, vOffset) * GetSourceTexelSize().xy;
    
#ifdef FXAA_ALPHA_CONTAINS_LUMA
    return GetSource(uv).a;
#else
    return GetSource(uv).g;
#endif
}

LumaNeighborhood GetLumaNeighborhood(float2 uv)
{
    LumaNeighborhood luma;
    luma.m  = GetLuma(uv);
    luma.n  = GetLuma(uv,  0.0,  1.0);
    luma.e  = GetLuma(uv,  1.0,  0.0);
    luma.s  = GetLuma(uv,  0.0, -1.0);
    luma.w  = GetLuma(uv, -1.0,  0.0);
    luma.ne = GetLuma(uv,  1.0,  1.0);
    luma.se = GetLuma(uv,  1.0, -1.0);
    luma.sw = GetLuma(uv, -1.0, -1.0);
    luma.nw = GetLuma(uv, -1.0,  1.0);
    luma.highest = max(max(max(max(luma.m, luma.n), luma.e), luma.s), luma.w);
    luma.lowest = min(min(min(min(luma.m, luma.n), luma.e), luma.s), luma.w);
    luma.range = luma.highest - luma.lowest;
    return luma;
}

bool CanSkipFXAA(LumaNeighborhood luma)
{
    float fixedThreshold = _FXAAConfig.x;
    float relativeThreshold = _FXAAConfig.y * luma.highest;
    return luma.range < max(fixedThreshold, relativeThreshold);
}

float GetSubpixelBlendFactor(LumaNeighborhood luma)
{
    float filter = 2.0 * (luma.n + luma.e + luma.s + luma.w);
    filter += luma.ne + luma.se + luma.sw + luma.nw;
    filter *= 1.0 / 12.0;
    filter = abs(filter - luma.m);
    filter = saturate(filter / luma.range);
    filter = smoothstep(0, 1, filter);
    return filter * filter * _FXAAConfig.z;
}

bool IsHorizontalEdge(LumaNeighborhood luma)
{
    float horizontal = 2.0 * abs(luma.n + luma.s - 2.0 * luma.m) + abs(luma.ne + luma.se - 2.0 * luma.e) + abs(luma.nw + luma.sw - 2.0 * luma.w);
    float vertical = 2.0 * abs(luma.e + luma.w - 2.0 * luma.m) + abs(luma.ne + luma.nw - 2.0 * luma.n) + abs(luma.se + luma.sw - 2.0 * luma.s);
    return horizontal >= vertical;
}

FXAAEdge GetFXAAEdge(LumaNeighborhood luma)
{
    FXAAEdge edge;
    edge.isHorizontal = IsHorizontalEdge(luma);

    float positive, negative;
    if (edge.isHorizontal)
    {
        edge.pixelStep = GetSourceTexelSize().y;
        positive = luma.n;
        negative = luma.s;
    }
    else
    {
        edge.pixelStep = GetSourceTexelSize().x;
        positive = luma.e;
        negative = luma.w;
    }

    float positiveDelta = abs(luma.m - positive);
    float negativeDelta = abs(luma.m - negative);
    if (positiveDelta < negativeDelta)
    {
        edge.pixelStep = -edge.pixelStep;
        edge.lumaGradient = negativeDelta;
        edge.otherLuma = negative;
    }
    else
    {
        edge.lumaGradient = positiveDelta;
        edge.otherLuma = positive;
    }
        
    return edge;
}

float GetEdgeBlendFactor(LumaNeighborhood luma, FXAAEdge edge, float2 uv)
{
    float2 edgeUV = uv;
    float2 uvStep = 0.0;
    if (edge.isHorizontal)
    {
        edgeUV.y += edge.pixelStep * 0.5;
        uvStep.x = GetSourceTexelSize().x;
    }
    else
    {
        edgeUV.x += edge.pixelStep * 0.5;
        uvStep.y = GetSourceTexelSize().y;
    }

    float edgeLuma = 0.5 * (luma.m + edge.otherLuma);
    float gradientThreshold = 0.25 * edge.lumaGradient;

    float2 uvP = edgeUV + uvStep;
    float lumaDeltaP = GetLuma(uvP) - edgeLuma;
    bool atEndP = abs(lumaDeltaP) >= gradientThreshold;

    int i;
    UNITY_UNROLL
    for (i = 0; i < EXTRA_EDGE_STEPS && !atEndP; i++) {
        uvP += uvStep * edgeStepSizes[i];
        lumaDeltaP = GetLuma(uvP) - edgeLuma;
        atEndP = abs(lumaDeltaP) >= gradientThreshold;
    }
    if (!atEndP) {
        uvP += uvStep * LAST_EDGE_STEP_GUESS;
    }

    float2 uvN = edgeUV - uvStep;
    float lumaDeltaN = GetLuma(uvN) - edgeLuma;
    bool atEndN = abs(lumaDeltaN) >= gradientThreshold;

    UNITY_UNROLL
    for (i = 0; i < EXTRA_EDGE_STEPS && !atEndN; i++) {
        uvN -= uvStep * edgeStepSizes[i];
        lumaDeltaN = GetLuma(uvN) - edgeLuma;
        atEndN = abs(lumaDeltaN) >= gradientThreshold;
    }
    if (!atEndN) {
        uvN -= uvStep * LAST_EDGE_STEP_GUESS;
    }

    float distanceToEndP, distanceToEndN;
    if (edge.isHorizontal) {
        distanceToEndP = uvP.x - uv.x;
        distanceToEndN = uv.x - uvN.x;
    }
    else {
        distanceToEndP = uvP.y - uv.y;
        distanceToEndN = uv.y - uvN.y;
    }

    float distanceToNearestEnd;
    bool deltaSign;
    if (distanceToEndP <= distanceToEndN) {
        distanceToNearestEnd = distanceToEndP;
        deltaSign = lumaDeltaP >= 0;
    }
    else {
        distanceToNearestEnd = distanceToEndN;
        deltaSign = lumaDeltaN >= 0;
    }

    if (deltaSign == (luma.m - edgeLuma >= 0)) {
        return 0.0;
    }
    else {
        return 0.5 - distanceToNearestEnd / (distanceToEndP + distanceToEndN);
    }
}

float4 FXAAPassFragment(Varyings input) : SV_Target
{
    LumaNeighborhood luma = GetLumaNeighborhood(input.screenUV);

    if (CanSkipFXAA(luma)) return GetSource(input.screenUV);

    FXAAEdge edge = GetFXAAEdge(luma);
    float blenderFactor = max( GetSubpixelBlendFactor(luma), GetEdgeBlendFactor(luma, edge, input.screenUV));
    float2 blendUV = input.screenUV;

    if (edge.isHorizontal)
    {
        blendUV.y += edge.pixelStep * blenderFactor;
    }
    else
    {
        blendUV.x += edge.pixelStep * blenderFactor;
    }
    
    return GetSource(blendUV);
}