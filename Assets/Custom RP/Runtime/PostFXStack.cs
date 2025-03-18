using UnityEngine;
using UnityEngine.Rendering;

public class PostFXStack
{
    public void Draw(CommandBuffer commandBuffer, RenderTargetIdentifier dst, Pass pass)
    {
        // use dst as render target
        // ------------------------
        commandBuffer.SetRenderTarget(dst, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        
        // draw with given pass
        // --------------------
        commandBuffer.DrawProcedural(Matrix4x4.identity, Settings.Material, (int) pass, MeshTopology.Triangles, 3);
    }
    
    public void Draw(CommandBuffer commandBuffer, RenderTargetIdentifier src, RenderTargetIdentifier dst, Pass pass)
    {
        // pass src to GPU
        // ---------------
        commandBuffer.SetGlobalTexture(_PostFXSource, src);
        
        // use dst as render target
        // ------------------------
        commandBuffer.SetRenderTarget(dst, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        
        // draw with given pass
        // --------------------
        commandBuffer.DrawProcedural(Matrix4x4.identity, Settings.Material, (int) pass, MeshTopology.Triangles, 3);
    }
    
    public void DrawFinal(CommandBuffer commandBuffer, RenderTargetIdentifier src, Pass finalPass)
    {
        // pass final blend mode to GPU
        // ----------------------------
        commandBuffer.SetGlobalFloat(_FinalSrcBlend, (float) FinalBlendMode.source);
        commandBuffer.SetGlobalFloat(_FinalDstBlend, (float) FinalBlendMode.destination);
        
        // pass src to GPU
        // ---------------
        commandBuffer.SetGlobalTexture(_PostFXSource, src);
        
        // determine load action
        // ---------------------
        RenderBufferLoadAction loadAction = FinalBlendMode.destination == BlendMode.Zero && Camera.rect == _FullViewRect 
            ? RenderBufferLoadAction.DontCare : RenderBufferLoadAction.Load;
        
        // use camera's framebuffer as render target
        // -----------------------------------------
        commandBuffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget, loadAction, RenderBufferStoreAction.Store);
        
        // make sure we're using correct viewport
        // --------------------------------------
        commandBuffer.SetViewport(Camera.pixelRect);
        
        // draw with final pass
        // --------------------
        commandBuffer.DrawProcedural(Matrix4x4.identity, Settings.Material, (int) finalPass, MeshTopology.Triangles, 3);
    }
    
    public enum Pass
    {
        // default
        Blit,
        // bloom
        BloomHorizontal,
        BloomVertical,
        BloomCombineAdditive,
        BloomCombineScatter,
        BloomPrefilter,
        BloomScatterFinal,
        // color grading
        ColorGradingNone,
        ColorGradingACES,
        ColorGradingNeutral,
        ColorGradingReinhard,
        ApplyColorGrading,
        ApplyColorGradingWithLuma,
        // final pass
        FinalRescale,
        FXAA,
        FXAAWithLuma,
    }

    public Camera                        Camera { get; set; }
    public Vector2Int                    AttachmentSize { get; set; }
    public PostFXSettings                Settings { get; set; }
    public CameraBufferSettings          BufferSettings { get; set; }
    public CameraSettings.FinalBlendMode FinalBlendMode { get; set; }
    
    private static readonly Rect _FullViewRect = new(0.0f, 0.0f, 1.0f, 1.0f);
    
    private static readonly int _PostFXSource  = Shader.PropertyToID("_PostFXSource");
    private static readonly int _FinalSrcBlend = Shader.PropertyToID("_FinalSrcBlend");
    private static readonly int _FinalDstBlend = Shader.PropertyToID("_FinalDstBlend");
}