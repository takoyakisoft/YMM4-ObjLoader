using Vortice.Direct3D11;

namespace ObjLoader.Api.DepthBuffer
{
    internal sealed class DepthBufferApi : IDepthBufferApi
    {
        private readonly Func<(ID3D11Texture2D? Texture, int Width, int Height)> _depthTextureProvider;
        private readonly Func<ID3D11ShaderResourceView?> _depthSrvProvider;
        private int _acquireCount;

        internal DepthBufferApi(
            Func<(ID3D11Texture2D? Texture, int Width, int Height)> depthTextureProvider,
            Func<ID3D11ShaderResourceView?> depthSrvProvider)
        {
            _depthTextureProvider = depthTextureProvider ?? throw new ArgumentNullException(nameof(depthTextureProvider));
            _depthSrvProvider = depthSrvProvider ?? throw new ArgumentNullException(nameof(depthSrvProvider));
        }

        public bool IsAvailable
        {
            get
            {
                var (tex, _, _) = _depthTextureProvider();
                return tex != null;
            }
        }

        public DepthBufferHandle? TryAcquireDepthBuffer()
        {
            var (tex, w, h) = _depthTextureProvider();
            if (tex == null) return null;

            var srv = _depthSrvProvider();
            if (srv == null) return null;

            Interlocked.Increment(ref _acquireCount);
            return new DepthBufferHandle(srv, w, h);
        }

        public void ReleaseDepthBuffer(DepthBufferHandle handle)
        {
            if (handle?.DepthSrv == null) return;
            Interlocked.Decrement(ref _acquireCount);
        }
    }
}