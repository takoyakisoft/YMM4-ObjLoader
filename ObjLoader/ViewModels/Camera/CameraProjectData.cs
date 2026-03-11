using ObjLoader.Plugin.CameraAnimation;

namespace ObjLoader.ViewModels.Camera;

internal class CameraProjectData
{
    public List<CameraKeyframe> Keyframes { get; set; } = [];
    public double Duration { get; set; }
    public bool IsTargetFixed { get; set; }
}