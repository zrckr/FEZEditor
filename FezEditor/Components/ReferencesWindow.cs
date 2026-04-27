using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components;

public class ReferencesWindow : DrawableGameComponent
{
    private readonly ResourceService _resourceService;

    private bool _open;

    private bool _pendingOpen;

    public ReferencesWindow(Game game) : base(game)
    {
        _resourceService = game.GetService<ResourceService>();
    }

    public void Show()
    {
        _pendingOpen = true;
    }

    public override void Draw(GameTime gameTime)
    {
        if (_pendingOpen)
        {
            ImGuiX.SetNextWindowCentered();
            ImGui.OpenPopup("References##referencesWindow");
            _open = true;
            _pendingOpen = false;
        }

        if (!_open)
        {
            return;
        }

        const ImGuiWindowFlags flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize;

        ImGuiX.SetNextWindowSize(new Vector2(480, 0));
        if (!ImGui.BeginPopupModal("References##referencesWindow", ref _open, flags))
        {
            return;
        }

        var paths = _resourceService.GetModReferencePaths();
        if (paths.Count == 0)
        {
            ImGui.TextDisabled("No reference providers.");
        }
        else
        {
            var removeAt = -1;
            for (var i = 0; i < paths.Count; i++)
            {
                ImGui.Text(paths[i]);
                ImGui.SameLine();
                if (ImGui.Button($"{Icons.FolderOpened}##open_{i}"))
                {
                    _resourceService.OpenInFileManager(paths[i]);
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Open in File Manager");
                }

                ImGui.SameLine();
                if (ImGui.Button($"{Icons.Trash}##remove_{i}"))
                {
                    removeAt = i;
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Remove reference");
                }
            }

            if (removeAt >= 0)
            {
                var updated = paths.ToList();
                updated.RemoveAt(removeAt);
                _resourceService.UpdateModReferences(updated);
                ImGui.EndPopup();
                return;
            }
        }

        ImGui.Separator();

        if (ImGui.Button($"{Icons.Package} Add PAK files"))
        {
            ImGui.CloseCurrentPopup();
            _open = false;
            FileDialog.Show(FileDialog.Type.OpenFile, pakFiles =>
            {
                var updated = _resourceService.GetModReferencePaths().ToList();
                updated.AddRange(pakFiles);
                _resourceService.UpdateModReferences(updated);
            }, new FileDialog.Options
            {
                Title = "Choose reference PAK files...",
                AllowMultiple = true,
                Filters = new[] { new FileDialog.Filter("PAK files", "pak") }
            });
        }

        ImGui.SameLine();

        if (ImGui.Button($"{Icons.Folder} Add folder"))
        {
            ImGui.CloseCurrentPopup();
            _open = false;
            FileDialog.Show(FileDialog.Type.OpenFolder, folders =>
            {
                var updated = _resourceService.GetModReferencePaths().ToList();
                updated.AddRange(folders);
                _resourceService.UpdateModReferences(updated);
            }, new FileDialog.Options
            {
                Title = "Choose reference folder..."
            });
        }

        ImGui.SameLine();

        if (ImGui.Button("Close") || ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }
}