#pragma once

// --------------------------------------INCLUDES-------------------------------------
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"

// ---------------------------------------MACROS--------------------------------------
#define SHOULD_FLIP_UV _ProjectionParams.x < 0.0

// --------------------------------TEXTURES AND SAMPLERS------------------------------
TEXTURE2D(_SourceTexture);

// --------------------------------------GLOBALS--------------------------------------

// --------------------------------------STRUCTS--------------------------------------
struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 screenUV   : VAR_SCREEN_UV;
};

// -------------------------------------FUNCTIONS-------------------------------------
Varyings DefaultPassVertex(uint vertexID : SV_VertexID)
{
    Varyings output;
    // create a triangle that covers clip space,
    // then rasterize it to cover the entire screen
    // --------------------------------------------
    output.positionCS = float4
    (
        vertexID <= 1 ? -1.0 :  3.0,
        vertexID == 1 ?  3.0 : -1.0,
        0.0, 1.0
    );
    output.screenUV = float2
    (
        vertexID <= 1 ? 0.0 : 2.0,
        vertexID == 1 ? 2.0 : 0.0
    );

    // flip uv.y if necessary
    // ----------------------
    if (SHOULD_FLIP_UV)
    {
        output.screenUV.y = 1.0 - output.screenUV.y;
    }
    
    return output;
}

float4 BlitPassFragment(Varyings input) : SV_Target
{
    return SAMPLE_TEXTURE2D_LOD(_SourceTexture, sampler_linear_clamp, input.screenUV, 0);
}

float CopyDepthPassFragment(Varyings input) : SV_Depth
{
    return SAMPLE_DEPTH_TEXTURE_LOD(_SourceTexture, sampler_point_clamp, input.screenUV, 0);
}