using System.Diagnostics;
using UnityEditor;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class GizmosPass
{
    [Conditional("UNITY_EDITOR")]
    public static void Record(RenderGraph renderGraph, CameraRendererCopier copier, in CameraRendererTextures textures)
    {
#if UNITY_EDITOR
        
        // early exit if gizmos are not enabled
        // ------------------------------------
        if (!Handles.ShouldRenderGizmos())  return;
        
        // add and build gizmos pass
        // -------------------------
        using RenderGraphBuilder builder = renderGraph.AddRenderPass(_Sampler.name, out GizmosPass pass, _Sampler);
            
        // set pass data
        // -------------
        pass.mCopier = copier;
        pass.mDepthAttachment = builder.ReadTexture(textures.depthAttachment);

        // set render function
        // -------------------
        builder.SetRenderFunc<GizmosPass>(static (pass, context) => pass.Render(context));
        
#endif
    }
    
#if UNITY_EDITOR
    private void Render(RenderGraphContext context)
    {
        // retrieve command buffer and render context from RenderGraphContext
        // ------------------------------------------------------------------
        CommandBuffer commandBuffer = context.cmd;
        ScriptableRenderContext renderContext = context.renderContext;
        
        // copy depth buffer
        // -----------------
        mCopier.CopyByDrawing(commandBuffer, mDepthAttachment, BuiltinRenderTextureType.CameraTarget, true);
        renderContext.ExecuteCommandBuffer(commandBuffer);
        commandBuffer.Clear();
        
        // draw gizmos
        // -----------
        context.renderContext.DrawGizmos(mCopier.Camera, GizmoSubset.PreImageEffects);
        context.renderContext.DrawGizmos(mCopier.Camera, GizmoSubset.PostImageEffects);
    }

    private CameraRendererCopier mCopier;
    private TextureHandle mDepthAttachment;
    private static readonly ProfilingSampler _Sampler = new ("Gizmos");
#endif
    
    
}
