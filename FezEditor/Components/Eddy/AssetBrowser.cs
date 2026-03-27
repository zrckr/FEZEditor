using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.ArtObject;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.TrileSet;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Serilog;

namespace FezEditor.Components.Eddy;

public class AssetBrowser : IDisposable
{
    private static readonly ILogger Logger = Logging.Create<AssetBrowser>();

    private static readonly Dictionary<string, Texture2D> SharedThumbnails = new();

    private static int s_instanceCount;

    private const float ThumbSize = 64f;

    private const float RecentThumbSize = 48f;

    private const float CellSpacing = 8f;

    private const float CellSize = ThumbSize + CellSpacing;

    private const float LabelHeight = 20f;

    private const float RowHeight = CellSize + LabelHeight;

    private const int MaxThumbnailsPerFrame = 2;

    private const int MaxRecentEntries = 10;

    public Entry SelectedEntry { get; private set; }

    private readonly List<Entry> _recentEntries = new();

    private readonly ResourceService _resources;

    private readonly Dictionary<AssetType, IReadOnlyList<Entry>> _entries = new();

    private readonly Queue<Entry> _pendingQueue = new();

    private string? _trileSetPath;

    private TrileSet? _trileSet;

    private readonly Dictionary<CollisionType, RTexture2D> _collisionTextures = new();

    private Texture2D _placeholder = null!;

    private string _filterEntries = string.Empty;

    public AssetBrowser(Game game)
    {
        _resources = game.GetService<ResourceService>();
        _resources.ProviderChanged += OnProviderChanged;
        s_instanceCount++;
    }

    public void SetTrileSet(string path, TrileSet set)
    {
        _trileSetPath = path;
        _trileSet = set;
        _entries.Clear();
    }

    public void LoadContent(IContentManager content)
    {
        _placeholder = content.Load<Texture2D>("Missing");
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

    public void Pick(string name, AssetType type)
    {
        if (_entries.TryGetValue(type, out var entries))
        {
            foreach (var entry in entries)
            {
                if (string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    Select(entry);
                    break;
                }
            }
        }
    }

    private void Select(Entry entry)
    {
        SelectedEntry = entry;
        if (entry == default)
        {
            return;
        }

        _recentEntries.Remove(entry);
        _recentEntries.Insert(0, entry);
        if (_recentEntries.Count > MaxRecentEntries)
        {
            _recentEntries.RemoveAt(_recentEntries.Count - 1);
        }
    }

    public void Draw()
    {
        ProcessQueue();
        TryBuildEntries();

        DrawSelectionBar();
        ImGui.Separator();

        var footerHeight = ImGui.GetFrameHeightWithSpacing() + ImGui.GetStyle().ItemSpacing.Y;
        ImGui.BeginChild("##Content", new NVector2(0, -footerHeight));

        if (_entries.Count > 0 && ImGui.BeginTabBar("##AssetTabs"))
        {
            foreach (var type in Enum.GetValues<AssetType>())
            {
                if (ImGui.BeginTabItem(type.GetLabel()))
                {
                    if (type == AssetType.Trile && _trileSetPath != null)
                    {
                        ImGui.TextDisabled(_trileSetPath);
                        ImGui.Separator();
                    }

                    if (_entries.TryGetValue(type, out var entries))
                    {
                        IReadOnlyList<Entry> ret;
                        if (string.IsNullOrEmpty(_filterEntries))
                        {
                            ret = entries;
                        }
                        else
                        {
                            ret = entries
                                .Where(entry => entry.Name.Contains(_filterEntries, StringComparison.OrdinalIgnoreCase))
                                .ToList();
                        }

                        DrawGrid(ret);
                    }

                    ImGui.EndTabItem();
                }
            }

            ImGui.EndTabBar();
        }

        ImGui.EndChild();

        ImGui.Separator();
        DrawFooter();
    }

    private void DrawSelectionBar()
    {
        var selected = SelectedEntry;
        var isAnySelected = selected != default;

        // Header line: "Selected: Name (Type)"
        ImGui.TextDisabled("Selected:");
        ImGui.SameLine();
        if (isAnySelected)
        {
            ImGui.TextUnformatted($"{selected.Name} ({selected.Type.GetLabel()})");
        }
        else
        {
            ImGui.TextDisabled("(none)");
        }

        // Thumbnail row
        const float barHeight = ThumbSize + CellSpacing * 2;
        ImGui.BeginChild("##SelectionBar", new NVector2(0, barHeight), ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar);

        var selectedThumb = SharedThumbnails.GetValueOrDefault(selected.CachePath) ?? _placeholder;
        ImGui.BeginDisabled(!isAnySelected);
        ImGuiX.Image(selectedThumb, new Vector2(ThumbSize));
        ImGui.EndDisabled();
        ImGui.SameLine();

        for (var i = 0; i < MaxRecentEntries; i++)
        {
            ImGui.SameLine();
            ImGui.PushID(i);

            if (i < _recentEntries.Count)
            {
                var recent = _recentEntries[i];
                var thumb = SharedThumbnails.GetValueOrDefault(recent.CachePath) ?? _placeholder;

                if (ImGuiX.ImageButton("##recent", thumb, new Vector2(RecentThumbSize)))
                {
                    Select(recent);
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip($"{recent.Name} ({recent.Type.GetLabel()})");
                }
            }
            else
            {
                ImGui.BeginDisabled();
                ImGuiX.Button("##recent", new Vector2(RecentThumbSize));
                ImGui.EndDisabled();
            }

            ImGui.PopID();
        }

        ImGui.EndChild();
    }

    private void DrawFooter()
    {
        ImGui.SetNextItemWidth(256);
        ImGui.InputTextWithHint("", "Filter assets...", ref _filterEntries, 255);

        if (!string.IsNullOrEmpty(_filterEntries))
        {
            ImGui.SameLine();
            if (ImGui.Button($"{Icons.Close}"))
            {
                _filterEntries = string.Empty;
            }
        }

        var isGenerating = _pendingQueue.Count > 0;
        if (isGenerating)
        {
            ImGui.BeginDisabled();
        }

        ImGui.SameLine();
        if (ImGui.Button($"{Icons.Refresh} Refresh Thumbnails"))
        {
            foreach (var entry in _entries.Values.SelectMany(e => e))
            {
                new ThumbnailGenerator(entry.CachePath, default).Delete();
            }

            ClearSharedThumbnails(_placeholder);
            _entries.Clear();
        }

        if (isGenerating)
        {
            ImGui.EndDisabled();
        }

        if (_pendingQueue.Count > 0)
        {
            var spinner = "|/-\\"[(int)(ImGui.GetTime() * 8) % 4];
            ImGui.SameLine();
            ImGui.TextDisabled($"{spinner} Generating thumbnails... ({_pendingQueue.Count} remaining)");
        }
    }

    private void ProcessQueue()
    {
        #region Process Queue

        var generated = 0;
        while (_pendingQueue.Count > 0)
        {
            var entry = _pendingQueue.Dequeue();

            try
            {
                var lastWrite = _resources.GetLastWriteTimeUtc(entry.Path);

                // Get thumbnail from cache
                var cacheProbe = new ThumbnailGenerator(entry.CachePath, lastWrite);
                var cached = cacheProbe.Load();
                if (cached != null)
                {
                    SharedThumbnails[entry.CachePath] = RepackerExtensions.ConvertToTexture2D(cached);
                    continue;
                }

                // Cache miss - limit expensive generation to avoid stalling
                if (generated >= MaxThumbnailsPerFrame)
                {
                    _pendingQueue.Enqueue(entry);
                    break;
                }

                // Load asset and generate thumbnail
                ThumbnailGenerator? generator = null;
                switch (entry.Type)
                {
                    case AssetType.ArtObject:
                        {
                            var asset = _resources.Load(entry.Path);
                            if (asset is ArtObject ao)  // exclude AOs with texture only
                            {
                                generator = new ThumbnailGenerator(entry.CachePath, lastWrite, ao);
                            }

                            break;
                        }

                    case AssetType.Trile:
                        {
                            if (_trileSet != null)
                            {
                                var trile = _trileSet.FindByName(entry.Name).Trile;
                                if (trile == null)
                                {
                                    break;
                                }

                                if (trile.Geometry.Vertices.Length > 0)
                                {
                                    var atlas = _trileSet.TextureAtlas;
                                    generator = new ThumbnailGenerator(entry.CachePath, lastWrite, trile, atlas);
                                }
                                else if (trile.Faces.TryGetValue(FaceOrientation.Front, out var collisionType) &&
                                         _collisionTextures.TryGetValue(collisionType, out var collisionTex))
                                {
                                    generator = new ThumbnailGenerator(entry.CachePath, lastWrite, collisionTex);
                                }
                            }

                            break;
                        }

                    case AssetType.BackgroundPlane:
                        {
                            var asset = _resources.Load(entry.Path);
                            if (asset is RAnimatedTexture anim)
                            {
                                generator = new ThumbnailGenerator(entry.CachePath, lastWrite, anim);
                            }
                            else if (asset is RTexture2D tex)
                            {
                                generator = new ThumbnailGenerator(entry.CachePath, lastWrite, tex);
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
                                generator = new ThumbnailGenerator(entry.CachePath, lastWrite, selected);
                            }

                            break;
                        }

                    default:
                        throw new InvalidOperationException();
                }

                if (generator == null)
                {
                    continue;
                }

                var thumb = generator.Generate();
                SharedThumbnails[entry.CachePath] = RepackerExtensions.ConvertToTexture2D(thumb);
                generator.Save(thumb);
                generated++;
            }
            catch (Exception e)
            {
                Logger.Warning(e, "Failed to generate thumbnail for {0}", entry.Path);
            }
        }

        #endregion
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _resources.ProviderChanged -= OnProviderChanged;

        s_instanceCount--;
        if (s_instanceCount <= 0)
        {
            ClearSharedThumbnails(_placeholder);
        }
    }

    private void TryBuildEntries()
    {
        if (_entries.Count > 0 || _resources.HasNoProvider)
        {
            return;
        }

        var triles = new List<Entry>();
        var artObjects = new List<Entry>();
        var planes = new List<Entry>();
        var npcFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var npcs = new List<Entry>();

        if (_trileSet != null && _trileSetPath != null)
        {
            triles.AddRange(_trileSet.Triles.Values.Select(trile => new Entry(trile.Name, _trileSetPath, AssetType.Trile)));
        }

        foreach (var file in _resources.Files)
        {
            if (file.StartsWith("Art Objects/", StringComparison.OrdinalIgnoreCase))
            {
                artObjects.Add(new Entry(file["Art Objects/".Length..], file, AssetType.ArtObject));
            }
            else if (file.StartsWith("Background Planes/", StringComparison.OrdinalIgnoreCase))
            {
                planes.Add(new Entry(file["Background Planes/".Length..], file, AssetType.BackgroundPlane));
            }
            else if (file.StartsWith("Character Animations/", StringComparison.OrdinalIgnoreCase) &&
                     !file.Contains("Metadata", StringComparison.OrdinalIgnoreCase))
            {
                var remainder = file["Character Animations/".Length..];
                var slashIndex = remainder.IndexOf('/');
                if (slashIndex >= 0)
                {
                    var folder = remainder[..slashIndex];
                    if (npcFolders.Add(folder))
                    {
                        npcs.Add(new Entry(folder, $"Character Animations/{folder}", AssetType.NonPlayableCharacter));
                    }
                }
            }
        }

        artObjects.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        triles.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        planes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        npcs.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        _entries[AssetType.ArtObject] = artObjects;
        _entries[AssetType.BackgroundPlane] = planes;
        _entries[AssetType.NonPlayableCharacter] = npcs;
        _entries[AssetType.Trile] = triles;

        SelectedEntry = triles.FirstOrDefault();

        // Pre-fill thumbnails with placeholder and enqueue for generation
        foreach (var entry in _entries.Values.SelectMany(e => e))
        {
            if (SharedThumbnails.TryAdd(entry.CachePath, _placeholder) ||
                SharedThumbnails[entry.CachePath] == _placeholder)
            {
                _pendingQueue.Enqueue(entry);
            }
        }

        Logger.Debug("Built {0} art objects, {1} triles, {2} planes, {3} NPCs",
            artObjects.Count, triles.Count, planes.Count, npcs.Count);
    }

    private unsafe void DrawGrid(IReadOnlyList<Entry> entries)
    {
        var availWidth = ImGui.GetContentRegionAvail().X;
        var columns = Math.Max((int)(availWidth / CellSize), 1);
        var totalRows = (entries.Count + columns - 1) / columns;

        if (!ImGui.BeginTable("##grid", columns, ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchSame))
        {
            return;
        }

        // Scroll to the selected entry when the table first appears
        var selectedIndex = -1;
        for (var k = 0; k < entries.Count; k++)
        {
            if (entries[k] == SelectedEntry)
            {
                selectedIndex = k;
                break;
            }
        }

        if (selectedIndex >= 0 && ImGui.IsWindowAppearing())
        {
            var selectedRow = selectedIndex / columns;
            ImGui.SetScrollY(selectedRow * RowHeight);
        }

        var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
        clipper.Begin(totalRows, RowHeight);

        while (clipper.Step())
        {
            for (var row = clipper.DisplayStart; row < clipper.DisplayEnd; row++)
            {
                ImGui.TableNextRow(ImGuiTableRowFlags.None, RowHeight);

                for (var col = 0; col < columns; col++)
                {
                    var i = row * columns + col;
                    if (i >= entries.Count)
                    {
                        break;
                    }

                    ImGui.TableSetColumnIndex(col);

                    var entry = entries[i];
                    var isSelected = SelectedEntry == entry;
                    var texture = SharedThumbnails.GetValueOrDefault(entry.CachePath, _placeholder);
                    var cellWidth = ImGui.GetColumnWidth();

                    ImGui.PushID(i);

                    // Compute thumbnail size preserving aspect ratio
                    var aspect = (float)texture.Width / texture.Height;
                    float thumbW, thumbH;
                    if (aspect >= 1f)
                    {
                        thumbW = ThumbSize;
                        thumbH = ThumbSize / aspect;
                    }
                    else
                    {
                        thumbH = ThumbSize;
                        thumbW = ThumbSize * aspect;
                    }

                    // Center thumbnail within the cell
                    var padX = (cellWidth - thumbW) * 0.5f;
                    var padY = (ThumbSize - thumbH) * 0.5f;
                    var cursor = ImGui.GetCursorPos();
                    var cellScreenPos = ImGui.GetCursorScreenPos();
                    ImGui.SetCursorPos(new NVector2(cursor.X + padX, cursor.Y + padY));
                    ImGuiX.Image(texture, new Vector2(thumbW, thumbH));

                    // Highlight selected asset on top of thumbnail
                    if (isSelected)
                    {
                        var dl = ImGui.GetWindowDrawList();
                        var highlightMax = new NVector2(cellScreenPos.X + ThumbSize, cellScreenPos.Y + ThumbSize);
                        var color = Color.LightGray with { A = 128 }; // 50%
                        dl.AddRectFilled(cellScreenPos, highlightMax, color.PackedValue);
                    }

                    // Restore cursor for the invisible click target over the whole cell
                    ImGui.SetCursorPos(cursor);
                    if (ImGui.InvisibleButton("##sel", new NVector2(cellWidth, ThumbSize)))
                    {
                        Select(entry);
                    }

                    if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    {
                        Select(entry);
                    }

                    // Label wrapped and centered below thumbnail
                    ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + cellWidth);
                    var textSize = ImGui.CalcTextSize(entry.Name, true);
                    var labelPad = (cellWidth - textSize.X) * 0.5f;
                    if (labelPad > 0)
                    {
                        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + labelPad);
                    }

                    ImGui.TextUnformatted(entry.Name);
                    ImGui.PopTextWrapPos();

                    ImGui.PopID();
                }
            }
        }

        clipper.End();
        clipper.Destroy();
        ImGui.EndTable();
    }

    private void OnProviderChanged()
    {
        _entries.Clear();
        _pendingQueue.Clear();
        _recentEntries.Clear();
        SelectedEntry = default;
    }

    private static void ClearSharedThumbnails(Texture2D? placeholder = null)
    {
        foreach (var tex in SharedThumbnails.Values)
        {
            if (tex != placeholder)
            {
                tex.Dispose();
            }
        }

        SharedThumbnails.Clear();
    }

    public readonly record struct Entry(string Name, string Path, AssetType Type)
    {
        public string CachePath => Type == AssetType.Trile ? $"{Path}/{Name}" : Path;
    }
}