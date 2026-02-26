using System.Numerics;

namespace ObjLoader.Api.Raycast
{
    public interface IRaycastApi
    {
        IReadOnlyList<RaycastHit> CastRay(Vector3 origin, Vector3 direction, RaycastFilter? filter = null);
        IReadOnlyList<RaycastHit> CastRayFromScreenNdc(Vector2 screenNdc, RaycastFilter? filter = null);
    }
}