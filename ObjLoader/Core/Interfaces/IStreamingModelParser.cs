using ObjLoader.Cache.Streaming;
using ObjLoader.Core.Models;

namespace ObjLoader.Core.Interfaces
{
    public interface IStreamingModelParser : IModelParser
    {
        bool SupportsStreaming { get; }
        ObjModel StreamToCache(string path, IStreamingCacheWriter cacheWriter);
    }
}