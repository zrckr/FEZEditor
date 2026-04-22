using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.ArtObject;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.TrileSet;
using ImGuiNET;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Serilog;

namespace FezEditor.Components;

[UsedImplicitly]
public class ThumbnailGenerator : DrawableGameComponent
{
    private static readonly ILogger Logger = Logging.Create<ThumbnailGenerator>();

    private readonly ResourceService _resources;

    private readonly Dictionary<CollisionType, RTexture2D> _collisionTextures = new();

    private float _progress;

    private string _status = "";

    private State _state = State.Processing;

    private State _previousState = State.Disposed;

    private CancellationTokenSource? _cts;

    public ThumbnailGenerator(Game game) : base(game)
    {
        _resources = game.GetService<ResourceService>();
        _ = ProcessAsync();
    }

    protected override void LoadContent()
    {
        var content = Game.GetService<ContentService>().Get(this);
        foreach (var collision in Enum.GetValues<CollisionType>())
        {
            var texture = content.Load<Texture2D>($"Textures/{collision}");
            var data = new byte[texture.Width * texture.Height * 4];
            texture.GetData(data);
            _collisionTextures[collision] = new RTexture2D
            {
                Width = texture.Width,
                Height = texture.Height,
                TextureData = data
            };
        }
    }

    public override void Update(GameTime gameTime)
    {
        if (_state == State.Disposed)
        {
            Game.RemoveComponent(this);
        }
    }

    public override void Draw(GameTime gameTime)
    {
        const string popup = "Thumbnails";
        if (_state != _previousState)
        {
            ImGuiX.SetNextWindowCentered();
            ImGui.OpenPopup(popup);
            _previousState = _state;
        }

        var isOpen = true;
        if (ImGui.BeginPopupModal(popup, ref isOpen, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse))
        {
            switch (_state)
            {
                case State.Processing:
                    ImGui.Text(_status);
                    ImGuiX.ProgressBar(_progress, new Vector2(400, 0), $"{_progress * 100:F1}%");
                    break;

                case State.Complete:
                    _state = State.Disposed;
                    ImGui.CloseCurrentPopup();
                    break;
            }

            ImGui.EndPopup();
        }

        if (!isOpen)
        {
            _state = State.Disposed;
        }
    }

    private async Task ProcessAsync()
    {
        _cts = new CancellationTokenSource();
        _state = State.Processing;
        _status = "Processing thumbnails...";
        _progress = 0f;

        try
        {
            var ct = _cts.Token;
            await Task.Run(() => ProcessInternal(ct), ct);
            _status = "Generation complete!";
            _progress = 1.0f;
        }
        catch (OperationCanceledException)
        {
            _status = "Generation cancelled";
        }
        catch (Exception ex)
        {
            _status = $"Error: {ex.Message}";
            Logger.Error(ex, "Thumbnail generation failed");
        }
        finally
        {
            _state = State.Complete;
        }
    }

    private void ProcessInternal(CancellationToken ct)
    {
        var entries = new Queue<Entry>();
        var npcFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in _resources.Files)
        {
            ct.ThrowIfCancellationRequested();
            if (file.StartsWith("Trile Sets/", StringComparison.OrdinalIgnoreCase))
            {
                var lastWrite = _resources.GetLastWriteTimeUtc(file);
                var trileNames = _resources.GetTrileSetList(file);
                foreach (var name in trileNames.Values)
                {
                    if (!new Thumbnailer($"{file}/{name}", lastWrite).HasInCache())
                    {
                        entries.Enqueue(new Entry(file, AssetType.Trile, name));
                    }
                }
            }
            else if (file.StartsWith("Art Objects/", StringComparison.OrdinalIgnoreCase))
            {
                var extension = _resources.GetExtension(file);
                if (!extension.EndsWith(".png"))
                {
                    entries.Enqueue(new Entry(file, AssetType.ArtObject));
                }
            }
            else if (file.StartsWith("Background Planes/", StringComparison.OrdinalIgnoreCase))
            {
                entries.Enqueue(new Entry(file, AssetType.BackgroundPlane));
            }
            else if (file.StartsWith("Character Animations/", StringComparison.OrdinalIgnoreCase) &&
                     !file.Contains("Metadata", StringComparison.OrdinalIgnoreCase))
            {
                var remainder = file["Character Animations/".Length..];
                var slashIndex = remainder.IndexOf('/');
                if (slashIndex >= 0)
                {
                    var folder = $"Character Animations/{remainder[..slashIndex]}";
                    if (npcFolders.Add(folder))
                    {
                        entries.Enqueue(new Entry(folder, AssetType.NonPlayableCharacter));
                    }
                }
            }
        }

        var processed = 0;
        var total = entries.Count;
        TrileSet? cachedTrileSet = null;
        string? cachedTrileSetPath = null;

        while (entries.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var entry = entries.Dequeue();
            try
            {
                var lastWrite = _resources.GetLastWriteTimeUtc(entry.Path);
                var cachePath = entry.CachePath;

                var cacheProbe = new Thumbnailer(cachePath, lastWrite);
                if (cacheProbe.HasInCache())
                {
                    Logger.Debug("Thumbnail for {0} already cached", cachePath);
                    processed++;
                    _progress = (float)processed / total;
                    continue;
                }

                Thumbnailer thumbnailer = null!;
                switch (entry.Type)
                {
                    case AssetType.ArtObject:
                        {
                            var ao = (ArtObject)_resources.Load(entry.Path);
                            thumbnailer = new Thumbnailer(cachePath, lastWrite, ao);
                            break;
                        }

                    case AssetType.Trile:
                        {
                            if (cachedTrileSetPath != entry.Path)
                            {
                                cachedTrileSet = (TrileSet)_resources.Load(entry.Path);
                                cachedTrileSetPath = entry.Path;
                            }

                            var trile = cachedTrileSet!.Triles.Values
                                .FirstOrDefault(t => t.Name == entry.TrileName);
                            if (trile == null)
                            {
                                break;
                            }

                            if (trile.Geometry.Indices.Length > 0)
                            {
                                thumbnailer = new Thumbnailer(cachePath, lastWrite, trile, cachedTrileSet.TextureAtlas);
                            }
                            else if (trile.Faces.TryGetValue(FaceOrientation.Front, out var collisionType) &&
                                     _collisionTextures.TryGetValue(collisionType, out var collisionTex))
                            {
                                thumbnailer = new Thumbnailer(cachePath, lastWrite, collisionTex);
                            }

                            break;
                        }

                    case AssetType.BackgroundPlane:
                        {
                            var asset = _resources.Load(entry.Path);
                            if (asset is RAnimatedTexture anim)
                            {
                                thumbnailer = new Thumbnailer(cachePath, lastWrite, anim);
                            }
                            else if (asset is RTexture2D tex)
                            {
                                thumbnailer = new Thumbnailer(cachePath, lastWrite, tex);
                            }

                            break;
                        }

                    case AssetType.NonPlayableCharacter:
                        {
                            var animations = _resources.LoadAnimations(entry.Path);

                            RAnimatedTexture? selected = null;
                            if (animations.TryGetValue("IdleWink", out var idleWink))
                            {
                                selected = idleWink;
                            }
                            else if (animations.TryGetValue("Idle", out var idle))
                            {
                                selected = idle;
                            }
                            else if (animations.TryGetValue("Walk", out var walk))
                            {
                                selected = walk;
                            }
                            else if (animations.Count > 0)
                            {
                                selected = animations.Values.First();
                            }

                            if (selected != null)
                            {
                                thumbnailer = new Thumbnailer(cachePath, lastWrite, selected);
                            }

                            break;
                        }

                    default:
                        throw new InvalidOperationException();
                }

                var thumbnail = thumbnailer.Generate();
                thumbnailer.Save(thumbnail);

                processed++;
                _progress = (float)processed / total;
            }
            catch (Exception e)
            {
                Logger.Warning(e, "Failed to generate thumbnail for {0}", entry.CachePath);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        Game.GetService<ContentService>().Unload(this);
        _cts?.Dispose();
        base.Dispose(disposing);
    }

    private enum State
    {
        Disposed,
        Processing,
        Complete
    }

    private readonly record struct Entry(string Path, AssetType Type, string? TrileName = null)
    {
        public string CachePath => TrileName != null ? $"{Path}/{TrileName}" : Path;
    }
}