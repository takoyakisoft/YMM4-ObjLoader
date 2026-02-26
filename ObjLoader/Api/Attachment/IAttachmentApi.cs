using ObjLoader.Api.Core;

namespace ObjLoader.Api.Attachment
{
    public interface IAttachmentApi
    {
        AttachmentHandle AttachObject(SceneObjectId childId, SceneObjectId parentId, in Transform localOffset);
        bool UpdateAttachmentOffset(AttachmentHandle handle, in Transform localOffset);
        bool Detach(AttachmentHandle handle);
        IReadOnlyList<AttachmentHandle> GetAttachments(SceneObjectId objectId);
    }
}