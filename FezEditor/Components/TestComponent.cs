using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Components;

public class TestComponent : EditorComponent
{
    private readonly IRenderingService _rs;

    private Rid _world;

    private Rid _camera;

    private Rid _rt;

    private Rid _material;

    private Rid _mesh;

    private Rid _instance;

    private float _elapsed;
    
    public TestComponent(Game game, string title) : base(game, title)
    {
        _rs = game.GetService<IRenderingService>();
    }

    public override void Initialize()
    {
        base.Initialize();
        
        // Create world + camera
        _world = _rs.WorldCreate();
        _camera = _rs.CameraCreate();
        _rs.WorldSetCamera(_world, _camera);
        
        // Create render target and bind to world
        _rt = _rs.RenderTargetCreate();
        _rs.RenderTargetSetWorld(_rt, _world);
        _rs.RenderTargetSetClearColor(_rt, Color.Black);
        
        // Create material
        var effect = new BasicEffect(Game.GraphicsDevice) { VertexColorEnabled = true };
        _material = _rs.MaterialCreate(effect);
        _rs.MaterialSetCullMode(_material, IRenderingService.CullMode.None);
        
        // Create mesh with a single triangle surface
        _mesh = _rs.MeshCreate();
        _rs.MeshAddSurface(_mesh, PrimitiveType.TriangleList, new IRenderingService.MeshSurface
        {
            Vertices = new[]
            {
                new Vector3( 0.0f,  0.5f, 0f),  // top
                new Vector3( 0.5f, -0.5f, 0f),  // bottom-right
                new Vector3(-0.5f, -0.5f, 0f),  // bottom-left
            },
            Indices = new[] { 0, 1, 2 },
            Colors = new[]
            {
                Color.Red,
                Color.Green,
                Color.Blue
            }
        }, _material);
        
        // Create instance under world root and assign mesh
        var root = _rs.WorldGetRoot(_world);
        _instance = _rs.InstanceCreate(root);
        _rs.InstanceSetMesh(_instance, _mesh);
        
        // Orthographic camera
        _rs.CameraSetView(_camera, Matrix.Identity);
        _rs.CameraSetProjection(_camera, Matrix.CreateOrthographic(2f, 2f, 0.1f, 10f));
        
        // Push the triangle back so it's in front of the near plane
        _rs.InstanceSetPosition(_instance, new Vector3(0, 0, -1f));
    }

    public override void Update(GameTime gameTime)
    {
        _elapsed += (float)gameTime.ElapsedGameTime.TotalSeconds;
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.Up, _elapsed);
        _rs.InstanceSetRotation(_instance, rotation);
    }

    public override void Draw(GameTime gameTime)
    {
        var size = ImGuiX.GetContentRegionAvail();
        var w = (int)size.X;
        var h = (int)size.Y;
            
        if (w > 0 && h > 0)
        {
            var texture = _rs.RenderTargetGetTexture(_rt);
            if (texture == null || texture.Width != w || texture.Height != h)
            {
                _rs.RenderTargetSetSize(_rt, w, h);
                var aspect = (float)w / h;
                _rs.CameraSetProjection(_camera, Matrix.CreateOrthographic(2f * aspect, 2f, 0.1f, 10f));
            }

            if (texture is { IsDisposed: false })
            {
                ImGuiX.Image(texture, size);
            }
        }
    }

    public override void Dispose()
    {
        _rs.FreeRid(_instance);
        _rs.FreeRid(_mesh);
        _rs.FreeRid(_material);
        _rs.FreeRid(_camera);
        _rs.FreeRid(_world);
        _rs.FreeRid(_rt);
    }
}