using ImGuiNET;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;

namespace FezEditor.Components;

[UsedImplicitly]
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

    private enum SortMode
    {
        NameAscending,
        NameDescending,
        TypeAscending,
        TypeDescending
    }

    public FileBrowser(Game game) : base(game)
    {
    }

    protected override void LoadContent()
    {
        _root = LoadDirectory(@"D:\Projects\fez-assets\Assets");
    }

    public override void Draw(GameTime gameTime)
    {
        ImGuiX.SetNextWindowSize(new Vector2(240, 0), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("File Browser", ImGuiWindowFlags.NoCollapse))
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
                            _selectionHistory.RemoveRange(_historyIndex + 1, _selectionHistory.Count - _historyIndex - 1);
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
                ImGui.SetNextItemWidth(-30);
                ImGui.InputTextWithHint("", "Filter Files", ref _filter, 255);
                ImGui.SameLine();
                if (ImGui.Button("?"))
                {
                    ImGui.OpenPopup("SortOptions");
                }

                if (ImGui.BeginPopup("SortOptions"))
                {
                    ImGui.SeparatorText("Sort by");

                    if (ImGui.MenuItem("Name (A-Z)", null, _sortMode == SortMode.NameAscending))
                    {
                        SortAllNodes(SortMode.NameAscending);
                    }

                    if (ImGui.MenuItem("Name (Z-A)", null, _sortMode == SortMode.NameDescending))
                    {
                        SortAllNodes(SortMode.NameDescending);
                    }

                    ImGui.Separator();

                    if (ImGui.MenuItem("Type (A-Z)", null, _sortMode == SortMode.TypeAscending))
                    {
                        SortAllNodes(SortMode.TypeAscending);
                    }

                    if (ImGui.MenuItem("Type (Z-A)", null, _sortMode == SortMode.TypeDescending))
                    {
                        SortAllNodes(SortMode.TypeDescending);
                    }

                    ImGui.EndPopup();
                }
            }
            ImGui.PopStyleVar();

            ImGui.Separator();

            if (ImGui.BeginChild("FileTree") && _root != null)
            {
                // Reduce tree node padding for more compact display
                ImGuiX.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4, 2));
                // Check if empty space was clicked to deselect
                if (ImGui.IsWindowHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !ImGui.IsAnyItemHovered())
                {
                    _selected = null;
                    _path = "";
                }

                _tree.Clear();
                _tree.Push((_root, false));
                
                while (_tree.Count > 0)
                {
                    var (node, shouldPop) = _tree.Pop();

                    // Handle TreePop for previously opened nodes
                    if (shouldPop)
                    {
                        ImGui.TreePop();
                        continue;
                    }

                    // Skip if it doesn't match filter (fuzzy search)
                    if (!string.IsNullOrEmpty(_filter) && !FuzzyMatch(node.Name, _filter))
                    {
                        continue;
                    }

                    var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth;
                    if (!node.IsDirectory)
                    {
                        flags |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;
                    }

                    if (_selected == node)
                    {
                        flags |= ImGuiTreeNodeFlags.Selected;
                    }

                    var nodeOpen = ImGui.TreeNodeEx($"{node.Path}##{node.Path}", flags, $"{node.Name}");
                    if (ImGui.IsItemClicked())
                    {
                        _selected = node;
                        _path = node.Path;

                        // Add to selection history
                        // Remove any forward history if we're not at the end
                        if (_historyIndex < _selectionHistory.Count - 1)
                        {
                            _selectionHistory.RemoveRange(_historyIndex + 1, _selectionHistory.Count - _historyIndex - 1);
                        }

                        // Only add if it's different from the last selection
                        if (_selectionHistory.Count == 0 || _selectionHistory[^1] != node)
                        {
                            _selectionHistory.Add(node);
                            _historyIndex = _selectionHistory.Count - 1;
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

                ImGui.PopStyleVar(); // Pop ItemSpacing
            }

            ImGui.EndChild();
        }

        ImGui.End();
    }

    private static FileNode LoadDirectory(string path)
    {
        var rootNode = new FileNode
        {
            Name = Path.GetFileName(path),
            Path = path,
            IsDirectory = true,
            Depth = 0,
            Extension = ""
        };

        var queue = new Queue<FileNode>();
        queue.Enqueue(rootNode);

        while (queue.Count > 0)
        {
            var currentNode = queue.Dequeue();
            if (!currentNode.IsDirectory)
            {
                continue;
            }

            try
            {
                var entries = Directory.GetFileSystemEntries(currentNode.Path);
                foreach (var entry in entries)
                {
                    var isDirectory = Directory.Exists(entry);
                    var child = new FileNode
                    {
                        Name = Path.GetFileName(entry),
                        Path = entry,
                        IsDirectory = isDirectory,
                        Depth = currentNode.Depth + 1,
                        Extension = isDirectory ? "" : Path.GetExtension(entry).TrimStart('.')
                    };

                    currentNode.Children.Add(child);
                    if (child.IsDirectory)
                    {
                        queue.Enqueue(child);
                    }
                }

                // Sort directories first, then files alphabetically (default sort)
                currentNode.Children = currentNode.Children
                    .OrderByDescending(n => n.IsDirectory)
                    .ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (UnauthorizedAccessException)
            {
                // Handle permission errors
            }
        }

        return rootNode;
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

    private void SortAllNodes(SortMode sortMode)
    {
        _sortMode = sortMode;
        var stack = new Stack<FileNode>();
        stack.Push(_root!);
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

    public class FileNode
    {
        public string Name { get; init; } = "";
        public string Path { get; init; } = "";
        public bool IsDirectory { get; init; }
        public List<FileNode> Children { get; set; } = new();
        public int Depth { get; init; } // Track depth for indentation
        public string Extension { get; init; } = "";
    }
}