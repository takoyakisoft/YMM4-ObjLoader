using ObjLoader.Attributes;
using ObjLoader.Cache.Streaming;
using ObjLoader.Core.Interfaces;
using ObjLoader.Core.Models;
using System.IO;
using System.Numerics;
using System.Text;

namespace ObjLoader.Parsers
{
    [ModelParser(2, ".pmd")]
    public class PmdParser : IStreamingModelParser
    {
        static PmdParser()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public bool CanParse(string extension) => extension.ToLowerInvariant() == ".pmd";

        public bool SupportsStreaming => true;

        public unsafe ObjModel Parse(string path)
        {
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);

            var magic = br.ReadBytes(3);
            if (Encoding.ASCII.GetString(magic) != "Pmd") return new ObjModel();

            float ver = br.ReadSingle();

            var encoding = Encoding.GetEncoding(932);

            string name = ReadString(br, 20, encoding);
            string comment = ReadString(br, 256, encoding);

            int vCount = br.ReadInt32();
            var vertices = GC.AllocateUninitializedArray<ObjVertex>(vCount, true);

            for (int i = 0; i < vCount; i++)
            {
                Vector3 p = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                Vector3 n = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                Vector2 uv = new Vector2(br.ReadSingle(), br.ReadSingle());

                br.ReadInt16();
                br.ReadInt16();
                br.ReadByte();
                br.ReadByte();

                vertices[i] = new ObjVertex { Position = p, Normal = n, TexCoord = uv };
            }

            int iCount = br.ReadInt32();
            var indices = GC.AllocateUninitializedArray<int>(iCount, true);
            for (int i = 0; i < iCount; i++)
            {
                indices[i] = br.ReadUInt16();
            }

            int mCount = br.ReadInt32();
            var parts = new List<ModelPart>(mCount);
            int indexOffset = 0;

            for (int i = 0; i < mCount; i++)
            {
                Vector4 diff = new Vector4(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                Vector3 spec = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                float specPow = br.ReadSingle();
                Vector3 amb = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                byte toonIdx = br.ReadByte();
                byte edgeFlag = br.ReadByte();
                int faceCount = br.ReadInt32();
                string texPath = ReadString(br, 20, encoding);

                if (!string.IsNullOrEmpty(texPath))
                {
                    if (texPath.Contains('*'))
                    {
                        texPath = texPath.Split('*')[0];
                    }

                    texPath = texPath.Replace('\\', Path.DirectorySeparatorChar);
                    if (!Path.IsPathRooted(texPath))
                        texPath = Path.Combine(Path.GetDirectoryName(path) ?? "", texPath);
                }

                Vector3 partMin = new Vector3(float.MaxValue);
                Vector3 partMax = new Vector3(float.MinValue);

                if (indexOffset + faceCount <= indices.Length)
                {
                    for (int k = 0; k < faceCount; k++)
                    {
                        int vIdx = indices[indexOffset + k];
                        if (vIdx >= 0 && vIdx < vertices.Length)
                        {
                            var p = vertices[vIdx].Position;
                            partMin = Vector3.Min(partMin, p);
                            partMax = Vector3.Max(partMax, p);
                        }
                    }
                }

                parts.Add(new ModelPart
                {
                    TexturePath = texPath,
                    IndexOffset = indexOffset,
                    IndexCount = faceCount,
                    BaseColor = diff,
                    Center = faceCount > 0 ? (partMin + partMax) * 0.5f : Vector3.Zero
                });

                indexOffset += faceCount;
            }

            ModelHelper.CalculateBounds(vertices, out Vector3 c, out float s);
            return new ObjModel { Vertices = vertices, Indices = indices, Parts = parts, ModelCenter = c, ModelScale = s, Name = name, Comment = comment };
        }

        public unsafe ObjModel StreamToCache(string path, IStreamingCacheWriter cacheWriter)
        {
            string vertexTempPath = Path.GetTempFileName();
            string indexTempPath = Path.GetTempFileName();

            try
            {
                using var fs = File.OpenRead(path);
                using var br = new BinaryReader(fs);

                var magic = br.ReadBytes(3);
                if (Encoding.ASCII.GetString(magic) != "Pmd") return new ObjModel();

                float ver = br.ReadSingle();
                var encoding = Encoding.GetEncoding(932);

                string name = ReadString(br, 20, encoding);
                string comment = ReadString(br, 256, encoding);

                int vCount = br.ReadInt32();

                Vector3 boundsMin = new Vector3(float.MaxValue);
                Vector3 boundsMax = new Vector3(float.MinValue);

                const int ChunkSize = 4096;
                var vertexChunk = new ObjVertex[ChunkSize];
                int chunkPos = 0;

                using (var vtf = new FileStream(vertexTempPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536))
                {
                    for (int i = 0; i < vCount; i++)
                    {
                        Vector3 p = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                        Vector3 n = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                        Vector2 uv = new Vector2(br.ReadSingle(), br.ReadSingle());

                        br.ReadInt16();
                        br.ReadInt16();
                        br.ReadByte();
                        br.ReadByte();

                        vertexChunk[chunkPos] = new ObjVertex { Position = p, Normal = n, TexCoord = uv };
                        boundsMin = Vector3.Min(boundsMin, p);
                        boundsMax = Vector3.Max(boundsMax, p);
                        chunkPos++;

                        if (chunkPos == ChunkSize)
                        {
                            fixed (ObjVertex* pV = vertexChunk)
                            {
                                var span = new ReadOnlySpan<byte>(pV, chunkPos * sizeof(ObjVertex));
                                vtf.Write(span);
                            }
                            chunkPos = 0;
                        }
                    }

                    if (chunkPos > 0)
                    {
                        fixed (ObjVertex* pV = vertexChunk)
                        {
                            var span = new ReadOnlySpan<byte>(pV, chunkPos * sizeof(ObjVertex));
                            vtf.Write(span);
                        }
                    }
                }

                int iCount = br.ReadInt32();
                var indexChunk = new int[ChunkSize];
                int idxChunkPos = 0;

                using (var itf = new FileStream(indexTempPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536))
                {
                    for (int i = 0; i < iCount; i++)
                    {
                        indexChunk[idxChunkPos] = br.ReadUInt16();
                        idxChunkPos++;

                        if (idxChunkPos == ChunkSize)
                        {
                            fixed (int* pI = indexChunk)
                            {
                                var span = new ReadOnlySpan<byte>(pI, idxChunkPos * sizeof(int));
                                itf.Write(span);
                            }
                            idxChunkPos = 0;
                        }
                    }

                    if (idxChunkPos > 0)
                    {
                        fixed (int* pI = indexChunk)
                        {
                            var span = new ReadOnlySpan<byte>(pI, idxChunkPos * sizeof(int));
                            itf.Write(span);
                        }
                    }
                }

                int mCount = br.ReadInt32();
                var parts = new List<ModelPart>(mCount);
                int indexOffset = 0;

                for (int i = 0; i < mCount; i++)
                {
                    Vector4 diff = new Vector4(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    br.ReadSingle();
                    new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    br.ReadByte();
                    br.ReadByte();
                    int faceCount = br.ReadInt32();
                    string texPath = ReadString(br, 20, encoding);

                    if (!string.IsNullOrEmpty(texPath))
                    {
                        if (texPath.Contains('*'))
                        {
                            texPath = texPath.Split('*')[0];
                        }

                        texPath = texPath.Replace('\\', Path.DirectorySeparatorChar);
                        if (!Path.IsPathRooted(texPath))
                            texPath = Path.Combine(Path.GetDirectoryName(path) ?? "", texPath);
                    }

                    parts.Add(new ModelPart
                    {
                        TexturePath = texPath,
                        IndexOffset = indexOffset,
                        IndexCount = faceCount,
                        BaseColor = diff,
                        Center = Vector3.Zero
                    });

                    indexOffset += faceCount;
                }

                Vector3 boundsSize = boundsMax - boundsMin;
                Vector3 center = (boundsMin + boundsMax) * 0.5f;
                float maxDim = Math.Max(boundsSize.X, Math.Max(boundsSize.Y, boundsSize.Z));
                float scale = maxDim > 1e-6f ? 1.5f / maxDim : 1.0f;

                cacheWriter.WriteMetadata(vCount, iCount, parts, center, scale);

                var streamBuffer = new byte[65536];

                using (var vtf = new FileStream(vertexTempPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.SequentialScan))
                {
                    int read;
                    while ((read = vtf.Read(streamBuffer)) > 0)
                    {
                        cacheWriter.WriteVertexChunk(streamBuffer.AsSpan(0, read));
                    }
                }

                using (var itf = new FileStream(indexTempPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.SequentialScan))
                {
                    int read;
                    while ((read = itf.Read(streamBuffer)) > 0)
                    {
                        cacheWriter.WriteIndexChunk(streamBuffer.AsSpan(0, read));
                    }
                }

                return new ObjModel
                {
                    Vertices = Array.Empty<ObjVertex>(),
                    Indices = Array.Empty<int>(),
                    Parts = parts,
                    ModelCenter = center,
                    ModelScale = scale,
                    Name = name,
                    Comment = comment
                };
            }
            finally
            {
                try { if (File.Exists(vertexTempPath)) File.Delete(vertexTempPath); } catch { }
                try { if (File.Exists(indexTempPath)) File.Delete(indexTempPath); } catch { }
            }
        }

        private string ReadString(BinaryReader br, int length, Encoding encoding)
        {
            var bytes = br.ReadBytes(length);
            int zeroIndex = Array.IndexOf(bytes, (byte)0);
            return encoding.GetString(bytes, 0, zeroIndex >= 0 ? zeroIndex : bytes.Length);
        }
    }
}