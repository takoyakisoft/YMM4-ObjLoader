using System.Numerics;
using System.Runtime.InteropServices;

namespace ObjLoader.Rendering.Core.Buffers
{
    [StructLayout(LayoutKind.Sequential, Pack = 16)]
    internal struct CBPerMaterial
    {
        public Vector4 BaseColor;
        public float LightEnabled;
        public float DiffuseIntensity;
        public float SpecularIntensity;
        public float Shininess;
        public Vector4 ToonParams;
        public Vector4 RimParams;
        public Vector4 RimColor;
        public Vector4 OutlineParams;
        public Vector4 OutlineColor;
        public Vector4 FogParams;
        public Vector4 FogColor;
        public Vector4 ColorCorrParams;
        public Vector4 VignetteParams;
        public Vector4 VignetteColor;
        public Vector4 ScanlineParams;
        public Vector4 ChromAbParams;
        public Vector4 MonoParams;
        public Vector4 MonoColor;
        public Vector4 PosterizeParams;
        public Vector4 PbrParams;
        public Vector4 IblParams;
        public Vector4 SsrParams;
        public Vector4 SsrParams2;
    }
}