using ObjLoader.Api.Core;

namespace ObjLoader.Api.Light
{
    public interface ILightApi
    {
        ApiResult<LightDescriptor> GetLight(int worldId);
        bool SetLight(int worldId, in LightDescriptor light);
        IReadOnlyList<int> GetWorldIds();
    }
}