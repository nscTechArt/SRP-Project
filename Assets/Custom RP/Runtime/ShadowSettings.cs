using System;
using UnityEngine;

[Serializable]
public class ShadowSettings
{
    [Header("Global"), Min(0.001f)]
    public float m_MaxDistance = 100.0f;
    [Range(0.001f, 1.0f)]
    public float m_FadeRatioOfMaxDistance = 0.1f;
    public FilterQuality m_FilterQuality = FilterQuality.Medium;
    [Space]
    public Directional m_Directional = new()
    {
        m_AtlasSize = TextureSize._1024,
        m_CascadeCount = 4,
        m_CascadeRatio1 = 0.1f,
        m_CascadeRatio2 = 0.25f,
        m_CascadeRatio3 = 0.5f,
        m_CascadeFade = 0.1f,
        m_CascadeBlendMode = Directional.CascadeBlendMode.Dither
    };
    [Space] public Other m_Other = new()
    {
        m_AtlasSize = TextureSize._1024,
    };

    public float DirectionalFilterSize => (float)m_FilterQuality + 2.0f;
    public float OtherFilterSize => (float)m_FilterQuality + 2.0f;
    
    
    // enums and structs
    // -----------------
    public enum TextureSize
    {
        _256 = 256, _512 = 512, _1024 = 1024, _2048 = 2048, _4096 = 4096, _8192 = 8192
    }
    public enum FilterMode
    {
        PCF2x2, PCF3x3, PCF5x5, PCF7x7
    }
    [Serializable]
    public struct Directional
    {
        public TextureSize      m_AtlasSize;
        [Range(1, 4)] 
        public int              m_CascadeCount;
        [Range(0.0f, 1.0f)]
        public float            m_CascadeRatio1, m_CascadeRatio2, m_CascadeRatio3;
        public Vector3          GetCascadeRatios => new(m_CascadeRatio1, m_CascadeRatio2, m_CascadeRatio3);
        [Range(0.001f, 1.0f)] 
        public float            m_CascadeFade;     
        public CascadeBlendMode m_CascadeBlendMode;
        public bool             m_SoftCascadeBlend;
        
        public enum CascadeBlendMode
        {
            Hard, Soft, Dither
        }
    }
    [Serializable]
    public struct Other
    {
        public TextureSize m_AtlasSize;
    }
    public enum FilterQuality { Low, Medium, High }
}