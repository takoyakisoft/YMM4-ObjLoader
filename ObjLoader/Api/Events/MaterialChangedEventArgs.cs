using ObjLoader.Api.Core;
using ObjLoader.Api.Material;

namespace ObjLoader.Api.Events
{
    public sealed class MaterialChangedEventArgs : EventArgs
    {
        public SceneObjectId ObjectId { get; }
        public int PartIndex { get; }
        public MaterialDescriptor NewMaterial { get; }

        public MaterialChangedEventArgs(SceneObjectId objectId, int partIndex, MaterialDescriptor newMaterial)
        {
            ObjectId = objectId;
            PartIndex = partIndex;
            NewMaterial = newMaterial;
        }
    }
}