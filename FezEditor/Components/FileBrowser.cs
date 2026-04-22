using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Level;
using FEZRepacker.Core.Definitions.Game.TrileSet;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components;

public class FileBrowser : DrawableGameComponent
{
    private FileNode? _root;

    private FileNode? _selected;

    private FileNode? _hoveredDir;

    private string _path = "";

    private Dirty<string> _filter = new("");

    private readonly HashSet<FileNode> _openDirs = new();

    private readonly Stack<(FileNode node, bool shouldPop)> _tree = new();

    private readonly List<FileNode> _selectionHistory = new();

    private int _historyIndex = -1;

    private SortMode _sortMode = SortMode.NameAscending;

    private readonly EditorService _editorService;

    private readonly ResourceService _resourceService;

    private readonly InputService _inputService;

    private readonly EditWindow _editWindow;

    private readonly ConfirmWindow _confirmWindow;

    private ThumbnailGenerator? _thumbnailGenerator;

    private bool _thumbnailsGenerated;

    private enum SortMode
    {
        NameAscending,
        NameDescending,
        TypeAscending,
        TypeDescending
    }

    public FileBrowser(Game game) : base(game)
    {
        _editorService = game.GetService<EditorService>();
        _resourceService = game.GetService<ResourceService>();
        _inputService = game.GetService<InputService>();
        _resourceService.ProviderChanged += UpdateNodeTree;
        game.AddComponent(_editWindow = new EditWindow(game));
        game.AddComponent(_confirmWindow = new ConfirmWindow(game));
    }

    protected override void Dispose(bool disposing)
    {
        Game.RemoveComponent(_confirmWindow);
        Game.RemoveComponent(_editWindow);
        base.Dispose(disposing);
    }

    public override void Update(GameTime gameTime)
    {
        if (_selected == null || _resourceService.IsReadonly)
        {
            return;
        }

        if (_inputService.IsActionJustPressed(InputActions.FileBrowserRename))
        {
            ShowRenameDialog(_selected.Path);
        }
        else if (_inputService.IsActionJustPressed(InputActions.FileBrowserDelete))
        {
            ShowDeleteDialog(_selected.Path);
        }
        else if (_inputService.IsActionJustPressed(InputActions.FileBrowserMove))
        {
            ShowMoveDialog(_selected.Path);
        }
        else if (_inputService.IsActionJustPressed(InputActions.FileBrowserCopyRelativePath))
        {
            ImGui.SetClipboardText(_selected.Path);
        }
        else if (_inputService.IsActionJustPressed(InputActions.FileBrowserCopyAbsolutePath))
        {
            ImGui.SetClipboardText(_resourceService.GetFullPath(_selected.Path));
        }
        else if (_inputService.IsActionJustPressed(InputActions.FileBrowserOpenInFileManager))
        {
            _resourceService.OpenInFileManager(_selected.Path);
        }
    }

    public void Draw()
    {
        if (_resourceService.HasNoProvider)
        {
            const string text = $"{Icons.Info} No resources";
            ImGuiX.SetTextCentered(text);
            ImGui.TextDisabled(text);
        }
        else
        {
            DrawToolbar();
            ImGui.Separator();
            DrawFileTree();
        }
    }

    private void DrawToolbar()
    {
        ImGuiX.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8, 4));
        {
            ImGui.BeginDisabled(_historyIndex <= 0);
            if (ImGui.ArrowButton("GoBack", ImGuiDir.Left))
            {
                _historyIndex--;
                _selected = _selectionHistory[_historyIndex];
                _path = _selected.Path;
            }

            ImGui.EndDisabled();

            ImGui.SameLine();
            ImGui.BeginDisabled(_historyIndex >= _selectionHistory.Count - 1);
            if (ImGui.ArrowButton("GoForward", ImGuiDir.Right))
            {
                _historyIndex++;
                _selected = _selectionHistory[_historyIndex];
                _path = _selected.Path;
            }

            ImGui.EndDisabled();

            ImGui.SameLine();
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputTextWithHint("##PathInput", "Selected Path", ref _path, 512, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                // Try to find and select the node at this path
                var node = FindNodeByPath(_path);
                if (node != null)
                {
                    _selected = node;

                    // Add to selection history
                    if (_historyIndex < _selectionHistory.Count - 1)
                    {
                        _selectionHistory.RemoveRange(_historyIndex + 1,
                            _selectionHistory.Count - _historyIndex - 1);
                    }

                    if (_selectionHistory.Count == 0 || _selectionHistory[^1] != node)
                    {
                        _selectionHistory.Add(node);
                        _historyIndex = _selectionHistory.Count - 1;
                    }
                }
            }
        }
        ImGui.PopStyleVar();

        ImGuiX.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8, 4));
        {
            ImGui.SetNextItemWidth(-40);
            var filter = _filter.Value;
            if (ImGui.InputTextWithHint("", "Filter Files", ref filter, 255))
            {
                _filter = filter;
            }

            if (!string.IsNullOrEmpty(_filter))
            {
                ImGui.SameLine();
                if (ImGui.Button(Icons.ClearAll))
                {
                    _filter = "";
                }
            }

            ImGui.SameLine();
            if (ImGui.Button(Icons.ListFilter))
            {
                ImGui.OpenPopup("SortOptions");
            }

            if (ImGui.BeginPopup("SortOptions"))
            {
                ImGui.SeparatorText("Sort by");

                if (ImGui.MenuItem("Name (A-Z)", null, _sortMode == SortMode.NameAscending))
                {
                    _sortMode = SortMode.NameAscending;
                    SortAllNodes();
                }

                if (ImGui.MenuItem("Name (Z-A)", null, _sortMode == SortMode.NameDescending))
                {
                    _sortMode = SortMode.NameDescending;
                    SortAllNodes();
                }

                ImGui.Separator();

                if (ImGui.MenuItem("Type (A-Z)", null, _sortMode == SortMode.TypeAscending))
                {
                    _sortMode = SortMode.TypeAscending;
                    SortAllNodes();
                }

                if (ImGui.MenuItem("Type (Z-A)", null, _sortMode == SortMode.TypeDescending))
                {
                    _sortMode = SortMode.TypeDescending;
                    SortAllNodes();
                }

                ImGui.EndPopup();
            }
        }
        ImGui.PopStyleVar();
    }

    private void DrawFileTree()
    {
        if (ImGui.BeginChild("FileTree") && _root != null)
        {
            // Check if empty space was clicked to deselect
            if (ImGui.IsWindowHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !ImGui.IsAnyItemHovered())
            {
                _selected = null;
                _path = "";
            }

            if (ImGui.BeginPopupContextWindow("##EmptySpaceContext",
                    ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.NoOpenOverItems))
            {
                DrawContextMenu(_hoveredDir ?? _root, flatten: true);
                ImGui.EndPopup();
            }

            var filtering = !string.IsNullOrEmpty(_filter);
            if (filtering)
            {
                UpdateFilterMatches(_root);
            }

            _hoveredDir = null;
            _tree.Clear();
            for (var i = _root.Children.Count - 1; i >= 0; i--)
            {
                _tree.Push((_root.Children[i], false));
            }

            ImGui.PushStyleVar(ImGuiStyleVar.IndentSpacing, 8);
            ImGuiX.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 6));
            while (_tree.Count > 0)
            {
                var (node, shouldPop) = _tree.Pop();

                // Handle TreePop for previously opened nodes
                if (shouldPop)
                {
                    ImGui.TreePop();
                    continue;
                }

                // Skip nodes that don't match the filter
                if (filtering && !node.MatchesFilter)
                {
                    continue;
                }

                var nodeFlags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth;

                if (!node.IsDirectory)
                {
                    nodeFlags |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;
                }

                if (_selected == node)
                {
                    nodeFlags |= ImGuiTreeNodeFlags.Selected;
                }

                if (node.IsDirectory && _filter.IsDirty)
                {
                    ImGui.SetNextItemOpen(filtering ? node.MatchesFilter : _openDirs.Contains(node), ImGuiCond.Always);
                }

                // Choose icon based on node type
                var icon = node.IsDirectory
                    ? node.IsOpen ? Icons.FolderOpened : Icons.Folder
                    : GetFileIcon(node.Extension);

                var label = $"{icon} {node.Name}";
                var nodeOpen = ImGui.TreeNodeEx($"{node.Path}##{node.Path}", nodeFlags, label);

                // Update open state for next frame
                if (node.IsDirectory)
                {
                    node.IsOpen = nodeOpen;
                    if (!filtering)
                    {
                        if (nodeOpen)
                        {
                            _openDirs.Add(node);
                        }
                        else
                        {
                            _openDirs.Remove(node);
                        }
                    }

                    if (nodeOpen && ImGui.IsItemHovered())
                    {
                        _hoveredDir = node;
                    }
                }

                if (ImGui.IsItemClicked())
                {
                    _selected = node;
                    _path = node.Path;

                    // Add to selection history
                    // Remove any forward history if we're not at the end
                    if (_historyIndex < _selectionHistory.Count - 1)
                    {
                        _selectionHistory.RemoveRange(_historyIndex + 1,
                            _selectionHistory.Count - _historyIndex - 1);
                    }

                    // Only add if it's different from the last selection
                    if (_selectionHistory.Count == 0 || _selectionHistory[^1] != node)
                    {
                        _selectionHistory.Add(node);
                        _historyIndex = _selectionHistory.Count - 1;
                    }
                }

                if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                {
                    if (!node.IsDirectory && ImGui.IsItemHovered())
                    {
                        _editorService.OpenEditorFor(node.Path);
                        _selected = null;
                        _path = "";
                    }
                }

                if (ImGui.BeginPopupContextItem())
                {
                    DrawContextMenu(node, flatten: false);
                    ImGui.EndPopup();
                }

                if (node.IsDirectory && nodeOpen)
                {
                    _tree.Push((node, true));
                    for (var i = node.Children.Count - 1; i >= 0; i--)
                    {
                        _tree.Push((node.Children[i], false));
                    }
                }
            }

            ImGui.PopStyleVar(2);
            _filter = _filter.Clean();

            ImGui.EndChild();
        }
    }

    private void DrawContextMenu(FileNode node, bool flatten)
    {
        if (_resourceService.IsReadonly)
        {
            return;
        }

        if (flatten)
        {
            DrawNewAssetMenuItems("New ", node.Path);
        }
        else
        {
            if (ImGui.BeginMenu($"{Icons.FileAdd} Create New Asset..."))
            {
                DrawNewAssetMenuItems(string.Empty, node.Path);
                ImGui.EndMenu();
            }

            var shortcut = _inputService.GetActionBinding(InputActions.FileBrowserCopyRelativePath);
            if (ImGui.MenuItem($"{Icons.Copy} Copy Relative Path", shortcut))
            {
                ImGui.SetClipboardText(node.Path);
            }

            shortcut = _inputService.GetActionBinding(InputActions.FileBrowserCopyAbsolutePath);
            if (ImGui.MenuItem("\tCopy Absolute Path", shortcut))
            {
                ImGui.SetClipboardText(_resourceService.GetFullPath(node.Path));
            }

            ImGui.Separator();
            shortcut = _inputService.GetActionBinding(InputActions.FileBrowserRename);
            if (ImGui.MenuItem($"{Icons.Rename} Rename", shortcut))
            {
                ShowRenameDialog(node.Path);
            }

            if (ImGui.MenuItem($"{Icons.Copy} Duplicate"))
            {
                _resourceService.Duplicate(node.Path);
            }

            shortcut = _inputService.GetActionBinding(InputActions.FileBrowserMove);
            if (ImGui.MenuItem($"{Icons.Move} Move", shortcut))
            {
                ShowMoveDialog(node.Path);
            }

            shortcut = _inputService.GetActionBinding(InputActions.FileBrowserDelete);
            if (ImGui.MenuItem($"{Icons.Remove} Delete", shortcut))
            {
                ShowDeleteDialog(node.Path);
            }
        }

        ImGui.Separator();

        var openShortcut = _inputService.GetActionBinding(InputActions.FileBrowserOpenInFileManager);
        if (ImGui.MenuItem($"{Icons.FolderOpened} Open in File Manager", openShortcut))
        {
            _resourceService.OpenInFileManager(node.Path);
        }
    }

    private void DrawNewAssetMenuItems(string prefix, string basePath)
    {
        foreach (var (name, type) in EditorService.GetAssetTypes())
        {
            if (ImGui.MenuItem(prefix + name))
            {
                ShowCreateDialog(basePath, type);
            }
        }
    }

    private void ShowCreateDialog(string basePath, Type assetType)
    {
        const string defaultName = "UNTITLED";

        var absoluteDir = _resourceService.GetFullPath(basePath);
        var extension = EditorService.GetExtensionForType(assetType);
        var options = new FileDialog.Options
        {
            DefaultLocation = Path.Combine(absoluteDir, defaultName),
            Title = "Create New " + assetType.Name,
            Filters = [new FileDialog.Filter(assetType.Name, extension)]
        };

        if (assetType == typeof(Level))
        {
            var options2 = new FileDialog.Options
            {
                Title = "Select Trile Set...",
                Filters = [new FileDialog.Filter("Trile Set", "fezts.glb")]
            };

            FileDialog.Show(FileDialog.Type.OpenFile, files =>
            {
                FileDialog.Show(FileDialog.Type.SaveFile, files2 =>
                {
                    var path = _resourceService.GetRelativePath(files[0].Replace(".fezts.glb", ""));
                    var trileSet = (TrileSet)_resourceService.Load(path);

                    var path2 = _resourceService.GetRelativePath(files2[0]);
                    var asset = EddyEditor.Create(defaultName, trileSet);

                    _resourceService.Save(path2, asset);
                }, options);
            }, options2);

            return;
        }

        FileDialog.Show(FileDialog.Type.SaveFile, files =>
        {
            var relativePath = _resourceService.GetRelativePath(files[0]);
            var asset = EditorService.CreateAssetOfType(assetType, defaultName);
            _resourceService.Save(relativePath, asset);
        }, options);
    }

    private void ShowRenameDialog(string path)
    {
        var newName = Path.GetFileName(path);
        _editWindow.Title = "Rename";
        _editWindow.Text = "Enter a new name:";
        _editWindow.EditValue = () =>
        {
            ImGui.InputText("##RenameInput", ref newName, 256);
            return !string.IsNullOrWhiteSpace(newName) && newName != Path.GetFileName(path);
        };
        _editWindow.Accepted = () =>
        {
            var dir = path.Contains('/') ? path[..path.LastIndexOf('/')] : string.Empty;
            var newPath = string.IsNullOrEmpty(dir) ? newName : $"{dir}/{newName}";
            _resourceService.Move(path, newPath);
        };
    }

    private void ShowMoveDialog(string path)
    {
        var options = new FileDialog.Options
        {
            DefaultLocation = _resourceService.GetFullPath(string.Empty),
            Title = "Move to folder"
        };

        FileDialog.Show(FileDialog.Type.OpenFolder, files =>
        {
            var targetDir = _resourceService.GetRelativePath(files[0]);
            var fileName = path.Contains('/') ? path[(path.LastIndexOf('/') + 1)..] : path;
            var newPath = string.IsNullOrEmpty(targetDir) ? fileName : $"{targetDir}/{fileName}";
            _resourceService.Move(path, newPath);
        }, options);
    }

    private void ShowDeleteDialog(string relativePath)
    {
        _confirmWindow.Text = $"Delete \"{Path.GetFileName(relativePath)}\"?";
        _confirmWindow.Confirmed = () => _resourceService.Delete(relativePath);
    }

    private bool UpdateFilterMatches(FileNode node)
    {
        if (!node.IsDirectory)
        {
            node.MatchesFilter = FuzzyMatch(node.Name, _filter);
            return node.MatchesFilter;
        }

        var anyChildMatches = false;
        foreach (var child in node.Children)
        {
            if (UpdateFilterMatches(child))
            {
                anyChildMatches = true;
            }
        }

        node.MatchesFilter = anyChildMatches;
        return anyChildMatches;
    }

    private void UpdateNodeTree()
    {
        if (_resourceService.HasNoProvider)
        {
            _thumbnailsGenerated = false;
        }
        else if (_thumbnailGenerator == null && !_thumbnailsGenerated)
        {
            _thumbnailGenerator = new ThumbnailGenerator(Game);
            _thumbnailGenerator.Disposed += (_, _) =>
            {
                _thumbnailGenerator = null;
                _thumbnailsGenerated = true;
                _resourceService.NotifyThumbnailsReady();
            };
            Game.AddComponent(_thumbnailGenerator);
        }

        BuildNodeTree();
        SortAllNodes();
    }

    private void BuildNodeTree()
    {
        _openDirs.Clear();
        if (_resourceService.HasNoProvider)
        {
            _root = null;
            _filter = "";
            return;
        }

        _root = new FileNode
        {
            Name = string.Empty,
            Path = string.Empty,
            IsDirectory = true,
            Depth = 0,
            IsOpen = true
        };
        _openDirs.Add(_root);

        var lookup = new Dictionary<string, FileNode>
        {
            [""] = _root
        };

        foreach (var path in _resourceService.Files)
        {
            var segments = path.Split('/');
            var currentPath = "";

            // Build directory nodes first
            for (var i = 0; i < segments.Length - 1; i++)
            {
                var parentPath = currentPath;
                currentPath = string.IsNullOrEmpty(parentPath)
                    ? segments[i]
                    : $"{parentPath}/{segments[i]}";

                if (!lookup.ContainsKey(currentPath))
                {
                    var parentNode = lookup[parentPath];
                    var dirNode = new FileNode
                    {
                        Name = segments[i],
                        Path = currentPath,
                        IsDirectory = true,
                        Depth = parentNode.Depth + 1
                    };

                    parentNode.Children.Add(dirNode);
                    lookup[currentPath] = dirNode;
                }
            }

            // Add the file node
            var fileName = segments[^1];
            var fileParentPath = string.Join('/', segments.Take(segments.Length - 1));
            var parentNodeForFile = lookup[fileParentPath];

            var fileNode = new FileNode
            {
                Name = fileName,
                Path = path,
                IsDirectory = false,
                Depth = parentNodeForFile.Depth + 1,
                Extension = _resourceService.GetExtension(path)
            };

            parentNodeForFile.Children.Add(fileNode);
        }
    }

    private FileNode? FindNodeByPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        var stack = new Stack<FileNode>();
        stack.Push(_root!);

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node.Path.Equals(path, StringComparison.OrdinalIgnoreCase))
            {
                return node;
            }

            foreach (var child in node.Children)
            {
                stack.Push(child);
            }
        }

        return null;
    }

    private static bool FuzzyMatch(string text, string pattern)
    {
        // Simple fuzzy matching: all pattern characters must appear in order in the text
        var textIndex = 0;
        var patternIndex = 0;

        while (textIndex < text.Length && patternIndex < pattern.Length)
        {
            if (char.ToLowerInvariant(text[textIndex]) == char.ToLowerInvariant(pattern[patternIndex]))
            {
                patternIndex++;
            }

            textIndex++;
        }

        return patternIndex == pattern.Length;
    }

    private void SortAllNodes()
    {
        if (_root == null)
        {
            return;
        }

        var stack = new Stack<FileNode>();
        stack.Push(_root);

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (!node.IsDirectory || node.Children.Count == 0)
            {
                continue;
            }

            node.Children = _sortMode switch
            {
                SortMode.NameAscending => node.Children
                    .OrderByDescending(n => n.IsDirectory)
                    .ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList(),

                SortMode.NameDescending => node.Children
                    .OrderByDescending(n => n.IsDirectory)
                    .ThenByDescending(n => n.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList(),

                SortMode.TypeAscending => node.Children
                    .OrderByDescending(n => n.IsDirectory)
                    .ThenBy(n => n.Extension, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList(),

                SortMode.TypeDescending => node.Children
                    .OrderByDescending(n => n.IsDirectory)
                    .ThenByDescending(n => n.Extension, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList(),

                _ => node.Children
            };

            for (var i = node.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(node.Children[i]);
            }
        }
    }

    private static string GetFileIcon(string extension)
    {
        if (string.IsNullOrEmpty(extension))
        {
            return Icons.File;
        }

        var parts = extension.TrimStart('.').Split('.');
        var lastExt = parts.Length > 0 ? parts[^1].ToLowerInvariant() : "";
        return lastExt switch
        {
            "json" => Icons.Json,
            "png" or "jpg" or "jpeg" or "gif" or "bmp" or "glb" => Icons.FileMedia,
            _ => Icons.File
        };
    }

    private class FileNode
    {
        public string Name { get; init; } = "";
        public string Path { get; init; } = "";
        public bool IsDirectory { get; init; }
        public List<FileNode> Children { get; set; } = new();
        public int Depth { get; init; } // Track depth for indentation
        public string Extension { get; init; } = "";
        public bool MatchesFilter { get; set; }
        public bool IsOpen { get; set; }
    }
}