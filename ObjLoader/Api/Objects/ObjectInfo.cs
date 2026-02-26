using ObjLoader.Api.Core;

namespace ObjLoader.Api.Objects
{
    public sealed class ObjectInfo
    {
        public SceneObjectId Id { get; }
        public string Name { get; }
        public string FilePath { get; }
        public Transform CurrentTransform { get; }
        public bool IsVisible { get; }
        public int WorldId { get; }

        public ObjectInfo(SceneObjectId id, string name, string filePath, Transform currentTransform, bool isVisible, int worldId)
        {
            Id = id;
            Name = name ?? string.Empty;
            FilePath = filePath ?? string.Empty;
            CurrentTransform = currentTransform;
            IsVisible = isVisible;
            WorldId = worldId;
        }
    }
}