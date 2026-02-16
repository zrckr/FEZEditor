using FezEditor.Structure;
using FezEditor.Tools;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components;

public class PoEditor : EditorComponent
{
    private static readonly string[] ColumnNames = ["Text Id", "Source Text", "Translation Text"];

    public override object Asset => _textStorage;

    private readonly EditWindow _edit;

    private readonly ConfirmWindow _confirm;

    private readonly TextStorage _textStorage;

    private readonly List<string[]> _textTable = new();

    private (int Row, int Column) _activeCell = (-1, -1);

    private string _cellText = "";

    private string _newEntryId = "";

    private string _pendingDeleteId = "";

    private Language _selectedLanguage = Language.English;

    private bool _disableTranslationColumn;

    private State _nextState = State.TableView;

    public PoEditor(Game game, string title, TextStorage textStorage) : base(game, title)
    {
        _textStorage = textStorage;
        History.Track(_textStorage);
        History.StateChanged += UpdateTableView;
        Game.AddComponent(_edit = new EditWindow(game));
        Game.AddComponent(_confirm = new ConfirmWindow(game));
    }

    public override void Dispose()
    {
        Game.RemoveComponent(_edit);
        Game.RemoveComponent(_confirm);
    }

    public override void LoadContent()
    {
        UpdateTableView();
    }

    private void UpdateTableView()
    {
        var englishStorage = _textStorage[Language.English.GetId()];
        var selectedStorage = _textStorage[_selectedLanguage.GetId()];

        _textTable.Clear();
        _disableTranslationColumn = _selectedLanguage == Language.English;

        foreach (var id in englishStorage.Keys)
        {
            var source = englishStorage[id];
            if (!selectedStorage.TryGetValue(id, out var translation) || _disableTranslationColumn)
            {
                translation = "";
            }

            _textTable.Add(new[] { id, source, translation });
        }
    }

    public override void Draw()
    {
        #region Toolbar

        ImGuiX.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 8));

        var language = (int)_selectedLanguage;
        var languages = Enum.GetNames<Language>();

        ImGui.SetNextItemWidth(120);
        if (ImGui.Combo("Language", ref language, languages, languages.Length))
        {
            _selectedLanguage = (Language)language;
            UpdateTableView();
        }

        ImGui.SameLine();
        if (ImGui.Button($"{Icons.Add} Add New Entry"))
        {
            _newEntryId = "";
            _nextState = State.AddEntry;
        }

        ImGui.Separator();

        #endregion

        #region Translations Table

        if (ImGui.BeginTable("##PoTable", 3,
                ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.Sortable | ImGuiTableFlags.ScrollY))
        {
            ImGuiX.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(8, 8));
            ImGui.TableSetupColumn("Id", ImGuiTableColumnFlags.WidthFixed, 180f);
            ImGui.TableSetupColumn("Source text", ImGuiTableColumnFlags.NoSort);
            ImGui.TableSetupColumn("Translation text", ImGuiTableColumnFlags.NoSort);
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableHeadersRow();
            ImGui.PopStyleVar();

            var sortSpecs = ImGui.TableGetSortSpecs();
            if (sortSpecs.SpecsDirty)
            {
                _textTable.Sort((a, b) =>
                {
                    var compare = string.Compare(a[0], b[0], StringComparison.Ordinal);
                    if (sortSpecs.Specs.SortDirection == ImGuiSortDirection.Descending)
                    {
                        compare = -compare;
                    }

                    return compare;
                });
                sortSpecs.SpecsDirty = false;
            }

            for (var i = 0; i < _textTable.Count; i++)
            {
                var row = _textTable[i];
                ImGui.TableNextRow(ImGuiTableRowFlags.None, 32f);

                for (var j = 0; j < row.Length; j++)
                {
                    ImGui.TableSetColumnIndex(j);

                    var flags = ImGuiSelectableFlags.None;
                    if (j == row.Length - 1 && _disableTranslationColumn)
                    {
                        flags |= ImGuiSelectableFlags.Disabled;
                    }

                    if (ImGui.Selectable(row[j], false, flags))
                    {
                        _activeCell = (i, j);
                        _cellText = row[j];
                        _nextState = State.MenuPopup;
                    }
                }
            }

            ImGui.EndTable();
        }

        #endregion

        DrawMenuPopup();
        DrawEditTextCellModal();
        DrawAddEntryModal();
        DrawRemoveEntryModal();

        ImGui.PopStyleVar();
    }

    private void DrawMenuPopup()
    {
        if (_nextState == State.MenuPopup)
        {
            ImGui.OpenPopup("##MenuPopup");
            _nextState = State.TableView;
        }

        if (ImGui.BeginPopup("##MenuPopup"))
        {
            if (ImGui.MenuItem("Edit This Cell"))
            {
                _nextState = State.EditCell;
            }

            if (ImGui.MenuItem("Add New Entry"))
            {
                _newEntryId = "";
                _nextState = State.AddEntry;
            }

            if (ImGui.MenuItem("Delete This Entry"))
            {
                _pendingDeleteId = _textTable[_activeCell.Row][_activeCell.Column];
                _nextState = State.DeleteEntry;
            }

            ImGui.EndPopup();
        }
    }

    private void DrawRemoveEntryModal()
    {
        if (_nextState != State.DeleteEntry)
        {
            return;
        }

        _confirm.Text = $"Delete \"{_pendingDeleteId}\" from all languages?";
        _confirm.Confirmed = () =>
        {
            using (History.BeginScope("Delete text entry"))
            {
                foreach (var storage in _textStorage.Values)
                {
                    if (storage.Remove(_pendingDeleteId))
                    {
                        _pendingDeleteId = "";
                    }
                }
            }
            UpdateTableView();
        };

        _confirm.Canceled = () => { _pendingDeleteId = ""; };

        _nextState = State.TableView;
    }

    private void DrawAddEntryModal()
    {
        if (_nextState != State.AddEntry)
        {
            return;
        }

        _edit.Text = "Enter new text ID";
        _edit.EditValue = () =>
        {
            ImGuiX.InputTextMultiline("##NewId", ref _newEntryId, 256, new Vector2(-1, 40));

            var idExists = _textStorage[Language.English.GetId()].ContainsKey(_newEntryId);
            if (idExists)
            {
                ImGuiX.TextColored(new Color(1, 0.3f, 0.3f, 1), "ID already exists.");
            }

            var idEmpty = string.IsNullOrWhiteSpace(_newEntryId);
            if (idEmpty)
            {
                ImGuiX.TextColored(new Color(1, 0.3f, 0.3f, 1), "ID cannot be empty.");
            }

            return !idExists && !idEmpty;
        };

        _edit.Accepted = () =>
        {
            using (History.BeginScope("Add text entry"))
            {
                foreach (var storage in _textStorage.Values)
                {
                    storage.TryAdd(_newEntryId, "");
                }
            }

            UpdateTableView();
        };

        _nextState = State.TableView;
    }

    private void DrawEditTextCellModal()
    {
        if (_nextState != State.EditCell)
        {
            return;
        }

        _edit.Text = $"Editing {ColumnNames[_activeCell.Column]}...";
        _edit.EditValue = () =>
        {
            ImGuiX.InputTextMultiline("##edit", ref _cellText, 2048, new Vector2(-1, 240));
            return true;
        };

        _edit.Accepted = () =>
        {
            var row = _textTable[_activeCell.Row];
            switch (_activeCell.Column)
            {
                case 0: // Row
                {
                    using (History.BeginScope("Update id of text"))
                    {
                        foreach (var storage in _textStorage.Values)
                        {
                            if (storage.Remove(row[_activeCell.Column], out var text))
                            {
                                storage.Add(_cellText, text);
                            }
                        }
                    }

                    break;
                }

                case 1: // Source
                {
                    using (History.BeginScope("Update source of text"))
                    {
                        var englishStorage = _textStorage[Language.English.GetId()];
                        englishStorage[row[0]] = NormalizeLineEndings(_cellText);
                    }

                    break;
                }

                case 2: // Translation
                {
                    using (History.BeginScope("Update translation of text"))
                    {
                        var languageStorage = _textStorage[_selectedLanguage.GetId()];
                        languageStorage[row[0]] = NormalizeLineEndings(_cellText);
                    }

                    break;
                }
            }

            _activeCell = (-1, -1);
            UpdateTableView();
        };

        _edit.Canceled = () =>
        {
            _activeCell = (-1, -1);
        };

        _nextState = State.TableView;
    }

    private static string NormalizeLineEndings(string text)
    {
        return text.Replace("\r\n", "\n").Replace("\n", "\r\n");
    }

    private enum State
    {
        TableView,
        MenuPopup,
        EditCell,
        AddEntry,
        DeleteEntry
    }
}