using ObjLoader.Plugin.CameraAnimation;

namespace ObjLoader.Services.Camera
{
    public class CameraService
    {
        public (double cx, double cy, double cz, double tx, double ty, double tz) CalculateCameraState(List<CameraKeyframe> keyframes, double time)
        {
            if (keyframes == null || keyframes.Count == 0) return (0, 0, 0, 0, 0, 0);

            CameraKeyframe? prev = null;
            CameraKeyframe? next = null;
            double maxPrevTime = double.MinValue;
            double minNextTime = double.MaxValue;

            for (int i = 0; i < keyframes.Count; i++)
            {
                var k = keyframes[i];
                if (k.Time <= time && k.Time > maxPrevTime)
                {
                    prev = k;
                    maxPrevTime = k.Time;
                }
                if (k.Time > time && k.Time < minNextTime)
                {
                    next = k;
                    minNextTime = k.Time;
                }
            }

            if (prev == null && next != null) return (next.CamX, next.CamY, next.CamZ, next.TargetX, next.TargetY, next.TargetZ);
            if (prev != null && next == null) return (prev.CamX, prev.CamY, prev.CamZ, prev.TargetX, prev.TargetY, prev.TargetZ);
            if (prev != null && next != null)
            {
                double t = (time - prev.Time) / (next.Time - prev.Time);
                double easedT = prev.Easing.Evaluate(t);
                return (
                    Lerp(prev.CamX, next.CamX, easedT),
                    Lerp(prev.CamY, next.CamY, easedT),
                    Lerp(prev.CamZ, next.CamZ, easedT),
                    Lerp(prev.TargetX, next.TargetX, easedT),
                    Lerp(prev.TargetY, next.TargetY, easedT),
                    Lerp(prev.TargetZ, next.TargetZ, easedT)
                );
            }
            return (0, 0, 0, 0, 0, 0);
        }

        private double Lerp(double a, double b, double t)
        {
            return a + (b - a) * t;
        }
    }
}