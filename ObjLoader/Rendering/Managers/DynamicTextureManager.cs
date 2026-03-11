using ObjLoader.Rendering.Managers.Interfaces;
using ObjLoader.Services.Textures;
using Vortice.Direct3D11;

namespace ObjLoader.Rendering.Managers
{
    public class DynamicTextureManager : IDynamicTextureManager
    {
        private readonly ITextureService _textureService;
        private readonly Dictionary<string, ID3D11ShaderResourceView> _cache = new();
        private readonly HashSet<string> _keysToRemoveBuffer = new();
        private readonly IReadOnlyDictionary<string, ID3D11ShaderResourceView> _readOnlyCache;
        private readonly object _lock = new object();
        private bool _disposed;

        public IReadOnlyDictionary<string, ID3D11ShaderResourceView> Textures => _readOnlyCache;

        public DynamicTextureManager(ITextureService textureService)
        {
            _textureService = textureService ?? throw new ArgumentNullException(nameof(textureService));
            _readOnlyCache = _cache;
        }

        public void Prepare(IEnumerable<string> usedPaths, ID3D11Device device)
        {
            lock (_lock)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(DynamicTextureManager));

                if (usedPaths == null || device == null)
                {
                    ClearInternal();
                    return;
                }

                _keysToRemoveBuffer.Clear();
                foreach (var key in _cache.Keys)
                {
                    _keysToRemoveBuffer.Add(key);
                }

                foreach (var path in usedPaths)
                {
                    _keysToRemoveBuffer.Remove(path);
                }

                foreach (var key in _keysToRemoveBuffer)
                {
                    if (_cache.TryGetValue(key, out var srv))
                    {
                        srv?.Dispose();
                        _cache.Remove(key);
                    }
                }

                foreach (var path in usedPaths)
                {
                    if (!_cache.ContainsKey(path))
                    {
                        try
                        {
                            var (srv, _) = _textureService.CreateShaderResourceView(path, device);
                            if (srv != null)
                            {
                                _cache[path] = srv;
                            }
                        }
                        catch
                        {
                        }
                    }
                }
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(DynamicTextureManager));
                ClearInternal();
            }
        }

        private void ClearInternal()
        {
            foreach (var srv in _cache.Values)
            {
                srv?.Dispose();
            }
            _cache.Clear();
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed) return;
                _disposed = true;
                ClearInternal();
            }
        }
    }
}
