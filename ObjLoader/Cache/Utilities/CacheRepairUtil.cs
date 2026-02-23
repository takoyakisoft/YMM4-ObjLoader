using ObjLoader.Cache.Core;
using System.IO;

namespace ObjLoader.Cache.Utilities
{
    public static class CacheRepairUtil
    {
        private const int MaxRetryCount = 2;
        private const int RetryDelayMs = 100;

        public static bool ValidateCacheIntegrity(string cacheDir)
        {
            try
            {
                if (!Directory.Exists(cacheDir)) return false;

                bool hasSingle = File.Exists(Path.Combine(cacheDir, "model.bin"));
                bool hasSplit = File.Exists(Path.Combine(cacheDir, "part.0.bin"));

                if (!hasSingle && !hasSplit) return false;

                if (hasSingle)
                {
                    return ValidateSingleFile(Path.Combine(cacheDir, "model.bin"));
                }

                return ValidateSplitFiles(cacheDir);
            }
            catch
            {
                return false;
            }
        }

        public static bool RepairIfNeeded(string cacheDir)
        {
            try
            {
                if (!Directory.Exists(cacheDir)) return false;

                if (ValidateCacheIntegrity(cacheDir)) return true;

                CleanCorruptedCache(cacheDir);
                return false;
            }
            catch
            {
                return false;
            }
        }

        public static bool ExecuteWithRetry(System.Action action)
        {
            for (int attempt = 0; attempt <= MaxRetryCount; attempt++)
            {
                try
                {
                    action();
                    return true;
                }
                catch
                {
                    if (attempt < MaxRetryCount)
                    {
                        System.Threading.Thread.Sleep(RetryDelayMs * (attempt + 1));
                    }
                }
            }
            return false;
        }

        public static T? ExecuteWithRetry<T>(System.Func<T> func) where T : class
        {
            for (int attempt = 0; attempt <= MaxRetryCount; attempt++)
            {
                try
                {
                    return func();
                }
                catch
                {
                    if (attempt < MaxRetryCount)
                    {
                        System.Threading.Thread.Sleep(RetryDelayMs * (attempt + 1));
                    }
                }
            }
            return null;
        }

        private static bool ValidateSingleFile(string filePath)
        {
            try
            {
                var fi = new FileInfo(filePath);
                if (fi.Length < 24) return false;

                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var br = new BinaryReader(fs);

                int signature = br.ReadInt32();
                if (signature != CacheHeader.CurrentSignature) return false;

                int version = br.ReadInt32();
                if (version < 1 || version > CacheHeader.CurrentVersion + 10) return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool ValidateSplitFiles(string cacheDir)
        {
            try
            {
                string firstPart = Path.Combine(cacheDir, "part.0.bin");
                if (!File.Exists(firstPart)) return false;

                var fi = new FileInfo(firstPart);
                if (fi.Length < 24) return false;

                using var fs = new FileStream(firstPart, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var br = new BinaryReader(fs);

                int signature = br.ReadInt32();
                if (signature != CacheHeader.CurrentSignature) return false;

                int version = br.ReadInt32();
                if (version < 1 || version > CacheHeader.CurrentVersion + 10) return false;

                var parts = Directory.GetFiles(cacheDir, "part.*.bin");
                for (int i = 0; i < parts.Length; i++)
                {
                    string expected = Path.Combine(cacheDir, $"part.{i}.bin");
                    if (!File.Exists(expected)) return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void CleanCorruptedCache(string cacheDir)
        {
            try
            {
                string tmpDir = Path.Combine(cacheDir, ".tmp");
                if (Directory.Exists(tmpDir))
                {
                    Directory.Delete(tmpDir, true);
                }

                foreach (var file in Directory.GetFiles(cacheDir, "*.bin"))
                {
                    try { File.Delete(file); } catch { }
                }

                string thumbFile = Path.Combine(cacheDir, "thumb.png");
                if (File.Exists(thumbFile))
                {
                    try { File.Delete(thumbFile); } catch { }
                }
            }
            catch { }
        }
    }
}