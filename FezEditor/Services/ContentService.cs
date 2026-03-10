using FezEditor.Tools;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Serilog;
using Serilog.Core;

namespace FezEditor.Services;

[UsedImplicitly]
public class ContentService : IDisposable
{
    private static readonly ILogger Logger = Logging.Create<ContentService>();

    private const string Root = "Content";

    private readonly Dictionary<object, IContentManager> _managers = new();

    private readonly IServiceProvider _services;

    public IContentManager Global { get; }

    public ContentService(Game game)
    {
        _services = game.Services;
        Global = Get(game);
    }

    public IContentManager Get<T>(T context) where T : class
    {
        if (!_managers.TryGetValue(context, out var manager))
        {
            if (FezEditor.IsDebugBuild)
            {
                manager = new DirContentManager(_services, Root);
            }
            else
            {
                manager = new ZipContentManager(_services, Path.ChangeExtension(Root, ".pkz"));
            }

            Logger.Information("Loaded {0} for {1}",
                manager.GetType().Name, context.GetType().Name);
            _managers.Add(context, manager);
        }

        return manager;
    }

    public void Unload<T>(T context) where T : class
    {
        if (_managers.Remove(context, out var manager))
        {
            manager.Unload();
            manager.Dispose();
            Logger.Information("Unloaded {0} for {1}",
                manager.GetType().Name, context.GetType().Name);
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        foreach (var cm in _managers.Values)
        {
            cm.Unload();
            cm.Dispose();
        }

        _managers.Clear();
    }
}