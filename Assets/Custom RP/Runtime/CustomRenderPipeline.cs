using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;

public partial class CustomRenderPipeline : RenderPipeline
{
    public CustomRenderPipeline(CustomRenderPipelineSettings settings)
    {
        // pass parameters to class fields
        // -------------------------------
        mSettings = settings;
        
        // graphics settings
        // -----------------
        GraphicsSettings.useScriptableRenderPipelineBatching = true;
        GraphicsSettings.lightsUseLinearIntensity = true;
        
        // editor related
        // --------------
        InitializeForEditor();
        
        // invoke renderer's constructor
        // -----------------------------
        mCameraRenderer = new CameraRenderer(settings.m_CameraRendererShader, mSettings.m_CameraDebuggerShader);
    }
    
    protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
    {
        // render all cameras
        // ------------------
        foreach (Camera camera in cameras)
        {
            mCameraRenderer.Render(mRenderGraph, context, camera, mSettings);
        }
        
        // once all cameras are rendered, end the frame
        // --------------------------------------------
        mRenderGraph.EndFrame();
    }
    
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        
        DisposeForEditor();
        
        mCameraRenderer.Dispose();
        
        mRenderGraph.Cleanup();
    }

    #region Fields
    // pipeline related
    // ----------------
    private readonly RenderGraph          mRenderGraph = new();
    private readonly CameraRenderer       mCameraRenderer;
    private readonly CameraBufferSettings mCameraBufferSettings;
    private readonly ShadowSettings       mShadowSettings;
    private readonly PostFXSettings       mPostFXSettings;
    private readonly int                  mColorLUTResolution;
    private readonly CustomRenderPipelineSettings mSettings;
    #endregion
    
    #region Deprecated in Unity 2022.3 But Still Required
    protected override void Render(ScriptableRenderContext context, Camera[] cameras) { }
    #endregion
}
