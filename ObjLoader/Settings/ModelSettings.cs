using System.IO;
using ObjLoader.Cache;
using ObjLoader.Infrastructure;
using ObjLoader.Localization;
using ObjLoader.Utilities;
using ObjLoader.Views.Controls;
using ObjLoader.ViewModels.Settings;
using Vortice.DXGI;
using YukkuriMovieMaker.Plugin;

namespace ObjLoader.Settings
{
    public class ModelSettings : SettingsBase<ModelSettings>
    {
        public override string Name => Texts.Settings_3DModel;
        public override SettingsCategory Category => SettingsCategory.Shape;
        public override bool HasSettingView => true;
        public override object SettingView => new ModelSettingsView { DataContext = new ModelSettingsViewModel(this) };
        public static ModelSettings Instance => Default;

        public const int DefaultMaxFileSizeMB = 500;
        public const int DefaultMaxGpuMemoryPerModelMB = 2048;
        public const int DefaultMaxTotalGpuMemoryMB = 8192;
        public const int DefaultMaxVertices = 10_000_000;
        public const int DefaultMaxIndices = 30_000_000;
        public const int DefaultMaxParts = 10000;
        public const int MinFileSizeMB = 10;
        public const int MaxFileSizeMBLimit = 10240;
        public const int MinGpuMemoryMB = 64;
        public const int MaxGpuMemoryMBLimit = 32768;
        public const int MinVertices = 10_000;
        public const int MaxVerticesLimit = 100_000_000;
        public const int MinIndices = 30_000;
        public const int MaxIndicesLimit = 300_000_000;
        public const int MinParts = 10;
        public const int MaxPartsLimit = 50_000;
        public const double DefaultD3DResourceReleaseDelay = 5.0;

        private double _d3dResourceReleaseDelay = DefaultD3DResourceReleaseDelay;
        public double D3DResourceReleaseDelay
        {
            get => _d3dResourceReleaseDelay;
            set => Set(ref _d3dResourceReleaseDelay, Math.Max(0.0, value));
        }

        private bool _isSandboxEnforced = false;
        public bool IsSandboxEnforced
        {
            get => _isSandboxEnforced;
            set => Set(ref _isSandboxEnforced, value);
        }

        private List<string> _allowedRoots = new List<string>();
        public List<string> AllowedRoots
        {
            get => _allowedRoots;
            set => Set(ref _allowedRoots, value ?? new List<string>());
        }

        private bool _enableAutoAudit = false;
        public bool EnableAutoAudit
        {
            get => _enableAutoAudit;
            set => Set(ref _enableAutoAudit, value);
        }

        private double _auditIntervalMinutes = 5.0;
        public double AuditIntervalMinutes
        {
            get => _auditIntervalMinutes;
            set => Set(ref _auditIntervalMinutes, Math.Max(0.5, value));
        }

        private double _leakThresholdMinutes = 30.0;
        public double LeakThresholdMinutes
        {
            get => _leakThresholdMinutes;
            set => Set(ref _leakThresholdMinutes, Math.Max(1.0, value));
        }

        private int _maxFileSizeMB = DefaultMaxFileSizeMB;
        public int MaxFileSizeMB
        {
            get => _maxFileSizeMB;
            set => Set(ref _maxFileSizeMB, Math.Clamp(value, MinFileSizeMB, MaxFileSizeMBLimit));
        }

        private int _maxGpuMemoryPerModelMB = DefaultMaxGpuMemoryPerModelMB;
        public int MaxGpuMemoryPerModelMB
        {
            get => _maxGpuMemoryPerModelMB;
            set => Set(ref _maxGpuMemoryPerModelMB, Math.Clamp(value, MinGpuMemoryMB, Math.Min(MaxGpuMemoryMBLimit, _maxTotalGpuMemoryMB)));
        }

        private int _maxTotalGpuMemoryMB = DefaultMaxTotalGpuMemoryMB;
        public int MaxTotalGpuMemoryMB
        {
            get => _maxTotalGpuMemoryMB;
            set
            {
                int clamped = Math.Clamp(value, MinGpuMemoryMB, MaxGpuMemoryMBLimit);
                if (Set(ref _maxTotalGpuMemoryMB, clamped))
                {
                    if (_maxGpuMemoryPerModelMB > _maxTotalGpuMemoryMB)
                    {
                        MaxGpuMemoryPerModelMB = _maxTotalGpuMemoryMB;
                    }
                }
            }
        }

        private int _maxVertices = DefaultMaxVertices;
        public int MaxVertices
        {
            get => _maxVertices;
            set => Set(ref _maxVertices, Math.Clamp(value, MinVertices, MaxVerticesLimit));
        }

        private int _maxIndices = DefaultMaxIndices;
        public int MaxIndices
        {
            get => _maxIndices;
            set => Set(ref _maxIndices, Math.Clamp(value, MinIndices, MaxIndicesLimit));
        }

        private int _maxParts = DefaultMaxParts;
        public int MaxParts
        {
            get => _maxParts;
            set => Set(ref _maxParts, Math.Clamp(value, MinParts, MaxPartsLimit));
        }

        public long MaxFileSizeBytes => (long)_maxFileSizeMB * 1024L * 1024L;
        public long MaxGpuMemoryPerModelBytes => (long)_maxGpuMemoryPerModelMB * 1024L * 1024L;
        public long MaxTotalGpuMemoryBytes => (long)_maxTotalGpuMemoryMB * 1024L * 1024L;

        public override void Initialize()
        {
            try
            {
                if (_isSandboxEnforced)
                    FileSystemSandbox.Instance.Enable();
                else
                    FileSystemSandbox.Instance.Disable();

                FileSystemSandbox.Instance.ClearAllowedRoots();
                foreach (var root in _allowedRoots)
                {
                    if (!string.IsNullOrWhiteSpace(root))
                        FileSystemSandbox.Instance.AddAllowedRoot(root);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ModelSettings.Initialize: Sandbox setup failed: {ex.Message}");
            }

            try
            {
                AdjustGpuMemoryLimit();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ModelSettings.Initialize: GPU info retrieval failed: {ex.Message}");
            }

            try
            {
                ResourceAuditor.Instance.SetLeakThreshold(TimeSpan.FromMinutes(Math.Max(1.0, _leakThresholdMinutes)));

                if (_enableAutoAudit)
                {
                    ResourceAuditor.Instance.Start(TimeSpan.FromMinutes(Math.Max(0.5, _auditIntervalMinutes)));
                }
                else
                {
                    ResourceAuditor.Instance.Stop();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ModelSettings.Initialize: Auditor setup failed: {ex.Message}");
            }
        }

        private void AdjustGpuMemoryLimit()
        {
            if (DXGI.CreateDXGIFactory1(out IDXGIFactory1? factory).Success && factory != null)
            {
                using (factory)
                {
                    long maxDedicatedVideoMemory = 0;
                    long maxSharedSystemMemory = 0;

                    for (int i = 0; factory.EnumAdapters1(i, out var adapter).Success; i++)
                    {
                        using (adapter)
                        {
                            var desc = adapter.Description1;
                            if ((desc.Flags & AdapterFlags.Software) == 0)
                            {
                                if ((long)desc.DedicatedVideoMemory > maxDedicatedVideoMemory)
                                {
                                    maxDedicatedVideoMemory = (long)desc.DedicatedVideoMemory;
                                    maxSharedSystemMemory = (long)desc.SharedSystemMemory;
                                }
                            }
                        }
                    }

                    long clampTargetMemory = 0;
                    if (maxDedicatedVideoMemory > 512L * 1024L * 1024L)
                    {
                        clampTargetMemory = maxDedicatedVideoMemory;
                    }
                    else if (maxSharedSystemMemory > 0)
                    {
                        clampTargetMemory = maxSharedSystemMemory;
                    }

                    if (clampTargetMemory > 0)
                    {
                        long maxMB = clampTargetMemory / (1024L * 1024L);
                        if (maxMB <= DefaultMaxTotalGpuMemoryMB)
                        {
                            MaxTotalGpuMemoryMB = Math.Min(MaxTotalGpuMemoryMB, (int)maxMB);
                        }
                    }
                }
            }
        }

        public bool IsFileSizeAllowed(long fileBytes)
        {
            return fileBytes <= MaxFileSizeBytes;
        }

        public bool IsGpuMemoryPerModelAllowed(long gpuBytes)
        {
            return gpuBytes <= MaxGpuMemoryPerModelBytes;
        }

        public bool IsVertexCountAllowed(int count)
        {
            return count <= _maxVertices;
        }

        public bool IsIndexCountAllowed(int count)
        {
            return count <= _maxIndices;
        }

        public bool IsPartCountAllowed(int count)
        {
            return count <= _maxParts;
        }

        public string ValidateModelComplexity(string fileName, int vertexCount, int indexCount, int partCount)
        {
            if (!IsVertexCountAllowed(vertexCount))
            {
                return string.Format(Texts.VertexCountExceeded, fileName, FormatCount(vertexCount), FormatCount(_maxVertices));
            }
            if (!IsIndexCountAllowed(indexCount))
            {
                return string.Format(Texts.IndexCountExceeded, fileName, FormatCount(indexCount), FormatCount(_maxIndices));
            }
            if (!IsPartCountAllowed(partCount))
            {
                return string.Format(Texts.PartCountExceeded, fileName, FormatCount(partCount), FormatCount(_maxParts));
            }
            return string.Empty;
        }

        private List<string> _cacheIndexPaths = new List<string>();
        public List<string> CacheIndexPaths
        {
            get => _cacheIndexPaths;
            set => Set(ref _cacheIndexPaths, value ?? new List<string>());
        }

        public CacheIndex GetCacheIndex()
        {
            var aggregatedIndex = new CacheIndex();

            foreach (var path in _cacheIndexPaths)
            {
                if (!File.Exists(path)) continue;

                try
                {
                    byte[] data = File.ReadAllBytes(path);
                    var loadedIndex = CacheIndex.FromBinary(data);
                    foreach (var kvp in loadedIndex.Entries)
                    {
                        aggregatedIndex.Entries[kvp.Key] = kvp.Value;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ModelSettings.GetCacheIndex: Failed to load index from '{path}': {ex.Message}");
                }
            }
            return aggregatedIndex;
        }

        public const int MaxCacheEntries = 10000;

        public void SaveCacheIndex(CacheIndex index)
        {
            if (index == null)
            {
                CacheIndexPaths = new List<string>();
                Save();
                return;
            }

            if (index.Entries.Count > MaxCacheEntries)
            {
                var keysToRemove = index.Entries.OrderBy(x => x.Value.LastAccessTime)
                                                .Take(index.Entries.Count - MaxCacheEntries)
                                                .Select(x => x.Key)
                                                .ToList();
                foreach (var key in keysToRemove)
                {
                    index.Entries.Remove(key);
                }

                UserNotification.ShowInfo(string.Format(Texts.CacheEntryLimitReached, MaxCacheEntries), Texts.ErrorTitle);
            }

            var newPaths = new List<string>();

            var groupedEntries = index.Entries.GroupBy(kvp =>
            {
                string? originalDir = Path.GetDirectoryName(kvp.Key);
                return string.IsNullOrEmpty(originalDir) ? string.Empty : originalDir;
            });

            foreach (var group in groupedEntries)
            {
                if (string.IsNullOrEmpty(group.Key)) continue;

                string cacheDir = Path.Combine(group.Key, ".cache");
                string indexFile = Path.Combine(cacheDir, "CacheIndex.dat");

                try
                {
                    if (!Directory.Exists(cacheDir))
                    {
                        var di = Directory.CreateDirectory(cacheDir);
                        di.Attributes |= FileAttributes.Hidden;
                    }

                    var partialIndex = new CacheIndex();
                    partialIndex.Version = index.Version;
                    foreach (var kvp in group)
                    {
                        partialIndex.Entries[kvp.Key] = kvp.Value;
                    }

                    File.WriteAllBytes(indexFile, partialIndex.ToBinary());
                    newPaths.Add(indexFile);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ModelSettings.SaveCacheIndex: Failed to save partial index to '{indexFile}': {ex.Message}");
                }
            }
            CacheIndexPaths = newPaths;
            Save();
        }

        private static string FormatCount(int value)
        {
            return value.ToString("N0");
        }
    }
}