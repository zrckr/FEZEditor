using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.ArtObject;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public class ArtObjectMesh : ActorComponent
{
    private RenderingService _rendering = null!;
    
    private Rid _mesh;

    private Rid _material;

    public override void Initialize()
    {
        _rendering = Game.GetService<RenderingService>();
        _mesh = _rendering.MeshCreate();
        _material = _rendering.MaterialCreate();
    }

    public void Load(ArtObject ao)
    {
        var texture = RepackerExtensions.ConvertToTexture2D(ao.Cubemap);
        _rendering.MaterialAssignBaseTexture(_material, texture);
        
        var effect = Game.Content.Load<Effect>("Effects/ArtObject");
        _rendering.MaterialAssignEffect(_material, effect);

        var surface = RepackerExtensions.ConvertToMesh(ao.Geometry.Vertices, ao.Geometry.Indices);
        _rendering.MeshAddSurface(_mesh, PrimitiveType.TriangleList, surface, _material);
        _rendering.InstanceSetMesh(Actor.InstanceRid, _mesh);
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        _rendering.FreeRid(_mesh);
        _rendering.FreeRid(_material);
    }
}