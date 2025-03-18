using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;

public class GeometryPass
{
    public static void Record
    (
        RenderGraph renderGraph, Camera camera, CullingResults cullingResults, 
        int renderingLayerMask, bool opaque, 
        in CameraRendererTextures cameraTextures, in LightingResources resources
    )
    {
        // add and build geometry pass
        // -------------------------------------
        ProfilingSampler sampler = opaque ? _SamplerOpaque : _SamplerTransparent;
        using var builder = renderGraph.AddRenderPass(sampler.name, out GeometryPass pass, sampler);
        
        // create and register render list
        // -------------------------------
        var listDesc = new RendererListDesc(_ShaderTagIDs, cullingResults, camera)
        {
            sortingCriteria = opaque ? SortingCriteria.CommonOpaque : SortingCriteria.CommonTransparent,
            rendererConfiguration = PerObjectData.ReflectionProbes | PerObjectData.Lightmaps | PerObjectData.ShadowMask | PerObjectData.LightProbe | PerObjectData.OcclusionProbe | PerObjectData.LightProbeProxyVolume | PerObjectData.OcclusionProbeProxyVolume,
            renderQueueRange = opaque ? RenderQueueRange.opaque : RenderQueueRange.transparent,
            renderingLayerMask = (uint)renderingLayerMask,
        };
        pass.mRendererList = builder.UseRendererList(renderGraph.CreateRendererList(listDesc));
        
        // indicate that the pass reads and writes to color/depth attachments
        // ------------------------------------------------------------------
        builder.ReadWriteTexture(cameraTextures.colorAttachment);
        builder.ReadWriteTexture(cameraTextures.depthAttachment);

        // for transparent objects, read color and depth copies
        // ----------------------------------------------------
        if (!opaque)
        {
            if (cameraTextures.colorCopy.IsValid()) builder.ReadTexture(cameraTextures.colorCopy);
            if (cameraTextures.depthCopy.IsValid()) builder.ReadTexture(cameraTextures.depthCopy);
        }
        
        // indicate that the pass reads light data buffers and shadow maps
        // ---------------------------------------------------------------
        builder.ReadComputeBuffer(resources.directionalLightDataBuffer);
        builder.ReadComputeBuffer(resources.otherLightDataBuffer);
        if (resources.tilesBuffer.IsValid())
            builder.ReadComputeBuffer(resources.tilesBuffer);
        builder.ReadComputeBuffer(resources.shadowResources.directionalShadowCascadesBuffer);
        builder.ReadComputeBuffer(resources.shadowResources.directionalShadowMatricesBuffer);
        builder.ReadComputeBuffer(resources.shadowResources.otherShadowDataBuffer);
        builder.ReadTexture(resources.shadowResources.directionalAtlas);
        builder.ReadTexture(resources.shadowResources.otherAtlas);
        
        // set render function
        // -------------------
        builder.SetRenderFunc<GeometryPass>(static (pass, context) => pass.Render(context));
    }
    
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
    private static readonly ProfilingSampler _SamplerOpaque = new ("Opaque Geometry");
    private static readonly ProfilingSampler _SamplerTransparent = new ("Transparent Geometry");
    private static readonly ShaderTagId[]    _ShaderTagIDs =
    {
        new("SRPDefaultUnlit"),
        new("CustomLit")
    };
}
