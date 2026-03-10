using System.Numerics;
using System.Runtime.InteropServices;

namespace ObjLoader.Rendering.Core.Buffers
{
    [StructLayout(LayoutKind.Sequential, Pack = 16)]
    internal struct CBPerFrame
    {
        public Matrix4x4 ViewProj;
        public Matrix4x4 InverseViewProj;
        public Vector4 CameraPos;
        public Vector4 LightPos;
        public Vector4 AmbientColor;
        public Vector4 LightColor;
        public Vector4 GridColor;
        public Vector4 GridAxisColor;
        public Matrix4x4 LightViewProj0;
        public Matrix4x4 LightViewProj1;
        public Matrix4x4 LightViewProj2;
        public Vector4 LightTypeParams;
        public Vector4 ShadowParams;
        public Vector4 CascadeSplits;
        public Vector4 EnvironmentParam;
        public Vector4 PcssParams;
    }
}