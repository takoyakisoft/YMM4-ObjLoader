namespace ObjLoader.Api.DepthBuffer
{
    public interface IDepthBufferApi
    {
        bool IsAvailable { get; }
        DepthBufferHandle? TryAcquireDepthBuffer();
        void ReleaseDepthBuffer(DepthBufferHandle handle);
    }
}