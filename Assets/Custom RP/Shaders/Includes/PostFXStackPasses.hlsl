#pragma once

// --------------------------------------INCLUDES-------------------------------------
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"

// ---------------------------------------MACROS--------------------------------------
#define SHOULD_FLIP_UV _ProjectionParams.x < 0.0

// --------------------------------TEXTURES AND SAMPLERS------------------------------
TEXTURE2D(_PostFXSource);
TEXTURE2D(_PostFXSource2);
TEXTURE2D(_ColorGradingLUT);

// --------------------------------------GLOBALS--------------------------------------
float4 _PostFXSource_TexelSize;
float4 _BloomThreshold;
float  _BloomIntensity;
float4 _ColorAdjustments;
float4 _ColorFilter;
float4 _WhiteBalance;
float4 _SplitToningShadows, _SplitToningHighlights;
float4 _ChannelMixerRed, _ChannelMixerGreen, _ChannelMixerBlue;
float4 _SMHShadows, _SMHMidtones, _SMHHighlights, _SMHRange;
float4 _ColorGradingLUTParameters;
bool   _ColorGradingLUTInLogC;
bool   _BlitBicubic;

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

float Luminance(float3 color, bool useACES)
{
    return useACES ? AcesLuminance(color) : Luminance(color);
}

float4 GetSourceTexelSize() { return _PostFXSource_TexelSize; }

float4 GetSource(float2 uv) { return SAMPLE_TEXTURE2D_LOD(_PostFXSource, sampler_linear_clamp, uv, 0); }

float4 GetSourceBicubic(float2 uv)
{
    return SampleTexture2DBicubic(TEXTURE2D_ARGS(_PostFXSource, sampler_linear_clamp), uv, _PostFXSource_TexelSize.zwxy, 1.0, 0.0);
}

float4 GetSource2(float2 uv) { return SAMPLE_TEXTURE2D_LOD(_PostFXSource2, sampler_linear_clamp, uv, 0); }

float3 ApplyBloomThreshold (float3 color)
{
    float brightness = Max3(color.r, color.g, color.b);
    float soft = brightness + _BloomThreshold.y;
    soft = clamp(soft, 0.0, _BloomThreshold.z);
    soft = soft * soft * _BloomThreshold.w;
    float contribution = max(soft, brightness - _BloomThreshold.x);
    contribution /= max(brightness, 0.00001);
    return color * contribution;
}

float3 ColorGradePostExposure(float3 color)
{
    return color * _ColorAdjustments.x;
}

float3 ColorGradeWhiteBalance(float3 color)
{
    color = LinearToLMS(color);
    color *= _WhiteBalance.rgb;
    return LMSToLinear(color);
}

float3 ColorGradeContrast(float3 color, bool useACES)
{
    color = useACES ? ACES_to_ACEScc(unity_to_ACES(color)) : LinearToLogC(color);
    color = (color - ACEScc_MIDGRAY) * _ColorAdjustments.y + ACEScc_MIDGRAY;
    return useACES ? ACES_to_ACEScg(ACEScc_to_ACES(color)) : LogCToLinear(color);
}

float3 ColorGradeColorFilter(float3 color)
{
    return color *= _ColorFilter.rgb;
}

float3 ColorGradeSplitToning(float3 color, bool useACES)
{
    color = PositivePow(color, 1.0 / 2.2);

    float balance = _SplitToningShadows.w;
    float t = saturate(Luminance(saturate(color), useACES) + balance);
    float3 shadows = lerp(0.5, _SplitToningShadows.rgb, 1.0 - t);
    float3 highlights = lerp(0.5, _SplitToningHighlights.rgb, t);

    color = SoftLight(color, shadows);
    color = SoftLight(color, highlights);
    
    return PositivePow(color, 2.2);
}

float3 ColorGradeChannelMixer(float3 color)
{
    return mul(float3x3(_ChannelMixerRed.rgb, _ChannelMixerGreen.rgb, _ChannelMixerBlue.rgb), color);
}

float3 ColorGradingShadowsMidtonesHighlights(float3 color, bool useACES)
{
    float luminance = Luminance(color, useACES);
    float shadowsWeight = 1.0 - smoothstep(_SMHRange.x, _SMHRange.y, luminance);
    float highlightsWeight = smoothstep(_SMHRange.z, _SMHRange.w, luminance);
    float midtonesWeight = 1.0 - shadowsWeight - highlightsWeight;
    return
        color * _SMHShadows.rgb * shadowsWeight +
        color * _SMHMidtones.rgb * midtonesWeight +
        color * _SMHHighlights.rgb * highlightsWeight;
}

float3 ColorGradeHueShift(float3 color)
{
    color = RgbToHsv(color);
    float hue = color.x + _ColorAdjustments.z;
    color.x = RotateHue(hue, 0.0, 1.0);
    return HsvToRgb(color);
}

float3 ColorGradeSaturation(float3 color, bool useACES)
{
    float luminance = Luminance(color, useACES);
    return (color - luminance) * _ColorAdjustments.w + luminance;
}

float3 ColorGrade(float3 color, bool useACES = false)
{
    // post exposure
    color = ColorGradePostExposure(color);
    // white balance
    color = ColorGradeWhiteBalance(color);
    // contrast
    color = ColorGradeContrast(color, useACES);
    // color filter
    color = ColorGradeColorFilter(color);
    // eliminate negative values
    color = max(color, 0.0);
    // split toning
    color = ColorGradeSplitToning(color, useACES);
    // channel mixer
    color = ColorGradeChannelMixer(color);
    // eliminate negative values
    color = max(color, 0.0);
    // shadows, midtones, highlights
    color = ColorGradingShadowsMidtonesHighlights(color, useACES);
    // hue shift
    color = ColorGradeHueShift(color);
    // saturation
    color = ColorGradeSaturation(color, useACES);
    // use ACES if necessary
    color = useACES ? ACEScg_to_ACES(color) : color;
    // eliminate negative values
    color = max(color, 0.0);
    
    return color;
}

float3 GetColorGradedLUT(float2 uv, bool useACES = false)
{
    float3 color = GetLutStripValue(uv, _ColorGradingLUTParameters);
    return ColorGrade(_ColorGradingLUTInLogC ? LogCToLinear(color) : color, useACES);
}

float3 ApplyColorGradingLUT(float3 color)
{
    return ApplyLut2D(TEXTURE2D_ARGS(_ColorGradingLUT, sampler_linear_clamp), saturate(_ColorGradingLUTInLogC ? LinearToLogC(color) : color), _ColorGradingLUTParameters.xyz);
}

float4 BlitPassFragment(Varyings input) : SV_Target
{
    return GetSource(input.screenUV);
}

float4 BloomHorizontalPassFragment(Varyings input) : SV_Target
{
    // gaussian horizontal constants
    // -----------------------------
    const float offsets[] = { -4.0, -3.0, -2.0, -1.0, 0.0, 1.0, 2.0, 3.0, 4.0 };
    const float weights[] = { 0.01621622, 0.05405405, 0.12162162, 0.19459459, 0.22702703, 0.19459459, 0.12162162, 0.05405405, 0.01621622 };

    // do horizontal blur
    // ------------------
    float3 color = 0.0;
    for (int i = 0; i < 9; i++)
    {
        float2 offset = float2(offsets[i] * 2.0 * GetSourceTexelSize().x, 0.0);
        color += GetSource(input.screenUV + offset).rgb * weights[i];
    }
    return float4(color, 1.0);
}

float4 BloomVerticalPassFragment(Varyings input) : SV_Target
{
    // gaussian vertical constants
    // ---------------------------
    const float offsets[] = { -3.23076923, -1.38461538, 0.0, 1.38461538, 3.23076923 };
    const float weights[] = { 0.07027027, 0.31621622, 0.22702703, 0.31621622, 0.07027027 };
    // do vertical blur
    // ----------------
    float3 color = 0.0;
    for (int i = 0; i < 5; i++)
    {
        float2 offset = float2(0.0, offsets[i] * GetSourceTexelSize().y);
        color += GetSource(input.screenUV + offset).rgb * weights[i];
    }
    return float4(color, 1.0);
}

float4 BloomCombineAdditivePassFragment (Varyings input) : SV_Target
{
    float3 lowRes  = GetSourceBicubic(input.screenUV).rgb;
    float4 highRes = GetSource2(input.screenUV);
    return float4(lowRes * _BloomIntensity + highRes.rgb, highRes.a);
}

float4 BloomCombineScatterPassFragment (Varyings input) : SV_Target
{
    float3 lowRes  = GetSourceBicubic(input.screenUV).rgb;
    float3 highRes = GetSource2(input.screenUV).rgb;
    return float4(lerp(highRes, lowRes, _BloomIntensity), 1.0);
}

float4 BloomPrefilterPassFragment (Varyings input) : SV_Target
{
    // fade fireflies
    // --------------
    const float2 offsets[] = { float2(0.0, 0.0), float2(-1.0, -1.0), float2(-1.0, 1.0), float2(1.0, -1.0), float2(1.0, 1.0) };

    float3 color = 0.0;
    float weightSum = 0.0;
    
    for (int i = 0; i < 5; i++)
    {
        float3 c = GetSource(input.screenUV + offsets[i] * GetSourceTexelSize().xy * 2.0).rgb;
        c = ApplyBloomThreshold(c);
        float w = 1.0 / (Luminance(c) + 1.0);
        color += c * w;
        weightSum += w;
    }
    color /= weightSum;
    return float4(color, 1.0);
}

float4 BloomScatterFinalPassFragment (Varyings input) : SV_Target
{
    float3 lowRes  = GetSourceBicubic(input.screenUV).rgb;
    float4 highRes = GetSource2(input.screenUV);

    lowRes += highRes.rgb - ApplyBloomThreshold(highRes.rgb);
    return float4(lerp(highRes.rgb, lowRes, _BloomIntensity), highRes.a);
}

float4 ColorGradingNonePassFragment (Varyings input) : SV_Target
{
    float3 color = GetColorGradedLUT(input.screenUV);
    return float4(color, 1.0);
}

float4 ColorGradingACESPassFragment (Varyings input) : SV_Target
{
    float3 color = GetColorGradedLUT(input.screenUV, true);
    color.rgb = AcesTonemap(color.rgb);
    return float4(color, 1.0);
}

float4 ColorGradingNeutralPassFragment (Varyings input) : SV_Target
{
    float3 color = GetColorGradedLUT(input.screenUV);
    color.rgb = NeutralTonemap(color.rgb);
    return float4(color, 1.0);
}

float4 ColorGradingReinhardPassFragment (Varyings input) : SV_Target
{
    float3 color = GetColorGradedLUT(input.screenUV);
    color.rgb /= color.rgb + 1.0;
    return float4(color, 1.0);
}

float4 ApplyColorGradingPassFragment(Varyings input) : SV_Target
{
    float4 color = GetSource(input.screenUV);
    color.rgb = ApplyColorGradingLUT(color.rgb);
    return color;
}

float4 ApplyColorGradingWithLumaPassFragment(Varyings input) : SV_Target
{
    float4 color = GetSource(input.screenUV);
    color.rgb = ApplyColorGradingLUT(color.rgb);
    color.a = sqrt(Luminance(color.rgb));
    return color;
}

float4 FinalRescalePassFragment(Varyings input) : SV_Target
{
    if (_BlitBicubic)
    {
        // for upscaling
        return GetSourceBicubic(input.screenUV);
    }
    return GetSource(input.screenUV);
}




