using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;
using FEZRepacker.Core.Definitions.Game.TrileSet;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public class TrilesMesh : ActorComponent, IPickable
{
    private const float DepthBias = -1e-4f;

    private const float SlopeScaleDepthBias = 0f;

    public int InstanceCount => _instances.Count;

    public bool HasGeometry { get; private set; }

    public bool Displacements
    {
        set
        {
            foreach (var displacement in _displacements.Values)
            {
                _rendering.InstanceSetVisibility(displacement, value);
            }
        }
    }

    private readonly OrderedDictionary<TrileEmplacement, InstanceData> _instances = new();

    private readonly Dictionary<TrileEmplacement, Rid> _displacements = new();

    private readonly Dictionary<(TrileEmplacement, int), InstanceData> _overlaps = new();

    private readonly RenderingService _rendering;

    private readonly Rid _mesh;

    private readonly Rid _multiMesh;

    private readonly Rid _material;

    private readonly Rid _displacementMesh;

    private readonly Rid _displacementMaterial;

    private bool _instancesDirty;

    private Vector3 _size;

    private Vector3 _offset;

    private Vector4 _collisionTypes;

    internal TrilesMesh(Game game, Actor actor) : base(game, actor)
    {
        _rendering = game.GetService<RenderingService>();
        _mesh = _rendering.MeshCreate();
        _material = _rendering.MaterialCreate();
        _multiMesh = _rendering.MultiMeshCreate();
        _rendering.MultiMeshSetMesh(_multiMesh, _mesh);
        _rendering.InstanceSetMultiMesh(actor.InstanceRid, _multiMesh);
        _displacementMesh = _rendering.MeshCreate();
        _displacementMaterial = _rendering.MaterialCreate();
    }

    public override void LoadContent(IContentManager content)
    {
        var effect = content.Load<Effect>("Effects/TrilesMesh");
        _rendering.MaterialAssignEffect(_material, effect);
        _rendering.MaterialAssignEffect(_displacementMaterial, _rendering.BasicEffectVertexColor);
        _rendering.MaterialSetCullMode(_displacementMaterial, CullMode.None);
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        _rendering.FreeRid(_multiMesh);
        _rendering.FreeRid(_mesh);
        _rendering.FreeRid(_material);
        _rendering.FreeRid(_displacementMesh);
        _rendering.FreeRid(_displacementMaterial);
    }

    public void Visualize(Trile trile, Texture2D? textureAtlas, IDictionary<string, Texture2D> collisionTextures)
    {
        _size = trile.Size.ToXna();
        _offset = trile.Offset.ToXna();
        _collisionTypes = new Vector4
        {
            X = (float)trile.Faces[FaceOrientation.Front],
            Y = (float)trile.Faces[FaceOrientation.Right],
            Z = (float)trile.Faces[FaceOrientation.Back],
            W = (float)trile.Faces[FaceOrientation.Left]
        };

        HasGeometry = !trile.Geometry.IsNullOrEmpty();

        if (HasGeometry)
        {
            if (textureAtlas != null)
            {
                _rendering.MaterialAssignBaseTexture(_material, textureAtlas);
            }

            foreach (var (name, texture) in collisionTextures)
            {
                _rendering.MaterialShaderSetParam(_material, name, texture);
            }

            _rendering.MaterialSetDepthBias(_material, 0f, 0f);

            var surface = RepackerExtensions.ConvertToMesh(trile.Geometry!.Vertices, trile.Geometry!.Indices);
            _rendering.MeshAddSurface(_mesh, PrimitiveType.TriangleList, surface, _material);
        }
        else
        {
            _rendering.MaterialSetDepthBias(_material, DepthBias, SlopeScaleDepthBias);
            var faces = new List<MeshSurface>(trile.Faces.Count);
            foreach (var face in trile.Faces.Keys)
            {
                var fallback = MeshSurface.CreateFaceCakeSlice(_size, face);
                fallback.Translate(_offset / 2f);
                faces.Add(fallback);
            }

            var fallbackMesh = MeshSurface.CreateMergedMesh(faces.ToArray());
            _rendering.MeshAddSurface(_mesh, PrimitiveType.TriangleList, fallbackMesh, _material);
        }

        var wireframe = MeshSurface.CreateWireframeBox(_size, Color.Magenta);
        wireframe.Translate(_offset / 2f);
        _rendering.MeshAddSurface(_displacementMesh, PrimitiveType.LineList, wireframe, _displacementMaterial);
    }

    public TrileEmplacement GetEmplacement(int index)
    {
        return _instances.GetAt(index).Key;
    }

    public void SetInstanceData(TrileEmplacement emplacement, Vector3 position, byte phi)
    {
        _instances[emplacement] =
            new InstanceData(position, phi, Mathz.TransparentBlack, !HasGeometry, _collisionTypes);
        _instancesDirty = true;

        FreeDisplacement(emplacement);
        if (IsOffsetInsideEmplacement(position, emplacement))
        {
            var instance = _rendering.InstanceCreate(Actor.InstanceRid);
            var pos = new Vector3(emplacement.X, emplacement.Y, emplacement.Z) + Mathz.EmplacementCenter;
            var rot = Mathz.GetTrileRotation(phi);
            _rendering.InstanceSetMesh(instance, _displacementMesh);
            _rendering.InstanceSetPosition(instance, pos);
            _rendering.InstanceSetRotation(instance, rot);
            _displacements[emplacement] = instance;
        }
    }

    private static bool IsOffsetInsideEmplacement(Vector3 position, TrileEmplacement emplacement)
    {
        var basePosition = new Vector3(emplacement.X, emplacement.Y, emplacement.Z);
        return position != basePosition &&
               Mathz.FezRound(position.X) == emplacement.X &&
               Mathz.FezRound(position.Y) == emplacement.Y &&
               Mathz.FezRound(position.Z) == emplacement.Z;
    }

    public void SetOverlapInstanceData(TrileEmplacement emplacement, int index, Vector3 position, byte phi, Color tint)
    {
        _overlaps[(emplacement, index)] = new InstanceData(position, phi, tint, !HasGeometry, _collisionTypes);
        _instancesDirty = true;
    }

    public void RemoveOverlapInstance(TrileEmplacement emplacement, int index)
    {
        _instancesDirty |= _overlaps.Remove((emplacement, index));
    }

    public IEnumerable<BoundingBox> GetBounds()
    {
        for (var i = 0; i < _instances.Count; i++)
        {
            var (_, instance) = _instances.GetAt(i);
            var phi = (byte)instance.Phi;
            var position = Mathz.GetTrileCenter(instance.Position, _offset, phi);
            var rotation = Mathz.GetTrileRotation(phi);
            yield return Mathz.ComputeBoundingBox(position, rotation, Vector3.One, _size);
        }
    }

    public PickHit? Pick(Ray ray)
    {
        float? nearestDist = null;
        var nearestIndex = -1;
        var index = 0;

        foreach (var box in GetBounds())
        {
            var dist = ray.Intersects(box);
            if (dist.HasValue && (!nearestDist.HasValue || dist.Value < nearestDist.Value))
            {
                nearestDist = dist.Value;
                nearestIndex = index;
            }

            index++;
        }

        if (nearestDist.HasValue)
        {
            return new PickHit(nearestDist.Value, nearestIndex);
        }

        return null;
    }

    public void RemoveInstance(TrileEmplacement emplacement)
    {
        _instancesDirty |= _instances.Remove(emplacement);
        FreeDisplacement(emplacement);
    }

    private void FreeDisplacement(TrileEmplacement emplacement)
    {
        if (_displacements.Remove(emplacement, out var displacement))
        {
            _rendering.FreeRid(displacement);
        }
    }

    public override void Update(GameTime gameTime)
    {
        if (_instancesDirty)
        {
            var total = _instances.Count + _overlaps.Count;
            _rendering.MultiMeshAllocate(_multiMesh, total, MultiMeshDataType.Matrix);
            _instancesDirty = false;

            // Upload normal triles
            for (var i = 0; i < _instances.Count; i++)
            {
                var (_, inst) = _instances.GetAt(i);
                _rendering.MultiMeshSetInstanceMatrix(_multiMesh, i, inst.ToStride());
            }

            // Upload overlapped triles
            var j = _instances.Count;
            foreach (var instance in _overlaps.Values)
            {
                _rendering.MultiMeshSetInstanceMatrix(_multiMesh, j++, instance.ToStride());
            }
        }
    }

    private readonly record struct InstanceData(
        Vector3 Position,
        int Phi,
        Color Tint,
        bool CollisionVisual,
        Vector4 CollisionTypes)
    {
        public Matrix ToStride()
        {
            var quaternion = Mathz.GetTrileRotation((byte)Phi);
            var tint = Tint.ToVector4();
            return new Matrix(
                Position.X, Position.Y, Position.Z, CollisionVisual ? 1f : 0f,
                quaternion.X, quaternion.Y, quaternion.Z, quaternion.W,
                tint.X, tint.Y, tint.Z, tint.W,
                CollisionTypes.X, CollisionTypes.Y, CollisionTypes.Z, CollisionTypes.W
            );
        }
    }
}