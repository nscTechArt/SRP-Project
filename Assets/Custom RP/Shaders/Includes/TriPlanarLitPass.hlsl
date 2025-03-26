#pragma once

// --------------------------------------INCLUDES-------------------------------------
#include "../ShaderLibrary/Common.hlsl"
#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"
#include "../ShaderLibrary/GI.hlsl"
#include "../ShaderLibrary/Lighting.hlsl"

// --------------------------------------MACROS---------------------------------------
#define UV_FUNCTION GetDefaultUV

// --------------------------------------STRUCTS--------------------------------------
struct TriPlanarUV
{
    float2 x;
    float2 y;
    float2 z;
};

struct Attributes
{
    float3 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float4 tangentOS  : TANGENT;

    // GI related
    // ----------
    GI_ATTRIBUTE_DATA
};
struct Varyings
{
    float4 positionCS_SS : SV_POSITION;
    float3 positionWS    : VAR_POSITION_WS;
    float3 normalWS      : VAR_NORMAL_WS;
    
#ifdef _NORMAL_MAP
    float4 tangentWS     : VAR_TANGENT_WS;
#endif
    
#ifdef _DETAIL_MAP
    float2 detailUV      : VAR_DETAIL_UV;
#endif

    // GI related
    // ----------
    GI_VARYINGS_DATA
};

// -------------------------------------FUNCTIONS-------------------------------------
float2 GetDefaultUV()
{
    return float2(0.0, 0.0);
}

float3 GetTriPlanarWeights(Varyings varyings)
{
    float3 weights = abs(varyings.normalWS);
    const float sum = weights.x + weights.y + weights.z;
    weights *= rcp(sum);
    return weights;
}

TriPlanarUV GetTriPlanarUV(Varyings varyings)
{
    TriPlanarUV triUV;
    const float3 pos = varyings.positionWS;
    triUV.x = pos.zy;
    triUV.y = pos.xz;
    triUV.z = pos.xy;

    // avoid mirrored UV
    // -----------------
    if (varyings.normalWS.x <  0) triUV.x.x = -triUV.x.x;
    if (varyings.normalWS.y <  0) triUV.y.x = -triUV.y.x;
    if (varyings.normalWS.z >= 0) triUV.z.x = -triUV.z.x;

    // offset UV
    // ---------
    triUV.x.y += 0.33;
    triUV.z.x += 0.33;
    
    return triUV;
}

void TriPlanar(inout Surface surface, Varyings varyings)
{
    TriPlanarUV triUV = GetTriPlanarUV(varyings);

    float3 albedoX = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, triUV.x).rgb;
    float3 albedoY = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, triUV.y).rgb;
    float3 albedoZ = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, triUV.z).rgb;

    const float3 blend = GetTriPlanarWeights(varyings);
    surface.color = 0;
    surface.color += albedoX * blend.x;
    surface.color += albedoY * blend.y;
    surface.color += albedoZ * blend.z;
}

Varyings TriPlanarLitPassVertex(Attributes input)
{
    Varyings output;
    output.positionWS    = TransformObjectToWorld(input.positionOS);
    output.positionCS_SS = TransformWorldToHClip(output.positionWS);
    output.normalWS      = TransformObjectToWorldNormal(input.normalOS);
    
#ifdef _NORMAL_MAP
    output.tangentWS     = float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w);
#endif
    
#ifdef _DETAIL_MAP
    output.detailUV      = TransformDetailUV(UV_FUNCTION());
#endif

    // GI related
    // ----------
    TRANSFER_GI_DATA(input, output)
    return output;
}    

float4 TriPlanarLitPassFragment(Varyings input) : SV_TARGET
{
    InputConfig config = GetInputConfig(input.positionCS_SS, UV_FUNCTION());
#ifdef _MASK_MAP
    config.useMask = true;
#endif
#ifdef _DETAIL_MAP
    config.detailUV = input.detailUV;
    config.useDetail = true;
#endif
    
    float4 baseColor = GetBaseColor(config);
    
    Surface surface;
    surface.position           = input.positionWS;
    surface.viewDir            = normalize(_WorldSpaceCameraPos - input.positionWS);
    surface.depth              = -TransformWorldToView(input.positionWS).z;
    surface.color              = baseColor.rgb;
    surface.alpha              = baseColor.a;
    surface.metallic           = GetMetallic(config);
    surface.occlusion          = GetOcclusion(config);
    surface.smoothness         = GetSmoothness(config);
    surface.fresnel            = GetFresnel();
    surface.reflectance        = GetReflectance();
    surface.dither             = InterleavedGradientNoise(config.fragment.positionSS, 0);
    surface.renderingLayerMask = asuint(unity_RenderingLayer.x);
#ifdef _NORMAL_MAP
    surface.normal             = NormalTangentToWorld(GetNormalTS(config), input.normalWS, input.tangentWS);
    surface.interpolatedNormal = input.normalWS;
#else
    surface.normal             = normalize(input.normalWS);
    surface.interpolatedNormal = surface.normal;
#endif

    TriPlanar(surface, input);

    BRDF brdf = GetBRDF(surface);

    // retrieve GI data
    // ----------------
    float2 lightMapUV = GI_FRAGMENT_DATA(input);
    GI gi = GetGI(lightMapUV, surface, brdf);

    // apply surface diffuse, gi, and real-time lighting
    // -------------------------------------------------
    float3 color = GetLighting(config.fragment, surface, brdf, gi);

    // apply emission
    // --------------
    color += GetEmission(config);
    
    return float4(color, surface.alpha);
}