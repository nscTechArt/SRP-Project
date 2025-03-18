using System.Diagnostics;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;

public class UnsupportedShaderPass
{
    [Conditional("UNITY_EDITOR")]
    public static void Record(RenderGraph renderGraph, Camera camera, CullingResults cullingResults)
    {
#if UNITY_EDITOR
        // add and build unsupported shader pass
        // -------------------------------------
        using var builder = renderGraph.AddRenderPass(_Sampler.name, out UnsupportedShaderPass unsupportedShaderPass, _Sampler);
        
        // create error material
        // ---------------------
        mErrorMaterial ??= new Material(Shader.Find("Hidden/InternalErrorShader"));
        
        // create and register render list
        // -------------------------------
        var listDesc = new RendererListDesc(_LegacyShaderTagIDs, cullingResults, camera)
        {
            overrideMaterial = mErrorMaterial,
            renderQueueRange = RenderQueueRange.all
        };
        unsupportedShaderPass.mRendererList = builder.UseRendererList(renderGraph.CreateRendererList(listDesc));
        
        // set render function
        // -------------------
        builder.SetRenderFunc<UnsupportedShaderPass>(static (pass, context) => pass.Render(context));
#endif
    }
    
#if UNITY_EDITOR

    private void Render(RenderGraphContext context)
    {
        // draw render list
        // ----------------
        context.cmd.DrawRendererList(mRendererList);
        
        // execute and clear command buffer
        // --------------------------------
        context.renderContext.ExecuteCommandBuffer(context.cmd);
        context.cmd.Clear();
    }
    
    private RendererListHandle mRendererList;
    private static Material    mErrorMaterial;
    private static readonly ProfilingSampler _Sampler = new ("Unsupported Shader");
    private static readonly ShaderTagId[]    _LegacyShaderTagIDs =
    {
        new("Always"),
        new("ForwardBase"),
        new("PrepassBase"),
        new("Vertex"),
        new("VertexLMRGBM"),
        new("VertexLM")
    };
#endif
}
