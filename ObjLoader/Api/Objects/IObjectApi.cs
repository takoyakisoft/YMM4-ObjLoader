using ObjLoader.Api.Core;

namespace ObjLoader.Api.Objects
{
    public interface IObjectApi
    {
        IReadOnlyList<SceneObjectId> GetAllObjectIds();
        ApiResult<ObjectInfo> GetObjectInfo(SceneObjectId id);
        ApiResult<Transform> GetTransform(SceneObjectId id);
        bool SetTransform(SceneObjectId id, in Transform transform);
        bool SetVisibility(SceneObjectId id, bool visible);
        bool SetParent(SceneObjectId childId, SceneObjectId? parentId);
        IReadOnlyList<SceneObjectId> GetChildren(SceneObjectId id);
    }
}