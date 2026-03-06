using System.Numerics;
using Vortice.Direct2D1;

namespace ObjLoader.Api.Draw
{
    public sealed class BillboardDescriptor
    {
        public ID2D1Image? Image { get; set; }
        public Vector3 WorldPosition { get; set; }
        public Vector2 Size { get; set; } = Vector2.One;
        public int WorldId { get; set; } = 0;
        public bool FaceCamera { get; set; } = true;
        public float Opacity { get; set; } = 1.0f;
        public Vector3 Rotation { get; set; } = Vector3.Zero;
        public Vector4 BlendColor { get; set; } = Vector4.One;
    }
}