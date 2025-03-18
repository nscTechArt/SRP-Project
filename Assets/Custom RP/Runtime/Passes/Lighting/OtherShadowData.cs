using System.Runtime.InteropServices;
using UnityEngine;

partial class Shadows
{
    [StructLayout(LayoutKind.Sequential)]
    private struct OtherShadowData
    {
        public OtherShadowData(Vector2 offset, float scale, float bias, float border, Matrix4x4 matrix)
        {
            tileData.x = offset.x * scale + border;
            tileData.y = offset.y * scale + border;
            tileData.z = scale - border - border;
            tileData.w = bias;
            shadowMatrix = matrix;
        }
        
        public Vector4   tileData;
        public Matrix4x4 shadowMatrix;

        public const int kStride = 4 * 4 + 4 * 16;
    }
}
