using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.ArtObject;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.TrileSet;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Serilog;

namespace FezEditor.Components;

[UsedImplicitly]
public class ThumbnailGenerator : DrawableGameComponent
{
    private static readonly ILogger Logger = Log.ForContext<ThumbnailGenerator>();

    private static readonly Dictionary<CollisionType, RTexture2D> CollisionTextures = new();

    private readonly ResourceService _resources;

    private readonly StatusService _statusService;

    private CancellationTokenSource? _cts;

    private int _complete;

    private bool _disposed;

    public ThumbnailGenerator(Game game) : base(game)
    {
        _resources = game.GetService<ResourceService>();
        _statusService = game.GetService<StatusService>();
    }

    protected override void LoadContent()
    {
        if (CollisionTextures.Count == 0)
        {
            var content = Game.GetService<ContentService>().Global;
            foreach (var collision in Enum.GetValues<CollisionType>())
            {
                var texture = content.Load<Texture2D>($"Textures/{collision}");
                var data = new byte[texture.Width * texture.Height * 4];
                texture.GetData(data);
                CollisionTextures[collision] = new RTexture2D
                {
                    Width = texture.Width,
                    Height = texture.Height,
                    TextureData = data
                };
            }
        }

        _ = ProcessAsync();
    }

    public override void Update(GameTime gameTime)
    {
        if (Volatile.Read(ref _complete) != 0)
        {
            Game.RemoveComponent(this);
        }
    }

    public void Cancel()
    {
        try
        {
            _cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The worker completed between reading and cancelling the source.
        }
    }

    private async Task ProcessAsync()
    {
        var cts = new CancellationTokenSource();
        _cts = cts;

        try
        {
            await Task.Run(() => ProcessInternal(cts.Token), cts.Token);
        }
        catch (OperationCanceledException)
        {
            Logger.Debug("Thumbnail generation cancelled");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Thumbnail generation failed");
        }
        finally
        {
            _cts = null;
            cts.Dispose();
            Volatile.Write(ref _complete, 1);
        }
    }

    private void ProcessInternal(CancellationToken ct)
    {
        var entries = new Queue<Entry>();
        var npcFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = _resources.Files.ToArray();

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var extension = _resources.GetExtension(file);
                if (file.StartsWith("Trile Sets/", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".fezts.glb", StringComparison.OrdinalIgnoreCase))
                {
                    var trileNames = _resources.GetTrileSetList(file);
                    foreach (var name in trileNames.Values)
                    {
                        EnqueueIfNeeded(entries, new Entry(file, AssetType.Trile, name));
                    }
                }
                else if (file.StartsWith("Art Objects/", StringComparison.OrdinalIgnoreCase) ||
                         extension.Equals(".fezao.glb", StringComparison.OrdinalIgnoreCase))
                {
                    if (!extension.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    {
                        EnqueueIfNeeded(entries, new Entry(file, AssetType.ArtObject));
                    }
                }
                else if (file.StartsWith("Background Planes/", StringComparison.OrdinalIgnoreCase))
                {
                    EnqueueIfNeeded(entries, new Entry(file, AssetType.BackgroundPlane));
                }
                else if (!file.Contains("Metadata", StringComparison.OrdinalIgnoreCase) &&
                         TryGetNpcFolder(file, out var folder) && npcFolders.Add(folder))
                {
                    EnqueueIfNeeded(entries, new Entry(folder, AssetType.NonPlayableCharacter));
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to inspect thumbnail source {0}", file);
            }
        }

        var processed = 0;
        var total = entries.Count;
        if (total == 0)
        {
            return;
        }

        using var activity = _statusService.BeginActivity($"Generating thumbnails (0/{total})", 0f);
        TrileSet? cachedTrileSet = null;
        string? cachedTrileSetPath = null;

        while (entries.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var entry = entries.Dequeue();
            try
            {
                var cachePath = entry.CachePath;
                var cacheProbe = new Thumbnailer(cachePath);
                if (!cacheProbe.NeedsGeneration())
                {
                    Logger.Debug("Thumbnail for {0} already cached", cachePath);
                    continue;
                }

                Thumbnailer? thumbnailer = null;
                switch (entry.Type)
                {
                    case AssetType.ArtObject:
                        {
                            var ao = _resources.Load<ArtObject>(entry.Path);
                            thumbnailer = new Thumbnailer(cachePath, ao);
                            break;
                        }

                    case AssetType.Trile:
                        {
                            if (cachedTrileSetPath != entry.Path)
                            {
                                cachedTrileSet = _resources.Load<TrileSet>(entry.Path);
                                cachedTrileSetPath = entry.Path;
                            }

                            var trile = cachedTrileSet!.Triles.Values
                                .FirstOrDefault(t => t.Name == entry.TrileName);
                            if (trile == null)
                            {
                                break;
                            }

                            if (!trile.Geometry.IsNullOrEmpty())
                            {
                                thumbnailer = new Thumbnailer(cachePath, trile, cachedTrileSet.TextureAtlas);
                            }
                            else if (trile.Faces.TryGetValue(FaceOrientation.Front, out var collisionType) &&
                                     CollisionTextures.TryGetValue(collisionType, out var collisionTex))
                            {
                                thumbnailer = new Thumbnailer(cachePath, collisionTex);
                            }

                            break;
                        }

                    case AssetType.BackgroundPlane:
                        {
                            var asset = _resources.Load<object>(entry.Path);
                            if (asset is RAnimatedTexture anim)
                            {
                                thumbnailer = new Thumbnailer(cachePath, anim);
                            }
                            else if (asset is RTexture2D tex)
                            {
                                thumbnailer = new Thumbnailer(cachePath, tex);
                            }

                            break;
                        }

                    case AssetType.NonPlayableCharacter:
                        {
                            var animations = _resources.LoadAnimations(entry.Path);
                            var selected = SelectNpcAnimation(animations);
                            if (selected != null)
                            {
                                thumbnailer = new Thumbnailer(cachePath, selected);
                            }

                            break;
                        }

                    default:
                        throw new InvalidOperationException();
                }

                if (thumbnailer != null)
                {
                    var thumbnail = thumbnailer.Generate();
                    thumbnailer.Save(thumbnail);
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to generate thumbnail for {0}", entry.CachePath);
            }
            finally
            {
                processed++;
                activity.Report($"Generating thumbnails ({processed}/{total})", (float)processed / total);
            }
        }
    }

    private static RAnimatedTexture? SelectNpcAnimation(Dictionary<string, RAnimatedTexture> animations)
    {
        if (animations.TryGetValue("IdleWink", out var idleWink)) return idleWink;
        if (animations.TryGetValue("Idle", out var idle)) return idle;
        if (animations.TryGetValue("Walk", out var walk)) return walk;
        return animations.Count > 0 ? animations.Values.First() : null;
    }

    private static void EnqueueIfNeeded(Queue<Entry> entries, Entry entry)
    {
        if (new Thumbnailer(entry.CachePath).NeedsGeneration())
        {
            entries.Enqueue(entry);
        }
    }

    private static bool TryGetNpcFolder(string path, out string folder)
    {
        const string prefix = "Character Animations/";
        if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            var remainder = path[prefix.Length..];
            var slashIndex = remainder.IndexOf('/');
            if (slashIndex >= 0)
            {
                folder = prefix + remainder[..slashIndex];
                return true;
            }
        }

        folder = string.Empty;
        return false;
    }

    internal static void MarkDirtyForSave(string path, object asset)
    {
        switch (asset)
        {
            case ArtObject:
                {
                    Thumbnailer.MarkDirty(new Entry(path, AssetType.ArtObject).CachePath);
                    break;
                }

            case TrileSet trileSet:
                foreach (var trile in trileSet.Triles.Values)
                {
                    Thumbnailer.MarkDirty(new Entry(path, AssetType.Trile, trile.Name).CachePath);
                }

                break;

            case RTexture2D:
            case RAnimatedTexture:
                if (path.StartsWith("Background Planes/", StringComparison.OrdinalIgnoreCase))
                {
                    Thumbnailer.MarkDirty(new Entry(path, AssetType.BackgroundPlane).CachePath);
                }
                else if (TryGetNpcFolder(path, out var folder))
                {
                    Thumbnailer.MarkDirty(new Entry(folder, AssetType.NonPlayableCharacter).CachePath);
                }

                break;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            _disposed = true;
            Cancel();
            base.Dispose(disposing);
        }
    }

    private readonly record struct Entry(string Path, AssetType Type, string? TrileName = null)
    {
        public string CachePath
        {
            get
            {
                var prefix = Type switch
                {
                    AssetType.Trile => "Trile Sets/",
                    AssetType.ArtObject => "Art Objects/",
                    AssetType.BackgroundPlane => "Background Planes/",
                    AssetType.NonPlayableCharacter => "Character Animations/",
                    _ => throw new ArgumentOutOfRangeException()
                };

                var path = Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    ? Path
                    : prefix + Path;

                return TrileName != null ? $"{path}/{TrileName}" : path;
            }
        }
    }
}