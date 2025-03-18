Shader "Hidden/Custom RP/Camera Debugger"
{
    SubShader
    {
        Cull Off ZTest Always ZWrite Off
        
        HLSLINCLUDE
        #include "Assets/Custom RP/ShaderLibrary/Common.hlsl"
        #include "Includes/CameraDebuggerPasses.hlsl"
        ENDHLSL

        Pass
        {
            Name "Forward+ Tiles"
            
            Blend SrcAlpha OneMinusSrcAlpha
            
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex   DefaultPassVertex
            #pragma fragment ForwardPlusTilesPassFragment
            ENDHLSL
        }
    }
}
