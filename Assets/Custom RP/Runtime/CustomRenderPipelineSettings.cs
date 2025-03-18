using System;
using UnityEngine;

[Serializable]
public class CustomRenderPipelineSettings
{
    public CameraBufferSettings m_CameraBufferSettings = new()
    {
        m_AllowHDR = true,
        m_RenderScale = 1.0f,
        m_FXAA = new CameraBufferSettings.FXAA
        {
            fixedThreshold = 0.0833f,
            relativeThreshold = 0.1667f,
            subpixelBlending = 0.75f
        }
    };
    
    public ForwardPlusSettings  m_ForwardPlusSettings;
    public ShadowSettings       m_ShadowSettings;
    public PostFXSettings       m_PostFXSettings;
    public ColorLUTResolution   m_ColorLUTResolution = ColorLUTResolution._64;
    public Shader               m_CameraRendererShader;
    public Shader               m_CameraDebuggerShader;
    
    public enum ColorLUTResolution { _16 = 16, _32 = 32, _64 = 64 }
}
