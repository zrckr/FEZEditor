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
    
    private Scene _scene = null!;

    private Actor _cameraActor = null!;

    private Actor _meshActor = null!;

    private TrixelObject _obj = null!;
    
    public ChrisEditor(Game game, string title, ArtObject ao) : base(game, title)
    {
        _ao = ao;
        History.Track(ao);
    }

    public override void LoadContent()
    {
        _scene = new Scene(Game);
        {
            _cameraActor = _scene.CreateActor();
            _cameraActor.Name = "Camera";
            
            var camera = _cameraActor.AddComponent<Camera>();
            var zoom = _cameraActor.AddComponent<ZoomControl>();
            _cameraActor.AddComponent<OrbitControl>();
            _cameraActor.AddComponent<OrientationGizmo>();

            camera.Projection = Camera.ProjectionType.Perspective;
            camera.FieldOfView = 90f;
            zoom.Distance = _ao.Size.X;
            zoom.MinDistance = 10f / 16f;
            zoom.MaxDistance = 16f;
        }
        {
            _meshActor = _scene.CreateActor();
            _meshActor.AddComponent<TrixelsMesh>();
        }
        
        _obj = TrixelMaterializer.Materialize(_ao);
        var mesh = _meshActor.GetComponent<TrixelsMesh>();
        mesh.Texture = RepackerExtensions.ConvertToTexture2D(_ao.Cubemap);
        mesh.Visualize(_obj);
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
            var texture = _scene.Viewport.GetTexture();
            if (texture == null || texture.Width != w || texture.Height != h)
            {
                _scene.Viewport.SetSize(w, h);
            }

            if (texture is { IsDisposed: false })
            {
                ImGuiX.Image(texture, size);

                var gizmo = _cameraActor.GetComponent<OrientationGizmo>();
                {
                    var imageMin = ImGuiX.GetItemRectMin();
                    gizmo.UseFaceLabels = true;
                    gizmo.Draw(imageMin + new Vector2(size.X - 8f, 8f));
                }
            }
        }
    }

    public override void Dispose()
    {
        _scene.Dispose();
        base.Dispose();
    }
}