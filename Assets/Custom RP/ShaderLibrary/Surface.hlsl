#pragma once

// --------------------------------------STRUCTS--------------------------------------
struct Surface
{
    float3 position;
    float3 normal;
    float3 tangent;
    float3 bitangent;
    float3 interpolatedNormal;
    float3 viewDir;
    float  depth;
    
    float3 baseColor;
    float  alpha;
    float  metallic;
    float  occlusion;
    float  smoothness;
    float  fresnel;
    float  reflectance;

    float  dither;

    float  roughness;
    float  subSurface;
    float  specular;
    float  specularTint;
    float  anisotropy;
    float  sheen;
    float  sheenTint;
    float  clearCoat;
    float  clearCoatGloss;

    uint   renderingLayerMask;
};
