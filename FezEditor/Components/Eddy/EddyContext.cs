using FezEditor.Actors;
using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Level;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

internal abstract class EddyContext : IDisposable
{
    protected const int InvalidId = -1;

    public Scene Scene { get; init; } = null!;

    public History History { get; init; } = null!;

    public Level Level { get; init; } = null!;

    public Camera Camera { get; init; } = null!;

    public AssetBrowser AssetBrowser { get; init; } = null!;

    public ResourceService ResourceService { get; init; } = null!;

    public InputService InputService { get; init; } = null!;

    public StatusService StatusService { get; init; } = null!;

    public IContentManager ContentManager { get; init; } = null!;

    public Dirty<EddyTool> Tool { get; set; }

    public Vector2 ViewportMin { get; set; }

    public CursorMesh Cursor { get; set; } = null!;

    public virtual bool Pick(Ray ray)
    {
        return false;
    }

    public virtual void Update()
    {
    }

    public virtual void Revisualize(bool partial = false)
    {
    }

    public virtual void DrawProperties()
    {
        ImGui.Text("Select any object\nto view its properties");
    }

    public virtual void Dispose()
    {
    }
}