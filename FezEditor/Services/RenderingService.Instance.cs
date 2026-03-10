using FezEditor.Structure;
using Microsoft.Xna.Framework;

namespace FezEditor.Services;

public partial class RenderingService
{
    private class InstanceData
    {
        public Rid Parent;
        public readonly List<Rid> Children = new();
        public bool Visible = true;
        public Dirty<Vector3> Position = new(Vector3.Zero);
        public Dirty<Quaternion> Rotation = new(Quaternion.Identity);
        public Dirty<Vector3> Scale = new(Vector3.One);
        public Dirty<Matrix> WorldMatrix = new(Matrix.Identity);
        public Dirty<Matrix> LocalMatrix = new(Matrix.Identity);
        public InstanceType Type = InstanceType.None;
        public Rid Internal;
    }

    private readonly Dictionary<Rid, InstanceData> _instances = new();

    private readonly Stack<InstanceData> _transformChain = new();

    public Rid InstanceCreate(Rid parent)
    {
        var rid = AllocateRid(typeof(InstanceData));
        _instances[rid] = new InstanceData();
        InstanceSetParent(rid, parent);
        Logger.Debug("Instance created {0} (parent {1})", rid, parent);
        return rid;
    }

    public void InstanceSetParent(Rid instance, Rid parent)
    {
        // Check for cycles: walk up from parent, if we hit instance it's a cycle.
        var current = parent;
        while (TryGetResource(_instances, current, out var check))
        {
            if (current == instance)
            {
                throw new ArgumentException("Cannot set parent: would create a cycle.");
            }

            current = check!.Parent;
        }

        var data = GetResource(_instances, instance);

        // Remove from old parent's children.
        if (TryGetResource(_instances, data.Parent, out var oldParent))
        {
            oldParent!.Children.Remove(instance);
            data.Parent = Rid.Invalid;
        }

        // Add to new parent's children.
        if (TryGetResource(_instances, parent, out var newParent))
        {
            data.Parent = parent;
            newParent!.Children.Add(instance);
        }
    }

    public void InstanceSetVisibility(Rid instance, bool visible)
    {
        GetResource(_instances, instance).Visible = visible;
    }

    public void InstanceSetPosition(Rid instance, Vector3 position)
    {
        var data = GetResource(_instances, instance);
        data.Position = new Dirty<Vector3>(position, true);
        MarkWorldMatrixDirty(instance);
    }

    public void InstanceSetRotation(Rid instance, Quaternion rotation)
    {
        var data = GetResource(_instances, instance);
        data.Rotation = new Dirty<Quaternion>(rotation, true);
        MarkWorldMatrixDirty(instance);
    }

    public void InstanceSetScale(Rid instance, Vector3 scale)
    {
        var data = GetResource(_instances, instance);
        data.Scale = new Dirty<Vector3>(scale, true);
        MarkWorldMatrixDirty(instance);
    }

    public void InstanceSetMesh(Rid instance, Rid mesh)
    {
        var data = GetResource(_instances, instance);
        data.Type = InstanceType.Mesh;
        data.Internal = mesh;
    }

    public void InstanceSetMultiMesh(Rid instance, Rid multimesh)
    {
        var data = GetResource(_instances, instance);
        data.Type = InstanceType.MultiMesh;
        data.Internal = multimesh;
    }

    public InstanceType InstanceGetType(Rid instance)
    {
        return GetResource(_instances, instance).Type;
    }

    public Rid InstanceGetWorld(Rid instance)
    {
        var current = instance;
        while (TryGetResource(_instances, current, out var data) && data!.Parent.IsValid)
        {
            current = data.Parent;
        }

        foreach (var (rid, world) in _worlds)
        {
            if (world.Root == current)
            {
                return rid;
            }
        }

        return Rid.Invalid;
    }

    public Rid InstanceGetParent(Rid instance)
    {
        return GetResource(_instances, instance).Parent;
    }

    public Vector3 InstanceGetPosition(Rid instance)
    {
        return GetResource(_instances, instance).Position.Value;
    }

    public Quaternion InstanceGetRotation(Rid instance)
    {
        return GetResource(_instances, instance).Rotation.Value;
    }

    public Vector3 InstanceGetScale(Rid instance)
    {
        return GetResource(_instances, instance).Scale.Value;
    }

    public bool InstanceIsVisible(Rid instance)
    {
        return GetResource(_instances, instance).Visible;
    }

    public Matrix InstanceGetWorldMatrix(Rid instance)
    {
        var data = GetResource(_instances, instance);
        return ComputeWorldMatrix(data);
    }

    private void MarkWorldMatrixDirty(Rid instanceRid)
    {
        var stack = new Stack<Rid>();
        stack.Push(instanceRid);

        while (stack.Count > 0)
        {
            var rid = stack.Pop();
            var instance = GetResource(_instances, rid);
            if (instance.WorldMatrix.IsDirty)
            {
                continue;
            }

            instance.WorldMatrix = instance.WorldMatrix.Marked();
            foreach (var childRid in instance.Children)
            {
                stack.Push(childRid);
            }
        }
    }

    private Matrix ComputeWorldMatrix(InstanceData instance)
    {
        if (!instance.WorldMatrix.IsDirty)
        {
            return instance.WorldMatrix.Value;
        }

        _transformChain.Push(instance);
        TryGetResource(_instances, instance.Parent, out var parentData);
        while (parentData is { WorldMatrix.IsDirty: true })
        {
            _transformChain.Push(parentData);
            TryGetResource(_instances, parentData.Parent, out parentData);
        }

        var parentWorld = parentData?.WorldMatrix.Value ?? Matrix.Identity;
        while (_transformChain.Count > 0)
        {
            var instanceData = _transformChain.Pop();

            if (instanceData is
                { Position.IsDirty: true } or
                { Rotation.IsDirty: true } or
                { Scale.IsDirty: true })
            {
                var localMatrix = Matrix.CreateScale(instanceData.Scale.Value)
                                  * Matrix.CreateFromQuaternion(instanceData.Rotation.Value)
                                  * Matrix.CreateTranslation(instanceData.Position.Value);

                instanceData.LocalMatrix = new Dirty<Matrix>(localMatrix);
                instanceData.Position = new Dirty<Vector3>(instanceData.Position.Value);
                instanceData.Rotation = new Dirty<Quaternion>(instanceData.Rotation.Value);
                instanceData.Scale = new Dirty<Vector3>(instanceData.Scale.Value);
            }

            instanceData.WorldMatrix = new Dirty<Matrix>(instanceData.LocalMatrix.Value * parentWorld);
            parentWorld = instanceData.WorldMatrix.Value;
        }

        return instance.WorldMatrix.Value;
    }

    private void DisposeInstanceTree(Rid instance)
    {
        // Unlink from parent before removing the tree.
        if (TryGetResource(_instances, instance, out var root) &&
            TryGetResource(_instances, root!.Parent, out var parent))
        {
            parent!.Children.Remove(instance);
        }

        var stack = new Stack<Rid>();
        stack.Push(instance);

        while (stack.Count > 0)
        {
            var rid = stack.Pop();
            if (_instances.Remove(rid, out var data))
            {
                foreach (var childRid in data.Children)
                {
                    stack.Push(childRid);
                }
            }
        }
    }
}