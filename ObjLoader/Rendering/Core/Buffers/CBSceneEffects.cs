using System.Numerics;
using System.Runtime.InteropServices;

namespace ObjLoader.Rendering.Core.Buffers
{
    [StructLayout(LayoutKind.Sequential, Pack = 16)]
    internal struct CBSceneEffects
    {
        public Vector4 RimParams;
        public Vector4 RimColor;
        public Vector4 OutlineParams;
        public Vector4 OutlineColor;
        public Vector4 FogParams;
        public Vector4 FogColor;
        public Vector4 IblParams;
        public Vector4 SsrParams;
        public Vector4 SsrParams2;
    }
}