using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using FezEditor.Services;
using FezEditor.Tools;

namespace FezEditor.Structure;

public class History : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        IncludeFields = true,
        WriteIndented = false,
        Converters = { new TrileEmplacementConverter() }
    };

    private static readonly Change EmptyChange = new(string.Empty, string.Empty);

    private readonly string _sessionDirectory = AppStorageService.CreateHistorySessionDirectory();

    private object? _tracked;

    private HistoryNode? _current;

    private HistoryNode? _saved;

    private Type TrackedType
    {
        get
        {
            if (_tracked == null)
            {
                throw new InvalidOperationException("Cannot use history before tracking an object!");
            }

            return _tracked.GetType();
        }
    }

    public bool CanUndo => _current?.Parent != null;

    public bool CanRedo => _current?.Child != null;

    public bool HasUnsavedChanges => _current != _saved;

    public event Action<Change>? StateChanged;

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _current = null;
        _saved = null;

        if (Directory.Exists(_sessionDirectory))
        {
            Directory.Delete(_sessionDirectory, true);
        }
    }

    public void Track(object target)
    {
        _tracked = target;
        ResetRoot();
        _saved = _current;
    }

    public IDisposable BeginScope(string name)
    {
        return new Scope(this, name);
    }

    public void Undo()
    {
        if (!CanUndo)
        {
            return;
        }

        var before = _current!;
        var after = before.Parent!;
        _current = after;

        Restore(after);
        StateChanged?.Invoke(new Change(ReadSnapshot(before), ReadSnapshot(after)));
    }

    public void Redo()
    {
        if (!CanRedo)
        {
            return;
        }

        var before = _current!;
        var after = before.Child!;
        _current = after;

        Restore(after);
        StateChanged?.Invoke(new Change(ReadSnapshot(before), ReadSnapshot(after)));
    }

    public void Clear()
    {
        ResetRoot();
        _saved = _current;
        StateChanged?.Invoke(EmptyChange);
    }

    public void MarkSaved()
    {
        _saved = _current;
    }

    private HistoryNode CaptureState(string name, HistoryNode? parent)
    {
        var json = JsonSerializer.Serialize(_tracked, TrackedType, JsonOptions);
        var path = Path.Combine(_sessionDirectory, $"[{DateTime.UtcNow.Ticks}] {ToFileName(name)}.json");
        File.WriteAllText(path, json);
        return new HistoryNode(path, parent);
    }

    private void Restore(HistoryNode node)
    {
        var restored = JsonSerializer.Deserialize(ReadSnapshot(node), TrackedType, JsonOptions)!;
        foreach (var property in TrackedType.GetProperties())
        {
            if (property is { CanRead: true, CanWrite: true } &&
                property.GetCustomAttribute<JsonIgnoreAttribute>() == null)
            {
                property.SetValue(_tracked, property.GetValue(restored));
            }
        }

        foreach (var field in TrackedType.GetFields())
        {
            if (!field.IsInitOnly &&
                field.GetCustomAttribute<JsonIgnoreAttribute>() == null)
            {
                field.SetValue(_tracked, field.GetValue(restored));
            }
        }
    }

    private void Push(HistoryNode before, HistoryNode after)
    {
        var beforeJson = ReadSnapshot(before);
        var afterJson = ReadSnapshot(after);
        if (beforeJson.Equals(afterJson))
        {
            DeleteSnapshot(after);
            return;
        }

        DeleteBranch(before.Child);
        before.Child = after;
        _current = after;
        StateChanged?.Invoke(new Change(beforeJson, afterJson));
    }

    private void ResetRoot()
    {
        if (Directory.Exists(_sessionDirectory))
        {
            Directory.Delete(_sessionDirectory, true);
        }

        Directory.CreateDirectory(_sessionDirectory);
        _current = _tracked == null ? null : CaptureState(string.Empty, null);
    }

    private static string ReadSnapshot(HistoryNode node)
    {
        return File.ReadAllText(node.Path);
    }

    private static void DeleteSnapshot(HistoryNode node)
    {
        if (File.Exists(node.Path))
        {
            File.Delete(node.Path);
        }
    }

    private static void DeleteBranch(HistoryNode? node)
    {
        while (node != null)
        {
            DeleteSnapshot(node);
            node = node.Child;
        }
    }

    private static string ToFileName(string name)
    {
        return string.IsNullOrWhiteSpace(name)
            ? "initial"
            : string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
    }

    public sealed record Change(string BeforeJson, string AfterJson);

    private sealed class Scope : IDisposable
    {
        private readonly History _service;

        private readonly HistoryNode _before;

        private readonly string _name;

        private bool _disposed;

        internal Scope(History service, string name)
        {
            _service = service;
            _before = service._current ?? throw new InvalidOperationException("Cannot use history before tracking an object!");
            _name = name;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            var after = _service.CaptureState(_name, _before);
            _service.Push(_before, after);
        }
    }

    private sealed class HistoryNode(string path, HistoryNode? parent)
    {
        public string Path { get; } = path;

        public HistoryNode? Parent { get; } = parent;

        public HistoryNode? Child { get; set; }
    }
}