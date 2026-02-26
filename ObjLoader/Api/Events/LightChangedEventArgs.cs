using ObjLoader.Api.Light;

namespace ObjLoader.Api.Events
{
    public sealed class LightChangedEventArgs : EventArgs
    {
        public int WorldId { get; }
        public LightDescriptor NewLight { get; }

        public LightChangedEventArgs(int worldId, LightDescriptor newLight)
        {
            WorldId = worldId;
            NewLight = newLight;
        }
    }
}