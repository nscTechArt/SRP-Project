using UnityEngine;
using UnityEngine.Rendering;
using FinalBlendMode = CameraSettings.FinalBlendMode;

public readonly struct CameraRendererCopier
{
    public CameraRendererCopier(Material material, Camera camera, FinalBlendMode finalBlendMode)
    {
        mMaterial       = material;
        Camera          = camera;
        mFinalBlendMode = finalBlendMode;
    }

    public void Copy(CommandBuffer commandBuffer, RenderTargetIdentifier src, RenderTargetIdentifier dst, bool isDepth)
    {
        if (_CopyTextureSupported) 
            commandBuffer.CopyTexture(src, dst);
        else  
            CopyByDrawing(commandBuffer, src, dst, isDepth);
    }

    public void CopyByDrawing(CommandBuffer commandBuffer, RenderTargetIdentifier src, RenderTargetIdentifier dst, bool isDepth)
    {
        commandBuffer.SetGlobalTexture(_SourceTexture, src);
        commandBuffer.SetRenderTarget(dst, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        commandBuffer.SetViewport(Camera.pixelRect);
        commandBuffer.DrawProcedural(Matrix4x4.identity, mMaterial, isDepth ? 1 : 0, MeshTopology.Triangles, 3);
    }

    public void CopyToCameraTarget(CommandBuffer commandBuffer, RenderTargetIdentifier src)
    {
        // setup final blend mode
        // ----------------------
        commandBuffer.SetGlobalFloat(_CameraSrcBlend, (float) mFinalBlendMode.source);
        commandBuffer.SetGlobalFloat(_CameraDstBlend, (float) mFinalBlendMode.destination);
        
        // set source texture
        // ------------------
        commandBuffer.SetGlobalTexture(_SourceTexture, src);
        
        // set render target and viewport
        // ------------------------------
        var loadAction = mFinalBlendMode.destination == BlendMode.Zero && Camera.rect == _FullViewRect ? RenderBufferLoadAction.DontCare : RenderBufferLoadAction.Load;
        commandBuffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget, loadAction, RenderBufferStoreAction.Store);
        commandBuffer.SetViewport(Camera.pixelRect);
        
        // draw a full screen triangle
        // ---------------------------
        commandBuffer.DrawProcedural(Matrix4x4.identity, mMaterial, 0, MeshTopology.Triangles, 3);
        
        // reset blend mode
        // ----------------
        commandBuffer.SetGlobalFloat(_CameraSrcBlend, 1f);
        commandBuffer.SetGlobalFloat(_CameraDstBlend, 0f);
    }
    
    private readonly Material       mMaterial;
    public           Camera         Camera { get; }
    private readonly FinalBlendMode mFinalBlendMode;
    private static readonly bool   _CopyTextureSupported = SystemInfo.copyTextureSupport > CopyTextureSupport.None;
    public static bool              mRequiresRenderTargetResetAfterCopy => !_CopyTextureSupported;
    private static readonly Rect   _FullViewRect = new(0f, 0f, 1f, 1f);
    private static readonly int    _SourceTexture  = Shader.PropertyToID("_SourceTexture");
    private static readonly int    _CameraSrcBlend = Shader.PropertyToID("_CameraSrcBlend");
    private static readonly int    _CameraDstBlend = Shader.PropertyToID("_CameraDstBlend");
}
