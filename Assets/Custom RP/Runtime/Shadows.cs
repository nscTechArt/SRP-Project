using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public partial class Shadows
{
    public void Setup(CullingResults cullingResults, ShadowSettings settings)
    {
        // pass parameters to class fields
        // -------------------------------
        mCullingResults = cullingResults;
        mSettings       = settings;
        
        // reset reserved light count
        // --------------------------
        mReservedDirLightCount = mReservedOtherLightCount = 0;
        
        // reset shadow mask enabled status
        // --------------------------------
        mShadowMaskEnabled = false;
    }

    public Vector4 ReserveDirectionalShadows(int visibleLightIndex, Light dirLight)
    {
        // we only reserve shadowed dir light, which meets the following conditions:
        // ------------------------------------------------------------------------
        bool shouldReserve =
        // 1. max shadowed dir light count is not reached
        mReservedDirLightCount < kMaxShadowedDirLightCount;
        // 2. dir light casts shadows
        shouldReserve &= dirLight.shadows != LightShadows.None;
        // 3. shadow strength is greater than 0
        shouldReserve &= dirLight.shadowStrength > 0.0f;
        if (!shouldReserve) return new Vector4(0.0f, 0.0f, 0.0f, -1.0f);

        // reserve shadowed dir light
        // --------------------------
        var shadowedDirLight = new ShadowedDirLight
        {
            visibleLightIndex = visibleLightIndex,
            slopeScaleBias = dirLight.shadowBias,
            nearPlaneOffset = dirLight.shadowNearPlane
        };
        _ShadowedDirLights[mReservedDirLightCount] = shadowedDirLight;
        
        // figure out status of shadow mask, if enabled, retrieve light mask channel
        // -------------------------------------------------------------------------
        float maskChannel = -1;
        LightBakingOutput bakingOutput = dirLight.bakingOutput;
        if (bakingOutput is { lightmapBakeType: LightmapBakeType.Mixed, mixedLightingMode: MixedLightingMode.Shadowmask })
        {
            mShadowMaskEnabled = true;
            maskChannel = bakingOutput.occlusionMaskChannel;
        }
        
        // if current dir light has no realtime shadows
        // --------------------------------------------
        if (!mCullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds _))
        {
            // we can simply return shadow strength, which will be used to scale baked shadows
            // additionally, we use negative strength to avoid shadow map sampling in shader
            return new Vector4(0.0f, -dirLight.shadowStrength, 0.0f, maskChannel);
        }
        
        // finally return full shadow data including:
        // ------------------------------------------
        Vector4 shadowData = new()
        {
            // 1. with cascade enabled, light index will be multiplied by cascade count
            x = mSettings.m_Directional.m_CascadeCount * mReservedDirLightCount++,
            // 2. shadow strength, which can be retrieved LightComponent.shadowStrength
            y = dirLight.shadowStrength,
            // 3. shadow normal bias, which can be retrieved LightComponent.shadowNormalBias
            z = dirLight.shadowNormalBias,
            // 4. shadow mask channel
            w = maskChannel
        };
        return shadowData;
    }

    public Vector4 ReserveOtherShadows(int visibleLightIndex, Light light)
    {
        // we only reserve shadowed other light, which meets the following conditions:
        // ---------------------------------------------------------------------------
        bool shouldReserve =
        // 1. other light casts shadows
        light.shadows != LightShadows.None;
        // 2. shadow strength is greater than 0
        shouldReserve &= light.shadowStrength > 0.0f;
        if (!shouldReserve) return new Vector4(0.0f, 0.0f, 0.0f, -1.0f);
        
        // figure out status of shadow mask, if enabled, retrieve light mask channel
        // -------------------------------------------------------------------------
        float maskChannel = -1.0f;
        LightBakingOutput bakingOutput = light.bakingOutput;
        if (bakingOutput is { lightmapBakeType: LightmapBakeType.Mixed, mixedLightingMode: MixedLightingMode.Shadowmask })
        {
            mShadowMaskEnabled = true;
            maskChannel = bakingOutput.occlusionMaskChannel;
        }
        
        // if current other light has no realtime shadows
        // ----------------------------------------------
        bool isPoint = light.type == LightType.Point;
        int newLightCount = mReservedOtherLightCount + (isPoint ? 6 : 1);
        if (newLightCount > kMaxShadowedOtherLightCount ||
            !mCullingResults.GetShadowCasterBounds(visibleLightIndex, out _))
        {
            return new Vector4(0.0f, -light.shadowStrength, 0.0f, maskChannel);
        }

        _ShadowedOtherLights[mReservedOtherLightCount] = new ShadowedOtherLight
        {
            visibleLightIndex = visibleLightIndex,
            slopeScaleBias = light.shadowBias,
            normalBias = light.shadowNormalBias,
            isPoint = isPoint
        };
        
        // finally return full shadow data including:
        // ------------------------------------------
        Vector4 shadowData = new()
        {
            // 1. tile index
            x = mReservedOtherLightCount,
            // 2. shadow strength, which can be retrieved LightComponent.shadowStrength
            y = light.shadowStrength,
            // 3. is light point or spot
            z = isPoint ? 1.0f : 0.0f,
            // 4. shadow mask channel
            w = maskChannel
        };
        mReservedOtherLightCount = newLightCount;
        return shadowData;
    }

    public void Render(RenderGraphContext context)
    {
        // retrieve context and command buffer
        // -----------------------------------
        mCommandBuffer = context.cmd;
        mContext = context.renderContext;
        
        // render dir shadows if needed
        // ----------------------------
        if (mReservedDirLightCount > 0) RenderDirectionalShadows();
        
        // render other shadows if needed
        // ------------------------------ 
        if (mReservedOtherLightCount > 0) RenderOtherShadows();
        
        // set filter quality keywords
        // ---------------------------
        SetShaderKeywords(_FilterQualityKeywords, (int)mSettings.m_FilterQuality - 1);
        
        // set atlas textures and buffers to GPU
        // -------------------------------------
        mCommandBuffer.SetGlobalBuffer(_DirectionalShadowCascades, mDirectionalShadowCascadesBuffer);
        mCommandBuffer.SetGlobalBuffer(_DirectionalShadowMatrices, mDirectionalShadowMatricesBuffer);
        mCommandBuffer.SetGlobalBuffer(_OtherShadowData, mOtherShadowDataBuffer);
        mCommandBuffer.SetGlobalTexture(_DirectionalShadowAtlas, mDirectionalShadowAtlas);
        mCommandBuffer.SetGlobalTexture(_OtherShadowAtlas, mOtherShadowAtlas);

        // set shadow mask shader keywords
        // -------------------------------
        int modeIndex = mShadowMaskEnabled ? QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask ? 0 : 1 : -1;
        SetShaderKeywords(_ShadowMaskKeywords, modeIndex);
        
        // set cascade count and shadow distance fade data
        // -----------------------------------------------
        SetShadowDistanceFadeData();
        mCommandBuffer.SetGlobalInt(_CascadeCount, mReservedDirLightCount > 0 ? mSettings.m_Directional.m_CascadeCount : 0);
        mCommandBuffer.SetGlobalVector(_ShadowDistanceFadeData, mShadowDistanceFadeData);
        
        // set shadow atlas size
        // ---------------------
        mCommandBuffer.SetGlobalVector(_ShadowAtlasSize, mAtlasSizes);
        
        // execute command buffer
        // ----------------------
        ExecuteCommandBuffer(); 
    }

    private void RenderDirectionalShadows()
    {
        // retrieve atlas size from shadow settings
        // ----------------------------------------
        int atlasSize = (int)mSettings.m_Directional.m_AtlasSize;
        mAtlasSizes.x = atlasSize;
        mAtlasSizes.y = 1.0f / atlasSize;
     
        // set atlas as render target and clear it
        // ---------------------------------------
        mCommandBuffer.SetRenderTarget(mDirectionalShadowAtlas, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        mCommandBuffer.ClearRenderTarget(true, false, Color.clear);
        
        // activate shadow pancaking
        // -------------------------
        mCommandBuffer.SetGlobalFloat(_ShadowPancaking, 1.0f);
        
        // begin profiling sample: Shadows
        // -------------------------------
        mCommandBuffer.BeginSample("Directional Shadows");
        ExecuteCommandBuffer();
        
        // figure out split count and tile size, given shadowed dir light count
        // --------------------------------------------------------------------
        int tileCountInAtlas = mReservedDirLightCount * mSettings.m_Directional.m_CascadeCount;
        HowToSplit howToSplit = 
            tileCountInAtlas <= 1 ? HowToSplit.OneTileInAtlas : 
            tileCountInAtlas <= 4 ? HowToSplit.FourTilesInAtlas : HowToSplit.SixteenTilesInAtlas;
        int shadowResolution = atlasSize / (int)howToSplit;
        
        // render shadows for each shadowed dir light
        // ------------------------------------------
        for (int lightIndex = 0; lightIndex < mReservedDirLightCount; lightIndex++)
        {
            RenderDirShadowForSingleLight(lightIndex, howToSplit, shadowResolution);
        }
        
        // pass shadow data to GPU
        // -----------------------
        mCommandBuffer.SetBufferData(mDirectionalShadowCascadesBuffer, _DirectionalShadowCascadeArray, 0, 0, mSettings.m_Directional.m_CascadeCount);
        mCommandBuffer.SetBufferData(mDirectionalShadowMatricesBuffer, _DirShadowMatrices, 0, 0, mReservedDirLightCount * mSettings.m_Directional.m_CascadeCount);
        
        // set cascade blend mode keywords
        // -------------------------------
        mCommandBuffer.SetKeyword(_SoftCascadeBlendKeyword, mSettings.m_Directional.m_SoftCascadeBlend);
        
        // end of rendering shadows
        // ------------------------
        mCommandBuffer.EndSample("Directional Shadows");
        ExecuteCommandBuffer();
    }
    
    private void RenderOtherShadows()
    {
        // retrieve atlas size from shadow settings
        // ----------------------------------------
        int atlasSize = (int)mSettings.m_Other.m_AtlasSize;
        mAtlasSizes.z = atlasSize;
        mAtlasSizes.w = 1.0f / atlasSize;
     
        // set atlas as render target and clear it
        // ---------------------------------------
        mCommandBuffer.SetRenderTarget(mOtherShadowAtlas, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        mCommandBuffer.ClearRenderTarget(true, false, Color.clear);
        
        // deactivate shadow pancaking
        // ---------------------------
        mCommandBuffer.SetGlobalFloat(_ShadowPancaking, 0.0f);
        
        // begin profiling sample: Shadows
        // -------------------------------
        mCommandBuffer.BeginSample("Other Shadows");
        ExecuteCommandBuffer();
        
        // figure out split count and tile size, given shadowed dir light count
        // --------------------------------------------------------------------
        int tileCountInAtlas = mReservedOtherLightCount;
        HowToSplit howToSplit = 
            tileCountInAtlas <= 1 ? HowToSplit.OneTileInAtlas : 
            tileCountInAtlas <= 4 ? HowToSplit.FourTilesInAtlas : HowToSplit.SixteenTilesInAtlas;
        int shadowResolution = atlasSize / (int)howToSplit;
        
        // render shadows for each shadowed dir light
        // ------------------------------------------
        for (int lightIndex = 0; lightIndex < mReservedOtherLightCount;)
        {
            if (_ShadowedOtherLights[lightIndex].isPoint)
            {
                RenderPointShadowForSingleLight(lightIndex, howToSplit, shadowResolution);
                lightIndex += 6;
            }
            else
            {
                RenderSpotShadowForSingleLight(lightIndex, howToSplit, shadowResolution);
                lightIndex += 1;
            }
            
        }
        
        // pass shadow data to shaders via command buffer
        // ----------------------------------------------
        mCommandBuffer.SetBufferData(mOtherShadowDataBuffer, _OtherShadowDataArray, 0, 0, mReservedOtherLightCount);
        
        // end of rendering shadows
        // ------------------------
        mCommandBuffer.EndSample("Other Shadows");
        ExecuteCommandBuffer();
    }

    private void RenderDirShadowForSingleLight(int lightIndex, HowToSplit howToSplit, int shadowResolution)
    {
        // retrieve shadowed dir light from array
        // --------------------------------------
        ShadowedDirLight shadowedDirLight = _ShadowedDirLights[lightIndex];

        // EXPLANATION:
        // with cascade enabled, we have to render all cascade for each single light 
        // cascade count is the tile count for each single light
        // this is why we have to multiply light index by cascade count
        // to get the start tile index of all tiles in the atlas for current light
        // -------------------------------------------------------------------------
        int cascadeCount = mSettings.m_Directional.m_CascadeCount;
        int startTileIndex = lightIndex * cascadeCount;
        Vector3 cascadeRatios = mSettings.m_Directional.GetCascadeRatios;
        
        // calculate culling factor for shadow split data
        // ----------------------------------------------
        float cullingFactor = Mathf.Max(0.0f, 0.8f - mSettings.m_Directional.m_CascadeFade);
        
        // render all cascades for current light
        // -------------------------------------
        float tileScale = 1.0f / (int)howToSplit;
        for (int cascadeIndex = 0; cascadeIndex < cascadeCount; cascadeIndex++)
        {
            // retrieve shadow split data and view/proj matrices of light space
            // ----------------------------------------------------------------
            mCullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives
            (
                shadowedDirLight.visibleLightIndex,
                cascadeIndex,
                cascadeCount,
                cascadeRatios,
                shadowResolution,
                shadowedDirLight.nearPlaneOffset,
                out Matrix4x4 lightView,
                out Matrix4x4 lightProj,
                out ShadowSplitData splitData
            );
        
            // setup ShadowDrawingSettings
            // ---------------------------
            // ReSharper disable once UseObjectOrCollectionInitializer
            var drawingSettings = new ShadowDrawingSettings(mCullingResults, shadowedDirLight.visibleLightIndex, BatchCullingProjectionType.Orthographic);
            splitData.shadowCascadeBlendCullingFactor = cullingFactor;
            drawingSettings.splitData = splitData;
            drawingSettings.useRenderingLayerMaskTest = true;
            
            // store culling spheres
            // since all dir lights share the same culling spheres, 
            // we only need to store them once
            // ---------------------------------------------------
            if (lightIndex == 0)
            {
                _DirectionalShadowCascadeArray[cascadeIndex] = new DirectionalShadowCascade(splitData.cullingSphere, shadowResolution, mSettings.DirectionalFilterSize); 
            }
        
            // set tile viewport for current cascade of current light
            // ------------------------------------------------------
            int tileIndex = startTileIndex + cascadeIndex;
            Vector2 tileOffset = SetTileViewportForSingleLight(tileIndex, howToSplit, shadowResolution);
        
            // store view-proj matrix of the specific tile of current light
            // ------------------------------------------------------------
            Matrix4x4 tileMatrix = CalculateTileMatrix(lightProj * lightView, tileOffset, tileScale);
            _DirShadowMatrices[tileIndex] = tileMatrix;
        
            // apply view/proj matrices of light space
            // ---------------------------------------
            mCommandBuffer.SetViewProjectionMatrices(lightView, lightProj);
        
            // finally we are ready to draw shadows
            // ------------------------------------
            // apply shadow slope scale bias
            mCommandBuffer.SetGlobalDepthBias(0.0f, shadowedDirLight.slopeScaleBias);
            ExecuteCommandBuffer();
            mContext.DrawShadows(ref drawingSettings);
            // reset shadow slope scale bias
            mCommandBuffer.SetGlobalDepthBias(0.0f, 0.0f);
        }
    }
    
    private void RenderSpotShadowForSingleLight(int lightIndex, HowToSplit howToSplit, int shadowResolution)
    {
        // retrieve shadowed dir light from array
        // --------------------------------------
        ShadowedOtherLight shadowedOtherLight = _ShadowedOtherLights[lightIndex];
        
        mCullingResults.ComputeSpotShadowMatricesAndCullingPrimitives
        (
            shadowedOtherLight.visibleLightIndex,
            out Matrix4x4 lightView,
            out Matrix4x4 lightProj,
            out ShadowSplitData splitData
        );
        
        // setup ShadowDrawingSettings
        // ---------------------------
        var drawingSettings = new ShadowDrawingSettings(mCullingResults, shadowedOtherLight.visibleLightIndex, BatchCullingProjectionType.Perspective)
        {
            splitData = splitData,
            useRenderingLayerMaskTest = true,
        };

        float texelSize = 2.0f / (shadowResolution * lightProj.m00);
        float filterSize = texelSize * mSettings.OtherFilterSize;
        float bias = shadowedOtherLight.normalBias * filterSize * 1.4142136f;
        Vector2 offset = SetTileViewportForSingleLight(lightIndex, howToSplit, shadowResolution);
        float tileScale = 1.0f / (int)howToSplit;
        _OtherShadowDataArray[lightIndex] = new OtherShadowData(offset, tileScale, bias, mAtlasSizes.w * 0.5f,
            CalculateTileMatrix(lightProj * lightView, offset, tileScale));
        
        // apply view/proj matrices of light space
        // ---------------------------------------
        mCommandBuffer.SetViewProjectionMatrices(lightView, lightProj);
    
        // finally we are ready to draw shadows
        // ------------------------------------
        // apply shadow slope scale bias
        mCommandBuffer.SetGlobalDepthBias(0.0f, shadowedOtherLight.slopeScaleBias);
        ExecuteCommandBuffer();
        mContext.DrawShadows(ref drawingSettings);
        // reset shadow slope scale bias
        mCommandBuffer.SetGlobalDepthBias(0.0f, 0.0f);
    }
    
    private void RenderPointShadowForSingleLight(int lightIndex, HowToSplit howToSplit, int shadowResolution)
    {
        // retrieve shadowed dir light from array
        // --------------------------------------
        ShadowedOtherLight shadowedOtherLight = _ShadowedOtherLights[lightIndex];
        
        // create ShadowDrawingSettings
        // ---------------------------
        var drawingSettings = new ShadowDrawingSettings(
            mCullingResults, shadowedOtherLight.visibleLightIndex, BatchCullingProjectionType.Perspective)
        {
            useRenderingLayerMaskTest = true
        };

        float texelSize = 2.0f / shadowResolution;
        float filterSize = texelSize * mSettings.OtherFilterSize;
        float bias = shadowedOtherLight.normalBias * filterSize * 1.4142136f;
        float tileScale = 1.0f / (int)howToSplit;
        float fovBias = Mathf.Atan(1.0f + bias + filterSize) * Mathf.Rad2Deg * 2.0f - 90.0f;
        
        for (int i = 0; i < 6; i++)
        {
            mCullingResults.ComputePointShadowMatricesAndCullingPrimitives
            (
                shadowedOtherLight.visibleLightIndex,
                (CubemapFace)i, fovBias,
                out Matrix4x4 lightView,
                out Matrix4x4 lightProj,
                out ShadowSplitData splitData
            );
            lightView.m11 = -lightView.m11;
            lightView.m12 = -lightView.m12;
            lightView.m13 = -lightView.m13;

            drawingSettings.splitData = splitData;

            int tileIndex = lightIndex + i;
            Vector2 offset = SetTileViewportForSingleLight(tileIndex, howToSplit, shadowResolution);

            _OtherShadowDataArray[tileIndex] = new OtherShadowData(offset, tileScale, bias, mAtlasSizes.w * 0.5f,
                CalculateTileMatrix(lightProj * lightView, offset, tileScale));
    
            // apply view/proj matrices of light space
            // ---------------------------------------
            mCommandBuffer.SetViewProjectionMatrices(lightView, lightProj);
    
            // finally we are ready to draw shadows
            // ------------------------------------
            // apply shadow slope scale bias
            mCommandBuffer.SetGlobalDepthBias(0.0f, shadowedOtherLight.slopeScaleBias);
            ExecuteCommandBuffer();
            mContext.DrawShadows(ref drawingSettings);
            // reset shadow slope scale bias
            mCommandBuffer.SetGlobalDepthBias(0.0f, 0.0f);
        }
        
    }

    private Vector2 SetTileViewportForSingleLight(int lightIndex, HowToSplit howToSplit, int tileSize)
    {
        // calculate viewport offset
        // -------------------------
        Vector2 offset = new Vector2(lightIndex % (int)howToSplit, lightIndex / (int)howToSplit);
        
        // then we know the center of viewport
        // -----------------------------------
        Vector2 position = new(offset.x * tileSize, offset.y * tileSize);
        
        // calculate viewport
        // ------------------
        Vector2 size = new(tileSize, tileSize);
        Rect viewport = new(position, size);

        // finally apply viewport
        // ----------------------
        mCommandBuffer.SetViewport(viewport);
        
        // don't forget to return offset
        // -----------------------------
        return offset;
    }
    
    private static Matrix4x4 CalculateTileMatrix(Matrix4x4 m, Vector2 tileOffset, float scale)
    {
        // negate z if reverse z is enabled
        // --------------------------------
        if (SystemInfo.usesReversedZBuffer)
        {
            m.m20 = -m.m20;
            m.m21 = -m.m21;
            m.m22 = -m.m22;
            m.m23 = -m.m23;
        }
        
        // ------------------------------------------------------------------------------------------
        // EXPLANATION:
        // when applied proj-view matrix, we get clip space coordinates, which are in range [-1, 1]
        // however, when talking about shadowmap, both texcoords and depth values are in range [0, 1]
        // this is why we need to scale and bias the matrix on CPU side
        // ------------------------------------------------------------------------------------------
        m.m00 = (0.5f * (m.m00 + m.m30) + tileOffset.x * m.m30) * scale;
        m.m01 = (0.5f * (m.m01 + m.m31) + tileOffset.x * m.m31) * scale;
        m.m02 = (0.5f * (m.m02 + m.m32) + tileOffset.x * m.m32) * scale;
        m.m03 = (0.5f * (m.m03 + m.m33) + tileOffset.x * m.m33) * scale;
        m.m10 = (0.5f * (m.m10 + m.m30) + tileOffset.y * m.m30) * scale;
        m.m11 = (0.5f * (m.m11 + m.m31) + tileOffset.y * m.m31) * scale;
        m.m12 = (0.5f * (m.m12 + m.m32) + tileOffset.y * m.m32) * scale;
        m.m13 = (0.5f * (m.m13 + m.m33) + tileOffset.y * m.m33) * scale;
        m.m20 = 0.5f * (m.m20 + m.m30);
        m.m21 = 0.5f * (m.m21 + m.m31);
        m.m22 = 0.5f * (m.m22 + m.m32);
        m.m23 = 0.5f * (m.m23 + m.m33);
        
        return m;
    }

    private void SetShadowDistanceFadeData()
    {
        // we will do some pre-calculation to avoid doing it in shader
        // -----------------------------------------------------------
        // max shadow distance
        mShadowDistanceFadeData.x = 1.0f / mSettings.m_MaxDistance;
        // fade ratio of max shadow distance
        mShadowDistanceFadeData.y = 1.0f / mSettings.m_FadeRatioOfMaxDistance;
        // fade ratio of last cascade
        float oneMinusCascadeFade = 1.0f - mSettings.m_Directional.m_CascadeFade;
        mShadowDistanceFadeData.z = 1.0f / (1.0f - oneMinusCascadeFade * oneMinusCascadeFade);
    }
    
    private void SetShaderKeywords(GlobalKeyword[] keywords, int enabledIndex)
    {
        for (int i = 0; i < keywords.Length; i++)
        {
            mCommandBuffer.SetKeyword(keywords[i], i == enabledIndex);
        }
    }

    public ShadowResources GetResources(RenderGraph renderGraph, RenderGraphBuilder builder)
    {
        // directional atlas
        // -----------------
        int atlasSize = (int)mSettings.m_Directional.m_AtlasSize;
        var textureDesc = new TextureDesc(atlasSize, atlasSize)
        {
            depthBufferBits = DepthBits.Depth32,
            isShadowMap = true,
            name = "Directional Shadow Atlas"
        };
        mDirectionalShadowAtlas = mReservedDirLightCount > 0
            ? builder.WriteTexture(renderGraph.CreateTexture(textureDesc))
            : renderGraph.defaultResources.defaultShadowTexture;

        // other atlas
        // -----------
        atlasSize = (int)mSettings.m_Other.m_AtlasSize;
        textureDesc.width = textureDesc.height = atlasSize;
        textureDesc.name = "Other Shadow Atlas";
        mOtherShadowAtlas = mReservedOtherLightCount > 0
            ? builder.WriteTexture(renderGraph.CreateTexture(textureDesc))
            : renderGraph.defaultResources.defaultShadowTexture;
        
        // create and register compute buffers
        // -----------------------------------
        // directional shadow cascades
        var bufferDesc = new ComputeBufferDesc
        {
            name = "Dir Shadow Cascades",
            stride = DirectionalShadowCascade.kStride,
            count = kMaxCascadeCount,
        };
        mDirectionalShadowCascadesBuffer = builder.WriteComputeBuffer(renderGraph.CreateComputeBuffer(bufferDesc));
        // directional shadow matrices
        bufferDesc.name = "Dir Shadow Matrices";
        bufferDesc.stride = 4 * 16;
        bufferDesc.count = kMaxShadowedDirLightCount * kMaxCascadeCount;
        mDirectionalShadowMatricesBuffer = builder.WriteComputeBuffer(renderGraph.CreateComputeBuffer(bufferDesc));
        // other shadow data
        bufferDesc.name = "Other Shadow Data";
        bufferDesc.stride = OtherShadowData.kStride;
        bufferDesc.count = kMaxShadowedOtherLightCount;
        mOtherShadowDataBuffer = builder.WriteComputeBuffer(renderGraph.CreateComputeBuffer(bufferDesc));

        // return ShadowTextures instance
        // ------------------------------
        return new ShadowResources(mDirectionalShadowAtlas, mOtherShadowAtlas, mDirectionalShadowCascadesBuffer, mDirectionalShadowMatricesBuffer, mOtherShadowDataBuffer);
    }
    
    private void ExecuteCommandBuffer()
    {
        mContext.ExecuteCommandBuffer(mCommandBuffer);
        mCommandBuffer.Clear();
    }
    
    private ScriptableRenderContext mContext;
    private CullingResults          mCullingResults;
    private ShadowSettings          mSettings;
    private CommandBuffer           mCommandBuffer;
    private bool                    mShadowMaskEnabled;
    private int                     mReservedDirLightCount;
    private int                     mReservedOtherLightCount;
    private TextureHandle           mDirectionalShadowAtlas;
    private TextureHandle           mOtherShadowAtlas;
    private ComputeBufferHandle     mOtherShadowDataBuffer;
    private ComputeBufferHandle     mDirectionalShadowCascadesBuffer;
    private ComputeBufferHandle     mDirectionalShadowMatricesBuffer;
    private Vector4                 mAtlasSizes;
    private static Vector4          mShadowDistanceFadeData;
    
    private static readonly ShadowedDirLight[]         _ShadowedDirLights = new ShadowedDirLight[kMaxShadowedDirLightCount];
    private static readonly ShadowedOtherLight[]       _ShadowedOtherLights = new ShadowedOtherLight[kMaxShadowedOtherLightCount];
    private static readonly Matrix4x4[]                _DirShadowMatrices = new Matrix4x4[kMaxShadowedDirLightCount * kMaxCascadeCount];
    private static readonly OtherShadowData[]          _OtherShadowDataArray = new OtherShadowData[kMaxShadowedOtherLightCount];
    private static readonly DirectionalShadowCascade[] _DirectionalShadowCascadeArray = new DirectionalShadowCascade[kMaxCascadeCount];
    private static readonly GlobalKeyword              _SoftCascadeBlendKeyword = GlobalKeyword.Create("_SOFT_CASCADE_BLEND");
    private static readonly GlobalKeyword[]            _FilterQualityKeywords =
    {
        GlobalKeyword.Create("_SHADOW_FILTER_MEDIUM"),
        GlobalKeyword.Create("_SHADOW_FILTER_HIGH"),
    };
    private static readonly GlobalKeyword[]            _ShadowMaskKeywords =
    {
        GlobalKeyword.Create("_SHADOW_MASK_ALWAYS"),
        GlobalKeyword.Create("_SHADOW_MASK_DISTANCE"),
    };
    
    private static readonly int _DirectionalShadowAtlas = Shader.PropertyToID("_DirectionalShadowAtlas");
    private static readonly int _DirectionalShadowMatrices = Shader.PropertyToID("_DirectionalShadowMatrices"); 
    private static readonly int _DirectionalShadowCascades = Shader.PropertyToID("_DirectionalShadowCascades");
    private static readonly int _OtherShadowAtlas = Shader.PropertyToID("_OtherShadowAtlas");
    private static readonly int _OtherShadowData = Shader.PropertyToID("_OtherShadowData");
    private static readonly int _CascadeCount = Shader.PropertyToID("_CascadeCount");
    private static readonly int _ShadowDistanceFadeData = Shader.PropertyToID("_ShadowDistanceFadeData");
    private static readonly int _ShadowPancaking = Shader.PropertyToID("_ShadowPancaking");
    private static readonly int _ShadowAtlasSize = Shader.PropertyToID("_ShadowAtlasSize");
    
    private const int kMaxShadowedDirLightCount = 4, kMaxShadowedOtherLightCount = 16, kMaxCascadeCount = 4;

    private struct ShadowedDirLight
    {
        // stores data per shadowed directional light
        // ------------------------------------------
        public int   visibleLightIndex;
        public float slopeScaleBias;
        public float nearPlaneOffset;
    }
    private struct ShadowedOtherLight
    {
        // stores data per shadowed other light
        // ------------------------------------
        public int   visibleLightIndex;
        public float slopeScaleBias;
        public float normalBias;
        public bool  isPoint;
    }
    private enum HowToSplit { OneTileInAtlas = 1, FourTilesInAtlas = 2, SixteenTilesInAtlas = 4 }
}

