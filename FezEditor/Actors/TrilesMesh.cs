using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Level;
using FEZRepacker.Core.Definitions.Game.TrileSet;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public class TrilesMesh : ActorComponent, IPickable
{
    public static readonly Quaternion[] PhiAngles = new[]
    {
        Quaternion.CreateFromAxisAngle(Vector3.Up, -MathF.Tau / 2f),
        Quaternion.CreateFromAxisAngle(Vector3.Up, -MathF.Tau / 4f),
        Quaternion.CreateFromAxisAngle(Vector3.Up, +MathF.Tau * 0f),
        Quaternion.CreateFromAxisAngle(Vector3.Up, +MathF.Tau / 4f)
    };

    public static readonly Vector3 EmplacementCenter = new(0.5f);

    private const float FallbackOversize = 1.001f;

    public int InstanceCount => _instances.Count;

    public bool HasGeometry { get; private set; }

    public bool Pickable { get; set; } = true;

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

    private readonly RenderingService _rendering;

    private readonly Rid _mesh;

    private readonly Rid _multiMesh;

    private readonly Rid _material;

    private readonly Rid _displacementMesh;

    private readonly Rid _displacementMaterial;

    private Texture2D? _texture;

    private Texture2D? _emptyTexture;

    private bool _instancesDirty;

    private Vector3 _size;

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
        _emptyTexture = content.Load<Texture2D>("Textures/Empty");
        _rendering.MaterialAssignEffect(_displacementMaterial, _rendering.BasicEffectVertexColor);
        _rendering.MaterialSetCullMode(_displacementMaterial, CullMode.None);
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        _texture?.Dispose();
        _rendering.FreeRid(_multiMesh);
        _rendering.FreeRid(_mesh);
        _rendering.FreeRid(_material);
    }

    public void Visualize(TrileSet trileSet, int id)
    {
        if (trileSet.Triles.TryGetValue(id, out var trile))
        {
            _size = trile.Size.ToXna();
            HasGeometry = trile.Geometry.Indices.Length > 0;
        }
        else
        {
            _size = Vector3.One;
            HasGeometry = false;
        }

        if (HasGeometry)
        {
            _texture?.Dispose();
            _texture = RepackerExtensions.ConvertToTexture2D(trileSet.TextureAtlas);
            _rendering.MaterialAssignBaseTexture(_material, _texture);

            var surface = RepackerExtensions.ConvertToMesh(trile!.Geometry.Vertices, trile.Geometry.Indices);
            _rendering.MeshAddSurface(_mesh, PrimitiveType.TriangleList, surface, _material);
        }
        else
        {
            _rendering.MaterialAssignBaseTexture(_material, _emptyTexture!);
            var fallback = MeshSurface.CreateTexturedBox(_size * FallbackOversize); // prevents z-fighting
            _rendering.MeshAddSurface(_mesh, PrimitiveType.TriangleList, fallback, _material);
        }

        var wireframe = MeshSurface.CreateWireframeBox(_size, Color.Magenta);
        _rendering.MeshAddSurface(_displacementMesh, PrimitiveType.LineList, wireframe, _displacementMaterial);
    }

    public TrileEmplacement GetEmplacement(int index)
    {
        return _instances.GetAt(index).Key;
    }

    public void SetInstanceData(TrileEmplacement emplacement, Vector3 position, byte phi)
    {
        _instances[emplacement] = new InstanceData(position, phi);
        _instancesDirty = true;

        FreeDisplacement(emplacement);
        if (emplacement.X != (int)position.X || emplacement.Y != (int)position.Y || emplacement.Z != (int)position.Z)
        {
            var instance = _rendering.InstanceCreate(Actor.InstanceRid);
            var pos = new Vector3(emplacement.X, emplacement.Y, emplacement.Z) + EmplacementCenter;
            var rot = PhiAngles[phi];
            _rendering.InstanceSetMesh(instance, _displacementMesh);
            _rendering.InstanceSetPosition(instance, pos);
            _rendering.InstanceSetRotation(instance, rot);
            _displacements[emplacement] = instance;
        }
    }

    public IEnumerable<BoundingBox> GetBounds()
    {
        for (var i = 0; i < _instances.Count; i++)
        {
            var (_, instance) = _instances.GetAt(i);
            var position = instance.Position + EmplacementCenter;
            var rotation = PhiAngles[instance.Phi];
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
        _instancesDirty = _instances.Remove(emplacement);
        FreeDisplacement(emplacement);
    }

    public void ClearInstances()
    {
        _instances.Clear();
        _instancesDirty = true;
        foreach (var emplacement in _displacements.Keys.ToList())
        {
            FreeDisplacement(emplacement);
        }
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
            _rendering.MultiMeshAllocate(_multiMesh, _instances.Count, MultiMeshDataType.Matrix);
            _instancesDirty = false;

            for (var i = 0; i < _instances.Count; i++)
            {
                var (_, instance) = _instances.GetAt(i);
                var data = instance.ToStride();
                _rendering.MultiMeshSetInstanceMatrix(_multiMesh, i, data);
            }
        }
    }

    private readonly record struct InstanceData(Vector3 Position, int Phi)
    {
        public Matrix ToStride()
        {
            var quaternion = (Phi is >= 0 and <= 3) ? PhiAngles[Phi] : Quaternion.Identity;
            return new Matrix(
                Position.X, Position.Y, Position.Z, 0f,
                quaternion.X, quaternion.Y, quaternion.Z, quaternion.W,
                0f, 0f, 0f, 0f,
                0f, 0f, 0f, 0f
            );
        }
    }
}