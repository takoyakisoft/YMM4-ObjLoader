using ObjLoader.Api.Core;

namespace ObjLoader.Api.Events
{
    public sealed class SceneChangedEventArgs : EventArgs
    {
        public SceneChangeType ChangeType { get; }
        public SceneObjectId? AffectedObjectId { get; }

        public SceneChangedEventArgs(SceneChangeType changeType, SceneObjectId? affectedObjectId = null)
        {
            ChangeType = changeType;
            AffectedObjectId = affectedObjectId;
        }
    }
}