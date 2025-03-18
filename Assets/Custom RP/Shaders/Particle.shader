Shader "Custom RP/Particles/Unlit"
{
    Properties
    {
        _BaseMap("Texture", 2D) = "white" {}
        _BaseColor("Color", Color) = (1.0, 1.0, 1.0, 1.0)
        [Toggle(_VERTEX_COLORS)] _VertexColors ("Vertex Colors", Float) = 0
        
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite ("Z Write", Float) = 1
    }
    
    SubShader
    {
        Pass
        {
            Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
            
            ZWrite [_ZWrite]
            
            HLSLPROGRAM
            #pragma shader_feature _VERTEX_COLORS
            #pragma vertex UnlitPassVertex
            #pragma fragment UnlitPassFragment
            #include "Includes/UnlitPass.hlsl"
            ENDHLSL
        }
    }
}