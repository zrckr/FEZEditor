using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public class TrixelsMesh : ActorComponent
{
    public Color SelectedColor { get; set; } = Color.Red;

    public float SelectedAlpha { get; set;} = 0.3f;

    public Texture2D? Texture { get; set; }

    public bool Wireframe
    {
        get => _wireframe;
        set
        {
            if (_wireframe != value)
            {
                _wireframe = value;
                _rendering.MaterialSetFillMode(_material, _wireframe ? FillMode.WireFrame : FillMode.Solid);
                _rendering.MaterialSetCullMode(_material, _wireframe ? CullMode.None : CullMode.CullClockwiseFace);
            }
        }
    }
    
    private RenderingService _rendering = null!;

    private Transform _transform = null!;

    private Rid _mesh;

    private Rid _multiMesh;

    private Rid _material;

    private bool _wireframe;

    public override void Initialize()
    {
        _rendering = Game.GetService<RenderingService>();
        _transform = Actor.GetComponent<Transform>();

        var effect = Game.Content.Load<Effect>("Effects/TrixelsMesh");
        _material = _rendering.MaterialCreate();
        _rendering.MaterialAssignEffect(_material, effect);
        _rendering.MaterialSetFillMode(_material, FillMode.Solid);
        _rendering.MaterialSetCullMode(_material, CullMode.CullClockwiseFace);

        var surface = new MeshSurface
        {
            Vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f)
            },
            Normals = new[]
            {
                Vector3.Forward,
                Vector3.Forward,
                Vector3.Forward,
                Vector3.Forward
            },
            Indices = new[]
            {
                0, 1, 2, // Triangle 1
                2, 1, 3 // Triangle 2
            }
        };

        _mesh = _rendering.MeshCreate();
        _rendering.MeshAddSurface(_mesh, PrimitiveType.TriangleList, surface, _material);

        _multiMesh = _rendering.MultiMeshCreate();
        _rendering.MultiMeshSetMesh(_multiMesh, _mesh);
        _rendering.InstanceSetMultiMesh(Actor.InstanceRid, _multiMesh);
    }

    public void Visualize(TrixelObject obj)
    {
        var faces = TrixelMaterializer.BuildVisibleFaces(obj).ToArray();
        _transform.Position = Vector3.Zero - obj.Size / 2f;
        
        _rendering.MultiMeshAllocate(_multiMesh, faces.Length, MultiMeshDataType.Matrix);
        _rendering.MaterialAssignBaseTexture(_material, Texture!);
        _rendering.MaterialShaderSetParam(_material, "SelectedColor", SelectedColor);
        _rendering.MaterialShaderSetParam(_material, "SelectedAlpha", SelectedAlpha);

        for (var i = 0; i < faces.Length; i++)
        {
            var emplacement = faces[i].Emplacement;
            var face = faces[i].Face;
            var selected = faces[i].Selected;

            var worldPos = (emplacement.ToVector3() + (Vector3.One + face.AsVector()) * 0.5f) * Mathz.TrixelSize;
            var quaternion = face.AsQuaternion();
            var (colStart, uAxis, vAxis, flipU, flipV) = face switch
            {
                FaceOrientation.Front => (0 / 6f, Vector3.UnitX, Vector3.UnitY, false, true),
                FaceOrientation.Right => (1 / 6f, Vector3.UnitZ, Vector3.UnitY, true, true),
                FaceOrientation.Back => (2 / 6f, Vector3.UnitX, Vector3.UnitY, true, true),
                FaceOrientation.Left => (3 / 6f, Vector3.UnitZ, Vector3.UnitY, false, true),
                FaceOrientation.Top => (4 / 6f, Vector3.UnitX, Vector3.UnitZ, false, false),
                FaceOrientation.Down => (5 / 6f, Vector3.UnitX, Vector3.UnitZ, false, true),
                _ => throw new InvalidOperationException()
            };

            var uSize = (int)((uAxis.X != 0 ? obj.Size.X : uAxis.Z != 0 ? obj.Size.Z : obj.Size.Y) / Mathz.TrixelSize);
            var vSize = (int)((vAxis.Y != 0 ? obj.Size.Y : vAxis.Z != 0 ? obj.Size.Z : obj.Size.X) / Mathz.TrixelSize);

            var uIndex = emplacement.X * uAxis.X + emplacement.Y * uAxis.Y + emplacement.Z * uAxis.Z;
            var vIndex = emplacement.X * vAxis.X + emplacement.Y * vAxis.Y + emplacement.Z * vAxis.Z;

            if (flipU)
            {
                uIndex = uSize - 1 - uIndex;
            }

            if (flipV)
            {
                vIndex = vSize - 1 - vIndex;
            }

            var uStep = 1f / (6f * uSize);
            var vStep = 1f / vSize;

            var u0 = colStart + uIndex * uStep;
            var v0 = vIndex * vStep;

            var uv0 = new Vector2(u0, v0 + vStep);
            var uv1 = new Vector2(u0 + uStep, v0 + vStep);
            var uv2 = new Vector2(u0, v0);
            var uv3 = new Vector2(u0 + uStep, v0);

            var data = new Matrix(
                worldPos.X, worldPos.Y, worldPos.Z, selected ? 1f : 0f,
                quaternion.X, quaternion.Y, quaternion.Z, quaternion.W,
                uv0.X, uv0.Y, uv1.X, uv1.Y,
                uv2.X, uv2.Y, uv3.X, uv3.Y
            );

            _rendering.MultiMeshSetInstanceMatrix(_multiMesh, i, data);
        }
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        _rendering.FreeRid(_multiMesh);
        _rendering.FreeRid(_mesh);
        _rendering.FreeRid(_material);
        Texture?.Dispose();
    }
}