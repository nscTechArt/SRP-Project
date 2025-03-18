using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

partial class LightingPass
{
    [StructLayout(LayoutKind.Sequential)]
    private struct DirectionalLightData
    {
        public DirectionalLightData (ref VisibleLight visibleLight, Light light, Vector4 shadowData)
        {
            // color
            // -----
            color = visibleLight.finalColor;
            
            // direction and rendering layer mask
            // ----------------------------------
            directionAndMask = -visibleLight.localToWorldMatrix.GetColumn(2);
            directionAndMask.w = light.renderingLayerMask.ReinterpretAsFloat();
            
            // shadow data
            // -----------
            this.shadowData = shadowData;
        }
        
        public Vector4 color;
        public Vector4 directionAndMask;
        public Vector4 shadowData;
        
        public const int kStride = 4 * 4 * 3;
    }
}
