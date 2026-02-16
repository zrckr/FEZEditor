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

    private string _path = "";

    private string _filter = "";

    private readonly Stack<(FileNode node, bool shouldPop)> _tree = new();

    private readonly List<FileNode> _selectionHistory = new();

    private int _historyIndex = -1;

    private SortMode _sortMode = SortMode.NameAscending;
    
    private readonly EditorService _editorService;
    
    private readonly ResourceService _resourceService;

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
        _resourceService.ProviderChanged += UpdateNodeTree;
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
        ImGuiX.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(4, 4));
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
            if (ImGui.InputText("##PathInput", ref _path, 512, ImGuiInputTextFlags.EnterReturnsTrue))
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
            ImGui.InputTextWithHint("", "Filter Files", ref _filter, 255);
            
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

            var filtering = !string.IsNullOrEmpty(_filter);
            if (filtering)
            {
                UpdateFilterMatches(_root);
            }

            _tree.Clear();
            _tree.Push((_root, false));

            ImGui.PushStyleVar(ImGuiStyleVar.IndentSpacing, 8);
            ImGuiX.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 4));
            while (_tree.Count > 0)
            {
                var (node, shouldPop) = _tree.Pop();

                // Handle TreePop for previously opened nodes
                if (shouldPop)
                {
                    ImGui.TreePop();
                    continue;
                }

                // Skip non-root nodes that don't match the filter
                if (filtering && node != _root && !node.MatchesFilter)
                {
                    continue;
                }

                var isRoot = node == _root;
                var nodeFlags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth;
                if (isRoot)
                {
                    nodeFlags |= ImGuiTreeNodeFlags.DefaultOpen;
                }

                if (!node.IsDirectory)
                {
                    nodeFlags |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;
                }

                if (_selected == node)
                {
                    nodeFlags |= ImGuiTreeNodeFlags.Selected;
                }

                // Force open directories when filtering so matched children are visible
                if (filtering && node.IsDirectory)
                {
                    ImGui.SetNextItemOpen(true);
                }

                var nodeOpen = ImGui.TreeNodeEx($"{node.Path}##{node.Path}", nodeFlags, $"{node.Name}");
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
                        var path = node.Path[(_root.Path.Length + 1)..];
                        _editorService.OpenEditorFor(path);
                    }
                }
                
                // if (ImGui.BeginPopupContextItem())
                // {
                //     if (ImGui.MenuItem("Open"))
                //     {
                //         /* ... */
                //     }
                //
                //     if (ImGui.MenuItem("Delete"))
                //     {
                //         /* ... */
                //     }
                //
                //     ImGui.EndPopup();
                // }

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
        }

        ImGui.EndChild();
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
        BuildNodeTree();
        SortAllNodes();
    }

    private void BuildNodeTree()
    {
        if (_resourceService.HasNoProvider)
        {
            _root = null;
            _filter = "";
            return;
        }
        
        _root = new FileNode
        {
            Name = _resourceService.Root,
            Path = _resourceService.Root,
            IsDirectory = true,
            Depth = 0
        };

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
                        Path = _root.Path + "/" + currentPath,
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
                Path = _root.Path + "/" + path,
                IsDirectory = false,
                Depth = parentNodeForFile.Depth + 1,
                Extension = Path.GetExtension(fileName)
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

    private class FileNode
    {
        public string Name { get; init; } = "";
        public string Path { get; init; } = "";
        public bool IsDirectory { get; init; }
        public List<FileNode> Children { get; set; } = new();
        public int Depth { get; init; } // Track depth for indentation
        public string Extension { get; init; } = "";
        public bool MatchesFilter { get; set; }
    }
}
