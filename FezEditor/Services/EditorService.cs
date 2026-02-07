using FezEditor.Components;
using FezEditor.Structure;
using JetBrains.Annotations;
using Serilog;

namespace FezEditor.Services;

[UsedImplicitly]
public class EditorService : IEditorService
{
    private static readonly ILogger Logger = Logging.Create<EditorService>();

    public EditorFlags Flags { get; private set; }

    public IEnumerable<EditorComponent> Editors => _editors;

    public EditorComponent? ActiveEditor { get; private set; }

    private readonly List<EditorComponent> _editors = new();

    private readonly List<EditorComponent> _pendingClose = new();

    public void OpenEditor(EditorComponent editor)
    {
        if (_editors.All(e => e.Title != editor.Title))
        {
            editor.Initialize();
            _editors.Add(editor);
            ActiveEditor = editor;
            UpdateFlags();
        }
    }

    public void CloseEditor(EditorComponent editor)
    {
        _pendingClose.Add(editor);
    }

    public void CloseAllEditors()
    {
        _pendingClose.AddRange(_editors);
    }

    public void MarkEditorActive(EditorComponent editor)
    {
        if (ActiveEditor != editor)
        {
            ActiveEditor = editor;
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
            if (_editors.Remove(editor))
            {
                editor.Dispose();
            }

            if (editor == ActiveEditor)
            {
                ActiveEditor = _editors.Count > 0 ? _editors[^1] : null;
                UpdateFlags();
            }
        }

        _pendingClose.Clear();
    }
    
    private void UpdateFlags()
    {
        if (ActiveEditor is WelcomeComponent)
        {
            Flags &= ~(EditorFlags.CloseFile | EditorFlags.QuitToWelcome);
        }
        else
        {
            Flags |= EditorFlags.QuitToWelcome;
            if (Editors.Any())
            {
                Flags |= EditorFlags.CloseFile;
            }
            else
            {
                Flags &= ~EditorFlags.CloseFile;
            }
        }
    }
}