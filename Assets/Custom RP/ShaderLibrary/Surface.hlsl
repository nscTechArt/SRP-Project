#pragma once

// --------------------------------------STRUCTS--------------------------------------
struct Surface
{
    float3 position;
    float3 normal;
    float3 interpolatedNormal;
    float3 viewDir;
    float  depth;
    
    float3 color;
    float  alpha;
    float  metallic;
    float  occlusion;
    float  smoothness;
    float  fresnel;
    float  reflectance;

    float  dither;

    uint   renderingLayerMask;
};
