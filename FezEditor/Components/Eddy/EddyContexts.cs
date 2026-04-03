using FezEditor.Actors;
using FezEditor.Structure;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

internal class EddyContexts : IDisposable
{
    public EddyContext Current { get; private set; } = null!;

    public CursorMesh Cursor { get; set; } = null!;

    public Dirty<bool> ShowCollisionMap { get; set; }

    public Dirty<bool> ShowPickableBounds { get; set; }

    private readonly List<EddyContext> _contexts = new();

    private EddyContext? _hoveredContext;

    public void AddOrdered<T>(T context) where T : EddyContext
    {
        if (_contexts.OfType<T>().Any())
        {
            throw new InvalidOperationException($"This context is already present {typeof(T).Name}");
        }

        _contexts.Add(context);
    }

    public void Init<T>() where T : EddyContext
    {
        Current = Get<T>();
    }

    public void Dispose()
    {
        foreach (var context in _contexts)
        {
            context.Dispose();
        }
    }

    public void CheckHovered(Ray ray, RaycastHit? hit, Vector2 viewport)
    {
        _hoveredContext = null;
        foreach (var context in _contexts)
        {
            if (context.IsHovered(ray, hit, viewport))
            {
                _hoveredContext = context;
            }
        }
    }

    public void ClearHover()
    {
        _hoveredContext = null;
    }

    public void Update()
    {
        if (ShowCollisionMap.IsDirty)
        {
            Get<TrileContext>().ShowCollisionMap(ShowCollisionMap.Value);
            ShowCollisionMap = ShowCollisionMap.Clean();
        }

        if (ShowPickableBounds.IsDirty)
        {
            Get<DefaultEddyContext>().ShowPickableBounds(ShowPickableBounds.Value);
            ShowPickableBounds = ShowPickableBounds.Clean();
        }

        var selected = _contexts.FirstOrDefault(c => c.IsSelected);
        EddyContext next;
        if (selected != null && !ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            next = selected;
        }
        else
        {
            next = _hoveredContext ?? Get<DefaultEddyContext>();
        }

        if (Current != next)
        {
            Console.WriteLine($"{Current} -> {next}");
            Current.End();
            Current = next;
            Current.Enter();
            return;
        }

        Get<DefaultEddyContext>().UpdateLighting();
        Current.Update();

        Cursor.ClearHover();
        Cursor.ClearSelection();
        foreach (var context in _contexts)
        {
            context.DrawCursor(Cursor);
        }
    }

    public void SyncTool(EddyTool tool)
    {
        foreach (var context in _contexts)
        {
            context.Tool = tool;
        }
    }

    public void RevisualizeAll()
    {
        foreach (var context in _contexts)
        {
            context.Revisualize();
        }

        Get<DefaultEddyContext>().PostRevisualize();
    }

    private T Get<T>() where T : EddyContext
    {
        return _contexts.OfType<T>().FirstOrDefault()!;
    }
}
