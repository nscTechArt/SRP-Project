using System.Diagnostics;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class DebugPass
{
    [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
    public static void Record(RenderGraph renderGraph, Camera camera, in LightingResources resources)
    {
        // early exit if the debug pass is not active
        // ------------------------------------------
        if (!CameraDebugger.IsActive || camera.cameraType > CameraType.SceneView) return;
        
        // add and build debug pass
        // ------------------------
        using var build = renderGraph.AddRenderPass(_Sampler.name, out DebugPass pass, _Sampler);
        
        // indicate pass would read tile data buffer
        // -----------------------------------------
        build.ReadComputeBuffer(resources.tilesBuffer);
        
        // set render function
        // -------------------
        build.SetRenderFunc<DebugPass>(static (pass, context) => CameraDebugger.Render(context));
    }
    
    private static readonly ProfilingSampler _Sampler = new("Debug");
}
