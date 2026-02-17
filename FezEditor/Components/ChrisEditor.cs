using FezEditor.Actors;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.ArtObject;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components;

public class ChrisEditor : EditorComponent
{
    public override object Asset => SaveContent();

    private readonly ArtObject _ao;
    
    private readonly Scene _scene;

    private readonly TrixelsMesh _mesh;

    private TrixelObject _obj = null!;
    
    public ChrisEditor(Game game, string title, ArtObject ao) : base(game, title)
    {
        _ao = ao;
        _scene = new Scene(game);
        {
            var pivot = _scene.CreateRootActor();
            pivot.AddComponent<OrbitControl>();
            {
                var cameraActor = _scene.CreateChildActor(pivot);
                var camera = cameraActor.AddComponent<Camera>();
                camera.Projection = Camera.ProjectionType.Perspective;
                camera.FieldOfView = 90f;

                var zoom = cameraActor.AddComponent<ZoomControl>();
                zoom.Distance = _ao.Size.X;
                zoom.MinDistance = 10f / 16f;
                zoom.MaxDistance = 16f;
            }
        }
        {
            var actor = _scene.CreateRootActor();
            _mesh = actor.AddComponent<TrixelsMesh>();
        }
    }

    public override void LoadContent()
    {
        _obj = TrixelMaterializer.Materialize(_ao);
        _mesh.Texture = RepackerExtensions.ConvertToTexture2D(_ao.Cubemap);
        _mesh.Visualize(_obj);
    }
    
    private object SaveContent()
    {
        var ao = TrixelMaterializer.DematerializeToArtObject(_obj);
        ao.Name = _ao.Name;
        ao.ActorType = _ao.ActorType;
        ao.NoSihouette = _ao.NoSihouette;
        return ao;
    }

    public override void Update(GameTime gameTime)
    {
        _scene.Update(gameTime);
    }

    public override void Draw()
    {
        var size = ImGuiX.GetContentRegionAvail();
        var w = (int)size.X;
        var h = (int)size.Y;

        if (w > 0 && h > 0)
        {
            var texture = _scene.GetViewportTexture();
            if (texture == null || texture.Width != w || texture.Height != h)
            {
                _scene.SetViewportSize(w, h);
                _scene.SetViewportAspectRatio((float)w / h);
            }

            if (texture is { IsDisposed: false })
            {
                ImGuiX.Image(texture, size);
            }
        }
    }

    public override void Dispose()
    {
        _scene.Dispose();
        base.Dispose();
    }
}