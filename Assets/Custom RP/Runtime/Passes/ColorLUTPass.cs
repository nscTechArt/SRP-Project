using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using static PostFXSettings;
using static PostFXStack;

public class ColorLUTPass
{
    public static TextureHandle Record(RenderGraph renderGraph, PostFXStack stack, int colorLUTResolution)
    {
        // add and build color lut pass
        // ----------------------------
        using var builder = renderGraph.AddRenderPass(_Sampler.name, out ColorLUTPass pass, _Sampler);
        
        // set pass data
        // -------------
        pass.mStack = stack;
        pass.mColorLUTResolution = colorLUTResolution;
        
        // create color lut texture
        // ------------------------
        int lutWidth = colorLUTResolution * colorLUTResolution;
        var desc = new TextureDesc(lutWidth, colorLUTResolution)
        {
            colorFormat = _ColorFormat,
            name = "Color LUT"
        };
        pass.mColorLUTTexture = builder.WriteTexture(renderGraph.CreateTexture(desc));
        
        // set render function
        // -------------------
        builder.SetRenderFunc<ColorLUTPass>(static (pass, context) => pass.Render(context));
        
        // return color lut texture
        // ------------------------
        return pass.mColorLUTTexture;
    }
    
    private void Render(RenderGraphContext context)
    {
        CommandBuffer commandBuffer = context.cmd;
        PostFXSettings settings = mStack.Settings;
        
        // configure color grading settings
        // --------------------------------
        Configure(commandBuffer, settings);
        
        // pass LUT parameters to GPU
        // --------------------------
        int lutHeight = mColorLUTResolution;
        int lutWidth  = lutHeight * lutHeight;
        Vector4 lutParams;
        lutParams.x = lutHeight;
        lutParams.y = 0.5f / lutWidth;
        lutParams.z = 0.5f / lutHeight;
        lutParams.w = lutHeight / (lutHeight - 1.0f);
        commandBuffer.SetGlobalVector(_ColorGradingLUTParameters, lutParams);
        
        // render LUT
        // ----------
        ToneMappingSettings.Mode mode = settings.ToneMapping.mode;
        Pass pass = Pass.ColorGradingNone + (int)mode;
        commandBuffer.SetGlobalFloat(_ColorGradingLUTInLogC, mStack.BufferSettings.m_AllowHDR && pass != Pass.ColorGradingNone ? 1.0f : 0.0f);
        mStack.Draw(commandBuffer, mColorLUTTexture, pass);
        
        // update LUT params before applying color grading
        // -----------------------------------------------
        lutParams.x = 1.0f / lutWidth;
        lutParams.y = 1.0f / lutHeight;
        lutParams.z = lutHeight - 1.0f;
        lutParams.w = 0.0f;
        commandBuffer.SetGlobalVector(_ColorGradingLUTParameters, lutParams);
        
        // pass LUT to GPU
        // ---------------
        commandBuffer.SetGlobalTexture(_ColorGradingLUT, mColorLUTTexture);
    }
    
    private static void Configure(CommandBuffer commandBuffer, PostFXSettings posFXSettings)
    {
        #region Color Adjustments

        // get color adjustments settings
        // ------------------------------
        var adjustmentsSettings = posFXSettings.ColorAdjustments;
        
        // remap and pass to GPU
        // ---------------------
        Vector4 adjustmentsParams;
        // exposure is measured in stops, we need to raise 2 to the power of configured value
        adjustmentsParams.x = Mathf.Pow(2.0f, adjustmentsSettings.postExposure);
        // remap contrast to 0.0f to 2.0f range
        adjustmentsParams.y = adjustmentsSettings.contrast * 0.01f + 1.0f;
        // remap hue shift to -1.0f to 1.0f range
        adjustmentsParams.z = adjustmentsSettings.hueShift / 360.0f;
        // remap saturation to 0.0f to 2.0f range
        adjustmentsParams.w = adjustmentsSettings.saturation * 0.01f + 1.0f;
        commandBuffer.SetGlobalVector(_ColorAdjustments, adjustmentsParams);
        commandBuffer.SetGlobalColor(_ColorFilter, adjustmentsSettings.colorFilter.linear);

        #endregion
        
        #region White Balance
        
        // get white balance settings
        // --------------------------
        var whiteBalanceSettings = posFXSettings.WhiteBalance;
        
        // remap and pass to GPU
        // ---------------------
        Vector3 whiteBalanceParams = ColorUtils.ColorBalanceToLMSCoeffs(whiteBalanceSettings.temperature, whiteBalanceSettings.tint);
        commandBuffer.SetGlobalVector(_WhiteBalance, whiteBalanceParams);
        
        #endregion

        #region Split Toning
        
        // get split toning settings
        // -------------------------
        var splitToningSettings = posFXSettings.SplitToning;
        
        // pass colors in gamma space
        // --------------------------
        Color shadowsWithBalance = splitToningSettings.shadows;
        shadowsWithBalance.a = splitToningSettings.balance;
        commandBuffer.SetGlobalColor(_SplitToningShadows, shadowsWithBalance);
        commandBuffer.SetGlobalColor(_SplitToningHighlights, splitToningSettings.highlights);
        
        #endregion

        #region Channel Mixer

        // get channel mixer settings
        // --------------------------
        var channelMixerSettings = posFXSettings.ChannelMixer;
        
        // pass to GPU
        // -----------
        commandBuffer.SetGlobalVector(_ChannelMixerRed, channelMixerSettings.red);
        commandBuffer.SetGlobalVector(_ChannelMixerGreen, channelMixerSettings.green);
        commandBuffer.SetGlobalVector(_ChannelMixerBlue, channelMixerSettings.blue);

        #endregion

        #region Shadows Midtones Highlights

        // get shadows midtones highlights settings
        // ----------------------------------------
        var shadowsMidtonesHighlightsSettings = posFXSettings.ShadowsMidtonesHighlights;
        
        // remap and pass to GPU
        // ---------------------
        commandBuffer.SetGlobalColor(_SMHShadows, shadowsMidtonesHighlightsSettings.shadows.linear);
        commandBuffer.SetGlobalColor(_SMHMidtones, shadowsMidtonesHighlightsSettings.midtones.linear);
        commandBuffer.SetGlobalColor(_SMHHighlights, shadowsMidtonesHighlightsSettings.highlights.linear);
        commandBuffer.SetGlobalVector(_SMHRange, new Vector4
        (
            shadowsMidtonesHighlightsSettings.shadowsStart, shadowsMidtonesHighlightsSettings.shadowsEnd,
            shadowsMidtonesHighlightsSettings.highlightsStart, shadowsMidtonesHighlightsSettings.highlightsEnd
        ));

        #endregion
    }
    
    private PostFXStack   mStack;
    private int           mColorLUTResolution;
    private TextureHandle mColorLUTTexture;

    private static readonly GraphicsFormat _ColorFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.HDR);
    
    private static readonly int _ColorAdjustments          = Shader.PropertyToID("_ColorAdjustments");
    private static readonly int _ColorFilter               = Shader.PropertyToID("_ColorFilter");
    private static readonly int _WhiteBalance              = Shader.PropertyToID("_WhiteBalance");
    private static readonly int _SplitToningShadows        = Shader.PropertyToID("_SplitToningShadows");
    private static readonly int _SplitToningHighlights     = Shader.PropertyToID("_SplitToningHighlights");
    private static readonly int _ChannelMixerRed           = Shader.PropertyToID("_ChannelMixerRed");
    private static readonly int _ChannelMixerGreen         = Shader.PropertyToID("_ChannelMixerGreen");
    private static readonly int _ChannelMixerBlue          = Shader.PropertyToID("_ChannelMixerBlue");
    private static readonly int _SMHShadows                = Shader.PropertyToID("_SMHShadows");
    private static readonly int _SMHMidtones               = Shader.PropertyToID("_SMHMidtones");
    private static readonly int _SMHHighlights             = Shader.PropertyToID("_SMHHighlights");
    private static readonly int _SMHRange                  = Shader.PropertyToID("_SMHRange");
    private static readonly int _ColorGradingLUT           = Shader.PropertyToID("_ColorGradingLUT");
    private static readonly int _ColorGradingLUTParameters = Shader.PropertyToID("_ColorGradingLUTParameters");
    private static readonly int _ColorGradingLUTInLogC     = Shader.PropertyToID("_ColorGradingLUTInLogC");
    
    private static readonly ProfilingSampler _Sampler = new("Color LUT");

}
