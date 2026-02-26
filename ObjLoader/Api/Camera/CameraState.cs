using System.Numerics;

namespace ObjLoader.Api.Camera
{
    public readonly struct CameraState
    {
        public Vector3 Position { get; }
        public Vector3 Target { get; }
        public float FovDegrees { get; }

        public CameraState(Vector3 position, Vector3 target, float fovDegrees)
        {
            Position = position;
            Target = target;
            FovDegrees = fovDegrees;
        }
    }
}