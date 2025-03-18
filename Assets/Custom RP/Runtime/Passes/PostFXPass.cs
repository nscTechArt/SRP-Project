using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using static PostFXStack;

public class PostFXPass
{
    public static void Record
    (
        RenderGraph renderGraph, PostFXStack stack,
        int colorLUTResolution, bool keepAlpha,
        in CameraRendererTextures cameraTextures
    )
    {
        // declare profiling scope
        // -----------------------
        using var _ = new RenderGraphProfilingScope(renderGraph, _GroupSampler);
        
        // retrieve textures from Bloom Pass and ColorGrading Pass
        // -------------------------------------------------------
        TextureHandle colorSource = BloomPass.Record(renderGraph, stack, cameraTextures);
        TextureHandle colorLUT = ColorLUTPass.Record(renderGraph, stack, colorLUTResolution);
        
        // add and build post fx pass
        // --------------------------
        using RenderGraphBuilder builder = renderGraph.AddRenderPass(_FinalSampler.name, out PostFXPass pass, _FinalSampler);
        
        // set pass data
        // -------------
        pass.mStack = stack;
        pass.mKeepAlpha = keepAlpha;
        pass.mColorSource = builder.ReadTexture(colorSource);
        builder.ReadTexture(colorLUT);

        // determine scale mode
        // --------------------
        if (stack.AttachmentSize.x == stack.Camera.pixelWidth)
        {
            pass.mScaleMode = ScaleMode.None;
        }
        else
        {
            pass.mScaleMode =
                stack.BufferSettings.m_BicubicRescaling ==
                CameraBufferSettings.BicubicRescalingMode.UpAndDown ||
                stack.BufferSettings.m_BicubicRescaling ==
                CameraBufferSettings.BicubicRescalingMode.UpOnly &&
                stack.AttachmentSize.x < stack.Camera.pixelWidth ?
                ScaleMode.Bicubic : ScaleMode.Linear;
        }

        // prepare for final draw
        // ----------------------
        bool applyFXAA = stack.BufferSettings.m_FXAA.enabled;
        if (applyFXAA || pass.mScaleMode != ScaleMode.None)
        {
            // initialize texture desc
            // -----------------------
            var desc = new TextureDesc(stack.AttachmentSize.x, stack.AttachmentSize.y)
            {
                colorFormat = _ColorFormat
            };
            
            // if FXAA is enabled, use a dedicated RT for color grading result
            // ---------------------------------------------------------------
            if (applyFXAA)
            {
                desc.name = "Color Grading Result";
                pass.mColorGradingResult = builder.CreateTransientTexture(desc);
            }
            
            // create a temporary LDR RT with scaled buffer size
            // -------------------------------------------------
            if (pass.mScaleMode != ScaleMode.None)
            {
                desc.name = "Scaled Result";
                pass.mScaledResult = builder.CreateTransientTexture(desc);
            }
        }
        
        // set render function
        // -------------------
        builder.SetRenderFunc<PostFXPass>(static (pass, context) => pass.Render(context));
    }

    private void Render(RenderGraphContext context)
    {
        // retrieve command buffer from RenderGraphContext
        // -----------------------------------------------
        CommandBuffer commandBuffer = context.cmd;
        
        // reset blend mode
        // ----------------
        commandBuffer.SetGlobalFloat(_FinalSrcBlend, 1.0f);
        commandBuffer.SetGlobalFloat(_FinalDstBlend, 0.0f);

        // prepare for final draw
        // ----------------------
        RenderTargetIdentifier finalSource;
        Pass finalPass;
        
        // if FXAA is enabled, apply color grading and tone mapping first
        // --------------------------------------------------------------
        if (mStack.BufferSettings.m_FXAA.enabled)
        {
            // pass FXAA configuration to GPU
            // ------------------------------
            ConfigureFXAA(commandBuffer);
            
            // draw color grading result
            // -------------------------
            finalSource = mColorGradingResult;
            finalPass = mKeepAlpha ? Pass.FXAA : Pass.FXAAWithLuma;
            Pass applyColorGradingPass = mKeepAlpha ? Pass.ApplyColorGrading : Pass.ApplyColorGradingWithLuma;
            mStack.Draw(commandBuffer, mColorSource, finalSource, applyColorGradingPass);
        }
        // if FXAA is not enabled, remember to apply color grading and tone mapping
        // ------------------------------------------------------------------------
        else
        {
            finalSource = mColorSource;
            finalPass = Pass.ApplyColorGrading;
        }

        // for non-scaled rendering, draw final pass directly
        // --------------------------------------------------
        if (mScaleMode == ScaleMode.None)
        {
            mStack.DrawFinal(commandBuffer, finalSource, finalPass);
        }
        // for scaled rendering
        // --------------------
        else
        {
            // first draw final pass to a scaled texture
            // -----------------------------------------
            mStack.Draw(commandBuffer, finalSource, mScaledResult, finalPass);
            
            // then output rescaled result to camera target
            // --------------------------------------------
            commandBuffer.SetGlobalFloat(_BlitBicubic, mScaleMode == ScaleMode.Bicubic ? 1.0f : 0.0f);
            mStack.DrawFinal(commandBuffer, mScaledResult, Pass.FinalRescale);
        }
        
        // execute and clear command buffer
        // --------------------------------
        context.renderContext.ExecuteCommandBuffer(commandBuffer);
        commandBuffer.Clear();
    }
    
    private void ConfigureFXAA(CommandBuffer commandBuffer)
    {
        // retrieve FXAA settings
        // ----------------------
        var fxaa = mStack.BufferSettings.m_FXAA;
        
        // set FXAA quality keywords
        // -------------------------
        commandBuffer.SetKeyword(_FXAAQualityLowKeyword, fxaa.quality == CameraBufferSettings.FXAA.Quality.Low);
        commandBuffer.SetKeyword(_FXAAQualityMediumKeyword, fxaa.quality == CameraBufferSettings.FXAA.Quality.Medium);
        
        // pass FXAA configuration to GPU
        // ------------------------------
        commandBuffer.SetGlobalVector(_FXAAConfig, new Vector4(fxaa.fixedThreshold, fxaa.relativeThreshold, fxaa.subpixelBlending));
    }
    
    private PostFXStack   mStack;
    private bool          mKeepAlpha;
    private ScaleMode     mScaleMode;

    private TextureHandle mColorSource;
    private TextureHandle mColorGradingResult;
    private TextureHandle mScaledResult;
    
    private static readonly GraphicsFormat _ColorFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.HDR);
    
    private static readonly int _FinalSrcBlend = Shader.PropertyToID("_FinalSrcBlend");
    private static readonly int _FinalDstBlend = Shader.PropertyToID("_FinalDstBlend");
    private static readonly int _FXAAConfig    = Shader.PropertyToID("_FXAAConfig");
    private static readonly int _BlitBicubic   = Shader.PropertyToID("_BlitBicubic");
    private static readonly GlobalKeyword _FXAAQualityLowKeyword = GlobalKeyword.Create("FXAA_QUALITY_LOW");
    private static readonly GlobalKeyword _FXAAQualityMediumKeyword = GlobalKeyword.Create("FXAA_QUALITY_MEDIUM");
    
    private static readonly ProfilingSampler _GroupSampler = new ("Post FX");
    private static readonly ProfilingSampler _FinalSampler = new ("Final Post FX");
    
    private enum ScaleMode { None, Linear, Bicubic, }
}
