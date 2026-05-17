using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public class TrixelsMesh : ActorComponent
{
    public Texture2D? Texture { get; private set; }
    public Texture2D? ColorTexture { get; private set; }
    public Texture2D? EmissionTexture { get; private set; }

    public IReadOnlyList<TrixelFace> Faces => _faces;

    public bool Wireframe
    {
        get => _wireframe;
        set
        {
            if (_wireframe != value)
            {
                _wireframe = value;
                _rendering.MaterialSetFillMode(_material, _wireframe ? FillMode.WireFrame : FillMode.Solid);
                _rendering.MaterialSetCullMode(_material, _wireframe ? CullMode.None : CullMode.CullCounterClockwiseFace);
            }
        }
    }

    public bool ShowEmission
    {
        get => _showEmission;
        set
        {
            if (_showEmission != value)
            {
                _showEmission = value;
                _rendering.MaterialShaderSetParam(_material, "ShowEmission", value);
            }
        }
    }

    private readonly RenderingService _rendering;

    private readonly Rid _mesh;

    private readonly Rid _material;

    private bool _wireframe;

    private bool _showEmission;

    private TrixelFace[] _faces = [];

    internal TrixelsMesh(Game game, Actor actor) : base(game, actor)
    {
        _rendering = game.GetService<RenderingService>();
        _mesh = _rendering.MeshCreate();
        _material = _rendering.MaterialCreate();
        _rendering.InstanceSetMesh(actor.InstanceRid, _mesh);
    }

    public override void LoadContent(IContentManager content)
    {
        var effect = content.Load<Effect>("Effects/TrixelsMesh");
        _rendering.MaterialAssignEffect(_material, effect);
        _rendering.MaterialSetFillMode(_material, FillMode.Solid);
        _rendering.MaterialSetCullMode(_material, CullMode.CullCounterClockwiseFace);
    }

    public void SetTexture(RTexture2D texture)
    {
        Texture?.Dispose();
        Texture = RepackerExtensions.ConvertToTexture2D(texture);

        ColorTexture?.Dispose();
        ColorTexture = RepackerExtensions.ExtractColorToTexture2D(texture);

        EmissionTexture?.Dispose();
        EmissionTexture = RepackerExtensions.ExtractEmissionToTexture2D(texture);
    }

    public void UpdateTextureDataFrom(RTexture2D texture)
    {
        if (Texture == null || ColorTexture == null || EmissionTexture == null)
        {
            return;
        }

        Texture.SetData(texture.TextureData);
        RepackerExtensions.ExtractColorToTexture2D(texture, ColorTexture);
        RepackerExtensions.ExtractColorToTexture2D(texture, EmissionTexture);
    }

    public void Visualize(TrixelObject obj)
    {
        _faces = TrixelMaterializer.BuildVisibleFaces(obj).ToArray();
        _rendering.MeshClear(_mesh);
        _rendering.MaterialAssignBaseTexture(_material, Texture!);

        var (vertices, indices) = TrixelMaterializer.Dematerialize(obj);
        if (vertices.Length > 0)
        {
            var surface = RepackerExtensions.ConvertToMesh(vertices, indices);
            _rendering.MeshAddSurface(_mesh, PrimitiveType.TriangleList, surface, _material);
        }
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        _rendering.FreeRid(_mesh);
        _rendering.FreeRid(_material);
        Texture?.Dispose();
        ColorTexture?.Dispose();
        EmissionTexture?.Dispose();
    }
}
