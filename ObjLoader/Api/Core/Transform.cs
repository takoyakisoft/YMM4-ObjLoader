using System.Numerics;

namespace ObjLoader.Api.Core
{
    public readonly struct Transform
    {
        public Vector3 Position { get; }
        public Vector3 RotationEulerDegrees { get; }
        public Vector3 Scale { get; }

        public Transform(Vector3 position, Vector3 rotationEulerDegrees, Vector3 scale)
        {
            Position = position;
            RotationEulerDegrees = rotationEulerDegrees;
            Scale = scale;
        }

        public static readonly Transform Identity = new(Vector3.Zero, Vector3.Zero, Vector3.One);

        public Matrix4x4 ToMatrix()
        {
            float rx = RotationEulerDegrees.X * MathF.PI / 180f;
            float ry = RotationEulerDegrees.Y * MathF.PI / 180f;
            float rz = RotationEulerDegrees.Z * MathF.PI / 180f;
            return Matrix4x4.CreateScale(Scale)
                 * Matrix4x4.CreateRotationZ(rz)
                 * Matrix4x4.CreateRotationX(rx)
                 * Matrix4x4.CreateRotationY(ry)
                 * Matrix4x4.CreateTranslation(Position);
        }
    }
}