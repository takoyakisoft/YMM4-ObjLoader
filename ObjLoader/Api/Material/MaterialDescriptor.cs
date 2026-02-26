using System.Windows.Media;

namespace ObjLoader.Api.Material
{
    public readonly struct MaterialDescriptor
    {
        public Color BaseColor { get; }
        public float Roughness { get; }
        public float Metallic { get; }
        public float DiffuseIntensity { get; }
        public float SpecularIntensity { get; }
        public float Shininess { get; }
        public string? TexturePath { get; }

        public MaterialDescriptor(Color baseColor, float roughness, float metallic, float diffuseIntensity, float specularIntensity, float shininess, string? texturePath)
        {
            BaseColor = baseColor;
            Roughness = roughness;
            Metallic = metallic;
            DiffuseIntensity = diffuseIntensity;
            SpecularIntensity = specularIntensity;
            Shininess = shininess;
            TexturePath = texturePath;
        }

        public static readonly MaterialDescriptor Default = new(
            Colors.White, 0.5f, 0.0f, 1.0f, 0.5f, 32.0f, null);
    }
}