#pragma once

// --------------------------------------INCLUDES-------------------------------------

// ---------------------------------------MACROS--------------------------------------

// --------------------------------TEXTURES AND SAMPLERS------------------------------
TEXTURE2D(_CameraColorTexture);
TEXTURE2D(_CameraDepthTexture);

// --------------------------------------GLOBALS--------------------------------------
float4 _CameraAttachmentSize;

// --------------------------------------STRUCTS--------------------------------------
struct Fragment
{
    float2 positionSS;
    float2 screenUV;
    float  depth;
    float  bufferDepth;
};

// -------------------------------------FUNCTIONS-------------------------------------
Fragment GetFragment(float4 positionSS)
{
    Fragment fragment;
    fragment.positionSS = positionSS.xy;
    fragment.screenUV = fragment.positionSS * _CameraAttachmentSize.xy;
    fragment.depth      = IsOrthographicCamera() ? OrthographicDepthBufferToLinear(positionSS.z) : positionSS.w;
    float rawDepth = SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture, sampler_point_clamp, fragment.screenUV, 0);
    fragment.bufferDepth = IsOrthographicCamera() ? OrthographicDepthBufferToLinear(rawDepth) : LinearEyeDepth(rawDepth, _ZBufferParams); 
    return fragment;
}

float4 GetBufferColor(Fragment fragment, float2 uvOffset = float2(0.0, 0.0))
{
    float2 uv = fragment.screenUV + uvOffset;
    return SAMPLE_TEXTURE2D_LOD(_CameraColorTexture, sampler_linear_clamp, uv, 0);
}