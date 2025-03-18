using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class CameraRenderer
{
    public CameraRenderer(Shader shader, Shader cameraDebuggerShader)
    {
        mMaterial = CoreUtils.CreateEngineMaterial(shader);
        CameraDebugger.Initialize(cameraDebuggerShader);
    }
    
    public void  Render
    (
        RenderGraph renderGraph, ScriptableRenderContext renderContext, 
        Camera camera, CustomRenderPipelineSettings settings
    )
    {
        // initialize render context
        // -------------------------
        mRenderContext = renderContext;
        
        // retrieve settings
        // -----------------
        CameraBufferSettings bufferSettings = settings.m_CameraBufferSettings;
        PostFXSettings       postFXSettings  = settings.m_PostFXSettings;
        ShadowSettings       shadowSettings  = settings.m_ShadowSettings;
        
        // retrieve camera with its sampler, settings
        // ------------------------------------------
        mCamera = camera;
        bool hasCRP = mCamera.TryGetComponent(out CustomRenderPipelineCamera crpCamera);
        ProfilingSampler cameraSampler  = hasCRP ? crpCamera.Sampler  : ProfilingSampler.Get(mCamera.cameraType);
        CameraSettings   cameraSettings = hasCRP ? crpCamera.Settings : _DefaultCameraSettings;
        
        // determine if Custom RP should have color/depth copies
        // -----------------------------------------------------
        bool copyColor = bufferSettings.m_CopyColor && cameraSettings.m_CopyColor && mCamera.cameraType != CameraType.Reflection;
        bool copyDepth = bufferSettings.m_CopyDepth && cameraSettings.m_CopyDepth && mCamera.cameraType != CameraType.Reflection;
        
        // determine if hdr is enabled
        // ---------------------------
        bufferSettings.m_AllowHDR &= mCamera.allowHDR;
        
        // determine if scaled rendering is enabled
        // ----------------------------------------
        float renderScale = cameraSettings.GetRenderScale(bufferSettings.m_RenderScale);
        bool useScaledRendering = renderScale is < 0.99f or > 1.01f;
        
        // determine buffer size
        // ---------------------
        renderScale = Mathf.Clamp(renderScale, kRenderScaleMin, kRenderScaleMax);
        Vector2Int attachmentSize = default;
        attachmentSize.x = useScaledRendering ? (int)(mCamera.pixelWidth * renderScale)  : mCamera.pixelWidth;
        attachmentSize.y = useScaledRendering ? (int)(mCamera.pixelHeight * renderScale) : mCamera.pixelHeight;
        
        // render UI for scene camera
        // -------------------------
#if UNITY_EDITOR
        if (mCamera.cameraType == CameraType.SceneView)
        {
            ScriptableRenderContext.EmitWorldGeometryForSceneView(mCamera);
            // avoid render scale in scene view
            // useScaledRendering = false;
        }
#endif
        
        // do culling
        // ----------
        if (!mCamera.TryGetCullingParameters(out ScriptableCullingParameters p)) return;
        p.shadowDistance = Mathf.Min(mCamera.farClipPlane, shadowSettings.m_MaxDistance);
        mCullingResults = mRenderContext.Cull(ref p);
        
        // determine if custom RP has active post FX
        // -----------------------------------------
        postFXSettings = cameraSettings.m_OverridePostFX ? cameraSettings.m_PostFXSettings : postFXSettings;
        bool hasActivePostFX = postFXSettings != null && PostFXSettings.AreApplicableTo(camera);
        
        // initialize render graph params
        // ------------------------------
        var renderGraphParams = new RenderGraphParameters
        {
            executionName           = cameraSampler.name,
            currentFrameIndex       = Time.frameCount,
            rendererListCulling     = true,
            scriptableRenderContext = mRenderContext,
            commandBuffer           = CommandBufferPool.Get(),
        };
        
        // record and execute render graph
        // -------------------------------
        using (renderGraph.RecordAndExecute(renderGraphParams))
        {
            // configure profiling scope
            // -------------------------
            using RenderGraphProfilingScope _ = new(renderGraph, cameraSampler);
            
            // pass light data to GPU, and retrieve shadow maps
            // ------------------------------------------------
            var lightingResources = LightingPass.Record(renderGraph, mCullingResults, attachmentSize, settings.m_ForwardPlusSettings, shadowSettings, cameraSettings.m_MaskLights ? cameraSettings.m_RenderingLayerMask : -1);
            
            // create attachments, set and clear render target
            // -----------------------------------------------
            var cameraTextures = SetupPass.Record(renderGraph, copyColor, copyDepth, bufferSettings.m_AllowHDR, attachmentSize, mCamera);
            
            // render opaque geometry and skybox
            // ---------------------------------
            GeometryPass.Record(renderGraph, mCamera, mCullingResults, cameraSettings.m_RenderingLayerMask, true, cameraTextures, lightingResources);
            SkyboxPass.Record(renderGraph, mCamera, cameraTextures);
            
            // copy color/depth attachments if needed
            // --------------------------------------
            var copier = new CameraRendererCopier(mMaterial, mCamera, cameraSettings.m_FinalBlendMode);
            CopyAttachmentsPass.Record(renderGraph, copyColor, copyDepth, copier, cameraTextures);
            
            // render transparent and unsupported shader geometry
            // --------------------------------------------------
            GeometryPass.Record(renderGraph, mCamera, mCullingResults, cameraSettings.m_RenderingLayerMask, false, cameraTextures, lightingResources);
            UnsupportedShaderPass.Record(renderGraph, mCamera, mCullingResults);
            
            // render post FX stack
            // --------------------
            if (hasActivePostFX)
            {
                mPostFXStack.Camera         = camera;
                mPostFXStack.AttachmentSize = attachmentSize;
                mPostFXStack.Settings       = postFXSettings;
                mPostFXStack.BufferSettings = bufferSettings;
                mPostFXStack.FinalBlendMode = cameraSettings.m_FinalBlendMode;
                PostFXPass.Record(renderGraph, mPostFXStack, (int)settings.m_ColorLUTResolution, cameraSettings.m_KeepAlpha, cameraTextures);
            }
            
            // output color attachment to camera frame buffer
            // ----------------------------------------------
            else FinalPass.Record(renderGraph, copier, cameraTextures);
            
            // render debug pass
            // -----------------
            DebugPass.Record(renderGraph, mCamera, lightingResources);
            
            // render gizmos for Unity Editor
            // ------------------------------
            GizmosPass.Record(renderGraph, copier, cameraTextures);
        }
        
        // finish this frame
        // -----------------
        mRenderContext.ExecuteCommandBuffer(renderGraphParams.commandBuffer);
        mRenderContext.Submit();
        CommandBufferPool.Release(renderGraphParams.commandBuffer);
    }
    
    public void Dispose()
    {
        CoreUtils.Destroy(mMaterial);
        CameraDebugger.Dispose();
    }

    private Camera                  mCamera;
    private ScriptableRenderContext mRenderContext;
    private CullingResults          mCullingResults;
    private readonly PostFXStack    mPostFXStack = new();
    private readonly Material       mMaterial;
    private const float             kRenderScaleMin = 0.1f;
    private const float             kRenderScaleMax = 2.0f;
    private static readonly CameraSettings _DefaultCameraSettings = new();
}
