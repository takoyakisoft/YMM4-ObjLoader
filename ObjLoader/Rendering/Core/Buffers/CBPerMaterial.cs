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
        public Vector4 PbrParams;
    }
}