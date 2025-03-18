using System;
using UnityEngine;

[Serializable]
public class CameraBufferSettings
{
    public bool m_AllowHDR;
    public bool m_CopyColor;
    public bool m_CopyDepth;
    [Range(0.1f, 2.0f)] 
    public float m_RenderScale;
    public BicubicRescalingMode m_BicubicRescaling;
    public FXAA m_FXAA;

    public enum BicubicRescalingMode
    {
        Off, UpOnly, UpAndDown
    }
    [Serializable]
    public struct FXAA
    {
        public bool enabled;
        [Range(0.0312f, 0.0833f)]
        public float fixedThreshold;
        [Range(0.063f, 0.333f)]
        public float relativeThreshold;
        [Range(0.0f, 1.0f)]
        public float subpixelBlending;
        public Quality quality;
        
        public enum Quality
        {
            Low, Medium, High
        }
    }
}
