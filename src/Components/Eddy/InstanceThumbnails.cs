using FezEditor.Services;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Level;
using FEZRepacker.Core.Definitions.Game.TrileSet;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Components.Eddy;

public sealed class InstanceThumbnails : IDisposable
{
    private static readonly Dictionary<string, Texture2D> SharedThumbnails = new();

    private static int s_instanceCount;

    private readonly ResourceService _resources;

    private readonly Level _level;

    private readonly TrileSet _trileSet;

    private Texture2D _placeholder = null!;

    public InstanceThumbnails(ResourceService resources, Level level, TrileSet trileSet)
    {
        _resources = resources;
        _level = level;
        _trileSet = trileSet;
        _resources.ThumbnailsReady += ClearSharedThumbnails;
        s_instanceCount++;
    }

    public void LoadContent(IContentManager content)
    {
        _placeholder = content.Load<Texture2D>("Missing");
    }

    public Texture2D Get(InstanceId instance)
    {
        if (instance is InstanceId.TrileGroup or InstanceId.Volume or InstanceId.Path or InstanceId.GroupPath)
        {
            return _placeholder;
        }

        var assetPath = instance switch
        {
            InstanceId.TrileBatch tb => "Trile Sets/" + _level.TrileSetName + "/" + _trileSet.Triles[tb.Id].Name,
            InstanceId.ArtObject ao => "Art Objects/" + _level.ArtObjects[ao.Id].Name,
            InstanceId.BackgroundPlane bp => "Background Planes/" + _level.BackgroundPlanes[bp.Id].TextureName,
            InstanceId.NonPlayableCharacter npc => "Character Animations/" + _level.NonPlayerCharacters[npc.Id].Name,
            InstanceId.Gomez => "Character Animations/Gomez",
            _ => throw new ArgumentOutOfRangeException(nameof(instance), instance, null)
        };

        return Get(assetPath);
    }

    public Texture2D Get(AssetEntry entry)
    {
        var assetPath = entry switch
        {
            AssetEntry.Trile t => "Trile Sets/" + t.Path,
            AssetEntry.ArtObject ao => "Art Objects/" + ao.Name,
            AssetEntry.BackgroundPlane bp => "Background Planes/" + bp.Name,
            AssetEntry.NonPlayableCharacter npc => "Character Animations/" + npc.Name,
            _ => throw new ArgumentOutOfRangeException(nameof(entry), entry, null)
        };

        return Get(assetPath);
    }

    private Texture2D Get(string assetPath)
    {
        var thumbnail = SharedThumbnails.GetValueOrDefault(assetPath);
        if (thumbnail != null)
        {
            return thumbnail;
        }

        var cacheProbe = new Thumbnailer(assetPath);
        if (cacheProbe.TryLoad(out var cached) && cached != null)
        {
            thumbnail = RepackerExtensions.ConvertToTexture2D(cached);
            SharedThumbnails[assetPath] = thumbnail;
            return thumbnail;
        }

        return _placeholder;
    }

    public void Dispose()
    {
        _resources.ThumbnailsReady -= ClearSharedThumbnails;
        s_instanceCount--;
        if (s_instanceCount < 1)
        {
            ClearSharedThumbnails();
        }
    }

    private void ClearSharedThumbnails()
    {
        foreach (var texture in SharedThumbnails.Values)
        {
            if (texture != _placeholder)
            {
                texture.Dispose();
            }
        }

        SharedThumbnails.Clear();
    }
}