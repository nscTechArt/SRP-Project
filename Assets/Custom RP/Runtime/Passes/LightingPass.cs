using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using Unity.Jobs;
using Unity.Mathematics;
using static Unity.Mathematics.math;

public partial class LightingPass
{
    public static LightingResources Record
    (
        RenderGraph renderGraph, 
        CullingResults cullingResults,
        Vector2Int attachmentSize,
        ForwardPlusSettings forwardPlusSettings,
        ShadowSettings shadowSettings, 
        int renderingLayerMask
    )
    {
        // add and build lighting pass
        // ---------------------------
        using RenderGraphBuilder builder = renderGraph.AddRenderPass(_Sampler.name, out LightingPass pass, _Sampler);
        
        // invoke Setup() on lighting pass
        // -------------------------------
        pass.Setup(cullingResults, attachmentSize, forwardPlusSettings, shadowSettings, renderingLayerMask);
        
        // create and register directional light data buffer
        // -------------------------------------------------
        var desc = new ComputeBufferDesc
        {
            name = "Dir Light Data",
            count = kMaxDirectionalLightCount,
            stride = DirectionalLightData.kStride
        };
        pass.mDirectionalLightDataBuffer = builder.WriteComputeBuffer(renderGraph.CreateComputeBuffer(desc));
        
        // create and register other light data buffer
        // -------------------------------------------
        desc.name = "Other Light Data";
        desc.count = kMaxOtherLightCount;
        desc.stride = OtherLightData.kStride;
        pass.mOtherLightDataBuffer = builder.WriteComputeBuffer(renderGraph.CreateComputeBuffer(desc));
        
        // create and register tiles buffer
        // --------------------------------
        desc.name = "Forward+ Tiles";
        desc.count = pass.TileCount * pass.mMaxTileDataSize;
        desc.stride = 4;
        pass.mTilesBuffer = builder.WriteComputeBuffer(renderGraph.CreateComputeBuffer(desc));
        
        // set render function
        // -------------------
        builder.SetRenderFunc<LightingPass>(static (pass, context) => pass.Render(context));
        
        // Lighting pass should bot be culled
        // ----------------------------------
        builder.AllowPassCulling(false);
        
        // return lighting resources
        // -------------------------
        return new LightingResources(pass.mDirectionalLightDataBuffer, pass.mOtherLightDataBuffer, pass.mTilesBuffer, pass.mShadows.GetResources(renderGraph, builder));
    }
    
    private static readonly ProfilingSampler _Sampler = new ("Lighting");

    private void Setup
    (
        CullingResults cullingResults, 
        Vector2Int attachmentSize,
        ForwardPlusSettings forwardPlusSettings,
        ShadowSettings shadowSettings, 
        int renderingLayerMask
    )
    {
        // pass parameters to class fields
        // -------------------------------
        mCullingResults = cullingResults;
        mMaxLightsPerTile = forwardPlusSettings.m_MaxLightsPerTile <= 0 ? 31 : forwardPlusSettings.m_MaxLightsPerTile;
        mMaxTileDataSize = mMaxLightsPerTile + 1;
        
        // setup shadows
        // -------------
        mShadows.Setup(cullingResults, shadowSettings);
        
        // create light bounds array
        // -------------------------
        mLightBounds = new NativeArray<float4>(kMaxOtherLightCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        
        // calculate tile size and count
        // -----------------------------
        float tileScreenPixelSize = forwardPlusSettings.m_TileSize <= 0 ? 16 : (float)forwardPlusSettings.m_TileSize;
        mScreenUVToTileCoords.x = attachmentSize.x / tileScreenPixelSize;
        mScreenUVToTileCoords.y = attachmentSize.y / tileScreenPixelSize;
        mTileCount.x = Mathf.CeilToInt(mScreenUVToTileCoords.x);
        mTileCount.y = Mathf.CeilToInt(mScreenUVToTileCoords.y);
        
        // setup lights
        // ------------
        SetupLights(renderingLayerMask);
    }
    
    private void SetupLights(int renderingLayerMask)
    {
        // get visible lights from culling results
        // ---------------------------------------
        NativeArray<VisibleLight> visibleLights = mCullingResults.visibleLights;

        
        int requiredMaxLightPerTile = Mathf.Min(mMaxLightsPerTile, visibleLights.Length);
        mTileDataSize = requiredMaxLightPerTile + 1;
        
        // iterate over visible lights
        // ---------------------------
        mDirectionalLightCount = mOtherLightCount = 0;
        for (int i = 0; i < visibleLights.Length; i++)
        {
            VisibleLight visibleLight = visibleLights[i];
            Light        light = visibleLight.light;
            
            // skip lights that are not camera's rendering layer
            // -------------------------------------------------
            if ((light.renderingLayerMask & renderingLayerMask) == 0) continue;
            
            // setup lights based on their type
            // --------------------------------
            switch (visibleLight.lightType)
            {
                case LightType.Directional:
                    if (mDirectionalLightCount < kMaxDirectionalLightCount)
                    {
                        _DirectionalLightDataArray[mDirectionalLightCount++] = new DirectionalLightData(ref visibleLight, light, mShadows.ReserveDirectionalShadows(i, light));
                    }
                    break;

                case LightType.Point:
                    if (mOtherLightCount < kMaxOtherLightCount)
                    {
                        SetupForwardPlus(mOtherLightCount, ref visibleLight);
                        
                        _OtherLightDataArray[mOtherLightCount++] = OtherLightData.CreatePointLight(ref visibleLight, light, mShadows.ReserveOtherShadows(i, light));
                    }
                    break;
            
                case LightType.Spot:
                    if (mOtherLightCount < kMaxOtherLightCount)
                    {
                        SetupForwardPlus(mOtherLightCount, ref visibleLight);
                        
                        _OtherLightDataArray[mOtherLightCount++] = OtherLightData.CreateSpotLight(ref visibleLight, light, mShadows.ReserveOtherShadows(i, light));
                    }
                    break;
                
                case LightType.Area:
                    
                case LightType.Disc:
                    break;
                
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        // create tile data array
        // ----------------------
        mTileData = new NativeArray<int>(TileCount * mTileDataSize, Allocator.TempJob);
        mForwardPlusJobHandle = new ForwardPlusTilesJob
        {
            lightBounds = mLightBounds,
            tileData    = mTileData,
            otherLightCount = mOtherLightCount,
            tileScreenUVSize = float2(1.0f / mScreenUVToTileCoords.x, 1.0f / mScreenUVToTileCoords.y),
            maxLightsPerTile = requiredMaxLightPerTile,
            tilesPerRow = mTileCount.x,
            tileDataSize = mTileDataSize
        }.ScheduleParallel(TileCount, mTileCount.x, default);
    }
    
    private void Render(RenderGraphContext context)
    {
        // retrieve command buffer from RenderGraphContext
        // -----------------------------------------------
        CommandBuffer cmd = context.cmd;
        
        // pass directional light data to GPU
        // ----------------------------------
        cmd.SetGlobalInt(_DirectionalLightCount, mDirectionalLightCount);
        cmd.SetBufferData(mDirectionalLightDataBuffer, _DirectionalLightDataArray, 0, 0, mDirectionalLightCount);
        cmd.SetGlobalBuffer(_DirectionalLightData, mDirectionalLightDataBuffer);
        
        // pass other light data to GPU
        // ----------------------------
        cmd.SetGlobalInt(_OtherLightCount, mOtherLightCount);
        cmd.SetBufferData(mOtherLightDataBuffer, _OtherLightDataArray, 0, 0, mOtherLightCount);
        cmd.SetGlobalBuffer(_OtherLightData, mOtherLightDataBuffer);
        
        // render shadow maps
        // ------------------
        mShadows.Render(context);
        
        // wait for Forward+ job to complete
        // ---------------------------------
        mForwardPlusJobHandle.Complete();
        
        // pass tile data to GPU
        // ---------------------
        cmd.SetBufferData(mTilesBuffer, mTileData, 0, 0, mTileData.Length);
        cmd.SetGlobalBuffer(_ForwardPlusTiles, mTilesBuffer);
        cmd.SetGlobalVector(_ForwardPlusTileSettings, new Vector4
        (
            mScreenUVToTileCoords.x, 
            mScreenUVToTileCoords.y,
            mTileCount.x.ReinterpretAsFloat(),
            mTileDataSize.ReinterpretAsFloat()
        ));
        
        // execute and clear command buffer
        // --------------------------------
        context.renderContext.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        
        // dispose light bounds and tile data
        // ----------------------------------
        mLightBounds.Dispose();
        mTileData.Dispose();
    }

    private void SetupForwardPlus(int lightIndex, ref VisibleLight visibleLight)
    {
        Rect rect = visibleLight.screenRect;
        mLightBounds[lightIndex] = float4(rect.xMin, rect.yMin, rect.xMax, rect.yMax);
    }
    
    private CullingResults   mCullingResults;
    private readonly Shadows mShadows = new();
    private int              mDirectionalLightCount;
    private int              mOtherLightCount;
    
    private ComputeBufferHandle mDirectionalLightDataBuffer;
    private ComputeBufferHandle mOtherLightDataBuffer;
    
    private ComputeBufferHandle mTilesBuffer;
    private Vector2             mScreenUVToTileCoords;
    private Vector2Int          mTileCount;
    private int                 TileCount => mTileCount.x * mTileCount.y;
    private int                 mMaxLightsPerTile;
    private int                 mTileDataSize;
    private int                 mMaxTileDataSize;
    private NativeArray<float4> mLightBounds;
    private NativeArray<int>    mTileData;
    private JobHandle           mForwardPlusJobHandle;
    
    private static readonly DirectionalLightData[] _DirectionalLightDataArray = new DirectionalLightData[kMaxDirectionalLightCount];
    private static readonly OtherLightData[]       _OtherLightDataArray = new OtherLightData[kMaxOtherLightCount];

    private static readonly int _DirectionalLightCount   = Shader.PropertyToID("_DirectionalLightCount");
    private static readonly int _DirectionalLightData    = Shader.PropertyToID("_DirectionalLightData");
    private static readonly int _OtherLightCount         = Shader.PropertyToID("_OtherLightCount");
    private static readonly int _OtherLightData          = Shader.PropertyToID("_OtherLightData");
    private static readonly int _ForwardPlusTiles        = Shader.PropertyToID("_ForwardPlusTiles");
    private static readonly int _ForwardPlusTileSettings = Shader.PropertyToID("_ForwardPlusTileSettings");

    private const int kMaxDirectionalLightCount = 4;
    private const int kMaxOtherLightCount = 128;
}

