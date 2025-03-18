using System;
using UnityEngine;
using UnityEngine.Rendering;

[Serializable]
public class CameraSettings
{
    // buffer related
    // --------------
    [Space]
    public bool           m_CopyColor = true;
    public bool           m_CopyDepth = true;
    public bool           m_KeepAlpha;
    
    // rendering layer mask related
    // ----------------------------
    [Space, RenderingLayerMaskField]
    public int            m_RenderingLayerMask = -1;
    public bool           m_MaskLights;
    
    // render scale related
    // --------------------
    [Space]
    public RenderScaleMode m_RenderScaleMode = RenderScaleMode.Inherit;
    [Range(0.1f, 2.0f)]
    public float           m_RenderScale = 1.0f;
    public float           GetRenderScale(float scale)
    {
        return m_RenderScaleMode switch
        {
            RenderScaleMode.Inherit => scale,
            RenderScaleMode.Override => m_RenderScale,
            _ => scale * m_RenderScale
        };
    }
    
    // post processing related
    // -----------------------
    [Space]
    public bool           m_OverridePostFX;
    public PostFXSettings m_PostFXSettings;
    public FinalBlendMode m_FinalBlendMode = new()
    {
        source      = BlendMode.One,
        destination = BlendMode.Zero
    };
    
    // enums and structs
    // -----------------
    [Serializable]
    public struct FinalBlendMode
    {
        public BlendMode source;
        public BlendMode destination;
    }
    public enum RenderScaleMode
    {
        Inherit, Multiply, Override,
    }
}
