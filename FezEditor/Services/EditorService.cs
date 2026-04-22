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

    private readonly List<EditorComponent> _pendingLoad = new();

    private EditorComponent? _pendingActiveEditor;

    private readonly Dictionary<EditorComponent, EditorTracking> _tracking = new();

    private readonly Game _game;

    private readonly InputService _inputService;

    private readonly ResourceService _resourceService;

    private readonly StatusService _statusService;

    private readonly AppStorageService _storageService;

    private EditorComponent? _activeEditor;

    public EditorService(Game game)
    {
        _game = game;
        _inputService = game.GetService<InputService>();
        _resourceService = game.GetService<ResourceService>();
        _statusService = game.GetService<StatusService>();
        _storageService = game.GetService<AppStorageService>();
    }

    public void Update(GameTime gameTime)
    {
        _statusService.ClearHints();

        if (_activeEditor == null || _pendingLoad.Contains(_activeEditor))
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
        foreach (var (editor, tracking) in _tracking)
        {
            if (tracking.Path == path)
            {
                _pendingActiveEditor = editor;
                return;
            }
        }

        _storageService.AddRecentFile(_resourceService.Root, path);
        var asset = _resourceService.Load(path);
        var newEditor = CreateEditorFor(asset, path);

        _tracking.Add(newEditor, new EditorTracking(path, false));
        OpenEditor(newEditor);
    }

    public void OpenEditor(EditorComponent editor)
    {
        if (_editors.All(e => e.Title != editor.Title))
        {
            _editors.Add(editor);
            _activeEditor = editor;
            _pendingActiveEditor = editor;
            _activeEditor.History.StateChanged += () =>
            {
                if (_tracking.TryGetValue(editor, out var tracking))
                {
                    tracking.HasChanges = true;
                    _tracking[editor] = tracking;
                }

                UpdateFlags();
            };
            _pendingLoad.Add(editor);
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

    public bool ShouldFocusEditor(EditorComponent editor)
    {
        return _pendingActiveEditor == editor;
    }

    public void MarkEditorActive(EditorComponent editor)
    {
        _activeEditor = editor;
        if (_pendingActiveEditor == editor)
        {
            _pendingActiveEditor = null;
        }

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
        return _tracking.TryGetValue(editor, out var tracking) && tracking.HasChanges;
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
            UpdateFlags();
        }
    }

    public void SaveActiveEditorChangesAs()
    {
        if (_tracking.TryGetValue(_activeEditor!, out var tracking))
        {
            var type = _activeEditor!.Asset.GetType();
            var extension = GetExtensionForType(type);

            var currentFullPath = _resourceService.GetFullPath(tracking.Path);
            currentFullPath = currentFullPath.Replace("." + extension, string.Empty);

            var options = new FileDialog.Options
            {
                DefaultLocation = currentFullPath,
                Filters = [new FileDialog.Filter(type.Name, extension)]
            };

            FileDialog.Show(FileDialog.Type.SaveFile, files =>
            {
                var relativePath = _resourceService.GetRelativePath(files[0]);
                _resourceService.Save(relativePath, _activeEditor!.Asset);
                _activeEditor!.Title = relativePath;
                _tracking[_activeEditor!] = new EditorTracking(relativePath, false);
                Logger.Information("Saved {0} as {1}", _activeEditor!, relativePath);
                UpdateFlags();
            }, options);
        }
    }

    public void SaveEditorChanges(EditorComponent editor)
    {
        if (_tracking.TryGetValue(editor, out var tracking) && tracking.HasChanges)
        {
            _resourceService.Save(tracking.Path, editor.Asset);
            tracking.HasChanges = false;
            _tracking[editor] = tracking;
            Logger.Information("Saving {0}", editor);
            UpdateFlags();
        }
    }

    public void FlushPendingLoads()
    {
        if (_pendingLoad.Count == 0)
        {
            return;
        }

        foreach (var editor in _pendingLoad)
        {
            editor.LoadContent();
            Logger.Debug("Loaded {0}", editor);
        }

        _pendingLoad.Clear();
        UpdateFlags();
    }

    public void FlushPendingCloses()
    {
        if (_pendingClose.Count == 0)
        {
            return;
        }

        foreach (var editor in _pendingClose)
        {
            _editors.Remove(editor);
            _tracking.Remove(editor);
            editor.Dispose();

            if (editor == _activeEditor)
            {
                _activeEditor = _editors.Count > 0 ? _editors[^1] : null;
                _pendingActiveEditor = _activeEditor;
            }
        }

        UpdateFlags();
        _pendingClose.Clear();
    }

    public bool IsEditorLoading(EditorComponent editor)
    {
        return _pendingLoad.Contains(editor);
    }

    private void UpdateFlags()
    {
        if (_activeEditor is WelcomeComponent)
        {
            Flags = EditorFlags.None;
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

        if (_tracking.TryGetValue(_activeEditor, out var activeTracking) && activeTracking.HasChanges)
        {
            Flags |= EditorFlags.SaveFile;
        }
        else
        {
            Flags &= ~EditorFlags.SaveFile;
        }

        if (_tracking.Values.Any(et => et.HasChanges))
        {
            Flags |= EditorFlags.SaveAll;
        }
        else
        {
            Flags &= ~EditorFlags.SaveAll;
        }
    }

    private record struct EditorTracking(string Path, bool HasChanges);
}