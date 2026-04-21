using ObjLoader.Api;
using ObjLoader.Api.Core;
using ObjLoader.Api.Draw;
using ObjLoader.Rendering.Core;
using ObjLoader.VideoEffect;
using System.Numerics;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.Direct3D11;
using Vortice.DXGI;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using AlphaMode = Vortice.DCommon.AlphaMode;

internal class SceneIntegrationVideoEffectProcessor : IVideoEffectProcessor
{
    private readonly SceneIntegrationVideoEffect item;
    private readonly IGraphicsDevicesAndContext devices;
    private volatile ID2D1Image? input;
    private SceneObjectId? objectId;
    private readonly Lock syncLock = new();
    private volatile ISceneServices? _cachedServices;
    private readonly nint _cachedDevicePointer;
    private volatile bool _disposed;
    private volatile BillboardDescriptor? _lastSnapshot;
    private ID2D1Image? lastSizedImage;
    private Vector2 lastImageSize;
    private volatile ID2D1Bitmap1? _stagingBitmap;
    private volatile ID2D1Bitmap1? _expiredStagingBitmap;
    private int _stagingW;
    private int _stagingH;
    private nint _stagingSharedHandle;

    public ID2D1Image Output => input ?? throw new NullReferenceException(nameof(input) + " is null");

    public SceneIntegrationVideoEffectProcessor(SceneIntegrationVideoEffect item, IGraphicsDevicesAndContext devices)
    {
        this.item = item;
        this.devices = devices;
        _cachedDevicePointer = devices.D3D?.Device?.NativePointer ?? IntPtr.Zero;
        ObjLoaderApi.SceneRegistrationChanged += OnSceneRegistrationChanged;
        item.X.PropertyChanged += OnPropertyChanged;
        item.Y.PropertyChanged += OnPropertyChanged;
        item.Z.PropertyChanged += OnPropertyChanged;
        item.Scale.PropertyChanged += OnPropertyChanged;
        item.ScaleX.PropertyChanged += OnPropertyChanged;
        item.ScaleY.PropertyChanged += OnPropertyChanged;
        item.RotationX.PropertyChanged += OnPropertyChanged;
        item.RotationY.PropertyChanged += OnPropertyChanged;
        item.RotationZ.PropertyChanged += OnPropertyChanged;
        item.Opacity.PropertyChanged += OnPropertyChanged;
        item.PropertyChanged += OnPropertyChanged;
    }

    private void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        item.TriggerUpdate();
    }

    public DrawDescription Update(EffectDescription effectDescription)
    {
        if (_disposed) return effectDescription.DrawDescription with { Opacity = 0 };

        var currentInput = input;
        if (currentInput == null)
        {
            TryRemoveBillboard();
            return effectDescription.DrawDescription with { Opacity = 0 };
        }

        var frame = effectDescription.ItemPosition.Frame;
        var length = effectDescription.ItemDuration.Frame <= 0 ? 1 : effectDescription.ItemDuration.Frame;
        var fps = effectDescription.FPS <= 0 ? 60 : effectDescription.FPS;

        var x = (float)item.X.GetValue(frame, length, fps);
        var y = (float)item.Y.GetValue(frame, length, fps);
        var z = (float)item.Z.GetValue(frame, length, fps);
        var scale = (float)item.Scale.GetValue(frame, length, fps);
        var scaleX = (float)item.ScaleX.GetValue(frame, length, fps);
        var scaleY = (float)item.ScaleY.GetValue(frame, length, fps);
        var rotX = (float)item.RotationX.GetValue(frame, length, fps);
        var rotY = (float)item.RotationY.GetValue(frame, length, fps);
        var rotZ = (float)item.RotationZ.GetValue(frame, length, fps);
        var opacity = (float)item.Opacity.GetValue(frame, length, fps);

        var baseSize = GetImageSize(currentInput);
        var size = new Vector2(-(baseSize.X * 0.01f * scale * scaleX), baseSize.Y * 0.01f * scale * scaleY);

        TryPreRenderToStaging(currentInput, baseSize);

        var stageImage = (ID2D1Image?)_stagingBitmap;
        if (stageImage == null)
        {
            TryRemoveBillboard();
            return effectDescription.DrawDescription with { Opacity = 0 };
        }

        var descriptor = new BillboardDescriptor
        {
            Image = stageImage,
            SharedHandle = _stagingSharedHandle,
            SharedWidth = _stagingW,
            SharedHeight = _stagingH,
            WorldPosition = new Vector3(x, y, z),
            Size = size,
            Rotation = new Vector3(rotX, rotY, rotZ),
            FaceCamera = item.FaceCamera,
            Opacity = opacity
        };

        _lastSnapshot = descriptor;

        var services = GetSceneServices();
        bool triggerNeeded = false;

        if (services?.Draw != null)
        {
            lock (syncLock)
            {
                triggerNeeded = ApplyBillboard(services, descriptor);
            }
        }

        if (triggerNeeded)
        {
            item.TriggerUpdate();
            services?.TriggerUpdate();
        }

        return effectDescription.DrawDescription with { Opacity = 0 };
    }

    private void TryPreRenderToStaging(ID2D1Image source, Vector2 baseSize)
    {
        int w = Math.Max(1, (int)Math.Ceiling((double)baseSize.X));
        int h = Math.Max(1, (int)Math.Ceiling((double)baseSize.Y));

        bool hasStagingAlready = _stagingBitmap != null && _stagingSharedHandle != IntPtr.Zero;
        bool lockTaken = false;
        if (hasStagingAlready)
        {
            Monitor.TryEnter(ObjLoaderSource.SharedRenderLock, 0, ref lockTaken);
            if (!lockTaken) return;
        }
        else
        {
            Monitor.Enter(ObjLoaderSource.SharedRenderLock, ref lockTaken);
        }
        try
        {
            var expired = Interlocked.Exchange(ref _expiredStagingBitmap, null);
            expired?.Dispose();

            Vortice.RawRectF bounds = default;
            bool hasBounds = false;
            try
            {
                bounds = devices.DeviceContext.GetImageLocalBounds(source);
                hasBounds = true;
            }
            catch { }

            if (hasBounds)
            {
                int bw = (int)Math.Ceiling((double)(bounds.Right - bounds.Left));
                int bh = (int)Math.Ceiling((double)(bounds.Bottom - bounds.Top));
                if (bw > 0) w = bw;
                if (bh > 0) h = bh;
            }

            if (w <= 0 || h <= 0) return;

            var current = _stagingBitmap;
            if (current != null && (_stagingW != w || _stagingH != h))
            {
                Interlocked.Exchange(ref _expiredStagingBitmap, current);
                _stagingBitmap = null;
                _stagingSharedHandle = IntPtr.Zero;
                current = null;
            }

            if (current == null)
            {
                try
                {
                    nint sharedHandle;
                    current = CreateStagingBitmap(w, h, out sharedHandle);
                    _stagingW = w;
                    _stagingH = h;
                    _stagingSharedHandle = sharedHandle;
                    _stagingBitmap = current;
                }
                catch { return; }
            }

            var d2d = devices.DeviceContext;
            var oldTarget = d2d.Target;
            var oldTransform = d2d.Transform;
            try
            {
                d2d.Target = current;
                d2d.BeginDraw();
                d2d.Clear(null);
                if (hasBounds)
                    d2d.Transform = System.Numerics.Matrix3x2.CreateTranslation(-bounds.Left, -bounds.Top);
                d2d.DrawImage(source);
                d2d.EndDraw();
            }
            catch
            {
                try { d2d.EndDraw(); } catch { }
            }
            finally
            {
                try { d2d.Target = oldTarget; } catch { try { d2d.Target = null; } catch { } }
                try { d2d.Transform = oldTransform; } catch { }
            }
        }
        finally
        {
            if (lockTaken) Monitor.Exit(ObjLoaderSource.SharedRenderLock);
        }
    }

    private ID2D1Bitmap1 CreateStagingBitmap(int w, int h, out nint sharedHandle)
    {
        var texDesc = new Texture2DDescription
        {
            Width = w,
            Height = h,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Default,
            BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
            CPUAccessFlags = CpuAccessFlags.None,
            MiscFlags = ResourceOptionFlags.Shared
        };
        using var tex = devices.D3D.Device.CreateTexture2D(texDesc);
        using var dxgiResource = tex.QueryInterface<IDXGIResource>();
        sharedHandle = dxgiResource.SharedHandle;
        using var surface = tex.QueryInterface<IDXGISurface>();
        var bitmapProps = new BitmapProperties1(
            new PixelFormat(Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied),
            96.0f, 96.0f,
            BitmapOptions.Target);
        return devices.DeviceContext.CreateBitmapFromDxgiSurface(surface, bitmapProps);
    }

    private bool ApplyBillboard(ISceneServices services, BillboardDescriptor descriptor)
    {
        if (services.Draw == null) return false;

        try
        {
            if (!objectId.HasValue)
            {
                objectId = services.Draw.CreateDynamicBillboard(descriptor);
                return true;
            }

            if (!services.Draw.UpdateBillboard(objectId.Value, descriptor))
            {
                objectId = services.Draw.CreateDynamicBillboard(descriptor);
                return true;
            }

            return false;
        }
        catch
        {
            objectId = null;
            return false;
        }
    }

    private void TryRemoveBillboard()
    {
        SceneObjectId? removedId;
        lock (syncLock)
        {
            removedId = objectId;
            if (!removedId.HasValue) return;
            objectId = null;
        }

        ISceneServices? services = null;
        try
        {
            services = GetSceneServices();
            services?.Draw?.RemoveBillboard(removedId.Value);
        }
        catch { }

        try { services?.TriggerUpdate(); }
        catch { }
    }

    private Vector2 GetImageSize(ID2D1Image image)
    {
        if (ReferenceEquals(image, lastSizedImage))
        {
            return lastImageSize;
        }

        var size = new Vector2(100, 100);

        nint nativePtr;
        try { nativePtr = image.NativePointer; }
        catch { return size; }
        if (nativePtr == IntPtr.Zero) return size;

        if (image is ID2D1Bitmap bitmap)
        {
            size = new Vector2(bitmap.Size.Width, bitmap.Size.Height);
        }
        else if (devices?.DeviceContext != null)
        {
            try
            {
                var bounds = devices.DeviceContext.GetImageLocalBounds(image);
                size = new Vector2(bounds.Right - bounds.Left, bounds.Bottom - bounds.Top);
            }
            catch
            {
                lastSizedImage = null;
                lastImageSize = default;
                return size;
            }
        }

        lastSizedImage = image;
        lastImageSize = size;
        return size;
    }

    public void ClearInput()
    {
        input = null;
        lastSizedImage = null;
        lastImageSize = default;
        _stagingSharedHandle = IntPtr.Zero;
        Interlocked.Exchange(ref _stagingBitmap, null)?.Dispose();
        Interlocked.Exchange(ref _expiredStagingBitmap, null)?.Dispose();
        TryRemoveBillboard();
    }

    public void SetInput(ID2D1Image? newInput)
    {
        input = newInput;

        if (newInput == null || _disposed) return;

        var services = GetSceneServices();
        if (services?.Draw == null) return;

        var prev = _lastSnapshot;
        var descriptor = prev != null
            ? new BillboardDescriptor
            {
                Image = _stagingBitmap ?? newInput,
                SharedHandle = _stagingSharedHandle,
                SharedWidth = _stagingW,
                SharedHeight = _stagingH,
                WorldPosition = prev.WorldPosition,
                Size = prev.Size,
                Rotation = prev.Rotation,
                FaceCamera = prev.FaceCamera,
                Opacity = prev.Opacity
            }
            : new BillboardDescriptor
            {
                Image = _stagingBitmap ?? newInput,
                SharedHandle = _stagingSharedHandle,
                SharedWidth = _stagingW,
                SharedHeight = _stagingH
            };

        bool triggerNeeded;
        lock (syncLock)
        {
            triggerNeeded = ApplyBillboard(services, descriptor);
        }

        if (triggerNeeded)
        {
            item.TriggerUpdate();
            services.TriggerUpdate();
        }
    }

    public void Dispose()
    {
        _disposed = true;
        ObjLoaderApi.SceneRegistrationChanged -= OnSceneRegistrationChanged;
        item.X.PropertyChanged -= OnPropertyChanged;
        item.Y.PropertyChanged -= OnPropertyChanged;
        item.Z.PropertyChanged -= OnPropertyChanged;
        item.Scale.PropertyChanged -= OnPropertyChanged;
        item.ScaleX.PropertyChanged -= OnPropertyChanged;
        item.ScaleY.PropertyChanged -= OnPropertyChanged;
        item.RotationX.PropertyChanged -= OnPropertyChanged;
        item.RotationY.PropertyChanged -= OnPropertyChanged;
        item.RotationZ.PropertyChanged -= OnPropertyChanged;
        item.Opacity.PropertyChanged -= OnPropertyChanged;
        item.PropertyChanged -= OnPropertyChanged;
        TryRemoveBillboard();
        _cachedServices = null;
        Interlocked.Exchange(ref _stagingBitmap, null)?.Dispose();
        Interlocked.Exchange(ref _expiredStagingBitmap, null)?.Dispose();
    }

    private void OnSceneRegistrationChanged(object? sender, SceneRegistrationChangedEventArgs e)
    {
        if (_disposed) return;

        if (e.IsRegistered)
        {
            ISceneServices? services = null;
            try
            {
                if (ObjLoaderApi.TryGetScene(e.InstanceId, out var s) && s != null && !s.IsDisposed)
                {
                    services = s;
                }
            }
            catch { return; }

            _cachedServices = null;

            if (services == null || _cachedDevicePointer == IntPtr.Zero || services.ContextPointer != _cachedDevicePointer)
            {
                return;
            }

            var currentInput = input;
            if (currentInput == null) return;

            var prev = _lastSnapshot;
            var descriptor = prev != null
                ? new BillboardDescriptor
                {
                    Image = _stagingBitmap ?? currentInput,
                    SharedHandle = _stagingSharedHandle,
                    SharedWidth = _stagingW,
                    SharedHeight = _stagingH,
                    WorldPosition = prev.WorldPosition,
                    Size = prev.Size,
                    Rotation = prev.Rotation,
                    FaceCamera = prev.FaceCamera,
                    Opacity = prev.Opacity
                }
                : new BillboardDescriptor
                {
                    Image = _stagingBitmap ?? currentInput,
                    SharedHandle = _stagingSharedHandle,
                    SharedWidth = _stagingW,
                    SharedHeight = _stagingH
                };

            bool triggerNeeded;
            lock (syncLock)
            {
                triggerNeeded = ApplyBillboard(services, descriptor);
            }

            if (triggerNeeded)
            {
                item.TriggerUpdate();
                services.TriggerUpdate();
            }
        }
        else
        {
            _cachedServices = null;
        }
    }

    private ISceneServices? GetSceneServices()
    {
        var cached = _cachedServices;
        if (cached != null && !cached.IsDisposed && cached.ContextPointer == _cachedDevicePointer)
        {
            return cached;
        }

        _cachedServices = null;

        try
        {
            var services = ObjLoaderApi.GetFirstScene();
            if (services != null && _cachedDevicePointer != IntPtr.Zero && services.ContextPointer == _cachedDevicePointer)
            {
                _cachedServices = services;
                return services;
            }
        }
        catch { }

        return null;
    }
}