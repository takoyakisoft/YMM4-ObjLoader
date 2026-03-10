using System.Numerics;
using Vortice.Direct3D11;
using ObjLoader.Rendering.Renderers;
using ObjLoader.Services.Rendering.Device;
using ObjLoader.Rendering.Core.Buffers;
using ObjLoader.Rendering.Core.Resources;

namespace ObjLoader.Services.Rendering.Passes;

internal class RenderPassContext
{
    public required ID3D11DeviceContext DeviceContext { get; init; }
    public required D3DResources Resources { get; init; }
    public required IReadOnlyList<LayerRenderData> Layers { get; init; }
    public required Matrix4x4[] LayerWorlds { get; init; }
    public required Matrix4x4[] LayerWvps { get; init; }
    
    public required IReadOnlyList<(int LayerIndex, int PartIndex)> OpaqueParts { get; init; }
    public required IReadOnlyList<TransparentPart> TransparentParts { get; init; }
    
    public required ID3D11Buffer GridVertexBuffer { get; init; }
    public required Vector4 GridColor { get; init; }
    public required Vector4 AxisColor { get; init; }
    
    public Matrix4x4 LightViewProj { get; set; } = Matrix4x4.Identity;
    public bool EnableShadow { get; set; }
    public bool RenderShadowMap { get; set; }
    
    public required ID3D11ShaderResourceView[] ShadowSrvArray { get; init; }
    public required ID3D11SamplerState[] SamplerArray { get; init; }
    public required ID3D11SamplerState[] ShadowSamplerArray { get; init; }
    public required IReadOnlyDictionary<string, ID3D11ShaderResourceView> DynamicTextures { get; init; }
    
    public required Matrix4x4 View { get; init; }
    public required Matrix4x4 Proj { get; init; }
    public required Vector3 CamPos { get; init; }
    
    public required ConstantBuffer<CBPerFrame> CbPerFrame { get; init; }
    public required ConstantBuffer<CBPerObject> CbPerObject { get; init; }
    public required ConstantBuffer<CBPerMaterial> CbPerMaterialCore { get; init; }
    public required ConstantBuffer<CBSceneEffects> CbSceneEffects { get; init; }
    public required ConstantBuffer<CBPostEffects> CbPostEffects { get; init; }
    public required ID3D11Buffer[] CbPerFrameArray { get; init; }
    public required ID3D11Buffer[] CbPerObjectArray { get; init; }
    public required ID3D11Buffer[] CbPerMaterialArray { get; init; }
    public required ID3D11Buffer[] CbSceneEffectsArray { get; init; }
    public required ID3D11Buffer[] CbPostEffectsArray { get; init; }
    
    public required bool IsWireframe { get; init; }
    public required bool IsInteracting { get; init; }
    public required bool IsGridVisible { get; init; }
    public required bool IsInfiniteGrid { get; init; }
    public required double GridScale { get; init; }
    
    public ApiObjectRenderer? ApiObjectRenderer { get; set; }
    public LocalDrawManagerAdapter? DrawManagerAdapter { get; set; }
    
    public required ID3D11RenderTargetView MainRtv { get; init; }
    public required ID3D11DepthStencilView MainDsv { get; init; }
    public required int ViewportWidth { get; init; }
    public required int ViewportHeight { get; init; }
}