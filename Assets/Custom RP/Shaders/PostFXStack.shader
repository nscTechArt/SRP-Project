Shader "Hidden/Custom RP/Post FX Stack"
{
    
    SubShader
    {
        Cull Off ZTest Always ZWrite Off
        
        HLSLINCLUDE
        #include "Assets/Custom RP/ShaderLibrary/Common.hlsl"
        #include "Includes/PostFXStackPasses.hlsl"
        ENDHLSL
        
        Pass
        {
            Name "Blit"
            
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   DefaultPassVertex
            #pragma fragment BlitPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "Bloom Horizontal"
            
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   DefaultPassVertex
            #pragma fragment BloomHorizontalPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "Bloom Vertical"
            
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   DefaultPassVertex
            #pragma fragment BloomVerticalPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "Bloom Combine Additive"
            
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   DefaultPassVertex
            #pragma fragment BloomCombineAdditivePassFragment
            ENDHLSL
        }

        Pass
        {
            Name "Bloom Combine Scatter"
            
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   DefaultPassVertex
            #pragma fragment BloomCombineScatterPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "Bloom Prefilter"
            
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   DefaultPassVertex
            #pragma fragment BloomPrefilterPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "Bloom Final Scatter"
            
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   DefaultPassVertex
            #pragma fragment BloomScatterFinalPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "Color Grading None"
            
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   DefaultPassVertex
            #pragma fragment ColorGradingNonePassFragment
            ENDHLSL
        }

        Pass
        {
            Name "Color Grading ACES"
            
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   DefaultPassVertex
            #pragma fragment ColorGradingACESPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "Color Grading Neutral"
            
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   DefaultPassVertex
            #pragma fragment ColorGradingNeutralPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "Color Grading Reinhard"
            
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   DefaultPassVertex
            #pragma fragment ColorGradingReinhardPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "Apply Color Grading"
            
            Blend [_FinalSrcBlend] [_FinalDstBlend]
            
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   DefaultPassVertex
            #pragma fragment ApplyColorGradingPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "Apply Color Grading With Luma"
            
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   DefaultPassVertex
            #pragma fragment ApplyColorGradingWithLumaPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "Final Rescale"
            
            Blend [_FinalSrcBlend] [_FinalDstBlend]
            
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   DefaultPassVertex
            #pragma fragment FinalRescalePassFragment
            ENDHLSL
        }

        Pass
        {
            Name "FXAA"
            
            Blend [_FinalSrcBlend] [_FinalDstBlend]
            
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   DefaultPassVertex
            #pragma fragment FXAAPassFragment
            #pragma multi_compile _ FXAA_QUALITY_MEDIUM FXAA_QUALITY_LOW
            #include "Includes/FXAAPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "FXAA With Luma"
            
            Blend [_FinalSrcBlend] [_FinalDstBlend]
            
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   DefaultPassVertex
            #pragma fragment FXAAPassFragment
            #pragma multi_compile _ FXAA_QUALITY_MEDIUM FXAA_QUALITY_LOW
            #define FXAA_ALPHA_CONTAINS_LUMA
            #include "Includes/FXAAPass.hlsl"
            ENDHLSL
        }
    }
}
