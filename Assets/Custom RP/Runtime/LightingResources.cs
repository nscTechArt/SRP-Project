using UnityEngine.Experimental.Rendering.RenderGraphModule;

public readonly ref struct LightingResources
{
    public LightingResources
    (
        ComputeBufferHandle directionalLightDataBuffer,
        ComputeBufferHandle otherLightDataBuffer,
        ComputeBufferHandle tilesBuffer,
        ShadowResources shadowResources
    )
    {
        this.directionalLightDataBuffer = directionalLightDataBuffer;
        this.otherLightDataBuffer = otherLightDataBuffer;
        this.tilesBuffer = tilesBuffer;
        this.shadowResources = shadowResources;
    }
    
    public readonly ComputeBufferHandle directionalLightDataBuffer;
    public readonly ComputeBufferHandle otherLightDataBuffer;
    public readonly ComputeBufferHandle tilesBuffer;
    public readonly ShadowResources shadowResources;
}
