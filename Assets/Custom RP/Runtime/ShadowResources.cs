using UnityEngine.Experimental.Rendering.RenderGraphModule;

public readonly ref struct ShadowResources
{
    public ShadowResources
    (
        TextureHandle directionalAtlas, 
        TextureHandle otherAtlas,
        ComputeBufferHandle directionalShadowCascadesBuffer,
        ComputeBufferHandle directionalShadowMatricesBuffer,
        ComputeBufferHandle otherShadowDataBuffer
    )
    {
        this.directionalAtlas = directionalAtlas;
        this.otherAtlas = otherAtlas;
        this.directionalShadowCascadesBuffer = directionalShadowCascadesBuffer;
        this.directionalShadowMatricesBuffer = directionalShadowMatricesBuffer;
        this.otherShadowDataBuffer = otherShadowDataBuffer;
    }
    
    public readonly TextureHandle directionalAtlas;
    public readonly TextureHandle otherAtlas;
    public readonly ComputeBufferHandle directionalShadowCascadesBuffer;
    public readonly ComputeBufferHandle directionalShadowMatricesBuffer;
    public readonly ComputeBufferHandle otherShadowDataBuffer;
}
