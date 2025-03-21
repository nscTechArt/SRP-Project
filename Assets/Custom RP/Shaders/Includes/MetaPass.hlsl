#pragma once

// --------------------------------------INCLUDES-------------------------------------
#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"

// -----------------------------------GLOBAL_VARS-------------------------------------
bool4 unity_MetaFragmentControl;
float unity_OneOverOutputBoost;
float unity_MaxOutputValue;

// ---------------------------------------MACROS--------------------------------------
#define BAKE_DIFFUSE_REFLECTIVITY unity_MetaFragmentControl.x
#define BAKE_EMISSION             unity_MetaFragmentControl.y

// --------------------------------------STRUCTS--------------------------------------
struct Attributes
{
    float3 positionOS : POSITION;
    float2 baseUV     : TEXCOORD0;
    float2 lightmapUV : TEXCOORD1;
};
struct Varyings
{
    float4 positionCS_SS : SV_POSITION;
    float2 baseUV        : VAR_BASE_UV;
};

// -------------------------------------FUNCTIONS-------------------------------------
Varyings MetaPassVertex(Attributes input)
{
    Varyings output;
    input.positionOS.xy  = input.lightmapUV * unity_LightmapST.xy + unity_LightmapST.zw;
    input.positionOS.z   = input.positionOS.z > 0.0 ? FLT_MIN : 0.0;
    output.positionCS_SS = TransformWorldToHClip(input.positionOS);
    output.baseUV        = TransformBaseUV(input.baseUV);
    return output;
}    

float4 MetaPassFragment(Varyings input) : SV_Target
{
    InputConfig config = GetInputConfig(input.positionCS_SS, input.baseUV);
    float4 baseColor = GetBaseColor(config);

    Surface surface;
    ZERO_INITIALIZE(Surface, surface);
    surface.color = baseColor.rgb;
    surface.metallic = GetMetallic(config);
    surface.smoothness = GetSmoothness(config);

    BRDF brdf = GetBRDF(surface);

    float4 meta = 0.0;
    if (BAKE_DIFFUSE_REFLECTIVITY)
    {
        meta = float4(brdf.diffuseColor, 1.0);
        // extra post-processing on the diffuse reflectivity
        meta.rgb += brdf.f0 * brdf.roughness * 0.5;
        meta.rgb  = min(PositivePow(meta.rgb, unity_OneOverOutputBoost), unity_MaxOutputValue);
    }
    else if (BAKE_EMISSION)
    {
        meta = float4(GetEmission(config), 1.0);
    }
    return meta;
}