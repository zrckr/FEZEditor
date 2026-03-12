using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public class Collider : ActorComponent
{
    public BoundingBox BoundingBox { get; private set; }

    public Vector3 Size
    {
        get => _size;
        set
        {
            if (_size != value)
            {
                _size = value;
                _meshDirty = true;
            }
        }
    }

    public bool DebugVisible { get; set; }

    public Color DebugColor { get; set; } = Color.Green;

    private readonly Transform _transform;

    private readonly RenderingService _rendering;

    private Rid _debugInstance;

    private Rid _debugMesh;

    private Rid _debugMaterial;

    private Vector3 _size;

    private bool _meshDirty;

    internal Collider(Game game, Actor actor) : base(game, actor)
    {
        _transform = actor.GetComponent<Transform>();
        _rendering = game.GetService<RenderingService>();
    }

    public override void Update(GameTime gameTime)
    {
        BoundingBox = Mathz.ComputeBoundingBox(
            _transform.Position, _transform.Rotation,
            _transform.Scale, Size
        );

        if (DebugVisible)
        {
            EnsureDebugResourcesCreated();
            if (_meshDirty)
            {
                RebuildDebugMesh();
                _meshDirty = false;
            }

            _rendering.MaterialSetAlbedo(_debugMaterial, DebugColor);
        }
        else
        {
            FreeDebugResources();
        }
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        FreeDebugResources();
    }

    private void EnsureDebugResourcesCreated()
    {
        if (!_debugInstance.IsValid)
        {
            _debugMesh = _rendering.MeshCreate();
            _debugMaterial = _rendering.MaterialCreate();
            _debugInstance = _rendering.InstanceCreate(Actor.InstanceRid);
            _rendering.InstanceSetMesh(_debugInstance, _debugMesh);

            _rendering.MaterialAssignEffect(_debugMaterial, _rendering.BasicEffect);
            _meshDirty = true;
        }
    }

    private void FreeDebugResources()
    {
        if (_debugInstance.IsValid)
        {
            _rendering.FreeRid(_debugMaterial);
            _rendering.FreeRid(_debugMesh);
            _rendering.FreeRid(_debugInstance);

            _debugMaterial = Rid.Invalid;
            _debugMesh = Rid.Invalid;
            _debugInstance = Rid.Invalid;
        }
    }

    private void RebuildDebugMesh()
    {
        var size = Size / 2f;
        var corners = new Vector3[]
        {
            new(-size.X, -size.Y, -size.Z), // 0 left-bottom-back
            new(size.X, -size.Y, -size.Z), // 1 right-bottom-back
            new(size.X, size.Y, -size.Z), // 2 right-top-back
            new(-size.X, size.Y, -size.Z), // 3 left-top-back
            new(-size.X, -size.Y, size.Z), // 4 left-bottom-front
            new(size.X, -size.Y, size.Z), // 5 right-bottom-front
            new(size.X, size.Y, size.Z), // 6 right-top-front
            new(-size.X, size.Y, size.Z) // 7 left-top-front
        };

        var indices = new[]
        {
            // back face
            0, 1, 1, 2, 2, 3, 3, 0,
            // front face
            4, 5, 5, 6, 6, 7, 7, 4,
            // connecting edges
            0, 4, 1, 5, 2, 6, 3, 7
        };

        var colors = Enumerable.Repeat(DebugColor, corners.Length).ToArray();
        var surface = new MeshSurface
        {
            Vertices = corners,
            Indices = indices,
            Colors = colors
        };

        _rendering.MeshClear(_debugMesh);
        _rendering.MeshAddSurface(_debugMesh, PrimitiveType.LineList, surface, _debugMaterial);
    }
}