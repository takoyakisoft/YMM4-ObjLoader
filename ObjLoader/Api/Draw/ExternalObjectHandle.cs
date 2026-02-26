using ObjLoader.Api.Core;

namespace ObjLoader.Api.Draw
{
    public sealed class ExternalObjectHandle
    {
        public SceneObjectId Id { get; }
        public Transform CurrentTransform { get; internal set; }
        public bool IsVisible { get; internal set; }
        public ExternalObjectDescriptor Descriptor { get; }

        internal ExternalObjectHandle(SceneObjectId id, ExternalObjectDescriptor descriptor)
        {
            Id = id;
            Descriptor = descriptor;
            CurrentTransform = descriptor.InitialTransform;
            IsVisible = descriptor.IsVisible;
        }
    }
}