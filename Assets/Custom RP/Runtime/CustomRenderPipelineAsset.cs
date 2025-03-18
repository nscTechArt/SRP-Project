using System;
using UnityEngine;
using UnityEngine.Rendering;

[Serializable, CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
public partial class CustomRenderPipelineAsset : RenderPipelineAsset
{
    protected override RenderPipeline CreatePipeline()
    {
        if ((m_Settings == null || m_Settings.m_CameraRendererShader == null) && m_CameraRendererShader != null)
        {
            m_Settings = new CustomRenderPipelineSettings()
            {
                m_CameraBufferSettings = m_CameraBuffer,
                m_ShadowSettings = m_ShadowSettings,
                m_PostFXSettings = m_PostFXSettings,
                m_ColorLUTResolution = (CustomRenderPipelineSettings.ColorLUTResolution)m_ColorLUTResolution,
                m_CameraRendererShader = m_CameraRendererShader
            };
        }

        if (m_PostFXSettings != null) m_PostFXSettings = null;
        if (m_CameraRendererShader != null) m_CameraRendererShader = null;
        
        return new CustomRenderPipeline(m_Settings);
    }

    [SerializeField]
    private CustomRenderPipelineSettings m_Settings;
    
    [SerializeField, HideInInspector] 
    private CameraBufferSettings m_CameraBuffer = new()
    {
        m_AllowHDR = true,
        m_RenderScale = 1.0f,
        m_FXAA = new CameraBufferSettings.FXAA
        {
            fixedThreshold = 0.0833f,
            relativeThreshold = 0.166f,
            subpixelBlending = 0.75f
        }
    };
    [SerializeField, HideInInspector]
    private ShadowSettings     m_ShadowSettings;
    [SerializeField, HideInInspector]
    private PostFXSettings     m_PostFXSettings;
    [SerializeField, HideInInspector]
    private ColorLUTResolution m_ColorLUTResolution = ColorLUTResolution._32;
    [SerializeField, HideInInspector]
    private Shader             m_CameraRendererShader;
    
    public enum ColorLUTResolution { _16 = 16, _32 = 32, _64 = 64 }
}
