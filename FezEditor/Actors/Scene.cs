using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using Microsoft.Xna.Framework;

namespace FezEditor.Actors;

public class Scene : IDisposable
{
    public SceneViewport Viewport { get; }

    public SceneLighting Lighting { get; }

    public Actor Root { get; }

    private readonly Game _game;

    private readonly Rid _worldRid;

    private readonly RenderingService _rendering;

    private readonly IContentManager _content;

    private readonly HashSet<Actor> _actors = new();

    private readonly Dictionary<Actor, HierarchyNode> _hierarchy = new();

    private bool _disposed;

    public Scene(Game game, IContentManager content)
    {
        _game = game;
        _content = content;
        _rendering = game.GetService<RenderingService>();

        _worldRid = _rendering.WorldCreate();
        Viewport = new SceneViewport(game, _worldRid);
        Lighting = new SceneLighting(game, _worldRid);

        var rootRid = _rendering.WorldGetRoot(_worldRid);
        Root = new Actor(_game, rootRid, _content);
        _actors.Add(Root);
        _hierarchy[Root] = new HierarchyNode(null, new List<Actor>());
    }

    public Actor CreateActor(Actor? parent = null)
    {
        var parentActor = parent ?? Root;
        var actor = new Actor(_game, parentActor.InstanceRid, _content);
        _actors.Add(actor);
        _hierarchy[parentActor].Children.Add(actor);
        _hierarchy[actor] = new HierarchyNode(parentActor, new List<Actor>());
        return actor;
    }

    public void DestroyActor(Actor actor)
    {
        if (actor == Root)
        {
            throw new InvalidOperationException("Cannot destroy the root actor.");
        }

        var parent = _hierarchy[actor].Parent;
        if (parent != null && _hierarchy.TryGetValue(parent, out var hierarchyNode))
        {
            hierarchyNode.Children.Remove(actor);
        }

        var stack = new Stack<Actor>();
        stack.Push(actor);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!_hierarchy.TryGetValue(current, out var node))
            {
                continue;
            }

            foreach (var child in node.Children)
            {
                stack.Push(child);
            }

            _hierarchy.Remove(current);
            _actors.Remove(current);
            current.Dispose();
        }
    }

    public RaycastHit? Raycast(Ray ray)
    {
        RaycastHit? nearest = null;
        var nearestDist = float.MaxValue;

        foreach (var actor in _actors)
        {
            if (actor.TryGetComponent<IPickable>(out var pickable) && (pickable?.Pickable ?? false))
            {
                var hit = pickable.Pick(ray);
                if (hit?.Distance < nearestDist)
                {
                    nearestDist = hit.Value.Distance;
                    nearest = new RaycastHit(actor, hit.Value.Distance, hit.Value.Index);
                }
            }
        }

        return nearest;
    }

    public Actor? GetParent(Actor actor)
    {
        return _hierarchy.TryGetValue(actor, out var node) ? node.Parent : null;
    }

    public IReadOnlyList<Actor> GetChildren(Actor actor)
    {
        return _hierarchy.TryGetValue(actor, out var node) ? node.Children : [];
    }

    public void Update(GameTime gameTime)
    {
        foreach (var actor in _actors.Where(a => a.Active))
        {
            actor.Update(gameTime);
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var actor in _actors)
        {
            actor.Dispose();
        }

        _actors.Clear();
        _hierarchy.Clear();
        Lighting.Dispose();
        Viewport.Dispose();
        _rendering.FreeRid(_worldRid);
    }

    private record HierarchyNode(Actor? Parent, List<Actor> Children);
}