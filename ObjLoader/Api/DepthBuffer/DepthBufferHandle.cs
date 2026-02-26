using Vortice.Direct3D11;

namespace ObjLoader.Api.DepthBuffer
{
    public sealed class DepthBufferHandle
    {
        public ID3D11ShaderResourceView? DepthSrv { get; }
        public int Width { get; }
        public int Height { get; }

        internal DepthBufferHandle(ID3D11ShaderResourceView? srv, int width, int height)
        {
            DepthSrv = srv;
            Width = width;
            Height = height;
        }
    }
}