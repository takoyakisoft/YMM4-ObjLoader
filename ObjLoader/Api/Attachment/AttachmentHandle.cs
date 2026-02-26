using ObjLoader.Api.Core;

namespace ObjLoader.Api.Attachment
{
    public sealed class AttachmentHandle
    {
        public SceneObjectId ChildId { get; }
        public SceneObjectId ParentId { get; }
        public Transform LocalOffset { get; internal set; }

        internal AttachmentHandle(SceneObjectId childId, SceneObjectId parentId, Transform localOffset)
        {
            ChildId = childId;
            ParentId = parentId;
            LocalOffset = localOffset;
        }
    }
}