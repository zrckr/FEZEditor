using System.Reflection;
using System.Security.Cryptography;
using System.Text;
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

    private readonly Dictionary<string, int> _chunkReferences = new(StringComparer.Ordinal);

    private readonly FastCdc _chunker = new();

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
        StateChanged?.Invoke(new Change(ReadJson(before), ReadJson(after)));
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
        StateChanged?.Invoke(new Change(ReadJson(before), ReadJson(after)));
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

    public IEnumerable<Entry> GetEntries()
    {
        if (_current == null)
        {
            yield break;
        }

        var root = _current;
        while (root.Parent != null)
        {
            root = root.Parent;
        }

        var isRedo = false;
        var index = 0;

        for (var node = root; node != null; node = node.Child, index++)
        {
            var state = node == _current
                ? EntryState.Current
                : isRedo
                    ? EntryState.Redo
                    : EntryState.Undo;

            if (node == _saved)
            {
                state |= EntryState.Saved;
            }

            yield return new Entry(index, node.Name, node.Timestamp, state);
            isRedo |= node == _current;
        }
    }

    public void JumpToEntry(int index)
    {
        if (_current == null || index < 0)
        {
            return;
        }

        var target = _current;
        while (target.Parent != null)
        {
            target = target.Parent;
        }

        for (var i = 0; i < index && target != null; i++)
        {
            target = target.Child;
        }

        if (target == null || target == _current)
        {
            return;
        }

        var before = _current;
        _current = target;
        Restore(target);
        StateChanged?.Invoke(new Change(ReadJson(before), ReadJson(target)));
    }

    private HistoryNode CaptureState(string name, HistoryNode? parent)
    {
        var chunks = new List<string>();
        var stream = new ChunkingStream(_chunker, bytes =>
        {
            var hash = Convert.ToHexString(SHA256.HashData(bytes.Span)).ToLowerInvariant();
            var path = GetChunkPath(hash);

            if (!File.Exists(path))
            {
                File.WriteAllBytes(path, bytes.Span);
            }

            _chunkReferences.TryGetValue(hash, out var references);
            _chunkReferences[hash] = references + 1;
            chunks.Add(hash);
        });

        try
        {
            JsonSerializer.Serialize(stream, _tracked, TrackedType, JsonOptions);
            stream.Complete();
            return new HistoryNode(name, DateTime.UtcNow, new Snapshot(checked((int)stream.Length), chunks), parent);
        }
        catch
        {
            _chunker.Reset();
            Release(chunks);
            throw;
        }
    }

    private void Restore(HistoryNode node)
    {
        var restored = JsonSerializer.Deserialize(ReadBytes(node), TrackedType, JsonOptions)!;
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
        if (before.Snapshot.Length == after.Snapshot.Length &&
            before.Snapshot.Chunks.SequenceEqual(after.Snapshot.Chunks))
        {
            Release(after.Snapshot.Chunks);
            return;
        }

        DeleteBranch(before.Child);
        before.Child = after;
        _current = after;
        StateChanged?.Invoke(new Change(ReadJson(before), ReadJson(after)));
    }

    private void ResetRoot()
    {
        if (Directory.Exists(_sessionDirectory)) Directory.Delete(_sessionDirectory, true);
        Directory.CreateDirectory(_sessionDirectory);
        _chunkReferences.Clear();
        _current = _tracked == null ? null : CaptureState("Initial", null);
    }

    private void DeleteBranch(HistoryNode? node)
    {
        while (node != null)
        {
            Release(node.Snapshot.Chunks);
            node = node.Child;
        }
    }

    private byte[] ReadBytes(HistoryNode node)
    {
        var result = new byte[node.Snapshot.Length];
        var offset = 0;

        foreach (var hash in node.Snapshot.Chunks)
        {
            var bytes = File.ReadAllBytes(GetChunkPath(hash));
            bytes.CopyTo(result, offset);
            offset += bytes.Length;
        }

        return result;
    }

    private string ReadJson(HistoryNode node)
    {
        return Encoding.UTF8.GetString(ReadBytes(node));
    }

    private void Release(IEnumerable<string> hashes)
    {
        foreach (var hash in hashes)
        {
            var references = _chunkReferences[hash] - 1;
            if (references == 0)
            {
                _chunkReferences.Remove(hash);
                File.Delete(GetChunkPath(hash));
            }
            else
            {
                _chunkReferences[hash] = references;
            }
        }
    }

    private string GetChunkPath(string hash)
    {
        return Path.Combine(_sessionDirectory, hash);
    }

    public sealed record Change(string BeforeJson, string AfterJson);

    [Flags]
    public enum EntryState
    {
        Undo = 0,
        Current = 1 << 0,
        Redo = 1 << 1,
        Saved = 1 << 2
    }

    public readonly record struct Entry(int Index, string Name, DateTime Timestamp, EntryState State);

    private sealed class Scope : IDisposable
    {
        private readonly History _service;

        private readonly HistoryNode _before;

        private readonly string _name;

        private bool _disposed;

        internal Scope(History service, string name)
        {
            _service = service;
            _before = service._current ??
                      throw new InvalidOperationException("Cannot use history before tracking an object!");
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

    private sealed class HistoryNode(string name, DateTime timestamp, Snapshot snapshot, HistoryNode? parent)
    {
        public string Name { get; } = name;

        public DateTime Timestamp { get; } = timestamp;

        public Snapshot Snapshot { get; } = snapshot;

        public HistoryNode? Parent { get; } = parent;

        public HistoryNode? Child { get; set; }
    }

    private sealed record Snapshot(int Length, IReadOnlyList<string> Chunks);

    private sealed class ChunkingStream(FastCdc chunker, Action<ReadOnlyMemory<byte>> emit) : Stream
    {
        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => !_completed;

        public override long Length => _length;

        private bool _completed;

        private long _length;

        public override long Position
        {
            get => _length;
            set => throw new NotSupportedException();
        }

        public void Complete()
        {
            if (_completed)
            {
                return;
            }

            chunker.Complete(WriteChunk);
            _completed = true;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Write(buffer.AsSpan(offset, count));
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            ObjectDisposedException.ThrowIf(_completed, this);
            chunker.Append(buffer, WriteChunk);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        private void WriteChunk(ReadOnlyMemory<byte> bytes)
        {
            _length += bytes.Length;
            emit(bytes);
        }
    }
}