using ObjLoader.Core.Mmd;
using ObjLoader.Core.Models;
using ObjLoader.Parsers;
using ObjLoader.Rendering.Core.Resolvers;
using ObjLoader.Rendering.Managers.Interfaces;
using ObjLoader.Rendering.Processors;
using ObjLoader.Services.Mmd.Animation;
using System.Runtime.CompilerServices;
using Vortice.Direct3D11;

namespace ObjLoader.Rendering.Managers
{
    public sealed class SkinningManager : ISkinningManager
    {
        private readonly Dictionary<string, SkinningState> _skinningStates = new Dictionary<string, SkinningState>();
        private readonly List<string> _staleGuidsBuffer = new List<string>();
        private readonly ID3D11Device _device;

        public SkinningManager(ID3D11Device device)
        {
            _device = device;
        }

        public void RegisterSkinningState(string guid, string filePath, ObjVertex[] vertices, VertexBoneWeight[] boneWeights)
        {
            _skinningStates[guid] = new SkinningState
            {
                FilePath = filePath,
                OriginalVertices = vertices,
                BoneWeights = boneWeights
            };
        }

        public void RemoveSkinningState(string guid)
        {
            if (_skinningStates.TryGetValue(guid, out var oldSkin))
            {
                oldSkin.GpuProcessor?.Dispose();
                if (!oldSkin.UseGpuSkinning)
                    oldSkin.DynamicVB?.Dispose();
                oldSkin.DynamicVB = null;
                _skinningStates.Remove(guid);
            }
        }

        public void CleanupStaleStates(HashSet<string> activeGuids)
        {
            if (_skinningStates.Count == 0) return;
            _staleGuidsBuffer.Clear();
            foreach (var key in _skinningStates.Keys)
            {
                if (!activeGuids.Contains(key))
                    _staleGuidsBuffer.Add(key);
            }
            foreach (var key in _staleGuidsBuffer)
                RemoveSkinningState(key);
        }

        public ID3D11Buffer? GetOverrideVertexBuffer(string guid)
        {
            if (_skinningStates.TryGetValue(guid, out var state))
            {
                return state.DynamicVB;
            }
            return null;
        }

        public void ProcessSkinning(string guid, string filePath, BoneAnimator? animator, double currentTime)
        {
            if (animator == null || !filePath.EndsWith(".pmx", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            bool needRebuild = false;
            if (_skinningStates.TryGetValue(guid, out var skinState))
            {
                if (!string.Equals(skinState.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
                {
                    RemoveSkinningState(guid);
                    skinState = null;
                    needRebuild = true;
                }
            }
            else
            {
                needRebuild = true;
            }

            if (needRebuild || skinState == null)
            {
                try
                {
                    var skinModel = new PmxParser().Parse(filePath);
                    if (skinModel.BoneWeights != null && skinModel.Bones.Count > 0)
                    {
                        skinState = new SkinningState
                        {
                            FilePath = filePath,
                            OriginalVertices = skinModel.Vertices.ToArray(),
                            BoneWeights = skinModel.BoneWeights
                        };
                        _skinningStates[guid] = skinState;
                    }
                }
                catch
                {
                }
            }

            if (skinState != null)
            {
                ApplySkinningToResource(animator, skinState, currentTime);
            }
        }

        private unsafe void ApplySkinningToResource(BoneAnimator animator, SkinningState skinState, double currentTime)
        {
            var boneTransforms = animator.ComputeBoneTransforms(currentTime);
            try
            {
                var device = _device;
                var context = device.ImmediateContext;

                if (skinState.GpuProcessor == null)
                {
                    skinState.GpuProcessor = new GpuSkinningProcessor();
                    try
                    {
                        skinState.GpuProcessor.Initialize(device);
                    }
                    catch
                    {
                        skinState.GpuProcessor.Dispose();
                        skinState.GpuProcessor = null;
                    }
                }

                if (skinState.GpuProcessor != null && skinState.GpuProcessor.IsAvailable)
                {
                    try
                    {
                        var gpuResult = skinState.GpuProcessor.ApplySkinning(device, context, skinState.OriginalVertices, skinState.BoneWeights, boneTransforms);
                        if (gpuResult != null)
                        {
                            if (!skinState.UseGpuSkinning)
                                skinState.DynamicVB?.Dispose();
                            skinState.DynamicVB = gpuResult;
                            skinState.UseGpuSkinning = true;
                            return;
                        }
                    }
                    catch
                    {
                    }
                }

                var skinnedVerts = VmdMotionApplier.ApplySkinning(skinState.OriginalVertices, skinState.BoneWeights, boneTransforms);
                try
                {
                    int originalLength = skinState.OriginalVertices.Length;
                    int requiredSize = originalLength * Unsafe.SizeOf<ObjVertex>();

                    if (skinState.UseGpuSkinning)
                    {
                        skinState.DynamicVB = null;
                        skinState.UseGpuSkinning = false;
                    }

                    if (skinState.DynamicVB != null)
                    {
                        var existingDesc = skinState.DynamicVB.Description;
                        if (existingDesc.ByteWidth != requiredSize)
                        {
                            skinState.DynamicVB.Dispose();
                            skinState.DynamicVB = null;
                        }
                    }

                    if (skinState.DynamicVB == null)
                    {
                        var desc = new BufferDescription(requiredSize, BindFlags.VertexBuffer, ResourceUsage.Dynamic, CpuAccessFlags.Write);
                        skinState.DynamicVB = device.CreateBuffer(desc);
                    }

                    var mapped = context.Map(skinState.DynamicVB, MapMode.WriteDiscard);
                    fixed (ObjVertex* pVerts = skinnedVerts)
                    {
                        Buffer.MemoryCopy(pVerts, (void*)mapped.DataPointer, mapped.RowPitch, requiredSize);
                    }
                    context.Unmap(skinState.DynamicVB, 0);
                }
                finally
                {
                    System.Buffers.ArrayPool<ObjVertex>.Shared.Return(skinnedVerts);
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                System.Buffers.ArrayPool<System.Numerics.Matrix4x4>.Shared.Return(boneTransforms);
            }
        }

        public void Dispose()
        {
            foreach (var kvp in _skinningStates)
            {
                kvp.Value.GpuProcessor?.Dispose();
                if (!kvp.Value.UseGpuSkinning)
                    kvp.Value.DynamicVB?.Dispose();
            }
            _skinningStates.Clear();
        }
    }
}