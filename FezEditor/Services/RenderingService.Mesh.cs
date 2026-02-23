using FezEditor.Structure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Services;

public partial class RenderingService
{
    private class SurfaceEntry
    {
        public VertexBuffer? VertexBuffer;
        public IndexBuffer? IndexBuffer;
        public int VertexCount;
        public int PrimitiveCount;
        public PrimitiveType PrimitiveType;
        public Rid Material;
    }
    
    private class MeshData
    {
        public readonly List<SurfaceEntry> Surfaces = new();
    }
    
    private readonly Dictionary<Rid, MeshData> _meshes = new();
    
    public Rid MeshCreate()
    {
        var rid = AllocateRid(typeof(MeshData));
        _meshes[rid] = new MeshData();
        return rid;
    }

    public void MeshClear(Rid mesh)
    {
        var data = GetResource(_meshes, mesh);
        DisposeBuffers(data);
        data.Surfaces.Clear();
    }

    public void MeshAddSurface(Rid mesh, PrimitiveType primitive, MeshSurface surface, Rid? material = null)
    {
        var surfaceEntry = new SurfaceEntry { PrimitiveType = primitive, Material = material ?? Rid.Invalid };
        UpdateSurfaceEntry(surfaceEntry, surface);
        GetResource(_meshes, mesh).Surfaces.Add(surfaceEntry);
    }

    public void MeshUpdateSurface(Rid mesh, int surfaceIdx, MeshSurface surface)
    {
        var data = GetResource(_meshes, mesh);
        ValidateSurfaceIndex(data, surfaceIdx);
        UpdateSurfaceEntry(data.Surfaces[surfaceIdx], surface);
    }

    public int MeshGetSurfaceCount(Rid mesh)
    {
        return GetResource(_meshes, mesh).Surfaces.Count;
    }

    public void MeshRemoveSurface(Rid mesh, int surfaceIdx)
    {
        var data = GetResource(_meshes, mesh);
        ValidateSurfaceIndex(data, surfaceIdx);
        DisposeBuffers(data, surfaceIdx);
        data.Surfaces.RemoveAt(surfaceIdx);
    }

    public PrimitiveType MeshGetSurfacePrimitiveType(Rid mesh, int surfaceIdx)
    {
        var data = GetResource(_meshes, mesh);
        ValidateSurfaceIndex(data, surfaceIdx);
        return data.Surfaces[surfaceIdx].PrimitiveType;
    }

    public void MeshSurfaceSetMaterial(Rid mesh, int surfaceIdx, Rid material)
    {
        var data = GetResource(_meshes, mesh);
        ValidateSurfaceIndex(data, surfaceIdx);
        data.Surfaces[surfaceIdx].Material = material;
    }

    public Rid MeshSurfaceGetMaterial(Rid mesh, int surfaceIdx)
    {
        var data = GetResource(_meshes, mesh);
        ValidateSurfaceIndex(data, surfaceIdx);
        return data.Surfaces[surfaceIdx].Material;
    }
    
    private void DrawMesh(RenderTargetData rt, WorldData world, Rid meshRid, InstanceMatrices matrices)
    {
        if (TryGetResource(_meshes, meshRid, out var mesh))
        {
            foreach (var surface in mesh!.Surfaces)
            {
                if (TryGetResource(_materials, surface.Material, out var mat) && mat != null)
                {
                    CheckMaterialEffect(mat);
                    ApplyMaterialState(mat);
                    if (mat.Effect is BasicEffect)
                    {
                        UpdateBasicEffect(mat, matrices);
                    }
                    else
                    {
                        UpdateBaseEffect(rt, world, mat, matrices);
                    }
                    DrawSurfaceEntry(surface, mat.Effect!);
                    RestoreDefaultState();
                }
            }
        }
    }
    
    private void DrawSurfaceEntry(SurfaceEntry surface, Effect effect)
    {
        if (surface is { VertexBuffer: not null, IndexBuffer: not null }) 
        {
            GraphicsDevice.SetVertexBuffer(surface.VertexBuffer);
            GraphicsDevice.Indices = surface.IndexBuffer;
            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawIndexedPrimitives(
                    primitiveType: surface.PrimitiveType,
                    baseVertex: 0, 
                    minVertexIndex: 0,
                    numVertices: surface.VertexCount,
                    startIndex: 0,
                    primitiveCount: surface.PrimitiveCount
                );
            }
        }
    }

    private void UpdateSurfaceEntry(SurfaceEntry entry, MeshSurface surface)
    {
        if (surface.Vertices.Length == 0 || surface.Indices.Length == 0)
        {
            return;
        }
        
        entry.VertexBuffer?.Dispose();
        entry.IndexBuffer?.Dispose();
        entry.VertexBuffer = null;
        entry.IndexBuffer = null;
        entry.VertexCount = surface.Vertices.Length;
        entry.PrimitiveCount = entry.PrimitiveType switch
        {
            PrimitiveType.LineList => surface.Indices.Length / 2,
            PrimitiveType.LineStrip => surface.Indices.Length - 1,
            PrimitiveType.TriangleList => surface.Indices.Length / 3,
            PrimitiveType.TriangleStrip => surface.Indices.Length - 2,
            _ => 0
        };

        if (entry.PrimitiveCount < 1)
        {
            throw new ArgumentException("Invalid number of indices: " + surface.Indices.Length);
        }
        
        var hasNormals = surface.Normals is { Length: > 0 };
        var hasTexCoords = surface.TexCoords is { Length: > 0 };
        var hasColors = surface.Colors is { Length: > 0 };

        if (hasTexCoords)
        {
            if (hasNormals)
            {
                var geometry = Enumerable.Range(0, surface.Vertices.Length)
                    .Select(i => new VertexPositionNormalTexture(surface.Vertices[i], surface.Normals![i], surface.TexCoords![i]))
                    .ToArray();
            
                entry.VertexBuffer = new VertexBuffer(GraphicsDevice, typeof(VertexPositionNormalTexture), geometry.Length, BufferUsage.WriteOnly);
                entry.VertexBuffer.SetData(geometry);
            }
            else if (hasColors)
            {
                var geometry = Enumerable.Range(0, surface.Vertices.Length)
                    .Select(i => new VertexPositionColorTexture(surface.Vertices[i], surface.Colors![i], surface.TexCoords![i]))
                    .ToArray();
            
                entry.VertexBuffer = new VertexBuffer(GraphicsDevice, typeof(VertexPositionColorTexture), geometry.Length, BufferUsage.WriteOnly);
                entry.VertexBuffer.SetData(geometry);
            }
            else
            {
                var geometry = Enumerable.Range(0, surface.Vertices.Length)
                    .Select(i => new VertexPositionTexture(surface.Vertices[i], surface.TexCoords![i]))
                    .ToArray();

                entry.VertexBuffer = new VertexBuffer(GraphicsDevice, typeof(VertexPositionTexture), geometry.Length, BufferUsage.WriteOnly);
                entry.VertexBuffer.SetData(geometry);
            }
        }
        else
        {
            if (hasNormals)
            {
                var geometry = Enumerable.Range(0, surface.Vertices.Length)
                    .Select(i => new VertexPositionNormalColor(surface.Vertices[i], surface.Normals![i], hasColors ? surface.Colors![i] : Color.White))
                    .ToArray();
                
                entry.VertexBuffer = new VertexBuffer(GraphicsDevice, typeof(VertexPositionNormalColor), geometry.Length, BufferUsage.WriteOnly);
                entry.VertexBuffer.SetData(geometry);
            }
            else
            {
                var geometry = Enumerable.Range(0, surface.Vertices.Length)
                    .Select(i => new VertexPositionColor(surface.Vertices[i], hasColors ? surface.Colors![i] : Color.White))
                    .ToArray();

                entry.VertexBuffer = new VertexBuffer(GraphicsDevice, typeof(VertexPositionColor), geometry.Length, BufferUsage.WriteOnly);
                entry.VertexBuffer.SetData(geometry);
            }
        }
        
        entry.IndexBuffer = new IndexBuffer(GraphicsDevice, IndexElementSize.ThirtyTwoBits, surface.Indices.Length, BufferUsage.WriteOnly);
        entry.IndexBuffer.SetData(surface.Indices);
    }
    
    private static void ValidateSurfaceIndex(MeshData mesh, int index)
    {
        if (index < 0 || index >= mesh.Surfaces.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Surface index {index} out of range [0, {mesh.Surfaces.Count})");
        }
    }

    private static void DisposeBuffers(MeshData mesh, int? index = null)
    {
        for (var i = 0; i < mesh.Surfaces.Count; i++)
        {
            if (index != null && i != index) continue;
            var surface = mesh.Surfaces[i];
            surface.VertexBuffer?.Dispose();
            surface.IndexBuffer?.Dispose();
            surface.VertexBuffer = null;
            surface.IndexBuffer = null;
        }
    }
}