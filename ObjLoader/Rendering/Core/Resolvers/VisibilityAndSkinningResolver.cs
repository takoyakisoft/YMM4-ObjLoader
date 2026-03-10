using System.IO;
using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using ObjLoader.Cache.Gpu;
using ObjLoader.Core.Models;
using ObjLoader.Core.Timeline;
using ObjLoader.Parsers;
using ObjLoader.Rendering.Managers.Interfaces;
using ObjLoader.Services.Mmd.Animation;
using ObjLoader.Services.Mmd.Parsers;
using YukkuriMovieMaker.Commons;
using ObjLoader.Rendering.Core.States;
using ObjLoader.Rendering.Core.Resources;

namespace ObjLoader.Rendering.Core.Resolvers;

internal sealed class VisibilityAndSkinningResolver(
    IGraphicsDevicesAndContext devices,
    ObjModelLoader loader,
    ISkinningManager skinningManager,
    IDynamicTextureManager dynamicTextureManager,
    GpuResourceFactory resourceFactory)
{
    private const int MaxHierarchyDepth = 100;

    public List<(LayerData Data, GpuResourceCacheItem Resource, LayerState State, ID3D11Buffer? OverrideVB)> LayersToRender { get; } = new(8);
    private readonly HashSet<string> _activeSkinningGuids = [];
    private readonly HashSet<string> _usedTexturePaths = [];

    public void Process(
        Dictionary<string, LayerState> newLayerStates,
        List<(string Guid, LayerState State, LayerData Data)> preCalcStates,
        double currentTime)
    {
        LayersToRender.Clear();

        var preCalcSpan = CollectionsMarshal.AsSpan(preCalcStates);
        foreach (var item in preCalcSpan)
        {
            if (!newLayerStates.TryGetValue(item.Guid, out var layerState)) continue;

            bool effectiveVisibility = layerState.IsVisible;
            var parentGuid = layerState.ParentGuid;
            int depth = 0;
            while (effectiveVisibility && !string.IsNullOrEmpty(parentGuid) && newLayerStates.TryGetValue(parentGuid, out var parentState))
            {
                if (!parentState.IsVisible)
                {
                    effectiveVisibility = false;
                    break;
                }
                parentGuid = parentState.ParentGuid;
                depth++;
                if (depth > MaxHierarchyDepth) break;
            }

            if (effectiveVisibility && !string.IsNullOrEmpty(layerState.FilePath))
            {
                GpuResourceCacheItem? resource = null;
                ObjModel? loadedModel = null;
                if (GpuResourceCache.Instance.TryGetValue(layerState.CacheKey, out var cached))
                {
                    if (cached != null && cached.Device == devices.D3D.Device)
                    {
                        resource = cached;
                    }
                }

                if (resource == null)
                {
                    loadedModel = loader.Load(layerState.FilePath);
                    if (loadedModel.Vertices.Length > 0)
                    {
                        resource = resourceFactory.Create(loadedModel, layerState.FilePath);
                    }
                }

                if (resource != null)
                {
                    if (item.Data.BoneAnimatorInstance == null && !string.IsNullOrEmpty(item.Data.VmdFilePath) && File.Exists(item.Data.VmdFilePath))
                    {
                        try
                        {
                            var vmdData = VmdParser.Parse(item.Data.VmdFilePath);
                            item.Data.VmdMotionData = vmdData;
                            if (vmdData.BoneFrames.Count > 0 && Path.GetExtension(layerState.FilePath).Equals(".pmx", StringComparison.OrdinalIgnoreCase))
                            {
                                loadedModel ??= loader.Load(layerState.FilePath);
                                if (loadedModel.BoneWeights != null)
                                {
                                    skinningManager.RegisterSkinningState(item.Guid, layerState.FilePath, loadedModel.Vertices, loadedModel.BoneWeights);
                                }
                                if (loadedModel.Bones.Count > 0)
                                {
                                    item.Data.BoneAnimatorInstance = new BoneAnimator(loadedModel.Bones, vmdData.BoneFrames, loadedModel.RigidBodies, loadedModel.Joints);
                                }
                            }
                        }
                        catch
                        {
                        }
                    }

                    double motionTime = currentTime - item.Data.VmdTimeOffset;
                    if (motionTime < 0) motionTime = 0;

                    skinningManager.ProcessSkinning(item.Guid, layerState.FilePath, item.Data.BoneAnimatorInstance, motionTime);

                    if (layerState.FilePath.EndsWith(".pmx", StringComparison.OrdinalIgnoreCase))
                        _activeSkinningGuids.Add(item.Guid);

                    ID3D11Buffer? overrideVB = skinningManager.GetOverrideVertexBuffer(item.Guid);

                    LayersToRender.Add((item.Data, resource, layerState, overrideVB));
                }
            }
        }
        skinningManager.CleanupStaleStates(_activeSkinningGuids);
        _activeSkinningGuids.Clear();
        PrepareDynamicTextures();
    }

    private void PrepareDynamicTextures()
    {
        var device = devices.D3D.Device;
        if (device == null) return;

        _usedTexturePaths.Clear();

        foreach (var layer in LayersToRender)
        {
            if (layer.State.PartMaterials != null)
            {
                foreach (var pm in layer.State.PartMaterials.Values)
                {
                    if (!string.IsNullOrEmpty(pm.TexturePath))
                    {
                        _usedTexturePaths.Add(pm.TexturePath!);
                    }
                }
            }
        }
        dynamicTextureManager.Prepare(_usedTexturePaths, device);
    }
}