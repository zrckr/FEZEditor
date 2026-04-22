using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.TrileSet;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Components.Eddy;

public class AssetBrowser : IDisposable
{
    private static readonly Dictionary<string, Texture2D> SharedThumbnails = new();

    private static int s_instanceCount;

    private const float ThumbSize = 64f;

    private const float RecentThumbSize = 48f;

    private const float CellSpacing = 8f;

    private const float CellSize = ThumbSize + CellSpacing;

    private const float LabelHeight = 20f;

    private const float RowHeight = CellSize + LabelHeight;

    private const int MaxRecentEntries = 10;

    private Entry _selectedEntry;

    private AssetType? _selectionDirtyType;

    private readonly List<Entry> _recentEntries = new();

    private readonly ResourceService _resources;

    private readonly Dictionary<AssetType, IReadOnlyList<Entry>> _entries = new();

    private string? _trileSetPath;

    private TrileSet? _trileSet;

    private Texture2D _placeholder = null!;

    private string _filterEntries = string.Empty;

    public AssetBrowser(Game game)
    {
        _resources = game.GetService<ResourceService>();
        _resources.ProviderChanged += OnProviderChanged;
        _resources.ProviderReset += OnProviderReset;
        _resources.ThumbnailsReady += BuildEntries;
        s_instanceCount++;
    }

    public void LoadContent(IContentManager content)
    {
        _placeholder = content.Load<Texture2D>("Missing");
    }

    private void BuildEntries()
    {
        if (_resources.HasNoProvider)
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
                var extension = _resources.GetExtension(file);
                if (!extension.EndsWith(".png"))
                {
                    artObjects.Add(new Entry(file["Art Objects/".Length..], file, AssetType.ArtObject));
                }
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

        if (triles.Count > 0 && _recentEntries.Count == 0)
        {
            Select(triles[0]);
        }

        _selectionDirtyType = null;

        foreach (var entry in _entries.Values.SelectMany(e => e))
        {
            if (SharedThumbnails.TryGetValue(entry.CachePath, out var value) && value != _placeholder)
            {
                continue;
            }

            var lastWrite = _resources.GetLastWriteTimeUtc(entry.Path);
            var cacheProbe = new Thumbnailer(entry.CachePath, lastWrite);
            if (cacheProbe.TryLoad(out var cached) && cached != null)
            {
                SharedThumbnails[entry.CachePath] = RepackerExtensions.ConvertToTexture2D(cached);
            }
            else
            {
                SharedThumbnails[entry.CachePath] = _placeholder;
            }
        }
    }

    public void SetTrileSet(string path, TrileSet set)
    {
        _trileSetPath = path;
        _trileSet = set;
        _entries.Clear();
        BuildEntries();
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

    public string GetSelectedEntry(AssetType type)
    {
        return _selectedEntry.Type == type ? _selectedEntry.Name : string.Empty;
    }

    public string? GetTrileNameById(int trileId)
    {
        return _trileSet?.Triles.GetValueOrDefault(trileId)?.Name;
    }

    public Texture2D? GetThumbnail(AssetType type, string name)
    {
        var entries = _entries.GetValueOrDefault(type) ?? [];
        var entry = entries.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
        if (entry == default)
        {
            return null;
        }

        var thumb = SharedThumbnails.GetValueOrDefault(entry.CachePath);
        return thumb == _placeholder ? null : thumb;
    }

    public bool Select(AssetType type)
    {
        if (_selectionDirtyType == type)
        {
            _selectionDirtyType = null;
            return true;
        }

        return false;
    }

    private void Select(Entry entry)
    {
        _selectedEntry = entry;
        _selectionDirtyType = entry.Type;
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
        DrawSelectionBar();
        ImGui.Separator();

        DrawFilter();
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
    }

    private void DrawSelectionBar()
    {
        // Show the most recently selected entry across all types
        var selected = _recentEntries.Count > 0 ? _recentEntries[0] : default;
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

    private void DrawFilter()
    {
        ImGui.InputTextWithHint("", "Filter assets...", ref _filterEntries, 255);
        if (!string.IsNullOrEmpty(_filterEntries))
        {
            ImGui.SameLine();
            if (ImGui.Button($"{Icons.Close}"))
            {
                _filterEntries = string.Empty;
            }
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _resources.ProviderChanged -= OnProviderChanged;
        _resources.ProviderReset -= OnProviderReset;
        _resources.ThumbnailsReady -= BuildEntries;

        s_instanceCount--;
        if (s_instanceCount <= 0)
        {
            ClearSharedThumbnails(_placeholder);
        }
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
            if (entries[k] == _selectedEntry)
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
                    var isSelected = _selectedEntry == entry;
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
        BuildEntries();
    }

    private void OnProviderReset()
    {
        _recentEntries.Clear();
        _selectedEntry = default;
        _selectionDirtyType = null;
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

    private readonly record struct Entry(string Name, string Path, AssetType Type)
    {
        public string CachePath => Type == AssetType.Trile ? $"{Path}/{Name}" : Path;
    }
}