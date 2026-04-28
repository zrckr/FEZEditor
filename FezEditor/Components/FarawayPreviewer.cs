using FezEditor.Components.Eddy;
using FezEditor.Services;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Level;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Serilog;
using Color = Microsoft.Xna.Framework.Color;

namespace FezEditor.Components;

public class FarawayPreviewer : DrawableGameComponent
{
    private static readonly ILogger Logger = Logging.Create<FarawayPreviewer>();

    private static readonly (string Label, EddyEditor.ViewMode Mode, float Yaw)[] Viewpoints =
    [
        ("Front", EddyEditor.ViewMode.Front, 0f),
        ("Right", EddyEditor.ViewMode.Right, MathHelper.PiOver2),
        ("Back",  EddyEditor.ViewMode.Back,  MathHelper.Pi),
        ("Left",  EddyEditor.ViewMode.Left,  -MathHelper.PiOver2)
    ];

    public bool IsExporting => _state is State.WaitFrame or State.Capturing;

    private readonly Level _level;

    private readonly EddyEditor _eddy;

    private readonly ResourceService _resources;

    private readonly EddyVisuals _savedVisuals;

    private (int W, int H) _savedRtSize;

    private int _viewpointIndex;

    private State _state = State.Idle;

    private ExportKind _pendingExport;

    private bool _closeRequested;

    public FarawayPreviewer(Game game, Level level, EddyEditor eddy) : base(game)
    {
        // draw before MainLayout (DrawOrder = -1) so capture happens before EddyEditor resizes the RT
        DrawOrder = -2;

        _level = level;
        _eddy = eddy;
        _resources = game.GetService<ResourceService>();

        _savedVisuals = eddy.Visuals.Value;
        _savedRtSize = _eddy.Scene.Viewport.GetSize();

        _eddy.Visuals = EddyVisuals.Preview;
        _eddy.SwitchToOrtho(Viewpoints[0].Mode, Viewpoints[0].Yaw);
        _viewpointIndex = 0;
    }

    public override void Update(GameTime gameTime)
    {
        if (_state == State.Idle && _closeRequested)
        {
            _state = State.Disposing;
        }

        if (_state == State.Disposing)
        {
            RestoreRt();
            _eddy.Visuals = _savedVisuals;
            _eddy.SwitchToPerspective();
            Game.RemoveComponent(this);
        }
    }

    public override void Draw(GameTime gameTime)
    {
        if (_state == State.WaitFrame)
        {
            _state = State.Capturing;
            return;
        }

        if (_state == State.Capturing)
        {
            Capture();
            _state = State.Idle;
            RestoreRt();
            _eddy.Visuals = EddyVisuals.Preview;
            return;
        }

        var open = true;

        const ImGuiWindowFlags flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse;
        if (!ImGui.Begin($"Level Previewer##{_level.Name}", ref open, flags))
        {
            ImGui.End();
            return;
        }

        if (!open)
        {
            _closeRequested = true;
            ImGui.End();
            return;
        }

        #region Viewpoint buttons

        ImGui.Text("Viewpoint:");
        ImGui.SameLine();
        for (var i = 0; i < Viewpoints.Length; i++)
        {
            if (i > 0)
            {
                ImGui.SameLine();
            }

            var isActive = i == _viewpointIndex;
            if (isActive)
            {
                ImGui.BeginDisabled();
            }

            if (ImGui.Button(Viewpoints[i].Label))
            {
                _viewpointIndex = i;
                _eddy.SwitchToOrtho(Viewpoints[i].Mode, Viewpoints[i].Yaw);
            }

            if (isActive)
            {
                ImGui.EndDisabled();
            }
        }

        #endregion

        ImGui.TextDisabled("RMB: Pan | Scroll: Zoom");
        ImGui.Separator();

        #region Export buttons

        if (_state == State.Idle)
        {
            if (ImGui.Button("Save as Faraway Thumb"))
            {
                BeginExport(ExportKind.FarawayThumb);
            }

            ImGui.SameLine();

            if (ImGui.Button("Save as Map Screen"))
            {
                BeginExport(ExportKind.MapScreen);
            }
        }
        else
        {
            ImGui.TextDisabled("Capturing...");
        }

        #endregion

        ImGui.End();
    }

    private void BeginExport(ExportKind kind)
    {
        _pendingExport = kind;
        _state = State.WaitFrame;

        _savedRtSize = _eddy.Scene.Viewport.GetSize();
        if (kind == ExportKind.FarawayThumb)
        {
            _eddy.Scene.Viewport.SetSize(512, 512);
            _eddy.Scene.Viewport.SetClearColor(new Color(255, 0, 255)); // magenta chroma-key
            _eddy.Visuals = EddyVisuals.Preview & ~EddyVisuals.Sky;
        }
        else
        {
            _eddy.Scene.Viewport.SetSize(128, 128);
            _eddy.Scene.Viewport.SetClearColor(Color.Black);
            _eddy.Visuals = EddyVisuals.Preview;
        }
    }

    private void RestoreRt()
    {
        _eddy.Scene.Viewport.SetClearColor(Color.Black);
        _eddy.Scene.Viewport.SetSize(_savedRtSize.W, _savedRtSize.H);
    }

    private void Capture()
    {
        if (_eddy.Scene.Viewport.GetTexture() is not RenderTarget2D texture)
        {
            Logger.Warning("Render target texture is null, skipping capture");
            return;
        }

        var pixels = new Color[texture.Width * texture.Height];
        texture.GetData(pixels);

        var viewpointLabel = Viewpoints[_viewpointIndex].Label;
        string outputPath;

        if (_pendingExport == ExportKind.FarawayThumb)
        {
            var dir = _resources.GetFullPath("Other Textures/faraway_thumbs");
            Directory.CreateDirectory(dir);
            outputPath = Path.Combine(dir, $"{_level.Name} ({viewpointLabel}).png");
        }
        else
        {
            var dir = _resources.GetFullPath("Other Textures/map_screens");
            Directory.CreateDirectory(dir);
            outputPath = Path.Combine(dir, $"{_level.Name}.png");
        }

        _ = Task.Run(() =>
        {
            try
            {
                var rgba = new byte[texture.Width * texture.Height * 4];
                for (var i = 0; i < pixels.Length; i++)
                {
                    var c = pixels[i];
                    if (_pendingExport == ExportKind.FarawayThumb && c is { R: > 200, G: < 50, B: > 200 })
                    {
                        rgba[i * 4 + 0] = 0;
                        rgba[i * 4 + 1] = 0;
                        rgba[i * 4 + 2] = 0;
                        rgba[i * 4 + 3] = 0;
                    }
                    else
                    {
                        rgba[i * 4 + 0] = c.R;
                        rgba[i * 4 + 1] = c.G;
                        rgba[i * 4 + 2] = c.B;
                        rgba[i * 4 + 3] = 255;
                    }
                }

                using var image = Image.LoadPixelData<Rgba32>(rgba, texture.Width, texture.Height);
                image.SaveAsPng(outputPath);

                Logger.Information("Saved {0}", outputPath);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to save capture");
            }
        });
    }

    private enum State
    {
        Idle,
        WaitFrame,
        Capturing,
        Disposing
    }

    private enum ExportKind
    {
        FarawayThumb,
        MapScreen
    }
}