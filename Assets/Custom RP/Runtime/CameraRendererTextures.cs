using UnityEngine.Experimental.Rendering.RenderGraphModule;

public readonly ref struct CameraRendererTextures
{
    public CameraRendererTextures(TextureHandle colorAttachment, TextureHandle depthAttachment, TextureHandle colorCopy, TextureHandle depthCopy)
    {
        this.colorAttachment = colorAttachment;
        this.depthAttachment = depthAttachment;
        this.colorCopy = colorCopy;
        this.depthCopy = depthCopy;
    }
    
    public readonly TextureHandle colorAttachment;
    public readonly TextureHandle depthAttachment;
    public readonly TextureHandle colorCopy;
    public readonly TextureHandle depthCopy;
}
