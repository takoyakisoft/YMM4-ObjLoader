using ObjLoader.Api.Core;

namespace ObjLoader.Api.Camera
{
    public sealed class CameraTransition
    {
        public Transform Target { get; }
        public TimeSpan Duration { get; }
        public EasingType Easing { get; }

        public CameraTransition(Transform target, TimeSpan duration, EasingType easing)
        {
            Target = target;
            Duration = duration;
            Easing = easing;
        }
    }
}