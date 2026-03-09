using System.Numerics;
using System.Runtime.CompilerServices;

namespace ObjLoader.Services.Mmd.Parsers;

[InlineArray(64)]
public struct Interpolation64
{
    private byte _element0;
}

[InlineArray(24)]
public struct Interpolation24
{
    private byte _element0;
}

public class VmdData
{
    public string ModelName { get; set; } = string.Empty;
    public List<VmdBoneFrame> BoneFrames { get; set; } = new List<VmdBoneFrame>();
    public List<VmdMorphFrame> MorphFrames { get; set; } = new List<VmdMorphFrame>();
    public List<VmdCameraFrame> CameraFrames { get; set; } = new List<VmdCameraFrame>();
}

public struct VmdBoneFrame
{
    public string BoneName;
    public uint FrameNumber;
    public Vector3 Position;
    public Quaternion Rotation;
    public Interpolation64 Interpolation;
}

public struct VmdMorphFrame
{
    public string MorphName;
    public uint FrameNumber;
    public float Weight;
}

public struct VmdCameraFrame
{
    public uint FrameNumber;
    public float Distance;
    public Vector3 Position;
    public Vector3 Rotation;
    public Interpolation24 Interpolation;
    public uint ViewAngle;
    public bool IsOrthographic;
}