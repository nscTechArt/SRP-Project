using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using static PostFXStack;

public class BloomPass
{
    public static TextureHandle Record(RenderGraph renderGraph, PostFXStack stack, in CameraRendererTextures cameraTextures)
    {
        // get bloom settings
        // ------------------
        var bloom = stack.Settings.Bloom;
        
        // begin bloom pyramid with half resolution
        // ----------------------------------------
        Vector2Int size = new Vector2Int(stack.Camera.pixelWidth, stack.Camera.pixelHeight) / 2;

        // early exit if bloom is disabled
        // -------------------------------
        if (bloom.maxIterationCount == 0 || 
            bloom.intensity <= 0.0f ||
            size.x < bloom.minDownscaleRes * 2 || 
            size.y < bloom.minDownscaleRes * 2)
        {
            return cameraTextures.colorAttachment;
        }
        
        // add and build bloom pass
        // ------------------------
        using var builder = renderGraph.AddRenderPass(_Sampler.name, out BloomPass pass, _Sampler);
        
        // set pass data
        // -------------
        pass.mStack = stack;
        pass.mColorSource = builder.ReadTexture(cameraTextures.colorAttachment);
        
        // create bloom prefilter texture as pyramid base
        // ----------------------------------------------
        TextureHandle[] pyramid = pass.mPyramid;
        var desc = new TextureDesc(size.x, size.y)
        {
            colorFormat = SystemInfo.GetGraphicsFormat(stack.BufferSettings.m_AllowHDR ? DefaultFormat.HDR : DefaultFormat.LDR),
            name = "Bloom Prefilter"
        };
        pyramid[0] = builder.CreateTransientTexture(desc);
        
        // complete bloom pyramid
        // ----------------------
        size /= 2;
        int pyramidIndex = 1;
        int i;
        for (i = 0; i < bloom.maxIterationCount; i++, pyramidIndex += 2)
        {
            // check if current level would become "degenerate"
            // ------------------------------------------------
            if (size.y < bloom.minDownscaleRes || size.x < bloom.minDownscaleRes) break;

            // horizontal texture
            // ------------------
            desc.width = size.x;
            desc.height = size.y;
            desc.name = "Bloom Pyramid H";
            pyramid[pyramidIndex] = builder.CreateTransientTexture(desc);
            
            // vertical texture
            // ----------------
            desc.name = "Bloom Pyramid V";
            pyramid[pyramidIndex + 1] = builder.CreateTransientTexture(desc);
            
            // prepare for next iteration
            // --------------------------
            size /= 2;
        }
        
        // keep track of step count
        // ------------------------
        pass.mStepCount = i;

        // create bloom result texture
        // ---------------------------
        desc.width = stack.AttachmentSize.x;
        desc.height = stack.AttachmentSize.y;
        desc.name = "Bloom Result";
        pass.mBloomResult = builder.WriteTexture(renderGraph.CreateTexture(desc));
        
        // set render function
        // -------------------
        builder.SetRenderFunc<BloomPass>(static (pass, context) => pass.Render(context));
        
        // return bloom result texture
        // ---------------------------
        return pass.mBloomResult;
    }
    
    private void Render(RenderGraphContext context)
    {
        // retrieve command buffer and bloom settings
        // ------------------------------------------
        CommandBuffer commandBuffer = context.cmd;
        var bloom = mStack.Settings.Bloom;
        
        // calculate bloom threshold
        // -------------------------
        Vector4 threshold;
        threshold.x  = Mathf.GammaToLinearSpace(bloom.threshold);
        threshold.y  = threshold.x * bloom.thresholdKnee;
        threshold.z  = 2.0f * threshold.y;
        threshold.w  = 0.25f / (threshold.y + 0.00001f);
        threshold.y -= threshold.x;
        commandBuffer.SetGlobalVector(_BloomThreshold, threshold);
        
        // prefilter bloom
        // ---------------
        mStack.Draw(commandBuffer, mColorSource, mPyramid[0], Pass.BloomPrefilter);
        
        // downsample blur
        // ---------------
        int src = 0, dst = 2;
        int i;
        for (i = 0; i < mStepCount; i++)
        {
            // split gaussian blur
            // -------------------
            int middle = dst - 1;
            mStack.Draw(commandBuffer, mPyramid[src], mPyramid[middle], Pass.BloomHorizontal);
            mStack.Draw(commandBuffer, mPyramid[middle], mPyramid[dst], Pass.BloomVertical);
            
            // prepare for next iteration
            // --------------------------
            src = dst;
            dst += 2;
        }
        
        // configure bloom combine and final pass
        // --------------------------------------
        Pass combinePass, finalPass;
        float finalIntensity;
        if (bloom.mode == PostFXSettings.BloomSettings.Mode.Additive)
        {
            combinePass = Pass.BloomCombineAdditive;
            finalPass   = Pass.BloomCombineAdditive;
            commandBuffer.SetGlobalFloat(_BloomIntensity, 1.0f);
            finalIntensity = bloom.intensity;
        }
        else
        {
            combinePass = Pass.BloomCombineScatter;
            finalPass =   Pass.BloomScatterFinal;
            commandBuffer.SetGlobalFloat(_BloomIntensity, bloom.scatter);
            finalIntensity = Mathf.Min(bloom.intensity, 1.0f);
        }
        
        // upsample and combine, if there are more than two iterations
        // -----------------------------------------------------------
        if (i > 1)
        {
            dst -= 5;
            for (i -= 1; i > 0; i--)
            {
                // combine low-res srcID with high-res dstID
                // -----------------------------------------
                commandBuffer.SetGlobalTexture(_PostFXSource2, mPyramid[dst + 1]);
                mStack.Draw(commandBuffer, mPyramid[src], mPyramid[dst], combinePass);
                
                // prepare for next iteration
                // --------------------------
                src  = dst;
                dst -= 2;
            }
        }
        
        // final combine
        // -------------
        commandBuffer.SetGlobalFloat(_BloomIntensity, finalIntensity);
        commandBuffer.SetGlobalTexture(_PostFXSource2, mColorSource);
        mStack.Draw(commandBuffer, mPyramid[src], mBloomResult, finalPass);
    }
    
    private PostFXStack              mStack;
    private int                      mStepCount;
    private TextureHandle            mColorSource;
    private TextureHandle            mBloomResult;
    private readonly TextureHandle[] mPyramid = new TextureHandle[2 * kMaxBloomPyramidLevels + 1];
    
    private static readonly int _BloomThreshold = Shader.PropertyToID("_BloomThreshold");
    private static readonly int _BloomIntensity = Shader.PropertyToID("_BloomIntensity");
    private static readonly int _PostFXSource2 = Shader.PropertyToID("_PostFXSource2");
    
    private static readonly ProfilingSampler _Sampler = new("Bloom");

    private const int kMaxBloomPyramidLevels = 16;
}
