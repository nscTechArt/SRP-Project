#pragma once

// --------------------------------TEXTURES AND SAMPLERS------------------------------
TEXTURE2D(_BaseMap); 
TEXTURE2D(_MaskMap);
TEXTURE2D(_NormalMap);
TEXTURE2D(_EmissionMap);
TEXTURE2D(_DetailMap);
TEXTURE2D(_DetailNormalMap);
SAMPLER(sampler_BaseMap);
SAMPLER(sampler_DetailMap);

// --------------------------------------CBUFFERS-------------------------------------
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _DetailMap_ST;
    float4 _BaseColor;
    float4 _EmissionColor;
    float  _Metallic;
    float  _Occlusion;
    float  _Smoothness;
    float  _Fresnel;
    float  _Reflectance;
    float  _NormalScale;
    float  _DetailAlbedo;
    float  _DetailSmoothness;
    float  _DetailNormalScale;
    float  _Roughness;
    float  _SubSurface;
    float  _Specular;
    float  _SpecularTint;
    float  _Anisotropy;
    float  _Sheen;
    float  _SheenTint;
    float  _ClearCoat;
    float  _ClearCoatGloss;
CBUFFER_END

// ------------------------------------- STRUCTS--------------------------------------
struct InputConfig
{
    Fragment fragment;
    float2   baseUV;
    float2   detailUV;
    bool     useMask;
    bool     useDetail;
};

// -------------------------------------FUNCTIONS-------------------------------------
float2 TransformBaseUV(float2 baseUV)
{
    return baseUV * _BaseMap_ST.xy + _BaseMap_ST.zw;
}

float2 TransformDetailUV(float2 detailUV)
{
    return detailUV * _DetailMap_ST.xy + _DetailMap_ST.zw;
}

InputConfig GetInputConfig(float4 positionSS, float2 baseUV, float2 detailUV = 0.0)
{
    InputConfig config;
    config.fragment = GetFragment(positionSS);
    config.baseUV = baseUV;
    config.detailUV = detailUV;
    config.useMask = false;
    config.useDetail = false;
    return config;
}

float4 GetDetail(InputConfig config)
{
    if (config.useDetail)
    {
        float4 map = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, config.detailUV);
        map *= 2.0;
        map -= 1.0;
        return map;
    }
    return 0.0;
}

float4 GetMask(InputConfig config)
{
    if (config.useMask) return SAMPLE_TEXTURE2D(_MaskMap, sampler_BaseMap, config.baseUV);
    return 1.0;
}

float4 GetBaseColor(InputConfig config)
{
    float4 map = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, config.baseUV);

    if (config.useDetail)
    {
        float detail = GetDetail(config).r * _DetailAlbedo;
        float detailMask = GetMask(config).b;
    
        map.rgb = lerp(sqrt(map.rgb), detail < 0.0 ? 0.0 : 1.0, abs(detail) * detailMask);
        map.rgb *= map.rgb;
    }
    
    return map * _BaseColor;
}

float GetMetallic(InputConfig config)
{
    float metallic = _Metallic;
    metallic *= GetMask(config).r;
    return metallic;
}

float GetOcclusion(InputConfig config)
{
    float strength = _Occlusion;
    float occlusion = GetMask(config).g;
    occlusion = lerp(occlusion, 1.0, strength);
    return occlusion;
}

float GetSmoothness(InputConfig config)
{
    float smoothness = _Smoothness;
    smoothness *= GetMask(config).a;

    if (config.useDetail)
    {
        float detail = GetDetail(config).b * _DetailSmoothness;
        float detailMask = GetMask(config).b;
        smoothness = lerp(smoothness, detail < 0.0 ? 0.0 : 1.0, abs(detail) * detailMask);
    }
    
    return smoothness;
}

float GetFresnel()
{
    return _Fresnel;
}

float GetReflectance()
{
    return _Reflectance;
}

float3 GetEmission(InputConfig config)
{
    float4 emissionMap = SAMPLE_TEXTURE2D(_EmissionMap, sampler_BaseMap, config.baseUV);
    return emissionMap.rgb * _EmissionColor.rgb;
}

float3 GetNormalTS(InputConfig config)
{
    float4 map = SAMPLE_TEXTURE2D(_NormalMap, sampler_BaseMap, config.baseUV);
    float scale = _NormalScale;
    float3 normal = DecodeNormal(map, scale);

    if (config.useDetail)
    {
        map = SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_DetailMap, config.detailUV);
        scale = _DetailNormalScale * GetMask(config).b;
        float3 detail = DecodeNormal(map, scale);
        normal = BlendNormalRNM(normal, detail);
    }
    
    return normal;
}

