using ObjLoader.Services.Camera;

namespace ObjLoader.ViewModels.Camera;

internal class CameraPlaybackController(
    CameraAnimationManager animationManager,
    Func<double> getCurrentTime,
    Action<double> setCurrentTime,
    Func<double> getMaxDuration,
    Action notifyIsPlayingChanged)
{
    public bool IsPlaying
    {
        get => animationManager.IsPlaying;
        set
        {
            if (value) animationManager.Start();
            else animationManager.Pause();
            notifyIsPlayingChanged();
        }
    }

    public void StopPlayback()
    {
        animationManager.Stop();
        setCurrentTime(0);
        notifyIsPlayingChanged();
    }

    public void PlaybackTick()
    {
        double nextTime = getCurrentTime() + 0.016;
        double maxDuration = getMaxDuration();
        if (nextTime >= maxDuration)
        {
            setCurrentTime(maxDuration);
            animationManager.Pause();
            notifyIsPlayingChanged();
        }
        else
        {
            setCurrentTime(nextTime);
        }
    }
}