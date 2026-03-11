using System.Collections.ObjectModel;
using System.IO;
using Microsoft.Win32;
using ObjLoader.Localization;
using ObjLoader.Plugin;
using ObjLoader.Plugin.CameraAnimation;
using ObjLoader.Services.Mmd.Animation;
using ObjLoader.Services.Mmd.Parsers;

namespace ObjLoader.ViewModels.Camera;

internal class CameraVmdManager(
    ObjLoaderParameter parameter,
    ObservableCollection<CameraKeyframe> keyframes,
    Action<double> setMaxDuration,
    Action<double> setCurrentTime,
    Action updateAnimation)
{
    public bool IsSelectedLayerPmx()
    {
        if (parameter.SelectedLayerIndex < 0 || parameter.SelectedLayerIndex >= parameter.Layers.Count) return false;
        var layer = parameter.Layers[parameter.SelectedLayerIndex];
        if (string.IsNullOrEmpty(layer.FilePath)) return false;
        return Path.GetExtension(layer.FilePath).Equals(".pmx", StringComparison.OrdinalIgnoreCase);
    }

    public void LoadVmdMotion(EventHandler<string>? onNotification)
    {
        var dialog = new OpenFileDialog
        {
            Filter = $"{Texts.Msg_VmdFileFilter}|*.vmd",
            Multiselect = false
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var vmdData = VmdParser.Parse(dialog.FileName);
            var layer = parameter.Layers[parameter.SelectedLayerIndex];

            layer.VmdMotionData = vmdData;
            layer.VmdFilePath = dialog.FileName;
            layer.VmdTimeOffset = 0;

            if (vmdData.BoneFrames.Count > 0)
            {
                var model = new Parsers.PmxParser().Parse(layer.FilePath);
                if (model.Bones.Count > 0)
                {
                    layer.BoneAnimatorInstance = new BoneAnimator(
                        model.Bones, vmdData.BoneFrames,
                        model.RigidBodies, model.Joints);
                }
            }

            if (vmdData.CameraFrames.Count > 0)
            {
                var newKeyframes = VmdMotionApplier.ConvertCameraFrames(vmdData)
                    .OrderBy(k => k.Time)
                    .ToList();

                keyframes.Clear();
                foreach (var kf in newKeyframes) keyframes.Add(kf);
                parameter.Keyframes = [.. keyframes];

                double duration = VmdMotionApplier.GetDuration(vmdData);
                if (duration > 0) setMaxDuration(duration);

                setCurrentTime(0);
                updateAnimation();
            }

            int totalFrames = vmdData.CameraFrames.Count + vmdData.BoneFrames.Count;
            onNotification?.Invoke(this, string.Format(Texts.Msg_VmdLoadSuccess, totalFrames));
        }
        catch (Exception ex)
        {
            onNotification?.Invoke(this, string.Format(Texts.Msg_VmdLoadFailed, ex.Message));
        }
    }
}