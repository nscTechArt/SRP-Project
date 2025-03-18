using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class SetupPass 
{
    public static CameraRendererTextures Record
    (
        RenderGraph renderGraph, bool copyColor, bool copyDepth, 
        bool useHDR, Vector2Int attachmentSize, Camera camera
    )
    {
        // add and build setup pass
        // ------------------------
        using RenderGraphBuilder builder = renderGraph.AddRenderPass(_Sampler.name, out SetupPass setupPass, _Sampler);
        
        // set pass data
        // -------------
        setupPass.mAttachmentSize             = attachmentSize;
        setupPass.mCamera                     = camera;
        setupPass.mClearFlags                 = camera.clearFlags;
        
        // create color attachment/copy
        // ----------------------------
        TextureHandle colorCopy = default;
        var desc = new TextureDesc(attachmentSize.x, attachmentSize.y)
        {
            colorFormat = SystemInfo.GetGraphicsFormat(useHDR ? DefaultFormat.HDR : DefaultFormat.LDR),
            name = "Color Attachment",
        };
        TextureHandle colorAttachment = setupPass.mColorAttachment = builder.WriteTexture(renderGraph.CreateTexture(desc));
        if (copyColor)
        {
            desc.name = "Color Copy";
            colorCopy = renderGraph.CreateTexture(desc);
        }
            
        // depth attachment/copy
        // ---------------------
        TextureHandle depthCopy = default;
        desc.depthBufferBits = DepthBits.Depth32;
        desc.name = "Depth Attachment";
        TextureHandle depthAttachment = setupPass.mDepthAttachment = builder.WriteTexture(renderGraph.CreateTexture(desc));
        if (copyDepth)
        {
            desc.name = "Depth Copy";
            depthCopy = renderGraph.CreateTexture(desc);
        }
            
        // if RP uses an intermediate depth attachment, make sure it's cleared
        // -------------------------------------------------------------------
        if (camera.clearFlags > CameraClearFlags.Color) setupPass.mClearFlags = CameraClearFlags.Color;

        // setup pass should not be culled since it clears render target
        // -------------------------------------------------------------
        builder.AllowPassCulling(false);
        
        // set render function
        // -------------------
        builder.SetRenderFunc<SetupPass>(static (pass, context) => pass.Render(context));
        
        // return color/depth TextureHandles
        // ---------------------------------
        return new CameraRendererTextures(colorAttachment, depthAttachment, colorCopy, depthCopy);
    }

    private void Render(RenderGraphContext context)
    {
        // setup camera-related global shader variables
        // --------------------------------------------
        context.renderContext.SetupCameraProperties(mCamera);
        
        // retrieve command buffer from RenderGraphContext
        // -----------------------------------------------
        CommandBuffer commandBuffer = context.cmd;
        
        // set intermediate attachments as render target
        // ---------------------------------------------
        // since we will clear render target later, use DontCare for load action to save some performance
        commandBuffer.SetRenderTarget
        (
            mColorAttachment, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
            mDepthAttachment, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
        );
        
        // clear render target
        // -------------------
        commandBuffer.ClearRenderTarget
        (
            mClearFlags <= CameraClearFlags.Depth, 
            mClearFlags <= CameraClearFlags.Color, 
            mClearFlags == CameraClearFlags.Color ? mCamera.backgroundColor.linear : Color.clear
        );
        
        // pass attachment size to GPU
        // ---------------------------
        commandBuffer.SetGlobalVector(_CameraAttachmentSize, new Vector4(1.0f / mAttachmentSize.x, 1.0f / mAttachmentSize.y, mAttachmentSize.x, mAttachmentSize.y));
        
        // execute and clear command buffer
        // --------------------------------
        context.renderContext.ExecuteCommandBuffer(commandBuffer);
        commandBuffer.Clear();
    }
    
    private Vector2Int       mAttachmentSize;
    private Camera           mCamera;
    private CameraClearFlags mClearFlags;
    private TextureHandle    mColorAttachment;
    private TextureHandle    mDepthAttachment;
    private static readonly ProfilingSampler _Sampler = new("Setup");
    private static readonly int              _CameraAttachmentSize = Shader.PropertyToID("_CameraAttachmentSize");
}
