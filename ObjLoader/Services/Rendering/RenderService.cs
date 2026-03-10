using System.Windows.Media.Imaging;
using ObjLoader.Infrastructure;
using ObjLoader.Rendering.Core;
using ObjLoader.Settings;
using ObjLoader.Services.Textures;
using ObjLoader.Rendering.Managers;
using ObjLoader.Rendering.Managers.Interfaces;
using ObjLoader.Rendering.Renderers;
using Vortice.Direct3D11;
using Vortice.Mathematics;
using System.Numerics;
using Matrix4x4 = System.Numerics.Matrix4x4;
using ObjLoader.Utilities.Logging;
using ObjLoader.Services.Rendering.Device;
using ObjLoader.Services.Rendering.Passes;
using ObjLoader.Services.Shared;
using ObjLoader.Rendering.Core.Buffers;
using ObjLoader.Rendering.Core.Resources;

namespace ObjLoader.Services.Rendering;

internal sealed class RenderService : IDisposable
{
    private const int MaxLayerArrayCapacity = 1024;

    private readonly string _instanceId = Guid.NewGuid().ToString("N").Substring(0, 8);
    private readonly object _bitmapLock = new();

    private readonly RenderDeviceManager _deviceManager;
    private readonly StagingBufferTransfer _stagingTransfer;
    private readonly IRenderPass[] _passes;

    private readonly ITextureService _textureService;
    private readonly IDynamicTextureManager _dynamicTextureManager;

    private D3DResources? _d3dResources;
    private ID3D11Buffer? _gridVertexBuffer;
    private ApiObjectRenderer? _apiObjectRenderer;

    private ConstantBuffer<CBPerFrame>? _cbPerFrame;
    private ConstantBuffer<CBPerObject>? _cbPerObject;
    private ConstantBuffer<CBPerMaterial>? _cbPerMaterial;

    private readonly ID3D11ShaderResourceView[] _nullSrv = new ID3D11ShaderResourceView[1];
    private readonly ID3D11ShaderResourceView[] _nullSrv4 = new ID3D11ShaderResourceView[4];
    private readonly ID3D11Buffer[] _cbPerFrameArray = new ID3D11Buffer[1];
    private readonly ID3D11Buffer[] _cbPerObjectArray = new ID3D11Buffer[1];
    private readonly ID3D11Buffer[] _cbPerMaterialArray = new ID3D11Buffer[1];
    private readonly ID3D11SamplerState[] _samplerArray = new ID3D11SamplerState[1];
    private readonly ID3D11SamplerState[] _shadowSamplerArray = new ID3D11SamplerState[1];
    private readonly ID3D11ShaderResourceView[] _shadowSrvArray = new ID3D11ShaderResourceView[1];

    private readonly List<(int LayerIndex, int PartIndex)> _opaquePartList = new();
    private readonly List<TransparentPart> _transparentParts = new();
    private readonly HashSet<string> _usedPathsBuffer = new();

    private Matrix4x4[]? _cachedLayerWorlds;
    private Matrix4x4[]? _cachedLayerWvps;
    private int _cachedLayerCapacity;

    private byte[]? _stagingBuffer;
    private int _stagingBufferSize;

    private bool _isDisposed = false;
    private string _targetInstanceId = "";

    private LocalDrawManagerAdapter? _localDrawAdapter;
    private ISceneDrawManager? _lastSharedDrawManager;

    public WriteableBitmap? SceneImage { get; private set; }
    public D3DResources? Resources => _d3dResources;
    public ID3D11Device? Device => _deviceManager.Device;

    public RenderService()
    {
        _deviceManager = new RenderDeviceManager(_instanceId);
        _stagingTransfer = new StagingBufferTransfer();
        _passes = [
            new ShadowRenderPass(),
            new GridRenderPass(),
            new OpaqueRenderPass(),
            new TransparentRenderPass(),
            new ApiObjectRenderPass()
        ];

        _textureService = new TextureService();
        _dynamicTextureManager = new DynamicTextureManager(_textureService);
    }

    private string TrackingKey(string resourceName) => $"RenderSvc:{_instanceId}:{resourceName}";

    public void Initialize(string targetInstanceId)
    {
        _targetInstanceId = targetInstanceId;
        if (!_deviceManager.Initialize()) return;

        var device = _deviceManager.Device!;
        _d3dResources = new D3DResources(device);

        float gSize = RenderingConstants.GridSize;
        float[] gridVerts = {
            -gSize, 0, gSize, -gSize, 0, -gSize, gSize, 0, gSize,
            gSize, 0, gSize, -gSize, 0, -gSize, gSize, 0, -gSize
        };
        int gridBufferSize = gridVerts.Length * 4;
        var vDesc = new BufferDescription(gridBufferSize, BindFlags.VertexBuffer, ResourceUsage.Immutable);
        unsafe
        {
            fixed (float* p = gridVerts) _gridVertexBuffer = device.CreateBuffer(vDesc, new SubresourceData(p));
        }

        ResourceTracker.Instance.Register(TrackingKey("GridVertexBuffer"), "ID3D11Buffer:Grid", _gridVertexBuffer, gridBufferSize);

        _apiObjectRenderer?.Dispose();
        _apiObjectRenderer = new ApiObjectRenderer(device, _d3dResources);

        _cbPerFrame = new ConstantBuffer<CBPerFrame>(device);
        _cbPerObject = new ConstantBuffer<CBPerObject>(device);
        _cbPerMaterial = new ConstantBuffer<CBPerMaterial>(device);

        _cbPerFrameArray[0] = _cbPerFrame.Buffer;
        _cbPerObjectArray[0] = _cbPerObject.Buffer;
        _cbPerMaterialArray[0] = _cbPerMaterial.Buffer;

        _samplerArray[0] = _d3dResources.SamplerState;
        _shadowSamplerArray[0] = _d3dResources.ShadowSampler;
    }

    public void Resize(int width, int height)
    {
        var newImage = _deviceManager.Resize(width, height, ref _stagingBuffer, ref _stagingBufferSize);
        if (newImage == null) return;

        _apiObjectRenderer?.Dispose();
        _apiObjectRenderer = null;
        _localDrawAdapter?.Dispose();
        _localDrawAdapter = null;
        _lastSharedDrawManager = null;

        if (_deviceManager.Device != null && _d3dResources != null)
        {
            _apiObjectRenderer = new ApiObjectRenderer(_deviceManager.Device, _d3dResources);
        }

        lock (_bitmapLock)
        {
            SceneImage = newImage;
        }
    }

    private void EnsureLayerArrayCapacity(int count)
    {
        if (_cachedLayerCapacity >= count && _cachedLayerWorlds != null && _cachedLayerWvps != null)
        {
            if (_cachedLayerCapacity > MaxLayerArrayCapacity && count <= MaxLayerArrayCapacity)
            {
                int shrunkCapacity = Math.Max(count, 8);
                _cachedLayerWorlds = new Matrix4x4[shrunkCapacity];
                _cachedLayerWvps = new Matrix4x4[shrunkCapacity];
                _cachedLayerCapacity = shrunkCapacity;
            }
            return;
        }
        int newCapacity = Math.Max(count, 8);
        _cachedLayerWorlds = new Matrix4x4[newCapacity];
        _cachedLayerWvps = new Matrix4x4[newCapacity];
        _cachedLayerCapacity = newCapacity;
    }

    public void Render(
        IReadOnlyList<LayerRenderData> layers,
        Matrix4x4 view,
        Matrix4x4 proj,
        Vector3 camPos,
        System.Windows.Media.Color themeColor,
        bool isWireframe,
        bool isGridVisible,
        bool isInfiniteGrid,
        double gridScale,
        bool isInteracting,
        bool enableShadow = true)
    {
        if (_isDisposed) return;
        if (_deviceManager.Device == null || _deviceManager.Context == null || _deviceManager.Rtv == null || _d3dResources == null || SceneImage == null || _deviceManager.StagingTexture == null) return;

        if (IsDeviceLost()) return;

        try
        {
            RenderInternal(layers, view, proj, camPos, themeColor, isWireframe, isGridVisible, isInfiniteGrid, gridScale, isInteracting, enableShadow);
        }
        catch (SharpGen.Runtime.SharpGenException ex) when (ex.HResult == unchecked((int)0x887A0005) || ex.HResult == unchecked((int)0x887A0006))
        {
            Logger<RenderService>.Instance.Error("Device lost during render", ex);
        }
        
        lock (_bitmapLock)
        {
            if (SceneImage != null && _stagingBuffer != null)
            {
                _stagingTransfer.UpdateBitmapFromStagingBuffer(SceneImage, _stagingBuffer, _deviceManager.ViewportWidth, _deviceManager.ViewportHeight);
            }
        }
    }

    private bool IsDeviceLost()
    {
        if (_deviceManager.Device == null) return true;
        try
        {
            var reason = _deviceManager.Device.DeviceRemovedReason;
            return reason.Failure;
        }
        catch (Exception ex)
        {
            Logger<RenderService>.Instance.Error("Failed to check device status", ex);
            return true;
        }
    }

    private void RenderInternal(
        IReadOnlyList<LayerRenderData> layers,
        Matrix4x4 view,
        Matrix4x4 proj,
        Vector3 camPos,
        System.Windows.Media.Color themeColor,
        bool isWireframe,
        bool isGridVisible,
        bool isInfiniteGrid,
        double gridScale,
        bool isInteracting,
        bool enableShadow)
    {
        var context = _deviceManager.Context!;
        
        if (_cbPerFrameArray[0] == null && _cbPerFrame != null) _cbPerFrameArray[0] = _cbPerFrame.Buffer;
        if (_cbPerObjectArray[0] == null && _cbPerObject != null) _cbPerObjectArray[0] = _cbPerObject.Buffer;
        if (_cbPerMaterialArray[0] == null && _cbPerMaterial != null) _cbPerMaterialArray[0] = _cbPerMaterial.Buffer;

        if (_samplerArray[0] == null) _samplerArray[0] = _d3dResources!.SamplerState;
        if (_shadowSamplerArray[0] == null) _shadowSamplerArray[0] = _d3dResources!.ShadowSampler;

        _usedPathsBuffer.Clear();
        foreach (var layer in layers)
        {
            if (layer.Data?.PartMaterials != null)
            {
                foreach (var pm in layer.Data.PartMaterials.Values)
                {
                    if (!string.IsNullOrEmpty(pm.TexturePath))
                    {
                        _usedPathsBuffer.Add(pm.TexturePath!);
                    }
                }
            }
        }
        if (_deviceManager.Device != null)
        {
            _dynamicTextureManager.Prepare(_usedPathsBuffer, _deviceManager.Device);
        }

        ComputeLayerTransforms(layers, view, proj);

        var (gridColor, axisColor) = PrepareRenderTargets(themeColor, isWireframe);

        ClassifyPartsByOpacity(layers, camPos);

        var dynamicTextures = _dynamicTextureManager.Textures;

        var passContext = new RenderPassContext
        {
            DeviceContext = context,
            Resources = _d3dResources!,
            Layers = layers,
            LayerWorlds = _cachedLayerWorlds!,
            LayerWvps = _cachedLayerWvps!,
            OpaqueParts = _opaquePartList,
            TransparentParts = _transparentParts,
            GridVertexBuffer = _gridVertexBuffer!,
            GridColor = gridColor,
            AxisColor = axisColor,
            EnableShadow = enableShadow,
            ShadowSrvArray = _shadowSrvArray,
            SamplerArray = _samplerArray,
            ShadowSamplerArray = _shadowSamplerArray,
            DynamicTextures = dynamicTextures,
            View = view,
            Proj = proj,
            CamPos = camPos,
            CbPerFrame = _cbPerFrame!,
            CbPerObject = _cbPerObject!,
            CbPerMaterial = _cbPerMaterial!,
            CbPerFrameArray = _cbPerFrameArray,
            CbPerObjectArray = _cbPerObjectArray,
            CbPerMaterialArray = _cbPerMaterialArray,
            IsWireframe = isWireframe,
            IsInteracting = isInteracting,
            IsGridVisible = isGridVisible,
            IsInfiniteGrid = isInfiniteGrid,
            GridScale = gridScale,
            ApiObjectRenderer = (!string.IsNullOrEmpty(_targetInstanceId) && _apiObjectRenderer != null) ? _apiObjectRenderer : null,
            DrawManagerAdapter = !string.IsNullOrEmpty(_targetInstanceId) ? GetOrCreateAdapter() : null,
            MainRtv = _deviceManager.Rtv!,
            MainDsv = _deviceManager.Dsv!,
            ViewportWidth = _deviceManager.ViewportWidth,
            ViewportHeight = _deviceManager.ViewportHeight,
        };

        if (passContext.DrawManagerAdapter != null)
        {
            passContext.DrawManagerAdapter.PurgeStaleEntries();
        }

        foreach(var pass in _passes)
        {
            pass.Render(passContext);
        }

        ClearAllResourceBindings();

        if (_deviceManager.ResolveTexture != null && _deviceManager.RenderTarget != null && _deviceManager.StagingTexture != null && _stagingBuffer != null)
        {
            _stagingTransfer.CopyToStagingBuffer(
                context,
                _deviceManager.ResolveTexture,
                _deviceManager.RenderTarget,
                _deviceManager.StagingTexture,
                _deviceManager.ViewportWidth,
                _deviceManager.ViewportHeight,
                _stagingBuffer);
        }
    }

    private void ComputeLayerTransforms(IReadOnlyList<LayerRenderData> layers, Matrix4x4 view, Matrix4x4 proj)
    {
        var settings = PluginSettings.Instance;
        EnsureLayerArrayCapacity(layers.Count);
        var layerWorlds = _cachedLayerWorlds!;
        var layerWvps = _cachedLayerWvps!;

        Matrix4x4 axisConversion = RenderingConstants.GetAxisConversionMatrix(settings.CoordinateSystem);

        for (int i = 0; i < layers.Count; i++)
        {
            var layer = layers[i];
            var modelResource = layer.Resource;

            Matrix4x4 world;
            if (layer.WorldMatrixOverride.HasValue)
            {
                world = layer.WorldMatrixOverride.Value;
            }
            else
            {
                var normalize = Matrix4x4.CreateTranslation(-modelResource.ModelCenter) * Matrix4x4.CreateScale(modelResource.ModelScale);
                normalize *= Matrix4x4.CreateTranslation(0, (float)layer.HeightOffset, 0);

                float scale = (float)(layer.Scale / 100.0);
                float rx = (float)(layer.Rx * Math.PI / 180.0);
                float ry = (float)(layer.Ry * Math.PI / 180.0);
                float rz = (float)(layer.Rz * Math.PI / 180.0);
                float tx = (float)layer.X;
                float ty = (float)layer.Y;
                float tz = (float)layer.Z;

                var placement = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateRotationZ(rz) * Matrix4x4.CreateRotationX(rx) * Matrix4x4.CreateRotationY(ry) * Matrix4x4.CreateTranslation(tx, ty, tz);

                world = normalize * axisConversion * placement;
            }

            layerWorlds[i] = world;
            layerWvps[i] = world * view * proj;
        }
    }

    private (Vector4 gridColor, Vector4 axisColor) PrepareRenderTargets(System.Windows.Media.Color themeColor, bool isWireframe)
    {
        double brightness = themeColor.R * 0.299 + themeColor.G * 0.587 + themeColor.B * 0.114;
        Color4 clearColor;
        Vector4 gridColor;
        Vector4 axisColor;

        if (brightness < 20)
        {
            clearColor = RenderingConstants.ClearColorDark;
            gridColor = RenderingConstants.GridColorDark;
            axisColor = RenderingConstants.AxisColorDark;
        }
        else if (brightness < 128)
        {
            clearColor = RenderingConstants.ClearColorMedium;
            gridColor = RenderingConstants.GridColorMedium;
            axisColor = RenderingConstants.AxisColorMedium;
        }
        else
        {
            clearColor = RenderingConstants.ClearColorLight;
            gridColor = RenderingConstants.GridColorLight;
            axisColor = RenderingConstants.AxisColorLight;
        }

        var context = _deviceManager.Context!;
        context.OMSetRenderTargets(_deviceManager.Rtv!, _deviceManager.Dsv);
        context.ClearRenderTargetView(_deviceManager.Rtv!, clearColor);
        context.ClearDepthStencilView(_deviceManager.Dsv!, DepthStencilClearFlags.Depth, 1.0f, 0);

        context.RSSetState(isWireframe ? _d3dResources!.WireframeRasterizerState : _d3dResources!.RasterizerState);
        context.OMSetDepthStencilState(_d3dResources.DepthStencilState);
        context.OMSetBlendState(_d3dResources.BlendState, new Color4(0, 0, 0, 0), -1);
        context.RSSetViewport(0, 0, _deviceManager.ViewportWidth, _deviceManager.ViewportHeight);

        return (gridColor, axisColor);
    }

    private void ClassifyPartsByOpacity(IReadOnlyList<LayerRenderData> layers, System.Numerics.Vector3 camPos)
    {
        _opaquePartList.Clear();
        _transparentParts.Clear();

        for (int i = 0; i < layers.Count; i++)
        {
            var layer = layers[i];
            var modelResource = layer.Resource;
            var world = _cachedLayerWorlds![i];

            for (int p = 0; p < modelResource.Parts.Length; p++)
            {
                if (layer.VisibleParts != null && !layer.VisibleParts.Contains(p)) continue;

                var part = modelResource.Parts[p];
                if (part.BaseColor.W < 0.99f)
                {
                    var center = Vector3.Transform(part.Center, world);
                    float distSq = Vector3.DistanceSquared(camPos, center);
                    _transparentParts.Add(new TransparentPart { LayerIndex = i, PartIndex = p, DistanceSq = distSq });
                }
                else
                {
                    _opaquePartList.Add((i, p));
                }
            }
        }
    }

    private void ClearAllResourceBindings()
    {
        var context = _deviceManager.Context;
        if (context == null) return;

        context.OMSetRenderTargets(0, Array.Empty<ID3D11RenderTargetView>(), null);
        context.OMSetBlendState(null, new Color4(0, 0, 0, 0), -1);
        context.OMSetDepthStencilState(null, 0);
        context.RSSetState(null);

        context.PSSetShaderResources(0, _nullSrv4);
        context.VSSetShaderResources(0, _nullSrv4);
    }

    private LocalDrawManagerAdapter? GetOrCreateAdapter()
    {
        var drawManager = SharedResourceRegistry.GetDrawManager(_targetInstanceId);
        if (drawManager == null) return null;
        if (_deviceManager.Device == null) return null;

        if (_localDrawAdapter == null || _lastSharedDrawManager != drawManager)
        {
            _localDrawAdapter?.Dispose();
            _localDrawAdapter = new LocalDrawManagerAdapter(drawManager, _deviceManager.Device);
            _lastSharedDrawManager = drawManager;
        }

        return _localDrawAdapter;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _dynamicTextureManager.Dispose();
        ResourceTracker.Instance.Unregister(TrackingKey("GridVertexBuffer"));

        _d3dResources?.Dispose();
        _d3dResources = null;
        _gridVertexBuffer?.Dispose();
        _gridVertexBuffer = null;

        _cbPerFrame?.Dispose(); _cbPerFrame = null;
        _cbPerObject?.Dispose(); _cbPerObject = null;
        _cbPerMaterial?.Dispose(); _cbPerMaterial = null;

        _localDrawAdapter?.Dispose();
        _localDrawAdapter = null;
        _lastSharedDrawManager = null;

        _apiObjectRenderer?.Dispose();
        _apiObjectRenderer = null;

        _cachedLayerWorlds = null;
        _cachedLayerWvps = null;
        _cachedLayerCapacity = 0;

        _stagingBuffer = null;
        _stagingBufferSize = 0;

        lock (_bitmapLock)
        {
            SceneImage = null;
        }

        _deviceManager.Dispose();
    }
}