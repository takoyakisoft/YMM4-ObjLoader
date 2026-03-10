namespace ObjLoader.Services.Rendering.Passes;

internal readonly record struct TransparentPart : IComparable<TransparentPart>
{
    public int LayerIndex { get; init; }
    public int PartIndex { get; init; }
    public float DistanceSq { get; init; }

    public int CompareTo(TransparentPart other)
    {
        return other.DistanceSq.CompareTo(DistanceSq);
    }
}
