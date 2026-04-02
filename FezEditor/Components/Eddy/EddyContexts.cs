using FezEditor.Actors;
using FezEditor.Structure;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

internal class EddyContexts : IDisposable
{
    public EddyContext Current { get; private set; } = null!;

    public EddyContext Previous { get; private set; } = null!;

    public EddyContext Selected { get; private set; } = null!;

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
    }

    public void Init<T>() where T : EddyContext
    {
        Current = Get<T>();
        Selected = Previous = Current;
    }

    public void Dispose()
    {
        foreach (var context in _contexts)
        {
            context.Dispose();
        }
    }

    public void TransitionTo<T>(params object[] args) where T : EddyContext
    {
        var next = _contexts.OfType<T>().First();
        if (Current != next)
        {
            Current.End();
            Previous = Current;
            Current = next;
            if (next is not DefaultEddyContext)
            {
                Selected = Current;
            }

            Current.Enter(args);
        }
    }

    public void TestConditions(Ray ray, Vector2 viewport)
    {
        foreach (var context in _contexts)
        {
            context.TestConditions(ray, viewport);
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
        Current.Update();
    }

    public void DrawDebug(Dictionary<string, string> stats)
    {
        Get<TrileContext>().DrawDebug(stats);
    }

    public void ProvideCursor(CursorMesh cursor)
    {
        foreach (var context in _contexts)
        {
            context.Cursor = cursor;
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