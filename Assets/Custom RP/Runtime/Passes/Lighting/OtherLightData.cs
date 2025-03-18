using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

partial class LightingPass
{
    [StructLayout(LayoutKind.Sequential)]
    private struct OtherLightData
    {
        public static OtherLightData CreatePointLight(ref VisibleLight visibleLight, Light light, Vector4 shadowData)
        {
            OtherLightData data;
        
            // color
            // -----
            data.color = visibleLight.finalColor;
        
            // position
            // --------
            data.position = visibleLight.localToWorldMatrix.GetColumn(3);
            data.position.w = 1.0f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        
            // direction and rendering layer mask
            // ----------------------------------
            data.directionAndMask = Vector4.zero;
            data.directionAndMask.w = light.renderingLayerMask.ReinterpretAsFloat();

            // spot angles
            // -----------
            data.spotAngle = new Vector4(0.0f, 1.0f);
        
            // shadow data
            // -----------
            data.shadowData = shadowData;
        
            return data;
        }
        
        public static OtherLightData CreateSpotLight(ref VisibleLight visibleLight, Light light, Vector4 shadowData)
        {
            OtherLightData data;
        
            // color
            // -----
            data.color = visibleLight.finalColor;
        
            // position
            // --------
            data.position = visibleLight.localToWorldMatrix.GetColumn(3);
            data.position.w = 1.0f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        
            // direction and rendering layer mask
            // ----------------------------------
            data.directionAndMask = -visibleLight.localToWorldMatrix.GetColumn(2);
            data.directionAndMask.w = light.renderingLayerMask.ReinterpretAsFloat();

            // spot angles
            // -----------
            float innerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * light.innerSpotAngle);
            float outerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * light.spotAngle);
            float a = 1.0f / Mathf.Max(innerCos - outerCos, 0.001f);
            float b = -outerCos * a;
            data.spotAngle = new Vector4(a, b);
        
            // shadow data
            // -----------
            data.shadowData = shadowData;
        
            return data;
        }
        
        public Vector4 color;
        public Vector4 position;
        public Vector4 directionAndMask;
        public Vector4 spotAngle;
        public Vector4 shadowData;
        
        public const int kStride = 4 * 4 * 5;
    }
}
