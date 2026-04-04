using FezEditor.Actors;
using FezEditor.Services;
using FezEditor.Structure;
using FEZRepacker.Core.Definitions.Game.Level;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

internal abstract class EddyContext : IDisposable
{
    protected static readonly Color HoverColor = Color.Blue with { A = 85 }; // 33%

    protected static readonly Color SelectionColor = Color.Red with { A = 85 }; // 33%

    protected const int InvalidId = -1;

    public Scene Scene { get; init; } = null!;

    public Clock Clock { get; init; } = null!;

    public History History { get; init; } = null!;

    public Level Level { get; init; } = null!;

    public Camera Camera { get; init; } = null!;

    public CursorMesh CursorMesh { get; internal set; } = null!;

    public Gizmo Gizmo { get; internal set; } = null!;

    public EddyContexts Contexts { get; init; } = null!;

    public AssetBrowser AssetBrowser { get; init; } = null!;

    public ResourceService ResourceService { get; init; } = null!;

    public StatusService StatusService { get; init; } = null!;

    public EddyTool Tool { get; set; }

    public virtual bool IsSelected => false;

    public virtual bool IsHovered(Ray ray, RaycastHit? hit)
    {
        return false;
    }

    public virtual void Update()
    {
    }

    public virtual void DrawCursor()
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