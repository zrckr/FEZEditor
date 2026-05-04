using System.Text.Json;
using System.Text.Json.Serialization;

namespace FezEditor.Structure;

public class History : IDisposable
{
    private const int MaxHistorySize = byte.MaxValue;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        IncludeFields = true,
        WriteIndented = false
    };

    private readonly LinkedList<UndoOperation> _undoStack = new();

    private readonly LinkedList<UndoOperation> _redoStack = new();

    private readonly HashSet<object> _tracked = new();

    public bool CanUndo => _undoStack.Count > 0;

    public bool CanRedo => _redoStack.Count > 0;

    public int UndoCount => _undoStack.Count;

    public int RedoCount => _redoStack.Count;

    public event Action<object?>? StateChanged;

    public void RegisterConverter(JsonConverter converter)
    {
        _jsonOptions.Converters.Add(converter);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        foreach (var uo in _undoStack)
        {
            uo.States.Clear();
        }

        foreach (var uo in _redoStack)
        {
            uo.States.Clear();
        }

        _undoStack.Clear();
        _redoStack.Clear();
        _tracked.Clear();
    }

    public void Track(object target)
    {
        _tracked.Add(target);
    }

    public void Untrack(object target)
    {
        _tracked.Remove(target);
    }

    public void TrackRange(IEnumerable<object> targets)
    {
        foreach (var target in targets)
        {
            _tracked.Add(target);
        }
    }

    public IDisposable BeginScope(string name, object? tag = null)
    {
        return new Scope(this, name, tag);
    }

    public void Undo()
    {
        if (!CanUndo)
        {
            return;
        }

        var op = _undoStack.Last!.Value;
        _undoStack.RemoveLast();

        _redoStack.AddLast(CaptureState(op.Name, op.Tag));
        if (_redoStack.Count > MaxHistorySize)
        {
            _redoStack.RemoveFirst();
        }

        Restore(op);
        StateChanged?.Invoke(op.Tag);
    }

    public void Redo()
    {
        if (!CanRedo)
        {
            return;
        }

        var op = _redoStack.Last!.Value;
        _redoStack.RemoveLast();

        _undoStack.AddLast(CaptureState(op.Name, op.Tag));
        if (_undoStack.Count > MaxHistorySize)
        {
            _undoStack.RemoveFirst();
        }

        Restore(op);
        StateChanged?.Invoke(op.Tag);
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        StateChanged?.Invoke(null);
    }

    private UndoOperation CaptureState(string name, object? tag)
    {
        var states = new Dictionary<object, (Type type, string Json)>();
        foreach (var target in _tracked)
        {
            var json = JsonSerializer.Serialize(target, target.GetType(), _jsonOptions);
            states[target] = (target.GetType(), json);
        }

        return new UndoOperation(name, tag, states);
    }

    private void Restore(UndoOperation op)
    {
        foreach (var (target, (type, json)) in op.States)
        {
            var restored = JsonSerializer.Deserialize(json, type, _jsonOptions)!;

            var targetType = target.GetType();
            foreach (var prop in targetType.GetProperties())
            {
                if (prop is { CanRead: true, CanWrite: true })
                {
                    prop.SetValue(target, prop.GetValue(restored));
                }
            }

            foreach (var field in targetType.GetFields())
            {
                if (!field.IsInitOnly)
                {
                    field.SetValue(target, field.GetValue(restored));
                }
            }
        }
    }

    private void Push(UndoOperation before, UndoOperation after)
    {
        var hasChanges = false;
        foreach (var (target, (_, jsonBefore)) in before.States)
        {
            if (after.States.TryGetValue(target, out var afterState) && jsonBefore != afterState.Json)
            {
                hasChanges = true;
                break;
            }
        }

        if (!hasChanges)
        {
            return;
        }

        _undoStack.AddLast(before);
        if (_undoStack.Count > MaxHistorySize)
        {
            _undoStack.RemoveFirst();
        }

        _redoStack.Clear();
        StateChanged?.Invoke(before.Tag);
    }

    private sealed class Scope : IDisposable
    {
        private readonly History _service;
        private readonly UndoOperation _before;
        private readonly object? _tag;
        private bool _disposed;

        internal Scope(History service, string name, object? tag)
        {
            _service = service;
            _before = service.CaptureState(name, _tag = tag);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            var after = _service.CaptureState(_before.Name, _tag);
            _service.Push(_before, after);
        }
    }

    private record UndoOperation(string Name, object? Tag, Dictionary<object, (Type type, string Json)> States);
}