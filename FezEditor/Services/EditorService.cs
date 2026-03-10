using FezEditor.Components;
using FezEditor.Structure;
using FezEditor.Tools;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Serilog;

namespace FezEditor.Services;

[UsedImplicitly]
public partial class EditorService
{
    private static readonly ILogger Logger = Logging.Create<EditorService>();

    public EditorFlags Flags { get; private set; }

    public IEnumerable<EditorComponent> Editors => _editors;

    private readonly List<EditorComponent> _editors = new();

    private readonly List<EditorComponent> _pendingClose = new();

    private readonly HashSet<EditorComponent> _loading = new();

    private readonly Dictionary<EditorComponent, EditorTracking> _tracking = new();

    private readonly Game _game;

    private readonly InputService _inputService;

    private readonly ResourceService _resourceService;

    private EditorComponent? _activeEditor;

    public EditorService(Game game)
    {
        _game = game;
        _inputService = game.GetService<InputService>();
        _resourceService = game.GetService<ResourceService>();
    }

    public void Update(GameTime gameTime)
    {
        if (_activeEditor == null || _loading.Contains(_activeEditor))
        {
            return;
        }

        _activeEditor.Update(gameTime);

        if (_inputService.IsActionJustPressed(InputActions.UiUndo))
        {
            UndoActiveEditorChanges();
        }
        else if (_inputService.IsActionJustPressed(InputActions.UiRedo))
        {
            RedoActiveEditorChanges();
        }
        else if (_inputService.IsActionJustPressed(InputActions.UiSave))
        {
            SaveActiveEditorChanges();
        }
        else if (_inputService.IsActionJustPressed(InputActions.UiSaveAll))
        {
            foreach (var editor in Editors)
            {
                SaveEditorChanges(editor);
            }
        }
    }

    public void OpenEditorFor(string path)
    {
        if (_tracking.Values.All(et => et.Path != path))
        {
            var asset = _resourceService.Load(path);
            var editor = CreateEditorFor(asset, path);

            _tracking.Add(editor, new EditorTracking(path, false));
            OpenEditor(editor);
        }
    }

    public void OpenEditor(EditorComponent editor)
    {
        if (_editors.All(e => e.Title != editor.Title))
        {
            _editors.Add(editor);
            _activeEditor = editor;
            _activeEditor.History.StateChanged += () =>
            {
                if (_tracking.TryGetValue(editor, out var tracking))
                {
                    tracking.HasChanges = true;
                    _tracking[editor] = tracking;
                }
            };
            editor.LoadContent();
            UpdateFlags();
            Logger.Information("Opened the {0}", editor);
        }
    }

    public void CloseEditor(EditorComponent editor)
    {
        _pendingClose.Add(editor);
        Logger.Debug("Closing the {0}...", editor);
    }

    public void CloseActiveEditor()
    {
        _pendingClose.Add(_activeEditor!);
        Logger.Debug("Closing the {0}...", _activeEditor!);
    }

    public void CloseAllEditors()
    {
        _pendingClose.AddRange(_editors);
        Logger.Debug("Closing {0} editor(s)...", _editors.Count);
    }

    public void MarkEditorActive(EditorComponent editor)
    {
        _activeEditor = editor;
        UpdateFlags();
    }

    public void UndoActiveEditorChanges()
    {
        _activeEditor!.History.Undo();
        Logger.Debug("Undo at {0}", _activeEditor);
    }

    public void RedoActiveEditorChanges()
    {
        _activeEditor!.History.Redo();
        Logger.Debug("Redo at {0}", _activeEditor);
    }

    public bool HasEditorUnsavedChanges(EditorComponent editor)
    {
        return _tracking.Any(kv => kv.Key == editor && kv.Value.HasChanges) && editor.History.UndoCount > 0;
    }

    public bool HasAnyEditorUnsavedChanges()
    {
        return _tracking.Any(kv => kv.Value.HasChanges);
    }

    public void SaveActiveEditorChanges()
    {
        if (_tracking.TryGetValue(_activeEditor!, out var tracking) && tracking.HasChanges)
        {
            _resourceService.Save(tracking.Path, _activeEditor!.Asset);
            tracking.HasChanges = false;
            _tracking[_activeEditor] = tracking;
            Logger.Information("Saving {0}", _activeEditor);
        }
    }

    public void SaveActiveEditorChangesAs()
    {
        FileDialog.Show(FileDialog.Type.SaveFile, result =>
        {
            if (result.Files.Length > 0 && _tracking.TryGetValue(_activeEditor!, out var tracking) &&
                tracking.HasChanges)
            {
                _resourceService.Save(result.Files[0], _activeEditor!.Asset);
                tracking.HasChanges = false;
                _tracking[_activeEditor] = tracking;
                Logger.Information("Saving {0} as...", _activeEditor);
            }
        });
    }

    public void SaveEditorChanges(EditorComponent editor)
    {
        if (_tracking.TryGetValue(_activeEditor!, out var tracking) && tracking.HasChanges)
        {
            _resourceService.Save(editor.Title, editor.Asset);
            tracking.HasChanges = false;
            _tracking[_activeEditor!] = tracking;
            Logger.Information("Saving {0}", editor);
        }
    }

    public void FlushPendingCloses()
    {
        if (_pendingClose.Count == 0)
        {
            return;
        }

        foreach (var editor in _pendingClose)
        {
            if (_editors.Remove(editor) && _tracking.Remove(editor))
            {
                editor.Dispose();
            }

            if (editor == _activeEditor)
            {
                _activeEditor = _editors.Count > 0 ? _editors[^1] : null;
            }
        }

        UpdateFlags();
        _pendingClose.Clear();
    }

    public bool IsEditorLoading(EditorComponent editor)
    {
        return _loading.Contains(editor);
    }

    private void UpdateFlags()
    {
        if (_activeEditor is WelcomeComponent)
        {
            Flags &= ~(EditorFlags.CloseFile | EditorFlags.QuitToWelcome);
            return;
        }

        Flags = EditorFlags.QuitToWelcome;
        if (_activeEditor == null)
        {
            return;
        }

        Flags |= EditorFlags.CloseFile;

        if (_activeEditor.History.CanUndo)
        {
            Flags |= EditorFlags.Undo;
        }
        else
        {
            Flags &= ~EditorFlags.Undo;
        }

        if (_activeEditor.History.CanRedo)
        {
            Flags |= EditorFlags.Redo;
        }
        else
        {
            Flags &= ~EditorFlags.Redo;
        }

        if (_resourceService.IsReadonly)
        {
            return;
        }

        if (_tracking.Values.Any(et => et.HasChanges))
        {
            Flags |= EditorFlags.SaveFile;
        }
        else
        {
            Flags &= ~EditorFlags.SaveFile;
        }
    }

    private record struct EditorTracking(string Path, bool HasChanges);
}