namespace ObjLoader.Api.Raycast
{
    public sealed class RaycastFilter
    {
        public int? WorldId { get; set; }
        public float MaxDistance { get; set; } = float.MaxValue;
        public bool IncludeExternalObjects { get; set; } = true;

        public static readonly RaycastFilter Default = new();
    }
}