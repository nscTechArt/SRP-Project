using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class CopyAttachmentsPass
{
    public static void Record
    (
        RenderGraph renderGraph, bool copyColor, bool copyDepth,
        CameraRendererCopier copier, in CameraRendererTextures textures
    )
    {
        // early exit if no need to copy
        // -----------------------------
        if (!copyColor && !copyDepth) return;

        // add and build copy attachments pass
        // -----------------------------------
        using var builder = renderGraph.AddRenderPass(_Sampler.name, out CopyAttachmentsPass pass, _Sampler);
        
        // set pass data
        // -------------
        pass.mCopier    = copier;
        pass.mCopyColor = copyColor;
        pass.mCopyDepth = copyDepth;
        pass.mColorAttachment = builder.ReadTexture(textures.colorAttachment);
        pass.mDepthAttachment = builder.ReadTexture(textures.depthAttachment);
        if (copyColor)
            pass.mColorTexture = builder.WriteTexture(textures.colorCopy);
        if (copyDepth)
            pass.mDepthTexture = builder.WriteTexture(textures.depthCopy);
        
        // set render function
        // -------------------
        builder.SetRenderFunc<CopyAttachmentsPass>(static (pass, context) => pass.Render(context));
    }
    
    private void Render(RenderGraphContext context)
    {
        // retrieve command buffer from RenderGraphContext
        // -----------------------------------------------
        CommandBuffer commandBuffer = context.cmd;
        
        // copy color if needed
        // --------------------
        if (mCopyColor)
        {
            mCopier.Copy(commandBuffer, mColorAttachment, mColorTexture, false);
            commandBuffer.SetGlobalTexture(_CameraColorTexture, mColorTexture);
        }
        
        // copy depth if needed
        // --------------------
        if (mCopyDepth)
        {
            mCopier.Copy(commandBuffer, mDepthAttachment, mDepthTexture, true);
            commandBuffer.SetGlobalTexture(_CameraDepthTexture, mDepthTexture);
        }
        
        // reset render target if needed
        // -----------------------------
        if (CameraRendererCopier.mRequiresRenderTargetResetAfterCopy)
        {
            commandBuffer.SetRenderTarget
            (
                mColorAttachment, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store,
                mDepthAttachment, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store
            );
        }
        
        // execute and clear command buffer
        // --------------------------------
        context.renderContext.ExecuteCommandBuffer(commandBuffer);
        commandBuffer.Clear();
    }

    private CameraRendererCopier mCopier;
    private bool                 mCopyColor;
    private bool                 mCopyDepth;
    private TextureHandle        mColorAttachment;
    private TextureHandle        mDepthAttachment;
    private TextureHandle        mColorTexture;
    private TextureHandle        mDepthTexture;
    
    private static readonly int _CameraColorTexture = Shader.PropertyToID("_CameraColorTexture");
    private static readonly int _CameraDepthTexture = Shader.PropertyToID("_CameraDepthTexture");
    private static readonly ProfilingSampler _Sampler = new ("Copy Attachments");
}
