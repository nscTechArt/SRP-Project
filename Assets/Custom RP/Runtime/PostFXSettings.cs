using System;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(menuName = "Rendering/Custom Post FX Settings")]
public class PostFXSettings : ScriptableObject
{
    public static bool AreApplicableTo(Camera camera)
    {
#if UNITY_EDITOR

        if (camera.cameraType == CameraType.SceneView &&
            !SceneView.currentDrawingSceneView.sceneViewState.showImageEffects)
        {
            return false;
        }
#endif
        return camera.cameraType <= CameraType.SceneView;
    }
    
    #region Shader

    [SerializeField] private Shader m_Shader;
    [NonSerialized] private Material mMaterial;
    public Material Material
    {
        get
        {
            if (mMaterial == null && m_Shader != null)
            {
                mMaterial = new Material(m_Shader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
            return mMaterial;
        }
    }

    #endregion

    #region Bloom

    [SerializeField] private BloomSettings m_Bloom = new()
    {
        scatter = 0.7f,
    };
    public BloomSettings Bloom => m_Bloom;
    
    [Serializable]
    public struct BloomSettings
    {
        [Min(0.0f)] public float           intensity;
        [Range(0.0f, 16.0f)] public int    maxIterationCount;
        [Min(1.0f)] public int             minDownscaleRes;
        [Min(0.0f)] public float           threshold;
        [Range(0.0f, 1.0f)] public float   thresholdKnee;
        public Mode                        mode;
        [Range(0.05f, 0.95f)] public float scatter;
        
        public enum Mode { Additive, Scattering }
    }

    #endregion

    #region Color Adjustments

    [SerializeField] private ColorAdjustmentsSettings m_ColorAdjustments = new()
    {
        colorFilter = Color.white,
    };
    public ColorAdjustmentsSettings ColorAdjustments => m_ColorAdjustments;
    
    [Serializable]
    public struct ColorAdjustmentsSettings
    {
        public float                          postExposure;
        [Range(-100.0f, 100.0f)] public float contrast;
        [ColorUsage(false, true)] 
        public Color                          colorFilter;
        [Range(-180.0f, 180.0f)] public float hueShift;
        [Range(-100.0f, 100.0f)] public float saturation;
    }

    #endregion

    #region White Balance

    [SerializeField] private WhiteBalanceSettings m_WhiteBalance;
    public WhiteBalanceSettings WhiteBalance => m_WhiteBalance;
    
    [Serializable]
    public struct WhiteBalanceSettings
    {
        [Range(-100.0f, 100.0f)] public float temperature;
        [Range(-100.0f, 100.0f)] public float tint;
    }
    
    #endregion

    #region Split Toning

    [SerializeField] private SplitToningSettings m_SplitToning = new()
    {
        shadows = Color.gray,
        highlights = Color.gray,
    };
    public SplitToningSettings SplitToning => m_SplitToning;
    
    [Serializable]
    public struct SplitToningSettings
    {
        [ColorUsage(false)] public Color shadows;
        [ColorUsage(false)] public Color highlights;
        [Range(-100.0f, 100.0f)] public float     balance;
    }

    #endregion

    #region Channel Mixer

    [SerializeField] private ChannelMixerSettings m_ChannelMixer = new ()
    {
        red = Vector3.right,
        green = Vector3.up,
        blue = Vector3.forward,
    };
    public ChannelMixerSettings ChannelMixer => m_ChannelMixer;
    
    [Serializable]
    public struct ChannelMixerSettings
    {
        public Vector3 red;
        public Vector3 green;
        public Vector3 blue;
    }

    #endregion

    #region Shadows Midtons Highlights

    [SerializeField] private ShadowsMidtonesHighlightsSettings m_ShadowsMidtonesHighlights = new()
    {
        shadows         = Color.white,
        midtones        = Color.white,
        highlights      = Color.white,
        shadowsEnd      = 0.3f,
        highlightsStart = 0.55f,
        highlightsEnd   = 1.0f
    };
    public ShadowsMidtonesHighlightsSettings ShadowsMidtonesHighlights => m_ShadowsMidtonesHighlights;
    
    [Serializable]
    public struct ShadowsMidtonesHighlightsSettings
    {
        [ColorUsage(false, true)]
        public Color shadows, midtones, highlights;
        [Range(0.0f, 2.0f)] public float shadowsStart;
        [Range(0.0f, 2.0f)] public float shadowsEnd;
        [Range(0.0f, 2.0f)] public float highlightsStart;
        [Range(0.0f, 2.0f)] public float highlightsEnd;
    }

    #endregion

    #region Tone Mapping

    [SerializeField] private ToneMappingSettings m_ToneMapping;
    public ToneMappingSettings ToneMapping => m_ToneMapping;
    
    [Serializable]
    public struct ToneMappingSettings
    {
        public Mode mode;
        
        public enum Mode { None, ACES, Neutral, Reinhard}
    }

    #endregion
}

