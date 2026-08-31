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

    private readonly LinkedList<UndoOperation> _undoStack = new();

    private readonly LinkedList<UndoOperation> _redoStack = new();

    private readonly string _sessionDirectory = AppStorageService.CreateHistorySessionDirectory();

    private long _nextSnapshotId;

    private object _tracked = null!;

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

    public bool CanUndo => _undoStack.Count > 0;

    public bool CanRedo => _redoStack.Count > 0;

    public event Action<Change>? StateChanged;

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _undoStack.Clear();
        _redoStack.Clear();

        if (Directory.Exists(_sessionDirectory))
        {
            Directory.Delete(_sessionDirectory, true);
        }
    }

    public void Track(object target)
    {
        _tracked = target;
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

        var after = _undoStack.Last!.Value;
        _undoStack.RemoveLast();

        var before = CaptureState(after.Name);
        _redoStack.AddLast(before);

        Restore(after);
        try
        {
            StateChanged?.Invoke(new Change(ReadSnapshot(before), ReadSnapshot(after)));
        }
        finally
        {
            DeleteSnapshot(after);
        }
    }

    public void Redo()
    {
        if (!CanRedo)
        {
            return;
        }

        var after = _redoStack.Last!.Value;
        _redoStack.RemoveLast();

        var before = CaptureState(after.Name);
        _undoStack.AddLast(before);

        Restore(after);
        try
        {
            StateChanged?.Invoke(new Change(ReadSnapshot(before), ReadSnapshot(after)));
        }
        finally
        {
            DeleteSnapshot(after);
        }
    }

    public void Clear()
    {
        DeleteSnapshots(_undoStack);
        DeleteSnapshots(_redoStack);
        _undoStack.Clear();
        _redoStack.Clear();
        StateChanged?.Invoke(EmptyChange);
    }

    private UndoOperation CaptureState(string name)
    {
        var json = JsonSerializer.Serialize(_tracked, TrackedType, JsonOptions);
        var path = Path.Combine(_sessionDirectory, $"{_nextSnapshotId++}.json");
        File.WriteAllText(path, json);
        return new UndoOperation(name, path);
    }

    private void Restore(UndoOperation op)
    {
        var restored = JsonSerializer.Deserialize(ReadSnapshot(op), TrackedType, JsonOptions)!;
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

    private void Push(UndoOperation before, UndoOperation after)
    {
        var beforeJson = ReadSnapshot(before);
        var afterJson = ReadSnapshot(after);
        DeleteSnapshot(after);

        if (beforeJson.Equals(afterJson))
        {
            DeleteSnapshot(before);
            return;
        }

        _undoStack.AddLast(before);

        DeleteSnapshots(_redoStack);
        _redoStack.Clear();
        StateChanged?.Invoke(new Change(beforeJson, afterJson));
    }

    private static string ReadSnapshot(UndoOperation op)
    {
        return File.ReadAllText(op.Path);
    }

    private static void DeleteSnapshot(UndoOperation op)
    {
        if (File.Exists(op.Path))
        {
            File.Delete(op.Path);
        }
    }

    private static void DeleteSnapshots(IEnumerable<UndoOperation> operations)
    {
        foreach (var operation in operations)
        {
            DeleteSnapshot(operation);
        }
    }

    public sealed record Change(string BeforeJson, string AfterJson);

    private sealed class Scope : IDisposable
    {
        private readonly History _service;

        private readonly UndoOperation _before;

        private bool _disposed;

        internal Scope(History service, string name)
        {
            _service = service;
            _before = service.CaptureState(name);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            var after = _service.CaptureState(_before.Name);
            _service.Push(_before, after);
        }
    }

    private sealed record UndoOperation(string Name, string Path);
}