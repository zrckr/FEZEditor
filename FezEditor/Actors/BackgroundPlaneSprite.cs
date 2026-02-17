using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public class BackgroundPlaneSprite : ActorComponent
{
    public Vector3 PlaneSize { get; set; }
    
    public bool Animated { get; private set; }

    public bool Billboard { get; set; } = false;

    public bool DoubleSided { get; set; } = false;

    public Color Color { get; set; } = Color.White;

    private readonly List<FrameContent> _frames = new();

    private RenderingService _rendering = null!;

    private Vector2 _textureSize = Vector2.Zero;

    private TimeSpan _frameElapsed = TimeSpan.Zero;

    private int _frameCounter;

    private Rid _mesh;

    private Rid _material;

    private Rid _camera;

    private Transform _transform = null!;

    public override void Initialize()
    {
        _rendering = Game.GetService<RenderingService>();
        _mesh = _rendering.MeshCreate();
        _material = _rendering.MaterialCreate();
        var world = _rendering.InstanceGetWorld(Actor.InstanceRid);
        _camera = _rendering.WorldGetCamera(world);
        _transform = Actor.GetComponent<Transform>();
    }

    public void Load(object plane)
    {
        Effect effect;
        Texture2D baseTexture;
        List<FrameContent> frames;

        switch (plane)
        {
            case RAnimatedTexture animatedTexture:
            {
                frames = animatedTexture.Frames;
                effect = Game.Content.Load<Effect>("Effects/AnimatedPlane");
                baseTexture = RepackerExtensions.ConvertToTexture2D(animatedTexture);
                PlaneSize = new Vector3(animatedTexture.AtlasWidth / 16f, animatedTexture.AtlasHeight / 16f, 0.125f);
                break;
            }

            case RTexture2D texture:
            {
                frames = new List<FrameContent>();
                effect = Game.Content.Load<Effect>("StaticPlane");
                baseTexture = RepackerExtensions.ConvertToTexture2D(texture);
                PlaneSize = new Vector3(texture.Width / 16f, texture.Height / 16f, 0.125f);
                break;
            }

            default:
            {
                throw new NotSupportedException();
            }
        }

        _textureSize = new Vector2(baseTexture.Width, baseTexture.Height);
        _frameCounter = 0;
        _frames.AddRange(frames);
        Animated = _frames.Count > 0;
        
        _rendering.InstanceSetMesh(Actor.InstanceRid, _mesh);
        UpdateMeshSurface();

        _rendering.MaterialAssignEffect(_material, effect);
        _rendering.MaterialSetBlendMode(_material, BlendMode.AlphaBlend);
        _rendering.MaterialAssignBaseTexture(_material, baseTexture);
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        _rendering.FreeRid(_mesh);
        _rendering.FreeRid(_material);
    }

    public override void Update(GameTime gameTime)
    {
        if (Animated)
        {
            var currentFrame = _frames[_frameCounter];
            if (_frameElapsed < currentFrame.Duration)
            {
                _frameElapsed += gameTime.ElapsedGameTime;
            }
            else
            {
                var transform = Mathz.CreateTextureTransform(currentFrame.Rectangle.ToXna(), _textureSize);
                _rendering.MaterialSetTextureTransform(_material, transform);
                _frameCounter = Mathz.Clamp(_frameCounter + 1, 0, _frames.Count - 1);
                _frameElapsed = TimeSpan.Zero;
            }
        }
        
        _rendering.MaterialSetAlbedo(_material, Color);
        _rendering.MaterialSetCullMode(_material,
            DoubleSided ? CullMode.None : CullMode.CullCounterClockwiseFace);
        
        var rotation = _transform.Rotation;
        if (Billboard && _camera.IsValid)
        {
            var viewMatrix = _rendering.CameraGetView(_camera);
            var invViewMatrix = Matrix.Invert(viewMatrix);
            var translation = invViewMatrix.Translation;

            var toCamera = (translation - _transform.Position) * new Vector3(1, 0, 1);
            var angleY = 0f;
            if (toCamera.LengthSquared() > 0.0001f)
            {
                toCamera.Normalize();
                angleY = (float)Math.Atan2(toCamera.X, toCamera.Z);
            }

            rotation = Quaternion.CreateFromAxisAngle(Vector3.Up, angleY);
        }
        
        _transform.Rotation = rotation;
        UpdateMeshSurface();
    }

    private void UpdateMeshSurface()
    {
        _rendering.MeshClear(_mesh);

        var halfSize = PlaneSize * 0.5f;
        var meshSurface = new MeshSurface
        {
            Vertices = new[]
            {
                new Vector3(-halfSize.X, -halfSize.Y, 0),
                new Vector3(halfSize.X, -halfSize.Y, 0),
                new Vector3(-halfSize.X, halfSize.Y, 0),
                new Vector3(halfSize.X, halfSize.Y, 0)
            },
            Normals = new[]
            {
                Vector3.Forward,
                Vector3.Forward,
                Vector3.Forward,
                Vector3.Forward
            },
            TexCoords = new[]
            {
                new Vector2(0, 1),
                new Vector2(1, 1),
                new Vector2(0, 0),
                new Vector2(1, 0)
            },
            Indices = DoubleSided
                ? new[]
                {
                    // Front face triangles (counter-clockwise when viewed from front)
                    0, 1, 2, // Triangle 1
                    2, 1, 3, // Triangle 2

                    // Back face triangles (clockwise when viewed from front)
                    0, 2, 1, // Triangle 1 (reversed order)
                    2, 3, 1 // Triangle 2 (reversed order)
                }
                : new[]
                {
                    0, 1, 2, // Triangle 1
                    2, 1, 3 // Triangle 2
                }
        };

        _rendering.MeshAddSurface(_mesh, PrimitiveType.TriangleList, meshSurface, _material);
    }
}