#pragma once

// --------------------------------------STRUCTS--------------------------------------
struct Attributes
{
    float3 positionOS : POSITION;
};
struct Varyings
{
    float4 positionCS_SS : SV_POSITION;
};

bool _ShadowPancaking;

// -------------------------------------FUNCTIONS-------------------------------------
Varyings ShadowCasterPassVertex(Attributes input)
{
    Varyings output;
    output.positionCS_SS = TransformObjectToHClip(input.positionOS);

    if (_ShadowPancaking)
    {
        // shadow pancaking, which means clamping vertex position to the near plane
        // ------------------------------------------------------------------------
    #if UNITY_REVERSED_Z
        output.positionCS_SS.z = min(output.positionCS_SS.z, output.positionCS_SS.w * UNITY_NEAR_CLIP_VALUE);
    #else
        output.positionCS_SS.z = max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
    #endif
    }
    
    return output;
}    

void ShadowCasterPassFragment()
{
    // doing nothing
    // -------------
}