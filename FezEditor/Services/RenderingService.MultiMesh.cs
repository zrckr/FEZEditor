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
        public int VisibleInstances = -1; // -1 = all
        public MultiMeshDataType DataType;
        public float[] UploadBuffer = Array.Empty<float>(); // pre-allocated, reused every frame
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
        data.InstanceBuffer?.Dispose();
        data.InstanceDeclaration?.Dispose();
        data.TemplateVertexBuffer = null;
        data.TemplateIndexBuffer = null;
        data.InstanceBuffer = null;
        data.InstanceDeclaration = null;
        data.InstanceCount = instances;
        data.DataType = dataType;
        data.VisibleInstances = instances;
        data.Dirty = true;
        
        // Pre-allocate upload buffer
        var stride = dataType.GetStride();
        var floatsPerInstance = dataType.GetFloatsPerInstance();
        data.UploadBuffer = new float[instances * floatsPerInstance];

        // Pre-fill instance indices (never change after allocation).
        for (var i = 0; i < instances; i++)
        {
            data.UploadBuffer[i * floatsPerInstance] = i;
        }

        // Build instance vertex declaration.
        // Layout: InstanceIndex (TEXCOORD1) + Data0..DataN (TEXCOORD2..TEXCOORD5)
        var elements = new VertexElement[1 + stride];
        var offset = 0;

        // InstanceIndex: float -> TEXCOORD1
        elements[0] = new VertexElement(offset, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 1);
        offset += sizeof(float);

        // Data0..DataN: Vector4 -> TEXCOORD2..TEXCOORD5
        for (var i = 0; i < stride; i++)
        {
            elements[1 + i] = new VertexElement(offset, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2 + i);
            offset += 16; // sizeof(Vector4)
        }

        // Allocate instance buffer.
        data.InstanceDeclaration = new VertexDeclaration(offset, elements);
        data.InstanceBuffer = new DynamicVertexBuffer(GraphicsDevice, data.InstanceDeclaration, instances, BufferUsage.WriteOnly);

        // Build template GPU buffers from the first surface of the referenced mesh.
        if (TryGetResource(_meshes, data.Mesh, out var mesh) && mesh!.Surfaces.Count != 0)
        {
            var surface = mesh.Surfaces[0];
            data.TemplatePrimitiveType = surface.PrimitiveType;
            data.TemplatePrimitiveCount = surface.PrimitiveCount;
            data.TemplateVertexCount = surface.VertexCount;
            data.TemplateVertexBuffer = surface.VertexBuffer;
            data.TemplateIndexBuffer = surface.IndexBuffer;
        }
    }

    public void MultiMeshDeallocate(Rid multiMesh)
    {
        var data = GetResource(_multiMeshes, multiMesh);
        data.InstanceBuffer?.Dispose();
        data.InstanceDeclaration?.Dispose();
        _multiMeshes[multiMesh] = new MultiMeshData();
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

        var offset = index * mm.DataType.GetFloatsPerInstance() + 1;
        var b = mm.UploadBuffer;
        b[offset] = value.M11;
        b[offset + 1] = value.M12;
        b[offset + 2] = value.M13;
        b[offset + 3] = value.M14;
        b[offset + 4] = value.M21;
        b[offset + 5] = value.M22;
        b[offset + 6] = value.M23;
        b[offset + 7] = value.M24;
        b[offset + 8] = value.M31;
        b[offset + 9] = value.M32;
        b[offset + 10] = value.M33;
        b[offset + 11] = value.M34;
        b[offset + 12] = value.M41;
        b[offset + 13] = value.M42;
        b[offset + 14] = value.M43;
        b[offset + 15] = value.M44;
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

        var offset = index * mm.DataType.GetFloatsPerInstance() + 1;
        var b = mm.UploadBuffer;
        b[offset] = value.X;
        b[offset + 1] = value.Y;
        b[offset + 2] = value.Z;
        b[offset + 3] = value.W;
        mm.Dirty = true;
    }

    private void DrawMultiMesh(RenderTargetData rt, WorldData world, Rid multiMeshRid, InstanceMatrices matrices)
    {
        if (!TryGetResource(_multiMeshes, multiMeshRid, out var mm))
        {
            return;
        }

        if (mm!.TemplateVertexBuffer == null || mm.TemplateIndexBuffer == null ||
            mm.InstanceBuffer == null || mm.InstanceDeclaration == null)
        {
            return;
        }

        var visible = mm.VisibleInstances < 0 ? mm.InstanceCount : mm.VisibleInstances;
        if (visible <= 0 || mm.TemplatePrimitiveCount <= 0)
        {
            return;
        }

        // Upload dirty instance data to GPU.
        if (mm.Dirty)
        {
            if (mm.InstanceBuffer != null)
            {
                var floatsPerInstance = mm.DataType.GetFloatsPerInstance();
                mm.InstanceBuffer.SetData(mm.UploadBuffer, 0, visible * floatsPerInstance, SetDataOptions.Discard);
            }
            mm.Dirty = false;
        }

        // Resolve material from first surface.
        MaterialData? mat = null;
        if (TryGetResource(_meshes, mm.Mesh, out var mesh))
        {
            var firstSurface = mesh!.Surfaces.FirstOrDefault();
            TryGetResource(_materials, firstSurface?.Material ?? Rid.Invalid, out mat);
        }

        if (mat == null)
        {
            return;
        }

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

        // Bind template geometry + instance buffer.
        GraphicsDevice.SetVertexBuffers(
            new VertexBufferBinding(mm.TemplateVertexBuffer, 0, 0),
            new VertexBufferBinding(mm.InstanceBuffer, 0, 1)
        );
        GraphicsDevice.Indices = mm.TemplateIndexBuffer;

        // Draw instanced.
        foreach (var pass in mat.Effect!.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawInstancedPrimitives(
                primitiveType: mm.TemplatePrimitiveType,
                baseVertex: 0,
                minVertexIndex: 0,
                numVertices: mm.TemplateVertexCount,
                startIndex: 0,
                primitiveCount: mm.TemplatePrimitiveCount,
                instanceCount: visible
            );
        }

        RestoreDefaultState();
    }

    private void InvalidateMultiMesh(Rid mesh)
    {
        foreach (var mm in _multiMeshes.Values)
        {
            if (mm.Mesh == mesh)
            {
                mm.Mesh = Rid.Invalid;
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
}