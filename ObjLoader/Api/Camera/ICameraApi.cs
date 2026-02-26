using ObjLoader.Api.Core;

namespace ObjLoader.Api.Camera
{
    public interface ICameraApi
    {
        CameraState GetState();
        void SetTransform(in Transform transform);
        void RequestSmoothMove(in CameraTransition transition);
        IDisposable Subscribe(Action<CameraState> onChanged);
    }
}