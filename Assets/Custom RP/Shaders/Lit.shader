Shader "Custom RP/Lit"
{
    Properties
    {
        _BaseMap("Texture", 2D) = "white" {}
        _BaseColor("Color", Color) = (0.5, 0.5, 0.5, 1.0)
        
        [Toggle(_MASK_MAP)] _MaskMapToggle("Mask Map", Float) = 0
        [NoScaleOffset] _MaskMap("Mask (MODS)", 2D) = "white" {}
        _Metallic("Metallic", Range(0, 1)) = 0.0
        _Occlusion("Occlusion", Range(0, 1)) = 1.0
        _Smoothness("Smoothness", Range(0, 1)) = 0.5
        _Fresnel("Fresnel", Range(0, 1)) = 0.5
        _Reflectance("Reflectance", Range(0, 1)) = 0.5
        
        [Toggle(_NORMAL_MAP)] _NormalMapToggle("Normal Map", Float) = 0
        [NoScaleOffset] _NormalMap("Normals", 2D) = "bump" {}
        _NormalScale("Normal Scale", Range(0, 1)) = 1.0
        
        [NoScaleOffset] _EmissionMap("Emission", 2D) = "white" {}
        [HDR] _EmissionColor("Emission", Color) = (0, 0, 0, 0)
        
        [Toggle(_DETAIL_MAP)] _DetailMapToggle("Detail Map", Float) = 0
        _DetailMap("Details", 2D) = "linearGrey" {}
        [NoScaleOffset] _DetailNormalMap("Detail Normals", 2D) = "bump" {}
        _DetailAlbedo("Detail Albedo", Range(0, 1)) = 1.0
        _DetailSmoothness("Detail Smoothness", Range(0, 1)) = 1.0
        _DetailNormalScale("Detail Normal Scale", Range(0, 1)) = 1.0
    }
    
    SubShader
    {
        Pass // CustomLit Pass
        {
            Name "CustomLit"
            
            Tags { "LightMode" = "CustomLit" }
            
            HLSLPROGRAM
            #pragma target 4.5

            #pragma shader_feature _NORMAL_MAP
            #pragma shader_feature _MASK_MAP
            #pragma shader_feature _DETAIL_MAP
            
            #pragma multi_compile _ _SHADOW_FILTER_MIDUM _SHADOW_FILTER_HIGH
            #pragma multi_compile _ _SOFT_CASCADE_BLEND

            #pragma multi_compile _ _SHADOW_MASK_ALWAYS _SHADOW_MASK_DISTANCE
            #pragma multi_compile _ LIGHTMAP_ON
            
            #pragma vertex   LitPassVertex
            #pragma fragment LitPassFragment

            #include "../ShaderLibrary/Common.hlsl"
            #include "Includes/LitInput.hlsl"
            #include "Includes/LitPass.hlsl"
            
            ENDHLSL
        }

        Pass // ShadowCaster Pass
        {
            Name "ShadowCaster"
            
            Tags { "LightMode" = "ShadowCaster" }
            
            ColorMask 0
            
            HLSLPROGRAM
            
            #pragma target 3.5
            
            #pragma vertex   ShadowCasterPassVertex
            #pragma fragment ShadowCasterPassFragment

            #include "../ShaderLibrary/Common.hlsl"
            #include "Includes/LitInput.hlsl"
            #include "Includes/ShadowCasterPass.hlsl" 
            ENDHLSL
        }

        Pass // Meta Pass
        {
            Name "Meta"
            
            Tags { "LightMode" = "Meta" }
            
            Cull Off
            
            HLSLPROGRAM
            
            #pragma target 3.5
            
            #pragma vertex   MetaPassVertex
            #pragma fragment MetaPassFragment

            #include "../ShaderLibrary/Common.hlsl"
            #include "Includes/LitInput.hlsl"
            #include "Includes/MetaPass.hlsl" 
            ENDHLSL
        }
    }

    CustomEditor "CustomShaderGUI"
}