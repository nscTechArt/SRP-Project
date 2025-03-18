#pragma once

// ---------------------------------------MACROS--------------------------------------

// --------------------------------------CBUFFERS-------------------------------------
CBUFFER_START(_CustomLight)
    int    _DirectionalLightCount;
    int    _OtherLightCount;
CBUFFER_END

// --------------------------------------STRUCTS--------------------------------------
struct Light
{
    float3 color;
    float3 direction;
    float  attenuation;
    uint   renderingLayerMask;
};
struct DirectionalLightData
{
    float4 color;
    float4 directionAndMask;
    float4 shadowData;
};
struct OtherLightData
{
    float4 color;
    float4 position;
    float4 directionalAndMask;
    float4 spotAngle;
    float4 shadowData;
};
StructuredBuffer<DirectionalLightData> _DirectionalLightData;
StructuredBuffer<OtherLightData>       _OtherLightData;

// -------------------------------------FUNCTIONS-------------------------------------
int GetDirectionalLightCount() { return _DirectionalLightCount; }

int GetOtherLightCount() { return _OtherLightCount; }

DirShadowData GetDirectionalLightShadowData(float4 lightShadowData, FragmentShadowData globalShadowData)
{
    DirShadowData data;

    // tileIndex
    // ---------
    int startTileIndex = lightShadowData.x;
    data.tileIndex = startTileIndex + globalShadowData.cascadeIndex;

    // strength
    // --------
    data.strength = lightShadowData.y;

    // normalBias
    // ----------
    data.normalBias = lightShadowData.z;

    // shadow mask channel
    // -------------------
    data.shadowMaskChannel = lightShadowData.w;
    
    return data;
}

OtherShadowData GetOtherShadowData(float4 lightShadowData)
{
    OtherShadowData data;
    data.strength = lightShadowData.y;
    data.tileIndex = lightShadowData.x;
    data.isPoint = lightShadowData.z;
    data.shadowMaskChannel = lightShadowData.w;
    data.lightPosition = 0.0;
    data.lightDirection = 0.0;
    data.spotDirection = 0.0;
    return data;
}

Light GetDirectionalLight(int lightIndex, Surface surface, FragmentShadowData globalShadowData)
{
    Light light;
    DirectionalLightData data = _DirectionalLightData[lightIndex];

    // color and direction
    // -------------------
    light.color = data.color.rgb;
    light.direction = data.directionAndMask.xyz;

    // attenuation
    // -----------
    DirShadowData dirShadowData = GetDirectionalLightShadowData(data.shadowData, globalShadowData);
    light.attenuation = GetDirLightAttenuation(dirShadowData, globalShadowData, surface);

    // rendering layer mask
    // --------------------
    light.renderingLayerMask = asuint(data.directionAndMask.w);
    
    return light;
}

Light GetOtherLight(int lightIndex, Surface surface, FragmentShadowData globalShadowData)
{
    Light light;
    OtherLightData data = _OtherLightData[lightIndex];

    // color
    // -----
    light.color = data.color.rgb;

    // direction
    // ---------
    float3 lightPosition = data.position.xyz;
    float3 ray = lightPosition - surface.position;
    light.direction = normalize(ray);
    
    // 1. distance attenuation
    // -----------------------
    float distanceSqr = max(dot(ray, ray), 0.00001);
    
    // 2. range attenuation
    // --------------------
    float rangeAttenuation = Square(saturate(1.0 - Square(distanceSqr * data.position.w)));
    
    // 3. spot attenuation
    // -------------------
    float3 spotDirection = data.directionalAndMask.xyz;
    float spotAttenuation = Square(saturate(dot(spotDirection, light.direction) * data.spotAngle.x + data.spotAngle.y));
    
    // 4. shadow attenuation
    // ---------------------
    OtherShadowData otherShadowData = GetOtherShadowData(data.shadowData);
    otherShadowData.lightPosition = lightPosition;
    otherShadowData.lightDirection = light.direction;
    otherShadowData.spotDirection = spotDirection;
    float shadowAttenuation = GetOtherLShadowAttenuation(otherShadowData, globalShadowData, surface);
    light.attenuation = shadowAttenuation * rangeAttenuation * spotAttenuation / distanceSqr;

    // rendering layer mask
    // --------------------
    light.renderingLayerMask = asuint(data.directionalAndMask.w);

    return light;
}