using Vortice.Direct3D11;
using ObjLoader.Rendering.Managers.Interfaces;
using ObjLoader.Api.Core;
using ObjLoader.Api.Draw;

namespace ObjLoader.Services.Rendering.Device;

internal sealed class LocalDrawManagerAdapter : ISceneDrawManager, IDisposable
{
    private sealed class LocalBillboardEntry : IDisposable
    {
        public nint Handle;
        public ID3D11Texture2D? Texture;
        public ID3D11ShaderResourceView? Srv;

        public void Dispose()
        {
            Srv?.Dispose();
            Texture?.Dispose();
        }
    }

    private readonly ISceneDrawManager _inner;
    private readonly ID3D11Device _device;
    private readonly Dictionary<SceneObjectId, LocalBillboardEntry> _localSrvs = new();
    private static readonly List<ExternalObjectHandle> _emptyExternalObjects = new();
    private readonly HashSet<SceneObjectId> _activeIds = new();
    private readonly List<SceneObjectId> _keysToRemove = new();

    public event EventHandler? Updated
    {
        add => _inner.Updated += value;
        remove => _inner.Updated -= value;
    }

    public bool IsDirty => _inner.IsDirty;
    public long UpdateCount => _inner.UpdateCount;

    public LocalDrawManagerAdapter(ISceneDrawManager inner, ID3D11Device device)
    {
        _inner = inner;
        _device = device;
    }

    public void UpdateFromApi(SceneDrawApi api) => _inner.UpdateFromApi(api);

    public IReadOnlyCollection<ExternalObjectHandle> GetExternalObjects() => _emptyExternalObjects;

    public IReadOnlyCollection<(SceneObjectId Id, BillboardDescriptor Desc)> GetBillboards() => _inner.GetBillboards();

    public ID3D11ShaderResourceView? GetBillboardSrv(SceneObjectId id)
    {
        nint handle = _inner.GetBillboardSharedHandle(id);
        if (handle == IntPtr.Zero)
        {
            if (_localSrvs.TryGetValue(id, out var stale))
            {
                stale.Dispose();
                _localSrvs.Remove(id);
            }
            return null;
        }

        if (_localSrvs.TryGetValue(id, out var cached))
        {
            if (cached.Handle == handle) return cached.Srv;
            cached.Dispose();
            _localSrvs.Remove(id);
        }

        try
        {
            var tex = _device.OpenSharedResource<ID3D11Texture2D>(handle);
            var srv = _device.CreateShaderResourceView(tex);
            _localSrvs[id] = new LocalBillboardEntry { Handle = handle, Texture = tex, Srv = srv };
            return srv;
        }
        catch
        {
            return null;
        }
    }

    public nint GetBillboardSharedHandle(SceneObjectId id) => _inner.GetBillboardSharedHandle(id);

    public void PurgeStaleEntries()
    {
        if (_localSrvs.Count == 0) return;
        var currentBillboards = _inner.GetBillboards();
        _activeIds.Clear();
        foreach (var b in currentBillboards)
        {
            _activeIds.Add(b.Id);
        }
        _keysToRemove.Clear();
        foreach (var key in _localSrvs.Keys)
        {
            if (!_activeIds.Contains(key)) _keysToRemove.Add(key);
        }
        for (int i = 0; i < _keysToRemove.Count; i++)
        {
            _localSrvs[_keysToRemove[i]].Dispose();
            _localSrvs.Remove(_keysToRemove[i]);
        }
    }

    public void ClearDirtyFlag() { }

    public void Clear() { }

    public void Dispose()
    {
        foreach (var entry in _localSrvs.Values)
        {
            entry.Dispose();
        }
        _localSrvs.Clear();
    }
}