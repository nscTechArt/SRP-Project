#pragma once

#include "../ShaderLibrary/Common.hlsl"

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _BaseColor;
CBUFFER_END

struct Attributes
{
    float3 positionOS : POSITION;
    float4 color      : COLOR;
    float2 baseUV     : TEXCOORD0;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
#ifdef _VERTEX_COLORS
    float4 color      : VAR_COLOR;
#endif
    float2 baseUV     : TEXCOORD0;
};

Varyings UnlitPassVertex(Attributes input)
{
    Varyings output;
    float3 positionWS = TransformObjectToWorld(input.positionOS);
    output.positionCS = TransformWorldToHClip(positionWS);
#ifdef _VERTEX_COLORS
    output.color = input.color;
#endif
    output.baseUV = input.baseUV * _BaseMap_ST.xy + _BaseMap_ST.zw;
    return output;
}    

float4 UnlitPassFragment(Varyings input) : SV_TARGET
{
    float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV);
    float4 baseColor = baseMap * _BaseColor;
#ifdef _VERTEX_COLORS
    baseColor *= input.color;
#endif
    return baseColor;
}