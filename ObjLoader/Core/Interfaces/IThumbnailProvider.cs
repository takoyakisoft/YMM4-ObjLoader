using ObjLoader.Core.Models;

namespace ObjLoader.Core.Interfaces
{
    public interface IThumbnailProvider
    {
        byte[] CreateThumbnail(ObjModel model, int width = 64, int height = 64);
    }
}