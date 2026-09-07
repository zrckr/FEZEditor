using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
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

    private bool _thumbnailScanPending;

    private bool _disposing;

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
        _disposing = true;
        _resourceService.ProviderChanged -= UpdateNodeTree;
        _thumbnailGenerator?.Dispose();
        Game.RemoveComponent(_confirmWindow);
        Game.RemoveComponent(_editWindow);
        base.Dispose(disposing);
    }

    public override void Update(GameTime gameTime)
    {
        if (_selected == null || _resourceService.IsReadonly || _selected.IsReference)
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
            const string text = $"{Lucide.Info} No resources";
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
            if (ImGui.InputTextWithHint("##PathInput", "Selected Path", ref _path, 512,
                    ImGuiInputTextFlags.EnterReturnsTrue))
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
            if (ImGui.InputTextWithHint("##FileFilter", "Filter Files", ref filter, 255))
            {
                _filter = filter;
            }

            if (!string.IsNullOrEmpty(_filter))
            {
                ImGui.SameLine();
                if (ImGui.Button(Lucide.ListX))
                {
                    _filter = "";
                }
            }

            ImGui.SameLine();
            if (ImGui.Button(Lucide.Funnel))
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
                var contextTarget = _hoveredDir ?? _root;
                if (!contextTarget.IsReference)
                {
                    DrawContextMenu(contextTarget, flatten: true);
                }

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
                string? icon;
                var disabled = false;
                if (node.IsDirectory)
                {
                    if (node is { IsReference: true, Path: "References" })
                    {
                        icon = Lucide.FolderSymlink;
                    }
                    else
                    {
                        icon = node.IsOpen ? Lucide.FolderOpen : Lucide.Folder;
                    }
                }
                else
                {
                    icon = EditorService.GetFileIcon(node.Extension);
                    disabled = icon == Lucide.FileQuestionMark;
                }

                if (disabled)
                {
                    unsafe
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, *ImGui.GetStyleColorVec4(ImGuiCol.TextDisabled));
                    }
                }

                var label = $"{icon} {node.Name}";
                var nodeKind = node.IsDirectory ? "Directory" : "File";
                var nodeOpen = ImGui.TreeNodeEx($"##{nodeKind}:{node.Path}", nodeFlags, label);

                if (disabled)
                {
                    ImGui.PopStyleColor();
                }

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
                    if (node is { IsReference: true, IsDirectory: false })
                    {
                        DrawReferenceContextMenu(node);
                    }
                    else if (!node.IsReference)
                    {
                        DrawContextMenu(node, flatten: false);
                    }

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

        }

        ImGui.EndChild();
    }

    private void DrawContextMenu(FileNode node, bool flatten)
    {
        if (_resourceService.IsReadonly)
        {
            return;
        }

        var directoryPath = node.IsDirectory
            ? node.Path
            : node.Path.Contains('/')
                ? node.Path[..node.Path.LastIndexOf('/')]
                : string.Empty;

        if (ImGui.MenuItem($"{Lucide.FolderPlus} Create New Directory..."))
        {
            ShowCreateDirectoryDialog(directoryPath);
        }

        if (ImGui.BeginMenu($"{Lucide.FilePlusCorner} Create New Asset..."))
        {
            foreach (var (name, type) in EditorService.AssetTypes)
            {
                var extension = "." + EditorService.GetExtensionForType(type);
                if (ImGui.MenuItem($"{EditorService.GetFileIcon(extension)} {name}"))
                {
                    ShowCreateDialog(directoryPath, type);
                }
            }

            ImGui.EndMenu();
        }

        if (!flatten)
        {
            var shortcut = _inputService.GetActionBinding(InputActions.FileBrowserCopyRelativePath);
            if (ImGui.MenuItem($"{Lucide.Copy} Copy Relative Path", shortcut))
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
            if (ImGui.MenuItem($"{Lucide.TextCursorInput} Rename", shortcut))
            {
                ShowRenameDialog(node.Path);
            }

            if (!node.IsDirectory && ImGui.MenuItem($"{Lucide.Copy} Duplicate"))
            {
                _resourceService.Duplicate(node.Path);
            }

            shortcut = _inputService.GetActionBinding(InputActions.FileBrowserMove);
            if (ImGui.MenuItem($"{Lucide.Move} Move", shortcut))
            {
                ShowMoveDialog(node.Path);
            }

            shortcut = _inputService.GetActionBinding(InputActions.FileBrowserDelete);
            if (ImGui.MenuItem($"{Lucide.Minus} Delete", shortcut))
            {
                ShowDeleteDialog(node.Path);
            }
        }

        ImGui.Separator();

        var openShortcut = _inputService.GetActionBinding(InputActions.FileBrowserOpenInFileManager);
        if (ImGui.MenuItem($"{Lucide.FolderOpen} Open in File Manager", openShortcut))
        {
            _resourceService.OpenInFileManager(node.Path);
        }
    }

    private void ShowCreateDirectoryDialog(string basePath)
    {
        var directoryName = "";
        _editWindow.Title = "Create Directory";
        _editWindow.Text = "Enter a directory name:";

        _editWindow.EditValue = () =>
        {
            ImGui.InputText("##CreateDirectoryInput", ref directoryName, 256);
            var validName = !string.IsNullOrWhiteSpace(directoryName) &&
                            directoryName is not "." and not ".." &&
                            Path.GetFileName(directoryName) == directoryName &&
                            directoryName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

            var path = string.IsNullOrEmpty(basePath) ? directoryName : $"{basePath}/{directoryName}";
            return validName && !Directory.Exists(_resourceService.GetFullPath(path));
        };

        _editWindow.Accepted = () =>
        {
            var path = string.IsNullOrEmpty(basePath) ? directoryName : $"{basePath}/{directoryName}";
            _resourceService.CreateDirectory(path);
        };
    }

    private void DrawReferenceContextMenu(FileNode node)
    {
        if (ImGui.MenuItem($"{Lucide.Copy} Copy to mod"))
        {
            _resourceService.CopyFromReference(node.Path);
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

        FileDialog.Show(FileDialog.Type.SaveFile, files =>
        {
            var relativePath = _resourceService.GetRelativePath(files[0]);
            _editorService.CreateAndSaveAsset(assetType, relativePath, defaultName);
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
            DefaultLocation = _resourceService.RootPath,
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

        node.MatchesFilter = anyChildMatches || FuzzyMatch(node.Name, _filter);
        return anyChildMatches;
    }

    public void RegenerateThumbnails()
    {
        AppStorageService.ClearThumbs();
        UpdateNodeTree();
    }

    private void UpdateNodeTree()
    {
        if (_resourceService.HasNoProvider)
        {
            _thumbnailScanPending = false;
            _thumbnailGenerator?.Cancel();
        }
        else
        {
            QueueThumbnailScan();
        }

        BuildNodeTree();
        SortAllNodes();
    }

    private void QueueThumbnailScan()
    {
        if (_thumbnailGenerator != null)
        {
            _thumbnailScanPending = true;
            _thumbnailGenerator.Cancel();
            return;
        }

        _thumbnailGenerator = new ThumbnailGenerator(Game);
        _thumbnailGenerator.Disposed += (_, _) =>
        {
            _thumbnailGenerator = null;
            if (_disposing)
            {
                return;
            }

            _resourceService.NotifyThumbnailsReady();
            if (_thumbnailScanPending && !_resourceService.HasNoProvider)
            {
                _thumbnailScanPending = false;
                QueueThumbnailScan();
            }
        };

        Game.AddComponent(_thumbnailGenerator);
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

        foreach (var entry in _resourceService.Entries)
        {
            if (entry is ResourceEntry.Directory)
            {
                AddDirectory(entry.Path);
                continue;
            }

            if (entry is ResourceEntry.File file)
            {
                var segments = file.Path.Split('/');
                var fileParentPath = string.Join('/', segments.Take(segments.Length - 1));
                AddDirectory(fileParentPath);

                var parentNode = lookup[fileParentPath];
                parentNode.Children.Add(new FileNode
                {
                    Name = segments[^1],
                    Path = file.Path,
                    IsDirectory = false,
                    IsReference = file.Path.StartsWith("References/", StringComparison.OrdinalIgnoreCase),
                    Depth = parentNode.Depth + 1,
                    Extension = file.Extension
                });
            }
        }

        return;

        void AddDirectory(string path)
        {
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var currentPath = "";
            foreach (var segment in segments)
            {
                var parentPath = currentPath;
                currentPath = string.IsNullOrEmpty(parentPath) ? segment : $"{parentPath}/{segment}";
                if (lookup.ContainsKey(currentPath))
                {
                    continue;
                }

                var parentNode = lookup[parentPath];
                var dirNode = new FileNode
                {
                    Name = segment,
                    Path = currentPath,
                    IsDirectory = true,
                    IsReference = currentPath.StartsWith("References", StringComparison.OrdinalIgnoreCase),
                    Depth = parentNode.Depth + 1
                };
                parentNode.Children.Add(dirNode);
                lookup[currentPath] = dirNode;
            }
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

            var childrenOrderedByContainer = node.Children
                .OrderBy(n => n.IsReference)
                .ThenByDescending(n => n.IsDirectory);

            var childrenOrdered = _sortMode switch
            {
                SortMode.NameAscending => childrenOrderedByContainer
                    .ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase),

                SortMode.NameDescending => childrenOrderedByContainer
                    .ThenByDescending(n => n.Name, StringComparer.OrdinalIgnoreCase),

                SortMode.TypeAscending => childrenOrderedByContainer
                    .ThenBy(n => n.Extension, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase),

                SortMode.TypeDescending => childrenOrderedByContainer
                    .ThenByDescending(n => n.Extension, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase),

                _ => childrenOrderedByContainer
            };

            node.Children = childrenOrdered.ToList();

            for (var i = node.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(node.Children[i]);
            }
        }
    }

    private class FileNode
    {
        public string Name { get; init; } = "";
        public string Path { get; init; } = "";
        public bool IsDirectory { get; init; }
        public bool IsReference { get; init; }
        public List<FileNode> Children { get; set; } = new();
        public int Depth { get; init; } // Track depth for indentation
        public string Extension { get; init; } = "";
        public bool MatchesFilter { get; set; }
        public bool IsOpen { get; set; }
    }
}