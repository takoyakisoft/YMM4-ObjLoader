using ObjLoader.Rendering.Managers.Interfaces;
using Vortice.Direct3D11;
using Vortice.DXGI;
using YukkuriMovieMaker.Commons;
using D2D = Vortice.Direct2D1;

namespace ObjLoader.Rendering.Managers
{
    internal sealed class RenderTargetManager : IRenderTargetManager
    {
        private ID3D11Texture2D? _renderTargetTexture;
        private ID3D11RenderTargetView? _renderTargetView;
        private ID3D11Texture2D? _depthStencilTexture;
        private ID3D11DepthStencilView? _depthStencilView;
        private ID3D11Texture2D? _depthCopyTexture;
        private ID3D11ShaderResourceView? _depthCopySRV;
        private D2D.ID2D1Bitmap1? _sharedBitmap;

        private readonly object _lock = new object();
        private int _width;
        private int _height;
        private bool _disposed;

        public ID3D11Texture2D? RenderTargetTexture => _renderTargetTexture;
        public ID3D11RenderTargetView? RenderTargetView => _renderTargetView;
        public ID3D11Texture2D? DepthStencilTexture => _depthStencilTexture;
        public ID3D11DepthStencilView? DepthStencilView => _depthStencilView;
        public ID3D11ShaderResourceView? DepthCopySRV => _depthCopySRV;
        public D2D.ID2D1Bitmap1? SharedBitmap => _sharedBitmap;

        public bool EnsureSize(IGraphicsDevicesAndContext devices, int width, int height)
        {
            if (devices == null) throw new ArgumentNullException(nameof(devices));
            if (width < 1 || height < 1) return false;

            lock (_lock)
            {
                if (_disposed) return false;

                if (_renderTargetView != null && _width == width && _height == height)
                {
                    return false;
                }

                DisposeResources();

                _width = width;
                _height = height;

                var device = devices.D3D.Device;

                _renderTargetTexture = CreateRenderTargetTexture(device, width, height);
                _renderTargetView = device.CreateRenderTargetView(_renderTargetTexture);

                _depthStencilTexture = CreateDepthStencilTexture(device, width, height);
                _depthStencilView = CreateDepthStencilView(device, _depthStencilTexture);

                _depthCopyTexture = CreateDepthCopyTexture(device, width, height);
                _depthCopySRV = CreateDepthCopySRV(device, _depthCopyTexture);

                _sharedBitmap = CreateSharedBitmap(devices, _renderTargetTexture);

                return true;
            }
        }

        public void CopyDepthBuffer(ID3D11DeviceContext context)
        {
            if (_depthCopyTexture == null || _depthStencilTexture == null) return;
            context.CopyResource(_depthCopyTexture, _depthStencilTexture);
        }

        private static ID3D11Texture2D CreateRenderTargetTexture(ID3D11Device device, int width, int height)
        {
            var texDesc = new Texture2DDescription
            {
                Width = width,
                Height = height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                CPUAccessFlags = CpuAccessFlags.None,
                MiscFlags = ResourceOptionFlags.None
            };
            return device.CreateTexture2D(texDesc);
        }

        private static ID3D11Texture2D CreateDepthStencilTexture(ID3D11Device device, int width, int height)
        {
            var texDesc = new Texture2DDescription
            {
                Width = width,
                Height = height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.R24G8_Typeless,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.DepthStencil,
                CPUAccessFlags = CpuAccessFlags.None,
                MiscFlags = ResourceOptionFlags.None
            };
            return device.CreateTexture2D(texDesc);
        }

        private static ID3D11DepthStencilView CreateDepthStencilView(ID3D11Device device, ID3D11Texture2D texture)
        {
            var dsvDesc = new DepthStencilViewDescription
            {
                Format = Format.D24_UNorm_S8_UInt,
                ViewDimension = DepthStencilViewDimension.Texture2D,
                Texture2D = new Texture2DDepthStencilView { MipSlice = 0 }
            };
            return device.CreateDepthStencilView(texture, dsvDesc);
        }

        private static ID3D11Texture2D CreateDepthCopyTexture(ID3D11Device device, int width, int height)
        {
            var texDesc = new Texture2DDescription
            {
                Width = width,
                Height = height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.R24G8_Typeless,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.ShaderResource,
                CPUAccessFlags = CpuAccessFlags.None,
                MiscFlags = ResourceOptionFlags.None
            };
            return device.CreateTexture2D(texDesc);
        }

        private static ID3D11ShaderResourceView CreateDepthCopySRV(ID3D11Device device, ID3D11Texture2D texture)
        {
            var srvDesc = new ShaderResourceViewDescription
            {
                Format = Format.R24_UNorm_X8_Typeless,
                ViewDimension = Vortice.Direct3D.ShaderResourceViewDimension.Texture2D,
                Texture2D = new Texture2DShaderResourceView { MipLevels = 1, MostDetailedMip = 0 }
            };
            return device.CreateShaderResourceView(texture, srvDesc);
        }

        private static D2D.ID2D1Bitmap1 CreateSharedBitmap(IGraphicsDevicesAndContext devices, ID3D11Texture2D renderTargetTexture)
        {
            using var surface = renderTargetTexture.QueryInterface<IDXGISurface>();
            var bmpProps = new D2D.BitmapProperties1(
                new Vortice.DCommon.PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
                96, 96, D2D.BitmapOptions.Target);

            return devices.DeviceContext.CreateBitmapFromDxgiSurface(surface, bmpProps);
        }

        private void DisposeResources()
        {
            SafeDispose(ref _sharedBitmap);
            SafeDispose(ref _depthCopySRV);
            SafeDispose(ref _depthCopyTexture);
            SafeDispose(ref _depthStencilView);
            SafeDispose(ref _depthStencilTexture);
            SafeDispose(ref _renderTargetView);
            SafeDispose(ref _renderTargetTexture);
        }

        private static void SafeDispose<T>(ref T? disposable) where T : class, IDisposable
        {
            var temp = disposable;
            disposable = null;
            if (temp != null)
            {
                try
                {
                    temp.Dispose();
                }
                catch
                {
                }
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed) return;
                _disposed = true;
                DisposeResources();
            }
        }
    }
}