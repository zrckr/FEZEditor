using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Level;
using FEZRepacker.Core.Definitions.Game.TrileSet;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public class TrileMesh : ActorComponent
{
    private const int MaxInstancesCount = 200;
    
    public int VisibleCount { get; set; }

    private readonly OrderedDictionary<TrileEmplacement, InstanceData> _instances = new();

    private RenderingService _rendering = null!;

    private Rid _mesh;

    private Rid _multiMesh;

    private Rid _material;
    
    private Vector3 _size;

    public override void Initialize()
    {
        _rendering = Game.GetService<RenderingService>();
        _mesh = _rendering.MeshCreate();
        _material = _rendering.MaterialCreate();
        _multiMesh = _rendering.MeshCreate();
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        _rendering.FreeRid(_multiMesh);
        _rendering.FreeRid(_mesh);
        _rendering.FreeRid(_material);
    }

    public void Load(TrileSet trileSet, int id)
    {
        var effect = Game.Content.Load<Effect>("Effects/Trile");
        _rendering.MaterialAssignEffect(_material, effect);

        var texture = RepackerExtensions.ConvertToTexture2D(trileSet.TextureAtlas);
        _rendering.MaterialAssignBaseTexture(_material, texture);

        var trile = trileSet.Triles[id];
        var surface = RepackerExtensions.ConvertToMesh(trile.Geometry.Vertices, trile.Geometry.Indices); 
        _rendering.MeshAddSurface(_mesh, PrimitiveType.TriangleList, surface, _material);
        
        _rendering.MultiMeshAllocate(_multiMesh, MaxInstancesCount, MultiMeshDataType.Vector4);
        _rendering.MultiMeshSetMesh(_multiMesh, _mesh);
        _rendering.InstanceSetMultiMesh(Actor.InstanceRid, _multiMesh);

        _size = trileSet.Triles[id].Size.ToXna();
    }

    public void Unload()
    {
        _instances.Clear();
        _size = Vector3.Zero;
        _rendering.InstanceSetMultiMesh(Actor.InstanceRid, Rid.Invalid);
        _rendering.MultiMeshDeallocate(_multiMesh);
        _rendering.MeshClear(_mesh);
        _rendering.MaterialReset(_material);
    }

    public void SetInstancePosition(TrileEmplacement emplacement, Vector3 position)
    {
        var instance = _instances[emplacement];
        instance.Position = position;
        _instances[emplacement] = instance;
    }

    public void SetInstanceRotation(TrileEmplacement emplacement, TrileRotation rotation)
    {
        var instance = _instances[emplacement];
        instance.Rotation = rotation;
        _instances[emplacement] = instance;
    }

    public BoundingBox GetInstanceCollider(TrileEmplacement emplacement)
    {
        var position = _instances[emplacement].Position;
        var rotation = _instances[emplacement].Rotation.AsQuaternion();
        return Mathz.ComputeBoundingBox(position, rotation, Vector3.One, _size);
    }
    
    public override void Update(GameTime gameTime)
    {
        _rendering.MultiMeshSetVisibleInstances(_multiMesh, VisibleCount);
        for (var i = 0; i < _instances.Count; i++)
        {
            var data = _instances.GetAt(i).Value.ToStride();
            _rendering.MultiMeshSetInstanceVector4(_multiMesh, i, data);
        }
    }

    private record struct InstanceData(Vector3 Position, TrileRotation Rotation)
    {
        public Vector4 ToStride() => new(Position, Rotation.AsPhi());
    }
}