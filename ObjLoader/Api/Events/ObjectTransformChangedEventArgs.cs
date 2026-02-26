using ObjLoader.Api.Core;

namespace ObjLoader.Api.Events
{
    public sealed class ObjectTransformChangedEventArgs : EventArgs
    {
        public SceneObjectId ObjectId { get; }
        public Transform NewTransform { get; }

        public ObjectTransformChangedEventArgs(SceneObjectId objectId, Transform newTransform)
        {
            ObjectId = objectId;
            NewTransform = newTransform;
        }
    }
}