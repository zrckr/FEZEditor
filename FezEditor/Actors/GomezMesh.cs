using System.Text.Json.Serialization;
using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public class GomezMesh : ActorComponent, IPickable
{
    public bool Pickable { get; set; } = true;

    private readonly RenderingService _rendering;

    private readonly Rid _mesh;

    private readonly Rid _frontMaterial;

    private readonly Rid _backMaterial;

    private readonly Transform _transform;

    private List<(Rectangle Rect, TimeSpan Duration)> _frontFrames = [];

    private Texture2D? _frontTexture;

    private Texture2D? _backTexture;

    private Vector3 _scale;

    private TimeSpan _frontElapsed = TimeSpan.Zero;

    private int _frontCounter;

    internal GomezMesh(Game game, Actor actor) : base(game, actor)
    {
        _rendering = game.GetService<RenderingService>();
        _mesh = _rendering.MeshCreate();
        _frontMaterial = _rendering.MaterialCreate();
        _backMaterial = _rendering.MaterialCreate();
        _rendering.InstanceSetMesh(actor.InstanceRid, _mesh);
        _transform = actor.GetComponent<Transform>();
    }

    public override void LoadContent(IContentManager content)
    {
        var effect = content.Load<Effect>("Effects/SpritePlaneMesh");

        _rendering.MaterialAssignEffect(_frontMaterial, effect);
        _rendering.MaterialSetCullMode(_frontMaterial, CullMode.CullClockwiseFace);
        _rendering.MaterialSetBlendMode(_frontMaterial, BlendMode.AlphaBlend);

        _rendering.MaterialAssignEffect(_backMaterial, effect);
        _rendering.MaterialSetCullMode(_backMaterial, CullMode.CullClockwiseFace);
        _rendering.MaterialSetBlendMode(_backMaterial, BlendMode.AlphaBlend);

        _frontTexture = content.Load<Texture2D>("Gomez/GomezFront");
        var frontSheet = content.LoadJson<SpriteData>("Gomez/GomezFront");
        _frontFrames = frontSheet.ToFrameList();
        _rendering.MaterialAssignBaseTexture(_frontMaterial, _frontTexture);

        _backTexture = content.Load<Texture2D>("Gomez/GomezBack");
        _rendering.MaterialAssignBaseTexture(_backMaterial, _backTexture);

        var frameSize = _frontFrames[0].Rect;
        _scale = new Vector3(frameSize.Width, frameSize.Height, 16f) * Mathz.TrixelSize;

        var frontQuad = MeshSurface.CreateFaceQuad(Vector3.One, Vector3.UnitY / 2f, FaceOrientation.Front);
        var backQuad = MeshSurface.CreateFaceQuad(Vector3.One, Vector3.UnitY / 2f, FaceOrientation.Back);
        _rendering.MeshAddSurface(_mesh, PrimitiveType.TriangleList, frontQuad, _frontMaterial);
        _rendering.MeshAddSurface(_mesh, PrimitiveType.TriangleList, backQuad, _backMaterial);

        ApplyTextureTransform(_frontMaterial, _frontTexture, frameSize);
        ApplyTextureTransform(_backMaterial, _backTexture, new Rectangle(0, 0, _backTexture.Width, _backTexture.Height));
    }

    public IEnumerable<BoundingBox> GetBounds()
    {
        var offset = Vector3.Transform(Vector3.UnitY * _scale.Y / 2f, _transform.Rotation);
        yield return Mathz.ComputeBoundingBox(_transform.Position + offset, _transform.Rotation, _scale, Vector3.One);
    }

    public PickHit? Pick(Ray ray)
    {
        var box = GetBounds().First();
        var dist = ray.Intersects(box);
        return dist.HasValue ? new PickHit(dist.Value, 0) : null;
    }

    public override void Update(GameTime gameTime)
    {
        if (_frontFrames.Count == 0)
        {
            return;
        }

        _frontElapsed += gameTime.ElapsedGameTime;
        if (_frontElapsed >= _frontFrames[_frontCounter].Duration)
        {
            _frontCounter = (_frontCounter + 1) % _frontFrames.Count;
            _frontElapsed = TimeSpan.Zero;
            ApplyTextureTransform(_frontMaterial, _frontTexture!, _frontFrames[_frontCounter].Rect);
        }

        _transform.Scale = _scale;
    }

    private void ApplyTextureTransform(Rid material, Texture2D texture, Rectangle rect)
    {
        var textureSize = new Vector2(texture.Width, texture.Height);
        _rendering.MaterialSetTextureTransform(material, Mathz.CreateTextureTransform(rect, textureSize));
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        _rendering.FreeRid(_frontMaterial);
        _rendering.FreeRid(_backMaterial);
        _rendering.FreeRid(_mesh);
    }

    // ReSharper disable once ClassNeverInstantiated.Local
    private record SpriteData(
        [property: JsonPropertyName("frames")] Dictionary<string, SpriteData.Frame> Frames)
    {
        // ReSharper disable once ClassNeverInstantiated.Local
        public record Frame(
            [property: JsonPropertyName("frame")] Rect FrameData,
            [property: JsonPropertyName("duration")]
            int Duration);

        // ReSharper disable once ClassNeverInstantiated.Local
        public record Rect(
            [property: JsonPropertyName("x")] int X,
            [property: JsonPropertyName("y")] int Y,
            [property: JsonPropertyName("w")] int W,
            [property: JsonPropertyName("h")] int H);

        public List<(Rectangle Rect, TimeSpan Duration)> ToFrameList()
        {
            var list = new List<(Rectangle, TimeSpan)>();
            foreach (var (rect, duration) in Frames.Values)
            {
                list.Add((new Rectangle(rect.X, rect.Y, rect.W, rect.H), TimeSpan.FromMilliseconds(duration)));
            }

            return list;
        }
    }




}
