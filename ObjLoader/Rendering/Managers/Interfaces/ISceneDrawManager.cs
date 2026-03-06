using ObjLoader.Api.Draw;
using Vortice.Direct3D11;

namespace ObjLoader.Rendering.Managers.Interfaces
{
    internal interface ISceneDrawManager : IDisposable
    {
        event EventHandler? Updated;
        bool IsDirty { get; }
        long UpdateCount { get; }
        void UpdateFromApi(SceneDrawApi api);
        IReadOnlyCollection<ExternalObjectHandle> GetExternalObjects();
        IReadOnlyCollection<(Api.Core.SceneObjectId Id, BillboardDescriptor Desc)> GetBillboards();
        ID3D11ShaderResourceView? GetBillboardSrv(Api.Core.SceneObjectId id);
        nint GetBillboardSharedHandle(Api.Core.SceneObjectId id);
        void ClearDirtyFlag();
        void Clear();
    }
}