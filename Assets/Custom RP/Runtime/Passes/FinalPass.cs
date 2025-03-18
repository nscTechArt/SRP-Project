using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class FinalPass
{
    public static void Record(RenderGraph renderGraph, CameraRendererCopier copier, in CameraRendererTextures textures)
    {
        // add and build final pass
        // ------------------------
        using RenderGraphBuilder builder = renderGraph.AddRenderPass(_Sampler.name, out FinalPass pass, _Sampler);
        
        // set pass data
        // -------------
        pass.mCopier = copier;
        pass.mColorAttachment = builder.ReadTexture(textures.colorAttachment);
        
        // set render function
        // -------------------
        builder.SetRenderFunc<FinalPass>(static (pass, context) => pass.Render(context));
    }

    private void Render(RenderGraphContext context)
    {
        // retrieve command buffer from RenderGraphContext
        // -----------------------------------------------
        CommandBuffer commandBuffer = context.cmd;
        
        // copy color attachment to camera target
        // --------------------------------------
        mCopier.CopyToCameraTarget(commandBuffer, mColorAttachment);
        
        // execute and clear command buffer
        // --------------------------------
        context.renderContext.ExecuteCommandBuffer(commandBuffer);
        commandBuffer.Clear();
    }

    private CameraRendererCopier mCopier;
    private TextureHandle        mColorAttachment;
    private static readonly ProfilingSampler _Sampler = new("Final");
}
