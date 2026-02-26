using ObjLoader.Api.Core;

namespace ObjLoader.Api.Draw
{
    public sealed class ExternalObjectDescriptor
    {
        public string FilePath { get; set; } = string.Empty;
        public Transform InitialTransform { get; set; } = Transform.Identity;
        public int WorldId { get; set; } = 0;
        public bool IsVisible { get; set; } = true;
        public string? VertexShaderPath { get; set; }
        public string? PixelShaderPath { get; set; }
    }
}