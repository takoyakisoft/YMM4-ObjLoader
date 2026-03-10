using System.Numerics;
using System.Runtime.InteropServices;

namespace ObjLoader.Rendering.Core.Buffers
{
    [StructLayout(LayoutKind.Sequential, Pack = 16)]
    internal struct CBPostEffects
    {
        public Vector4 ColorCorrParams;
        public Vector4 VignetteParams;
        public Vector4 VignetteColor;
        public Vector4 ScanlineParams;
        public Vector4 ChromAbParams;
        public Vector4 MonoParams;
        public Vector4 MonoColor;
        public Vector4 PosterizeParams;
    }
}
