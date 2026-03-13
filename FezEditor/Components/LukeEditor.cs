using FezEditor.Actors;
using FezEditor.Structure;
using FEZRepacker.Core.Definitions.Game.Sky;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components;

public class LukeEditor : EditorComponent
{
    public override object Asset => _sky;

    private readonly Sky _sky;

    private Clock _clock = null!;

    private Scene _scene = null!;

    private SkyVisualizer _visualizer;

    public LukeEditor(Game game, string title, Sky sky) : base(game, title)
    {
        _sky = sky;
        History.Track(sky);
    }

    public override void LoadContent()
    {
        _clock = new Clock();
        _scene = new Scene(Game, ContentManager);
        {
            var actor = _scene.CreateActor();
            actor.Name = "Camera";
            var camera = actor.AddComponent<Camera>();
            camera.Projection = Camera.ProjectionType.Orthographic;
            camera.Offset = Vector3.Backward * 250f;
            camera.Size = 10f;
        }

        _visualizer = new SkyVisualizer(_scene, _clock);
        _visualizer.Visualize(_sky);
    }

    public override void Update(GameTime gameTime)
    {
        _clock.Tick(gameTime);
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
                InputService.CaptureScroll(ImGui.IsItemHovered());

                var imageMin = ImGuiX.GetItemRectMin();
                ImGuiX.DrawStats(imageMin + new Vector2(8, 8), RenderingService.GetStats());

                var topCenter = imageMin + new Vector2(size.X / 2f, 8f);
                ImGuiX.DrawTime(topCenter, _clock.CurrentTime);
            }
        }

        DrawControlPanel();
    }

    private void DrawControlPanel()
    {
        const ImGuiWindowFlags flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize |
                                       ImGuiWindowFlags.NoCollapse;
        if (ImGui.Begin($"Control##{Title}", flags))
        {
            var timeFactor = _clock.TimeFactor;
            if (ImGui.DragFloat("Time Factor", ref timeFactor, 0.1f, 1f, 20f))
            {
                _clock.TimeFactor = timeFactor;
            }

            ImGui.End();
        }
    }

    public override void Dispose()
    {
        _scene.Dispose();
        base.Dispose();
    }
}