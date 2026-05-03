using FezEditor.Services;
using FezEditor.Tools;
using Microsoft.Xna.Framework;

namespace FezEditor.Components;

public class TitleBar : GameComponent
{
    private const string MainTitle = "FEZEditor";

    private readonly EditorService _editorService;
    private readonly ResourceService _resourceService;

    public TitleBar(Game game) : base(game)
    {
        _editorService = game.GetService<EditorService>();
        _resourceService = game.GetService<ResourceService>();
        _editorService.ActiveEditorChanged += UpdateWindowTitle;
        _resourceService.ProviderChanged += UpdateWindowTitle;

        UpdateWindowTitle();
    }

    private void UpdateWindowTitle()
    {
        var windowTitle = MainTitle;
        if (!_resourceService.HasNoProvider)
        {
            var resourceName = ResourceService.GetProviderDisplayName(_resourceService.Root);
            windowTitle = $"{resourceName} - {windowTitle}";
        }

        if (_editorService.ActiveEditor != null)
        {
            var fileName = Path.GetFileName(_editorService.ActiveEditor.Title);
            windowTitle = $"{fileName} - {windowTitle}";
        }

        Game.Window.Title = windowTitle;
    }

    protected override void Dispose(bool disposing)
    {
        _editorService.ActiveEditorChanged -= UpdateWindowTitle;
        _resourceService.ProviderChanged -= UpdateWindowTitle;
    }
}