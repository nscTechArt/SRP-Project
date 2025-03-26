#pragma once

// ---------------------------------------MACROS--------------------------------------
#define UV_FUNCTION GetDefaultUV

// --------------------------------------STRUCTS--------------------------------------
struct TriPlanarUV
{
    float2 x;
    float2 y;
    float2 z;
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
    return weights *= rcp(sum);
}

TriPlanarUV GetTriPlanarUV(Varyings varyings)
{
    // get basic uv from positionWS
    // ----------------------------
    TriPlanarUV triUV;
    const float3 pos = varyings.positionWS * _BaseMap_ST.x;
    triUV.x = pos.zy;
    triUV.y = pos.xz;
    triUV.z = pos.xy;

    // avoid mirrored UV
    // -----------------
    if (varyings.normalWS.x <  0) triUV.x.x = -triUV.x.x;
    if (varyings.normalWS.y <  0) triUV.y.x = -triUV.y.x;
    if (varyings.normalWS.z >= 0) triUV.z.x = -triUV.z.x;

    // offset UV to avoid repetition
    // -----------------------------
    triUV.x.y += 0.33;
    triUV.z.x += 0.33;
    
    return triUV;
}

float3 GetNormalTS(float2 uv)
{
    float4 map = SAMPLE_TEXTURE2D(_NormalMap, sampler_BaseMap, uv);
    float scale = _NormalScale;
    float3 normal = DecodeNormal(map, scale);
    return normal;
}

float3 BlendTriPlanarNormal(float3 mappedNormal, float3 surfaceNormal)
{
    float3 n;
    n.xy = mappedNormal.xy + surfaceNormal.xy;
    n.z  = mappedNormal.z * surfaceNormal.z;
    return n;
}

void TriPlanar(inout Surface surface, Varyings varyings, InputConfig config)
{
    // get triplanar uv and weights
    // ----------------------------
    TriPlanarUV triUV = GetTriPlanarUV(varyings);
    const float3 blend = GetTriPlanarWeights(varyings);

    // albedo mapping
    // --------------
    float3 albedoX = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, triUV.x).rgb;
    float3 albedoY = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, triUV.y).rgb;
    float3 albedoZ = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, triUV.z).rgb;
    surface.color += albedoX * blend.x;
    surface.color += albedoY * blend.y;
    surface.color += albedoZ * blend.z;
    surface.color *= _BaseColor;

    // mask mapping
    // ------------
    float4 maskX = SAMPLE_TEXTURE2D(_MaskMap, sampler_BaseMap, triUV.x);
    float4 maskY = SAMPLE_TEXTURE2D(_MaskMap, sampler_BaseMap, triUV.y);
    float4 maskZ = SAMPLE_TEXTURE2D(_MaskMap, sampler_BaseMap, triUV.z);
    float4 mask  = maskX * blend.x + maskY * blend.y + maskZ * blend.z;
    surface.metallic = _Metallic * mask.r;
    surface.occlusion = _Occlusion * mask.g;
    surface.smoothness = _Smoothness * mask.a;

    // normal mapping
    // --------------
#ifdef _NORMAL_MAP
    float3 tangentNormalX = GetNormalTS(triUV.x);
    float3 tangentNormalY = GetNormalTS(triUV.y);
    float3 tangentNormalZ = GetNormalTS(triUV.z);
    if (varyings.normalWS.x <  0) tangentNormalX.x = -tangentNormalX.x;
    if (varyings.normalWS.y <  0) tangentNormalY.x = -tangentNormalY.x;
    if (varyings.normalWS.z >= 0) tangentNormalZ.x = -tangentNormalZ.x;
    
    float3 worldNormalX = BlendTriPlanarNormal(tangentNormalX, varyings.normalWS.zyx).zyx;
    float3 worldNormalY = BlendTriPlanarNormal(tangentNormalY, varyings.normalWS.xzy).xzy;
    float3 worldNormalZ = BlendTriPlanarNormal(tangentNormalZ, varyings.normalWS);
    surface.normal             = normalize(worldNormalX * blend.x + worldNormalY * blend.y + worldNormalZ * blend.z);
    surface.interpolatedNormal = normalize(varyings.normalWS);
#else
    surface.normal             = normalize(varyings.normalWS);
    surface.interpolatedNormal = surface.normal;
#endif
}
