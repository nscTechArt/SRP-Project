using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class SkyboxPass
{
    public static void Record(RenderGraph renderGraph, Camera camera, in CameraRendererTextures textures)
    {
        // early out if no skybox
        // ----------------------
        if (camera.clearFlags != CameraClearFlags.Skybox) return;
        
        // add and build skybox pass
        // -------------------------
        using var builder = renderGraph.AddRenderPass(_Sampler.name, out SkyboxPass skyboxPass, _Sampler);
        
        // set pass data
        // -------------
        skyboxPass.mCamera = camera;
        
        // skybox-drawing would read both color and depth attachments, also write to color attachment
        // ------------------------------------------------------------------------------------------
        builder.ReadWriteTexture(textures.colorAttachment);
        builder.ReadTexture(textures.depthAttachment);
        
        // set render function
        // -------------------
        builder.SetRenderFunc<SkyboxPass>(static (pass, context) => pass.Render(context));
    }

    private void Render(RenderGraphContext context)
    {
        // execute and clear command buffer
        // --------------------------------
        context.renderContext.ExecuteCommandBuffer(context.cmd);
        context.cmd.Clear();
        
        // draw skybox
        // -----------
        context.renderContext.DrawSkybox(mCamera);
    }

    private Camera mCamera;
    private static readonly ProfilingSampler _Sampler = new("Skybox");
}
