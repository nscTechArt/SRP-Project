Shader "Hidden/Custom RP/Camera Renderer"
{

    SubShader
    {
        Cull Off ZTest Always ZWrite Off
        
        HLSLINCLUDE
        #include "Assets/Custom RP/ShaderLibrary/Common.hlsl"
        #include "Includes/CameraRendererPasses.hlsl"
        ENDHLSL

        Pass
        {
            Name "Copy Color"
            
            Blend [_CameraSrcBlend] [_CameraDstBlend]
            
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   DefaultPassVertex
            #pragma fragment BlitPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "Copy Depth"
            
            ColorMask 0
            ZWrite On
            
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   DefaultPassVertex
            #pragma fragment CopyDepthPassFragment
            ENDHLSL
        }
    }
}
