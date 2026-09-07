using FezEditor.Structure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Services;

public partial class RenderingService
{
    private class MultiMeshData
    {
        public Rid Mesh;
        public int InstanceCount;
        public int InstanceCapacity;
        public int VisibleInstances = -1; // -1 = all
        public MultiMeshDataType DataType;
        public Matrix[] MatrixUploadBuffer = Array.Empty<Matrix>();
        public Vector4[] VectorUploadBuffer = Array.Empty<Vector4>();
        public bool Dirty = true;

        // GPU buffers for hardware instancing.
        public VertexBuffer? TemplateVertexBuffer;
        public IndexBuffer? TemplateIndexBuffer;
        public DynamicVertexBuffer? InstanceBuffer;
        public VertexDeclaration? InstanceDeclaration;
        public int TemplateVertexCount;
        public int TemplatePrimitiveCount;
        public PrimitiveType TemplatePrimitiveType;
    }

    private readonly Dictionary<Rid, MultiMeshData> _multiMeshes = new();

    public Rid MultiMeshCreate()
    {
        var rid = AllocateRid(typeof(MultiMeshData));
        _multiMeshes[rid] = new MultiMeshData();
        Logger.Verbose("MultiMesh created {0}", rid);
        return rid;
    }

    public void MultiMeshSetMesh(Rid multiMesh, Rid mesh)
    {
        GetResource(_multiMeshes, multiMesh).Mesh = mesh;
    }

    public Rid MultiMeshGetMesh(Rid multiMesh)
    {
        return GetResource(_multiMeshes, multiMesh).Mesh;
    }

    public void MultiMeshAllocate(Rid multiMesh, int instances, MultiMeshDataType dataType)
    {
        var data = GetResource(_multiMeshes, multiMesh);
        EnsureMultiMeshCapacity(data, instances, dataType);
        data.InstanceCount = instances;
        data.VisibleInstances = instances;
        data.Dirty = true;
        Logger.Verbose("MultiMesh {0} allocated {1} instance(s), dataType={2}", multiMesh, instances, dataType);
    }

    public int MultiMeshSetInstances(Rid multiMesh, IEnumerable<Matrix> instances)
    {
        ArgumentNullException.ThrowIfNull(instances);
        var data = GetResource(_multiMeshes, multiMesh);
        var count = 0;

        if (instances.TryGetNonEnumeratedCount(out var knownCount))
        {
            EnsureMultiMeshCapacity(data, knownCount, MultiMeshDataType.Matrix);
        }

        foreach (var instance in instances)
        {
            if (count == data.InstanceCapacity)
            {
                var capacity = Math.Max(4, data.InstanceCapacity * 2);
                EnsureMultiMeshCapacity(data, capacity, MultiMeshDataType.Matrix);
            }

            data.MatrixUploadBuffer[count++] = instance;
        }

        data.InstanceCount = count;
        data.VisibleInstances = count;
        data.Dirty = true;
        return count;
    }

    public void MultiMeshDeallocate(Rid multiMesh)
    {
        var data = GetResource(_multiMeshes, multiMesh);
        data.InstanceBuffer?.Dispose();
        data.InstanceDeclaration?.Dispose();
        _multiMeshes[multiMesh] = new MultiMeshData();
        Logger.Verbose("MultiMesh {0} deallocated", multiMesh);
    }

    public int MultiMeshGetInstanceCount(Rid multiMesh)
    {
        return GetResource(_multiMeshes, multiMesh).InstanceCount;
    }

    public void MultiMeshSetVisibleInstances(Rid multiMesh, int visible)
    {
        GetResource(_multiMeshes, multiMesh).VisibleInstances = visible;
    }

    public int MultiMeshGetVisibleInstances(Rid multiMesh)
    {
        var data = GetResource(_multiMeshes, multiMesh);
        return data.VisibleInstances < 0 ? data.InstanceCount : data.VisibleInstances;
    }

    public void MultiMeshSetInstanceMatrix(Rid multiMesh, int index, Matrix value)
    {
        var mm = GetResource(_multiMeshes, multiMesh);
        ValidateMultiMeshIndex(mm, index);
        if (mm.DataType != MultiMeshDataType.Matrix)
        {
            throw new InvalidOperationException(
                "MultiMesh was allocated with Vector4 data type, use MultiMeshSetInstanceVector4");
        }

        mm.MatrixUploadBuffer[index] = value;
        mm.Dirty = true;
    }

    public void MultiMeshSetInstanceVector4(Rid multiMesh, int index, Vector4 value)
    {
        var mm = GetResource(_multiMeshes, multiMesh);
        ValidateMultiMeshIndex(mm, index);
        if (mm.DataType != MultiMeshDataType.Vector4)
        {
            throw new InvalidOperationException(
                "MultiMesh was allocated with Matrix data type, use MultiMeshSetInstanceMatrix");
        }

        mm.VectorUploadBuffer[index] = value;
        mm.Dirty = true;
    }

    private void DrawMultiMesh(RenderTargetData rt, WorldData world, Rid multiMeshRid, InstanceMatrices matrices)
    {
        if (!TryGetResource(_multiMeshes, multiMeshRid, out var mm))
        {
            return;
        }

        var visible = mm!.VisibleInstances < 0 ? mm.InstanceCount : mm.VisibleInstances;
        if (visible <= 0)
        {
            return;
        }

        if (mm.InstanceBuffer == null || mm.InstanceDeclaration == null)
        {
            throw new InvalidOperationException($"MultiMesh {multiMeshRid} was not allocated.");
        }

        if (mm.TemplateVertexBuffer == null || mm.TemplateIndexBuffer == null)
        {
            if (!TryGetResource(_meshes, mm.Mesh, out var md))
            {
                throw new InvalidOperationException($"MultiMesh {multiMeshRid} does not have assigned mesh.");
            }

            // Build template GPU buffers from the first surface of the referenced mesh.
            if (md!.Surfaces.Count > 0)
            {
                var surface = md.Surfaces[0];
                mm.TemplatePrimitiveType = surface.PrimitiveType;
                mm.TemplatePrimitiveCount = surface.PrimitiveCount;
                mm.TemplateVertexCount = surface.VertexCount;
                mm.TemplateVertexBuffer = surface.VertexBuffer;
                mm.TemplateIndexBuffer = surface.IndexBuffer;
            }
        }

        if (mm.TemplatePrimitiveCount <= 0)
        {
            return;
        }

        // Upload dirty instance data to GPU.
        if (mm.Dirty)
        {
            if (mm.InstanceBuffer != null)
            {
                if (mm.DataType == MultiMeshDataType.Matrix)
                {
                    mm.InstanceBuffer.SetData(mm.MatrixUploadBuffer, 0, visible, SetDataOptions.Discard);
                }
                else
                {
                    mm.InstanceBuffer.SetData(mm.VectorUploadBuffer, 0, visible, SetDataOptions.Discard);
                }
            }

            mm.Dirty = false;
        }

        // Resolve material from first surface.
        var material = Rid.Invalid;
        if (TryGetResource(_meshes, mm.Mesh, out var mesh))
        {
            var firstSurface = mesh!.Surfaces.FirstOrDefault();
            material = firstSurface?.Material ?? Rid.Invalid;
        }

        if (!TryGetResource(_materials, material, out var mat) || mat == null)
        {
            return;
        }

        ApplyMaterialState(mat);
        if (mat.Effect is BasicEffect)
        {
            UpdateBasicEffect(world, mat, matrices);
        }
        else if (mat.Effect != null)
        {
            UpdateBaseEffect(rt, world, mat, matrices);
        }
        else
        {
            throw new InvalidOperationException($"Effect was not assigned to material {material}!");
        }

        // Bind template geometry + instance buffer.
        GraphicsDevice.SetVertexBuffers(
            new VertexBufferBinding(mm.TemplateVertexBuffer, 0, 0),
            new VertexBufferBinding(mm.InstanceBuffer, 0, 1)
        );
        GraphicsDevice.Indices = mm.TemplateIndexBuffer;

        // Draw instanced.
        foreach (var pass in mat.Effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawInstancedPrimitives(
                mm.TemplatePrimitiveType,
                0,
                0,
                mm.TemplateVertexCount,
                0,
                mm.TemplatePrimitiveCount,
                visible
            );
            _drawCalls++;
            _primitives += mm.TemplatePrimitiveCount * visible;
        }

        RestoreDefaultState();
    }

    private void InvalidateMultiMesh(Rid mesh, bool removeMesh = false)
    {
        foreach (var mm in _multiMeshes.Values)
        {
            if (mm.Mesh == mesh)
            {
                // Clear cached buffers before their mesh disposes or replaces them
                if (removeMesh)
                {
                    mm.Mesh = Rid.Invalid;
                }

                mm.TemplateVertexBuffer = null;
                mm.TemplateIndexBuffer = null;
                mm.TemplateVertexCount = 0;
                mm.TemplatePrimitiveCount = 0;
            }
        }
    }

    private static void ValidateMultiMeshIndex(MultiMeshData mm, int index)
    {
        if (index < 0 || index >= mm.InstanceCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index),
                $"MultiMesh instance index {index} out of range [0, {mm.InstanceCount})");
        }
    }

    private void EnsureMultiMeshCapacity(MultiMeshData data, int capacity, MultiMeshDataType dataType)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        var typeChanged = data.InstanceCapacity > 0 && data.DataType != dataType;
        if (!typeChanged && capacity <= data.InstanceCapacity)
        {
            data.DataType = dataType;
            return;
        }

        data.InstanceBuffer?.Dispose();
        data.InstanceDeclaration?.Dispose();
        data.InstanceBuffer = null;
        data.InstanceDeclaration = null;
        data.DataType = dataType;
        data.InstanceCapacity = capacity;

        if (dataType == MultiMeshDataType.Matrix)
        {
            Array.Resize(ref data.MatrixUploadBuffer, capacity);
            data.VectorUploadBuffer = Array.Empty<Vector4>();
        }
        else
        {
            Array.Resize(ref data.VectorUploadBuffer, capacity);
            data.MatrixUploadBuffer = Array.Empty<Matrix>();
        }

        if (capacity == 0)
        {
            return;
        }

        var stride = dataType.GetStride();
        var elements = new VertexElement[stride];
        for (var i = 0; i < stride; i++)
        {
            elements[i] = new VertexElement(i * 16, VertexElementFormat.Vector4,
                VertexElementUsage.TextureCoordinate, 1 + i);
        }

        data.InstanceDeclaration = new VertexDeclaration(stride * 16, elements);
        data.InstanceBuffer =
            new DynamicVertexBuffer(GraphicsDevice, data.InstanceDeclaration, capacity, BufferUsage.WriteOnly);
    }
}