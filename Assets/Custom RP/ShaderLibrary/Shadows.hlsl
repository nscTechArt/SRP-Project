#pragma once

// --------------------------------------INCLUDES-------------------------------------
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"

// ---------------------------------------MACROS--------------------------------------
#if defined(_SHADOW_FILTER_HIGH)
    #define DIRECTIONAL_FILTER_SAMPLES 16
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
    #define OTHER_FILTER_SAMPLES 16
    #define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#elif defined(_SHADOW_FILTER_MEDIUM)
    #define DIRECTIONAL_FILTER_SAMPLES 9
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
    #define OTHER_FILTER_SAMPLES 9
    #define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#else
    #define DIRECTIONAL_FILTER_SAMPLES 4
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
    #define OTHER_FILTER_SAMPLES 4
    #define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#endif

#define SAMPLER_SHADOW sampler_linear_clamp_compare
#define MaxShadowDistance _ShadowDistanceFadeData.x
#define DistanceFadeRatio _ShadowDistanceFadeData.y
#define CascadeFadeRatio  _ShadowDistanceFadeData.z
#define TileTexelSize     _DirectionalShadowCascades[fragShadowData.cascadeIndex].data.y
#define NextTileTexelSize _DirectionalShadowCascades[fragShadowData.cascadeIndex + 1].data.y

// --------------------------------TEXTURES AND SAMPLERS------------------------------
TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
TEXTURE2D_SHADOW(_OtherShadowAtlas);
SAMPLER_CMP(SAMPLER_SHADOW);

// --------------------------------------CBUFFERS-------------------------------------
CBUFFER_START(_CustomShadows)
    int      _CascadeCount;
    float4   _ShadowDistanceFadeData;
    float4   _ShadowAtlasSize;
CBUFFER_END

// --------------------------------------STRUCTS--------------------------------------
struct DirShadowData
{
    int   tileIndex;
    float strength;
    float normalBias;
    int   shadowMaskChannel;
};
struct OtherShadowData
{
    float  strength;
    int    tileIndex;
    bool   isPoint;
    int    shadowMaskChannel;
    float3 lightPosition;
    float3 lightDirection;
    float3 spotDirection;
};
struct ShadowMask
{
    bool   alwaysMode;
    bool   distanceMode;
    float4 shadows;
};
struct FragmentShadowData
{
    // realtime shadows
    // ----------------
    int   cascadeIndex;
    float cascadeBlendFactor;

    // baked shadows
    // -------------
    ShadowMask shadowMask;

    // for both
    // --------
    float realtimeShadowStrength;
};
struct DirectionalShadowCascade
{
    float4 cullingSphere;
    float4 data;
};
struct OtherShadowBufferData
{
    float4 tileData;
    float4x4 shadowMatrix;
};
StructuredBuffer<DirectionalShadowCascade> _DirectionalShadowCascades;
StructuredBuffer<float4x4> _DirectionalShadowMatrices;
StructuredBuffer<OtherShadowBufferData> _OtherShadowData;

static const float3 KPointShadowPlanes[6] =
{
    float3(-1.0, 0.0, 0.0),
    float3(1.0, 0.0, 0.0),
    float3(0.0, -1.0, 0.0),
    float3(0.0, 1.0, 0.0),
    float3(0.0, 0.0, -1.0),
    float3(0.0, 0.0, 1.0)
};

// -------------------------------------FUNCTIONS-------------------------------------
FragmentShadowData GetFragmentShadowData(Surface surface)
{
    FragmentShadowData data;
    
    // we will update shadowMask with GI data later
    // --------------------------------------------
    data.shadowMask.alwaysMode = false;
    data.shadowMask.distanceMode = false;
    data.shadowMask.shadows = 1.0;
    
    // realtimeShadowStrength is a fade factor based on distance
    // ---------------------------------------------------------
    data.realtimeShadowStrength = FadeFactor(surface.depth, MaxShadowDistance, DistanceFadeRatio);
    
    // find cascade index based on distances to culling spheres
    // --------------------------------------------------------
    int cascadeIndex;
    data.cascadeBlendFactor = 1.0;
    for (cascadeIndex = 0; cascadeIndex < _CascadeCount; cascadeIndex++)
    {
        DirectionalShadowCascade cascade = _DirectionalShadowCascades[cascadeIndex];
        float3 sphereCenter = cascade.cullingSphere.xyz;
        float  sphereRadiusSqr = cascade.cullingSphere.w;
        float  distanceSqr = DistanceSquared(surface.position, sphereCenter);
        if (distanceSqr < sphereRadiusSqr)
        {
            // shadows should fade out between cascades
            // ----------------------------------------
            float fade = FadeFactor(distanceSqr, cascade.data.x, CascadeFadeRatio);
            if (cascadeIndex != _CascadeCount - 1) data.cascadeBlendFactor = fade;
            else data.realtimeShadowStrength *= fade;

            // cascade index found, time to break loop
            // ---------------------------------------
            break;
        }
    }
    
    // if fragment is not in any cascade, it should not be shadowed
    // ------------------------------------------------------------
    if (cascadeIndex == _CascadeCount && _CascadeCount > 0)
        data.realtimeShadowStrength = 0.0;

    // apply dithered blending between cascades if needed
    // --------------------------------------------------
#ifndef _SOFT_CASCADE_BLEND
    else if (data.cascadeBlendFactor < surface.dither) cascadeIndex += 1;
    
    // no blend between cascades if requested
    // --------------------------------------
    data.cascadeBlendFactor = 1.0;
#endif
    
    // done with cascade index, assign it to data
    // ------------------------------------------
    data.cascadeIndex = cascadeIndex;
    return data;
}

float SampleDirShadow(float3 shadowCoords)
{
    #if defined(DIRECTIONAL_FILTER_SETUP)
    float weights[DIRECTIONAL_FILTER_SAMPLES];
    float2 positions[DIRECTIONAL_FILTER_SAMPLES];
    float4 size = _ShadowAtlasSize.yyxx;
    DIRECTIONAL_FILTER_SETUP(size, shadowCoords.xy, weights, positions);
    float shadow = 0;
    for (int i = 0; i < DIRECTIONAL_FILTER_SAMPLES; i++)
    {
        shadow += weights[i] * SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas, SAMPLER_SHADOW, float3(positions[i].xy, shadowCoords.z));
    }
    return shadow;
    #else
    return SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas, SAMPLER_SHADOW, shadowCoords);
    #endif
}

float SampleOtherShadow(float3 shadowCoords, float3 bounds)
{
    shadowCoords.xy = clamp(shadowCoords.xy, bounds.xy, bounds.xy + bounds.z);
    
    #if defined(OTHER_FILTER_SETUP)
    float weights[OTHER_FILTER_SAMPLES];
    float2 positions[OTHER_FILTER_SAMPLES];
    float4 size = _ShadowAtlasSize.wwzz;
    OTHER_FILTER_SETUP(size, shadowCoords.xy, weights, positions);
    float shadow = 0;
    for (int i = 0; i < OTHER_FILTER_SAMPLES; i++)
    {
        shadow += weights[i] * SAMPLE_TEXTURE2D_SHADOW(_OtherShadowAtlas, SAMPLER_SHADOW, float3(positions[i].xy, shadowCoords.z));
    }
    return shadow;
    #else
    return SAMPLE_TEXTURE2D_SHADOW(_OtherShadowAtlas, SAMPLER_SHADOW, shadowCoords);
    #endif
}

float GetRealtimeShadows(DirShadowData dirShadowData, FragmentShadowData fragShadowData, Surface surface)
{
    // calculate shadow coordinates with normal bias applied
    // -----------------------------------------------------
    float3 normalBias = TileTexelSize * dirShadowData.normalBias * surface.interpolatedNormal;
    float4 position = float4(surface.position + normalBias, 1.0);
    float3 shadowCoords = mul(_DirectionalShadowMatrices[dirShadowData.tileIndex], position).xyz;

    // sample shadow map, with PCF if needed
    // -------------------------------------
    float shadow = SampleDirShadow(shadowCoords);

    // apply cascade blend factor
    // --------------------------
    if (fragShadowData.cascadeBlendFactor < 1.0)
    {
        float3 nextNormalBias = NextTileTexelSize * dirShadowData.normalBias * surface.interpolatedNormal;
        float4 nextPosition = float4(surface.position + nextNormalBias, 1.0);
        float3 nextShadowCoords = mul(_DirectionalShadowMatrices[dirShadowData.tileIndex + 1], nextPosition).xyz;
        shadow = lerp(SampleDirShadow(nextShadowCoords), shadow, fragShadowData.cascadeBlendFactor);
    }

    return shadow;
}

float GetBakedShadows(ShadowMask mask, int maskChannel)
{
    float shadows = 1.0;

    // use shadow mask if enabled
    // --------------------------
    if (mask.alwaysMode || mask.distanceMode)
    {
        // select appropriate channel
        if (maskChannel >= 0) shadows = mask.shadows[maskChannel];
    }

    return shadows;
}

float GetBakedShadowsOnly(ShadowMask mask, int maskChannel, float strength)
{
    // we get shadows from shadow mask if enabled
    // ------------------------------------------
    if (mask.alwaysMode || mask.distanceMode)
    {
        return lerp(1.0, GetBakedShadows(mask, maskChannel), strength);
    }

    // otherwise, we return zero shadow attenuation
    // --------------------------------------------
    return 1.0;
}

float MixBakedAndRealtimeShadows(FragmentShadowData fragShadowData, float shadows, int shadowMaskChannel, float dirLightShadowStrength)
{
    float bakedShadows = GetBakedShadows(fragShadowData.shadowMask, shadowMaskChannel);

    // if ShadowMask Mode is enabled, dirLightShadowStrength only affects realtime shadows
    // -----------------------------------------------------------------------------------
    if (fragShadowData.shadowMask.alwaysMode)
    {
        shadows = lerp(1.0, shadows, fragShadowData.realtimeShadowStrength);
        shadows = min(bakedShadows, shadows);
    }

    // if DistanceShadowMask mode is enabled, lerp between baked and realtime shadows
    // ------------------------------------------------------------------------------
    if (fragShadowData.shadowMask.distanceMode)
    {
        shadows = lerp(bakedShadows, shadows, fragShadowData.realtimeShadowStrength);
    }

    // apply shadow strength from Light Component in Unity Editor
    // ----------------------------------------------------------
    return lerp(1.0, shadows, dirLightShadowStrength);
}

float GetDirLightAttenuation(DirShadowData dirShadowData, FragmentShadowData fragShadowData, Surface surface)
{
    float finalShadows;
    
    // if there is no realtime shadow, we can still render with shadow from shadow mask
    // --------------------------------------------------------------------------------
    if (dirShadowData.strength * fragShadowData.realtimeShadowStrength <= 0.0)
    {
        // in this case, shadow strength of dir light is passed from CPU as negative value
        float dirLightShadowStrength = abs(dirShadowData.strength);
        finalShadows = GetBakedShadowsOnly(fragShadowData.shadowMask, dirShadowData.shadowMaskChannel, dirLightShadowStrength);
    }
    
    // otherwise we sample realtime shadows, and mix with baked shadows, if there is any
    // ---------------------------------------------------------------------------------
    else
    {
        float realtimeShadows = GetRealtimeShadows(dirShadowData, fragShadowData, surface);
        finalShadows = MixBakedAndRealtimeShadows(fragShadowData, realtimeShadows, dirShadowData.shadowMaskChannel, dirShadowData.strength);
    }
    
    return finalShadows;
}

float GetOtherShadow(OtherShadowData other, FragmentShadowData global, Surface surface)
{
    float tileIndex = other.tileIndex;
    float3 lightPlane = other.spotDirection;
    if (other.isPoint)
    {
        float faceOffset = CubeMapFaceID(-other.lightDirection);
        tileIndex += faceOffset;
        lightPlane = KPointShadowPlanes[faceOffset];
    }

    OtherShadowBufferData data = _OtherShadowData[tileIndex];
    
    float3 surfaceToLight = other.lightPosition - surface.position;
    float distanceToLightPlane = dot(surfaceToLight, lightPlane);
    float3 normalBias = surface.interpolatedNormal * (distanceToLightPlane * data.tileData.w);
    float4 positionSTS = mul(data.shadowMatrix, float4(surface.position + normalBias, 1.0));
    return SampleOtherShadow(positionSTS.xyz / positionSTS.w, data.tileData.xyz);
}

float GetOtherLShadowAttenuation(OtherShadowData other, FragmentShadowData global, Surface surface)
{
    float shadow;

    // if there is no realtime shadow, we can still render with shadow from shadow mask
    // --------------------------------------------------------------------------------
    if (other.strength * global.realtimeShadowStrength <= 0.0)
    {
        shadow = GetBakedShadowsOnly(global.shadowMask, other.shadowMaskChannel, abs(other.strength));
    }

    // otherwise we sample realtime shadows, and mix with baked shadows, if there is any
    // ---------------------------------------------------------------------------------
    else
    {
        shadow = GetOtherShadow(other, global, surface);
        shadow = MixBakedAndRealtimeShadows(global, shadow, other.shadowMaskChannel, other.strength);
    }

    return shadow;
}