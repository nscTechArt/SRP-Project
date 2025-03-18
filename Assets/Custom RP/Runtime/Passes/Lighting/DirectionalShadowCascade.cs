using System.Runtime.InteropServices;
using UnityEngine;

partial class Shadows
{
    [StructLayout(LayoutKind.Sequential)]
    private struct DirectionalShadowCascade
    {
        public DirectionalShadowCascade(Vector4 cullingSphere, float tileSize, float filterSize)
        {
            float texelSize = 2.0f * cullingSphere.w / tileSize;
            filterSize *= texelSize;
            cullingSphere.w -= filterSize;
            cullingSphere.w *= cullingSphere.w;
            this.cullingSphere = cullingSphere;
            data = new Vector4(1.0f / cullingSphere.w, filterSize * 1.4142136f);
        }
        
        public Vector4 cullingSphere;
        public Vector4 data;

        public const int kStride = 4 * 4 * 2;
    }
}
