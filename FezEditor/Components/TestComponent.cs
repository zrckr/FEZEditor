using FezEditor.Actors;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components;

public class TestComponent : EditorComponent
{
    private Scene _test = null!;

    public TestComponent(Game game, string title) : base(game, title)
    {
    }

    public override void LoadContent()
    {
        _test = new Scene(Game, ContentManager);
        {
            var actor = _test.CreateActor();
            var camera = actor.AddComponent<Camera>();
            var transform = actor.GetComponent<Transform>();
            camera.Projection = Camera.ProjectionType.Orthographic;
            camera.Size = 4f;
            transform.Position = new Vector3(0f, 0f, 4f);
            transform.Rotation = Quaternion.Identity;
        }
        {
            var actor = _test.CreateActor();
            actor.AddComponent<TestMesh>();
        }
    }

    public override void Update(GameTime gameTime)
    {
        _test.Update(gameTime);
    }

    public override void Draw()
    {
        var size = ImGuiX.GetContentRegionAvail();
        var w = (int)size.X;
        var h = (int)size.Y;

        if (w > 0 && h > 0)
        {
            var texture = _test.Viewport.GetTexture();
            if (texture == null || texture.Width != w || texture.Height != h)
            {
                _test.Viewport.SetSize(w, h);
            }

            if (texture is { IsDisposed: false })
            {
                ImGuiX.Image(texture, size);
                InputService.CaptureScroll(ImGui.IsItemHovered());
            }
        }
    }

    public override void Dispose()
    {
        _test.Dispose();
        base.Dispose();
    }
}