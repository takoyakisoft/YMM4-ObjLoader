using ObjLoader.Api.Core;

namespace ObjLoader.Api.Draw
{
    public interface ISceneDrawApi
    {
        SceneObjectId AddExternalObject(ExternalObjectDescriptor descriptor);
        bool UpdateExternalObject(SceneObjectId id, in Transform transform);
        bool SetExternalObjectVisibility(SceneObjectId id, bool visible);
        bool RemoveExternalObject(SceneObjectId id);
        ApiResult<ExternalObjectHandle> GetExternalObject(SceneObjectId id);
        IReadOnlyList<SceneObjectId> GetExternalObjectIds();

        SceneObjectId CreateDynamicBillboard(BillboardDescriptor descriptor);
        bool UpdateBillboard(SceneObjectId id, BillboardDescriptor descriptor);
        bool RemoveBillboard(SceneObjectId id);
        IReadOnlyList<SceneObjectId> GetBillboardIds();
    }
}