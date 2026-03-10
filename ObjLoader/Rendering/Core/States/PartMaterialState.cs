using System.Windows.Media;

namespace ObjLoader.Rendering.Core.States
{
    internal readonly record struct PartMaterialState
    {
        public double Roughness { get; init; }
        public double Metallic { get; init; }
        public Color BaseColor { get; init; }
        public string? TexturePath { get; init; }
    }
}