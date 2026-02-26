using ObjLoader.Api.Core;
using ObjLoader.Core.Models;
using ObjLoader.Core.Timeline;
using ObjLoader.Services.Layers;

namespace ObjLoader.Api.Material
{
    internal sealed class MaterialApi : IMaterialApi
    {
        private readonly ILayerManager _layerManager;

        internal MaterialApi(ILayerManager layerManager)
        {
            _layerManager = layerManager ?? throw new ArgumentNullException(nameof(layerManager));
        }

        public ApiResult<MaterialDescriptor> GetMaterial(SceneObjectId objectId, int partIndex)
        {
            var layer = FindLayer(objectId);
            if (layer == null) return ApiResult<MaterialDescriptor>.Fail($"Object not found: {objectId}");

            if (layer.PartMaterials != null && layer.PartMaterials.TryGetValue(partIndex, out var pm))
            {
                var desc = new MaterialDescriptor(
                    pm.BaseColor,
                    (float)pm.Roughness,
                    (float)pm.Metallic,
                    1.0f, 0.5f, 32.0f,
                    pm.TexturePath);
                return ApiResult<MaterialDescriptor>.Ok(desc);
            }

            return ApiResult<MaterialDescriptor>.Ok(new MaterialDescriptor(
                layer.BaseColor,
                0.5f, 0.0f,
                1.0f, 0.5f, 32.0f,
                null));
        }

        public bool SetMaterial(SceneObjectId objectId, int partIndex, in MaterialDescriptor material)
        {
            var layer = FindLayer(objectId);
            if (layer == null) return false;

            if (layer.PartMaterials == null) return false;

            if (!layer.PartMaterials.TryGetValue(partIndex, out var existing))
            {
                existing = new PartMaterialData();
                layer.PartMaterials[partIndex] = existing;
            }

            existing.BaseColor = material.BaseColor;
            existing.Roughness = material.Roughness;
            existing.Metallic = material.Metallic;
            existing.TexturePath = material.TexturePath;
            return true;
        }

        public int GetPartCount(SceneObjectId objectId)
        {
            var layer = FindLayer(objectId);
            if (layer == null || string.IsNullOrEmpty(layer.FilePath)) return 0;

            return layer.PartMaterials?.Count ?? 0;
        }

        private LayerData? FindLayer(SceneObjectId id)
        {
            foreach (var layer in _layerManager.Layers)
            {
                if (layer.Guid == id.Guid) return layer;
            }
            return null;
        }
    }
}