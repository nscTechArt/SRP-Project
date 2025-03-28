#pragma once

// --------------------------------------INCLUDES-------------------------------------
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"

// ---------------------------------------MACROS--------------------------------------
#ifdef LIGHTMAP_ON
    #define GI_ATTRIBUTE_DATA               float2 lightMapUV : TEXCOORD1;
    #define GI_VARYINGS_DATA                float2 lightMapUV : VAR_LIGHT_MAP_UV;
    #define TRANSFER_GI_DATA(input, output) output.lightMapUV = input.lightMapUV * unity_LightmapST.xy + unity_LightmapST.zw;
    #define GI_FRAGMENT_DATA(input)         input.lightMapUV
#else
    #define GI_ATTRIBUTE_DATA
    #define GI_VARYINGS_DATA
    #define TRANSFER_GI_DATA(input, output)
    #define GI_FRAGMENT_DATA(input)         0.0
#endif
#define LPPV_ENABLED unity_ProbeVolumeParams.x

// --------------------------------TEXTURES AND SAMPLERS------------------------------
TEXTURE2D(unity_Lightmap);            SAMPLER(samplerunity_Lightmap);
TEXTURE2D(unity_ShadowMask);          SAMPLER(samplerunity_ShadowMask);
TEXTURECUBE(unity_SpecCube0);         SAMPLER(samplerunity_SpecCube0);
TEXTURE3D_FLOAT(unity_ProbeVolumeSH); SAMPLER(samplerunity_ProbeVolumeSH);

// --------------------------------------STRUCTS--------------------------------------
struct GI
{
    // indirect lighting
    // -----------------
    float3 diffuse;
    float3 specular;
    ShadowMask shadowMask;
};

// -------------------------------------FUNCTIONS-------------------------------------
float3 SampleLightMap(float2 lightMapUV)
{
#ifdef LIGHTMAP_ON
    // to keep codes clean and readable, we pre-declare part of parameters
    // -------------------------------------------------------------------
    const float4 lightMapST = float4(1.0, 1.0, 0.0, 0.0);
    const bool isLightMapCompressed =
    #ifdef UNITY_LIGHTMAP_FULL_HDR
        false;
    #else
        true;
    #endif
    const float4 decodeInstructions = float4(LIGHTMAP_HDR_MULTIPLIER, LIGHTMAP_HDR_EXPONENT, 0.0, 0.0);
    return SampleSingleLightmap(TEXTURE2D_ARGS(unity_Lightmap, samplerunity_Lightmap), lightMapUV, lightMapST, isLightMapCompressed, decodeInstructions);
#else
    return 0.0;
#endif
}

float3 SampleLightProbe(Surface surface)
{
#ifdef LIGHTMAP_ON
    // only non-static object needs to sample light probe
    // --------------------------------------------------
    return 0.0;
#else
    // for dynamic objects, there are two options
    // ------------------------------------------
    // 1. Light Probe Proxy Volume (LPPV)
    if (LPPV_ENABLED)
    {
        return SampleProbeVolumeSH4
        (
            TEXTURE3D_ARGS(unity_ProbeVolumeSH, samplerunity_ProbeVolumeSH),
            surface.position, surface.normal,
            unity_ProbeVolumeWorldToObject,
            unity_ProbeVolumeParams.y, unity_ProbeVolumeParams.z,
            unity_ProbeVolumeMin.xyz, unity_ProbeVolumeSizeInv.xyz
        );
    }
    // 2. Light Probe
    float4 coefficients[7];
    coefficients[0] = unity_SHAr;
    coefficients[1] = unity_SHAg;
    coefficients[2] = unity_SHAb;
    coefficients[3] = unity_SHBr;
    coefficients[4] = unity_SHBg;
    coefficients[5] = unity_SHBb;
    coefficients[6] = unity_SHC;
    return max(0.0, SampleSH9(coefficients, surface.normal));
#endif
}

float4 SampleBakedShadows(float2 lightMapUV, Surface surface)
{
#ifdef LIGHTMAP_ON
    return SAMPLE_TEXTURE2D(unity_ShadowMask, samplerunity_ShadowMask, lightMapUV);
#else
    if (LPPV_ENABLED)
    {
        return SampleProbeOcclusion
        (
            TEXTURE3D_ARGS(unity_ProbeVolumeSH, samplerunity_ProbeVolumeSH),
            surface.position, unity_ProbeVolumeWorldToObject,
            unity_ProbeVolumeParams.y, unity_ProbeVolumeParams.z,
            unity_ProbeVolumeMin.xyz, unity_ProbeVolumeSizeInv.xyz
        ); 
    }
    return unity_ProbesOcclusion;
#endif
}

float3 SampleEnvironment(Surface surface, BRDF brdf)
{
    float3 uvw = reflect(-surface.viewDir, surface.normal);
    float mip = PerceptualRoughnessToMipmapLevel(brdf.perceptualRoughness);
    float4 environment = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, uvw, mip);

    // make sure that we interpret data from cubemap correctly
    // -------------------------------------------------------
    return DecodeHDREnvironment(environment, unity_SpecCube0_HDR);
}

GI GetGI(float2 lightMapUV, Surface surface, BRDF brdf)
{
    GI gi;

    // diffuse
    // -------
    gi.diffuse = SampleLightMap(lightMapUV) + SampleLightProbe(surface);

    // specular
    // --------
    gi.specular = SampleEnvironment(surface, brdf);
    
    // shadow mask
    // -----------
    gi.shadowMask.alwaysMode = false;
    gi.shadowMask.distanceMode = false;
    gi.shadowMask.shadows = 1.0;
#ifdef _SHADOW_MASK_ALWAYS
    gi.shadowMask.alwaysMode = true;
    gi.shadowMask.shadows = SampleBakedShadows(lightMapUV, surface);
# elif defined(_SHADOW_MASK_DISTANCE)
    gi.shadowMask.distanceMode = true;
    gi.shadowMask.shadows = SampleBakedShadows(lightMapUV, surface);
#endif
    
    return gi;
}