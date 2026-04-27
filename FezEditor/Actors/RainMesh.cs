using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public class RainMesh : ActorComponent
{
    private static readonly Vector4 RainColor = new Color(145, 182, 255, 32).ToVector4();

    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(2f);

    private const float FallSpeed = 50f;

    private const float FadeOutDuration = 1f / 6f;

    private const float MinHeight = 3f;

    private const float MaxHeight = 6f;

    public Camera Camera { get; set; } = null!;

    public Vector3 LevelSize { get; set; }

    private BoundingBox SpawnVolume => new(Vector3.Zero, LevelSize);

    private readonly RenderingService _rendering;

    private readonly Rid _mesh;

    private readonly Rid _multiMesh;

    private readonly Rid _material;

    private readonly RainDrop[] _drops = new RainDrop[200];

    private readonly Random _random = new(101);

    internal RainMesh(Game game, Actor actor) : base(game, actor)
    {
        _rendering = game.GetService<RenderingService>();
        _mesh = _rendering.MeshCreate();
        _material = _rendering.MaterialCreate();
        _multiMesh = _rendering.MultiMeshCreate();
        _rendering.MultiMeshSetMesh(_multiMesh, _mesh);
        _rendering.InstanceSetMultiMesh(actor.InstanceRid, _multiMesh);
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        _rendering.FreeRid(_multiMesh);
        _rendering.FreeRid(_mesh);
        _rendering.FreeRid(_material);
    }

    public override void LoadContent(IContentManager content)
    {
        var effect = content.Load<Effect>("Effects/RainMesh");
        var texture = content.Load<Texture2D>("RainDrop");
        var surface = MeshSurface.CreateQuad(Vector3.One);

        _rendering.MaterialAssignEffect(_material, effect);
        _rendering.MaterialAssignBaseTexture(_material, texture);
        _rendering.MaterialSetBlendMode(_material, BlendMode.AlphaBlend);
        _rendering.MaterialSetDepthWrite(_material, false);
        _rendering.MaterialSetDepthTest(_material, CompareFunction.LessEqual);
        _rendering.MaterialSetCullMode(_material, CullMode.None);

        _rendering.MeshAddSurface(_mesh, PrimitiveType.TriangleList, surface, _material);
        _rendering.MultiMeshAllocate(_multiMesh, _drops.Length, MultiMeshDataType.Matrix);
        for (var i = 0; i < _drops.Length; i++)
        {
            _drops[i] = new RainDrop(_random, SpawnVolume, seeding: true);
        }
    }

    public override void Update(GameTime gameTime)
    {
        #region Update Drops

        var viewMatrix = Camera.View;
        for (var i = 0; i < _drops.Length; i++)
        {
            var drop = _drops[i];
            drop.Age += gameTime.ElapsedGameTime;

            if (drop.Age >= Lifetime)
            {
                drop = new RainDrop(_random, SpawnVolume, seeding: false);
                _drops[i] = drop;
            }

            drop.Position.Y -= FallSpeed * (float)gameTime.ElapsedGameTime.TotalSeconds;

            var color = RainColor;
            var elapsed = (float)(drop.Age.TotalSeconds / Lifetime.TotalSeconds);
            if (elapsed > 1f - FadeOutDuration)
            {
                color.W *= (1f - elapsed) / FadeOutDuration;
            }

            var rotation = Mathz.CreateYBillboard(viewMatrix, drop.Position);
            var scale = new Vector3(1f, drop.Height, 1f) * Mathz.TrixelSize;

            _rendering.MultiMeshSetInstanceMatrix(_multiMesh, i, new Matrix(
                drop.Position.X, drop.Position.Y, drop.Position.Z, 0f,
                rotation.X, rotation.Y, rotation.Z, rotation.W,
                scale.X, scale.Y, scale.Z, 0f,
                color.X, color.Y, color.Z, color.W
            ));
        }

        #endregion
    }

    private class RainDrop
    {
        public Vector3 Position;

        public TimeSpan Age;

        public readonly float Height;

        public RainDrop(Random random, BoundingBox volume, bool seeding)
        {
            Age = seeding
                ? TimeSpan.FromTicks(random.NextInt64() % Lifetime.Ticks)
                : TimeSpan.Zero;

            var startY = seeding
                ? random.Between(volume.Min.Y, volume.Max.Y)
                : volume.Max.Y;

            Position = new Vector3(
                random.Between(volume.Min.X, volume.Max.X),
                startY,
                random.Between(volume.Min.Z, volume.Max.Z)
            );

            Height = random.Between(MinHeight, MaxHeight);
        }
    }
}