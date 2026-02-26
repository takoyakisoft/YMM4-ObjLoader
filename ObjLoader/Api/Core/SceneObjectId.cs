namespace ObjLoader.Api.Core
{
    public readonly record struct SceneObjectId(string Guid)
    {
        public static readonly SceneObjectId Empty = new(string.Empty);

        public bool IsEmpty => string.IsNullOrEmpty(Guid);

        public static SceneObjectId NewId() => new(System.Guid.NewGuid().ToString("D"));

        public override string ToString() => Guid;
    }
}