#pragma once

// --------------------------------------INCLUDES-------------------------------------
#include "../ShaderLibrary/Common.hlsl"
#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"
#include "../ShaderLibrary/GI.hlsl"
#include "../ShaderLibrary/Lighting.hlsl"

// --------------------------------------STRUCTS--------------------------------------
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

// --------------------------------------INCLUDES-------------------------------------
#include "../ShaderLibrary/TriPlanar.hlsl"

// -------------------------------------FUNCTIONS-------------------------------------
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
    
    Surface surface = (Surface)0;
    surface.position           = input.positionWS;
    surface.viewDir            = normalize(_WorldSpaceCameraPos - input.positionWS);
    surface.depth              = -TransformWorldToView(input.positionWS).z;
    surface.alpha              = GetBaseColor(config).a;
    surface.fresnel            = GetFresnel();
    surface.reflectance        = GetReflectance();
    surface.dither             = InterleavedGradientNoise(config.fragment.positionSS, 0);
    surface.renderingLayerMask = asuint(unity_RenderingLayer.x);
    TriPlanar(surface, input, config);

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