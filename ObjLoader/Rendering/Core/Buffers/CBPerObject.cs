using System.Numerics;
using System.Runtime.InteropServices;

namespace ObjLoader.Rendering.Core.Buffers
{
    [StructLayout(LayoutKind.Sequential, Pack = 16)]
    internal struct CBPerObject
    {
        public Matrix4x4 WorldViewProj;
        public Matrix4x4 World;
    }
}