using FezEditor.Actors;
using FezEditor.Structure;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

internal class EddyContexts : IDisposable
{
    public EddyContext Current { get; private set; } = null!;

    public EddyContext? Hovered { get; private set; }

    public CursorMesh Cursor { get; set; } = null!;

    public Gizmo Gizmo { get; set; } = null!;

    public Dirty<bool> ShowCollisionMap { get; set; }

    public Dirty<bool> ShowPickableBounds { get; set; }

    private readonly List<EddyContext> _contexts = new();

    public void AddOrdered<T>(T context) where T : EddyContext
    {
        if (_contexts.OfType<T>().Any())
        {
            throw new InvalidOperationException($"This context is already present {typeof(T).Name}");
        }

        _contexts.Add(context);
        if (context is DefaultEddyContext)
        {
            Current = context;
        }
    }

    public void Dispose()
    {
        foreach (var context in _contexts)
        {
            context.Dispose();
        }
    }

    public void CheckHovered(Ray ray, RaycastHit? hit)
    {
        Hovered = null;
        foreach (var context in _contexts)
        {
            context.Gizmo = Gizmo;
            context.CursorMesh = Cursor;
            if (context.IsHovered(ray, hit))
            {
                Hovered = context;
            }
        }
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

        Get<DefaultEddyContext>().UpdateLighting();

        var next = Current;
        if (!_contexts.Any(c => c.IsSelected))
        {
            next = Hovered ?? Get<DefaultEddyContext>();
        }

        if (Current != next)
        {
            Current = next;
        }

        Current.Update();
        Cursor.ClearHover();
        Cursor.ClearSelection();
        Hovered?.DrawCursor();
        Current.DrawCursor();
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