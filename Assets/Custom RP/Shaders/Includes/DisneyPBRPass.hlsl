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
    float2 baseUV     : TEXCOORD0;

    // GI related
    // ----------
    GI_ATTRIBUTE_DATA
};
struct Varyings
{
    float4 positionCS_SS : SV_POSITION;
    float3 positionWS    : VAR_POSITION_WS;
    float3 normalWS      : VAR_NORMAL_WS;
    float2 baseUV        : VAR_BASE_UV;

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
Varyings DisneyPBRPassVertex(Attributes input)
{
    Varyings output;
    output.positionWS    = TransformObjectToWorld(input.positionOS);
    output.positionCS_SS = TransformWorldToHClip(output.positionWS);
    output.normalWS      = TransformObjectToWorldNormal(input.normalOS);
#ifdef _NORMAL_MAP
    output.tangentWS     = float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w);
#endif
    output.baseUV        = TransformBaseUV(input.baseUV);
#ifdef _DETAIL_MAP
    output.detailUV      = TransformDetailUV(input.baseUV);
#endif

    // GI related
    // ----------
    TRANSFER_GI_DATA(input, output)
    return output;
}    

float4 DisneyPBRPassFragment(Varyings input) : SV_TARGET
{
    InputConfig config = GetInputConfig(input.positionCS_SS, input.baseUV);
#ifdef _MASK_MAP
    config.useMask = true;
#endif
#ifdef _DETAIL_MAP
    config.detailUV = input.detailUV;
    config.useDetail = true;
#endif

    bool disneyPBR = false;
    #ifdef _DISNEY_PBR
    disneyPBR = true;
    #endif


    float4 baseColor = GetBaseColor(config);
    
    Surface surface = (Surface)0;
    surface.position           = input.positionWS;
    surface.viewDir            = normalize(_WorldSpaceCameraPos - input.positionWS);
    surface.depth              = -TransformWorldToView(input.positionWS).z;
    surface.baseColor          = baseColor.rgb;
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
    surface.tangent = normalize(input.tangentWS.xyz);
    float3 sgn = input.tangentWS.w * GetOddNegativeScale();
    float3 bitangent = cross(surface.normal, surface.tangent) * sgn;
    surface.bitangent = normalize(bitangent);
#else
    surface.normal             = normalize(input.normalWS);
    surface.interpolatedNormal = surface.normal;
#endif

    
    // For odd-negative scale transforms we need to flip the sign
    
    surface.roughness = 1.0 - surface.smoothness;
    surface.subSurface = _SubSurface;
    surface.specular   = _Specular;
    surface.specularTint = _SpecularTint;
    surface.sheen     = _Sheen;
    surface.sheenTint = _SheenTint;
    surface.anisotropy = _Anisotropy;
    surface.clearCoat = _ClearCoat;
    surface.clearCoatGloss = _ClearCoatGloss;
    
    BRDF brdf = GetBRDF(surface);

    // retrieve GI data
    // ----------------
    float2 lightMapUV = GI_FRAGMENT_DATA(input);
    GI gi = GetGI(lightMapUV, surface, brdf);

    // apply surface diffuse, gi, and real-time lighting
    // -------------------------------------------------
    float3 color = GetLighting(config.fragment, surface, brdf, gi, disneyPBR);

    // apply emission
    // --------------
    color += GetEmission(config);
    
    return float4(color, surface.alpha);
}