using ObjLoader.Api.Core;

namespace ObjLoader.Api.Material
{
    public interface IMaterialApi
    {
        ApiResult<MaterialDescriptor> GetMaterial(SceneObjectId objectId, int partIndex);
        bool SetMaterial(SceneObjectId objectId, int partIndex, in MaterialDescriptor material);
        int GetPartCount(SceneObjectId objectId);
    }
}