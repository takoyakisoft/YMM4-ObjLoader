using ObjLoader.Api;
using ObjLoader.Api.Core;
using ObjLoader.Api.Draw;
using ObjLoader.VideoEffect;
using System.Numerics;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

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

        var descriptor = new BillboardDescriptor
        {
            Image = currentInput,
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
            catch { }
        }

        lastSizedImage = image;
        lastImageSize = size;
        return size;
    }

    public void ClearInput()
    {
        input = null;
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
                Image = newInput,
                WorldPosition = prev.WorldPosition,
                Size = prev.Size,
                Rotation = prev.Rotation,
                FaceCamera = prev.FaceCamera,
                Opacity = prev.Opacity
            }
            : new BillboardDescriptor { Image = newInput };

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
                    Image = currentInput,
                    WorldPosition = prev.WorldPosition,
                    Size = prev.Size,
                    Rotation = prev.Rotation,
                    FaceCamera = prev.FaceCamera,
                    Opacity = prev.Opacity
                }
                : new BillboardDescriptor { Image = currentInput };

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