using System.Numerics;
using System.Runtime.CompilerServices;

namespace ObjLoader.Systems.Models;

[InlineArray(64)]
public struct GenericInterpolation64
{
    private byte _element0;
}

public struct GenericBoneFrame
{
    public string BoneName;
    public uint FrameNumber;
    public Vector3 Position;
    public Quaternion Rotation;
    public GenericInterpolation64 Interpolation;
}