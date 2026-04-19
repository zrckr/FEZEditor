using System.Text.Json;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Level;
using FezEditor.Scripting;
using FezEditor.Services;
using FEZRepacker.Core.Definitions.Game.Level.Scripting;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

internal class ScriptBrowser : IDisposable
{
    private static readonly string[] Items = new[] { "(no events)" };

    private const int Columns = 4;

    private const float RowHeight = 48f;

    private const float TriggerFormHeight = 188f;

    private const float ConditionFormHeight = 188f;

    private const float ActionFormHeight = 188f;

    private readonly Game _game;

    private readonly Level _level;

    private readonly IEddyEditor _eddy;

    private readonly InputService _input;

    private readonly ConfirmWindow _confirm;

    private Script? _script;

    private int _id;

    private int _triggerIndex = -1;

    private int _conditionIndex = -1;

    private int _actionIndex = -1;

    private Entity? _pickTarget;

    public ScriptBrowser(Game game, Level level, IEddyEditor eddy)
    {
        _game = game;
        _level = level;
        _eddy = eddy;
        _input = game.GetService<InputService>();
        game.AddComponent(_confirm = new ConfirmWindow(game));
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _game.RemoveComponent(_confirm);
    }

    public void Draw()
    {
        PollInstanceBrowserPick();
        DrawTable();
        DrawEditorWindow();
    }

    private void PollInstanceBrowserPick()
    {
        if (_pickTarget == null || !_eddy.InstanceBrowser.Select(out var selection))
        {
            return;
        }

        _eddy.InstanceBrowser.Consume();
        using (_eddy.History.BeginScope("Pick Entity Identifier"))
        {
            _pickTarget.Identifier = selection.id;
        }

        _pickTarget = null;
    }

    #region Table

    private void DrawTable()
    {
        // Disable mouse capturing for calling context menu via RMB
        _input.CaptureMouse(false);

        var tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.Sortable | ImGuiTableFlags.ScrollY;
        var tableSize = new NVector2(0, ImGui.GetContentRegionAvail().Y - ImGui.GetFrameHeightWithSpacing());

        if (ImGui.BeginTable("##ScriptList", Columns, tableFlags, tableSize))
        {
            ImGuiX.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(8, 8));
            ImGui.TableSetupColumn("Id", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Triggers", ImGuiTableColumnFlags.NoSort);
            ImGui.TableSetupColumn("Conditions", ImGuiTableColumnFlags.NoSort);
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.NoSort);
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableHeadersRow();
            ImGui.PopStyleVar();

            foreach (var (id, script) in _level.Scripts.ToArray())
            {
                ImGui.TableNextRow(ImGuiTableRowFlags.None, RowHeight);
                if (ImGui.IsPopupOpen($"##ScriptCtx{id}"))
                {
                    ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(ImGuiCol.HeaderActive));
                }

                ImGui.TableSetColumnIndex(0);
                ImGui.Selectable($"{id}##sel{id}", false, ImGuiSelectableFlags.SpanAllColumns,
                    new NVector2(0, RowHeight));
                if (ImGui.BeginPopupContextItem($"##ScriptCtx{id}"))
                {
                    if (ImGui.MenuItem($"{Lucide.Plus} Add New"))
                    {
                        CreateNewScript();
                    }

                    if (ImGui.MenuItem($"{Lucide.Pencil} Edit"))
                    {
                        OpenEditor(id, script);
                    }

                    if (ImGui.MenuItem($"{Lucide.Copy} Clone"))
                    {
                        var nextId = _level.Scripts.Keys.DefaultIfEmpty(-1).Max() + 1;
                        var json = JsonSerializer.Serialize(script);
                        var clone = JsonSerializer.Deserialize<Script>(json)!;
                        _level.Scripts.Add(nextId, clone);
                    }

                    if (ImGui.MenuItem($"{Lucide.X} Delete"))
                    {
                        _confirm.Title = "Script Browser";
                        _confirm.Text = "Delete this script?";
                        _confirm.ConfirmButtonText = "Yes";
                        _confirm.DenyButtonText = "No";
                        _confirm.Confirmed = () => _level.Scripts.Remove(id);
                        _confirm.Denied = null;
                    }

                    ImGui.EndPopup();
                }

                ImGui.TableSetColumnIndex(1);
                ImGui.Text(TruncateLines(script.Triggers.Select(st => st.Stringify())));

                ImGui.TableSetColumnIndex(2);
                ImGui.Text(TruncateLines(script.Conditions.Select(sc => sc.Stringify())));

                ImGui.TableSetColumnIndex(3);
                ImGui.Text(TruncateLines(script.Actions.Select(sa => sa.Stringify())));
            }

            ImGui.EndTable();
        }

        DrawTableFooter();
    }

    private static string TruncateLines(IEnumerable<string> lines, int max = 3)
    {
        var list = lines.ToList();
        if (list.Count <= max)
        {
            return string.Join("\n", list);
        }

        var head = list.Take(max / 2);
        var tail = list.TakeLast(max / 2);
        return string.Join("\n", head) + "\n...\n" + string.Join("\n", tail);
    }

    private void DrawTableFooter()
    {
        if (ImGui.Button($"{Lucide.Plus} Add new"))
        {
            CreateNewScript();
        }
    }

    private void CreateNewScript()
    {
        var nextId = _level.Scripts.Keys.DefaultIfEmpty(-1).Max() + 1;
        _level.Scripts.Add(nextId, new Script());
        OpenEditor(nextId, _level.Scripts[nextId]);
    }

    #endregion

    #region Editor Window

    private void OpenEditor(int id, Script script)
    {
        _id = id;
        _script = script;
        _triggerIndex = -1;
        _conditionIndex = -1;
        _actionIndex = -1;
    }

    private void DrawEditorWindow()
    {
        if (_script == null)
        {
            return;
        }

        var title = $"Edit {_script.Name} ({_id}) script##ScriptEditor";
        var open = true;

        ImGuiX.SetNextWindowSize(new Vector2(960, 640), ImGuiCond.FirstUseEver);
        if (ImGui.Begin(title, ref open, ImGuiWindowFlags.NoCollapse))
        {
            DrawScriptHeader();

            var availSize = ImGui.GetContentRegionAvail();
            var width = availSize.X / 3f;

            if (ImGuiX.BeginChild("##Triggers", new Vector2(width, 0), ImGuiChildFlags.Border))
            {
                DrawTriggers();
                ImGui.EndChild();
            }

            ImGui.SameLine();

            if (ImGuiX.BeginChild("##Conditions", new Vector2(width, 0), ImGuiChildFlags.Border))
            {
                DrawConditions();
                ImGui.EndChild();
            }

            ImGui.SameLine();

            if (ImGuiX.BeginChild("##Actions", Vector2.Zero, ImGuiChildFlags.Border))
            {
                DrawActions();
                ImGui.EndChild();
            }

            ImGui.End();
        }

        if (!open)
        {
            _script = null;
        }
    }

    private void DrawScriptHeader()
    {
        ImGui.TextDisabled($"#{_id}");
        ImGui.SameLine();

        ImGui.SetNextItemWidth(160f);
        var name = _script!.Name;
        if (ImGui.InputText("##Name", ref name, 255))
        {
            using (_eddy.History.BeginScope("Edit Script Name"))
            {
                _script.Name = name;
            }
        }

        ImGui.SameLine();

        var hasTimeout = _script.Timeout.HasValue;
        if (ImGui.Checkbox("Timeout##hdr", ref hasTimeout))
        {
            using (_eddy.History.BeginScope("Edit Script Timeout Flag"))
            {
                _script.Timeout = hasTimeout ? TimeSpan.Zero : null;
            }
        }

        ImGui.SameLine();
        ImGui.BeginDisabled(!_script.Timeout.HasValue);
        ImGui.SetNextItemWidth(64f);
        var timeout = (float)(_script.Timeout?.TotalSeconds ?? 0d);
        if (ImGui.InputFloat("s##timeout", ref timeout, 0f, 0f, "%.1f"))
        {
            using (_eddy.History.BeginScope("Edit Script Timeout Value"))
            {
                _script.Timeout = TimeSpan.FromSeconds(timeout);
            }
        }

        ImGui.EndDisabled();

        ImGui.SameLine();

        var oneTime = _script.OneTime;
        if (ImGui.Checkbox("One-Time##hdr", ref oneTime))
        {
            using (_eddy.History.BeginScope("Edit Script OneTime"))
            {
                _script.OneTime = oneTime;
            }
        }

        ImGui.SameLine();
        ImGui.BeginDisabled(!oneTime);
        var levelWideOnly = _script.LevelWideOneTime;
        if (ImGui.Checkbox("Level-Wide##hdr", ref levelWideOnly))
        {
            using (_eddy.History.BeginScope("Edit Script LevelWideOneTime"))
            {
                _script.LevelWideOneTime = levelWideOnly;
            }
        }

        ImGui.EndDisabled();

        ImGui.SameLine();

        var disabled = _script.Disabled;
        if (ImGui.Checkbox("Disabled##hdr", ref disabled))
        {
            using (_eddy.History.BeginScope("Edit Script Disabled"))
            {
                _script.Disabled = disabled;
            }
        }

        ImGui.SameLine();

        var triggerless = _script.Triggerless;
        if (ImGui.Checkbox("Triggerless##hdr", ref triggerless))
        {
            using (_eddy.History.BeginScope("Edit Script Triggerless"))
            {
                _script.Triggerless = triggerless;
            }
        }

        ImGui.SameLine();

        var ignoreEndTriggers = _script.IgnoreEndTriggers;
        if (ImGui.Checkbox("Ignore End-Triggers##hdr", ref ignoreEndTriggers))
        {
            using (_eddy.History.BeginScope("Edit Script Ignore End-Triggers"))
            {
                _script.IgnoreEndTriggers = ignoreEndTriggers;
            }
        }

        ImGui.SameLine();

        var isWinCondition = _script.IsWinCondition;
        if (ImGui.Checkbox("Completion Condition##hdr", ref isWinCondition))
        {
            using (_eddy.History.BeginScope("Edit Script Completion Condition"))
            {
                _script.IsWinCondition = isWinCondition;
            }
        }
    }

    private static ScriptApiEntry? FindEntry(string typeName)
    {
        return Array.Find(ScriptingApi.Entries, e => e.TypeName == typeName);
    }

    private void DrawEntityFields(Entity entity, ref string dependentField, string scopeLabel)
    {
        var typeNames = Array.ConvertAll(ScriptingApi.Entries, e => e.TypeName);
        var typeIdx = Math.Max(0, Array.IndexOf(typeNames, entity.Type));

        if (ImGui.Combo("Entity Type", ref typeIdx, typeNames, typeNames.Length))
        {
            using (_eddy.History.BeginScope($"Change {scopeLabel} Entity Type"))
            {
                entity.Type = typeNames[typeIdx];
                dependentField = "";
                var newEntry = FindEntry(entity.Type);
                if (newEntry is { IsStatic: true })
                {
                    entity.Identifier = null;
                }
            }
        }

        var entry = FindEntry(entity.Type);
        if (entry is not { IsStatic: true })
        {
            var id = entity.Identifier ?? 0;
            if (ImGui.InputInt("Identifier", ref id))
            {
                using (_eddy.History.BeginScope($"Change {scopeLabel} Entity Identifier"))
                {
                    entity.Identifier = id;
                }
            }

            var context = GetEntityContext(entity.Type);
            if (context.HasValue)
            {
                var isPicking = _pickTarget == entity;
                if (isPicking)
                {
                    ImGui.BeginDisabled();
                }

                if (ImGui.Button($"{Lucide.Target} Pick##{scopeLabel}"))
                {
                    _pickTarget = entity;
                }

                if (isPicking)
                {
                    ImGui.EndDisabled();
                    ImGui.SameLine();
                    ImGui.TextDisabled("Click instance in browser...");
                }
            }
        }
    }

    private static EddyContext? GetEntityContext(string typeName) => typeName switch
    {
        "ArtObject" => EddyContext.ArtObject,
        "Group" or "RotatingGroup" or "SuckBlock" or "Switch" or "SpinBlock" => EddyContext.Trile,
        "Npc" => EddyContext.NonPlayableCharacter,
        "Volume" => EddyContext.Volume,
        "Path" => EddyContext.Path,
        "Script" => EddyContext.Script,
        "Plane" => EddyContext.BackgroundPlane,
        _ => null
    };

    private void DrawTriggers()
    {
        ImGui.Text("Triggers (WHEN)");
        ImGui.Separator();

        if (ImGui.Button($"{Lucide.Plus} Add"))
        {
            using (_eddy.History.BeginScope("Add Trigger"))
            {
                _script!.Triggers.Add(new ScriptTrigger());
                _triggerIndex = _script.Triggers.Count - 1;
            }
        }

        ImGui.SameLine();
        ImGui.BeginDisabled(_triggerIndex == -1);
        if (ImGui.Button($"{Lucide.Copy} Clone"))
        {
            using (_eddy.History.BeginScope("Clone Trigger"))
            {
                var clone = JsonSerializer.Deserialize<ScriptTrigger>(
                    JsonSerializer.Serialize(_script!.Triggers[_triggerIndex]))!;
                _script.Triggers.Add(clone);
                _triggerIndex = _script.Triggers.Count - 1;
            }
        }

        ImGui.SameLine();
        if (ImGui.Button($"{Lucide.Trash2} Remove"))
        {
            using (_eddy.History.BeginScope("Remove Trigger"))
            {
                _script!.Triggers.RemoveAt(_triggerIndex);
                _triggerIndex = -1;
            }
        }

        ImGui.EndDisabled();

        ImGui.Separator();

        if (ImGuiX.BeginChild("##TriggerList", new Vector2(0, -TriggerFormHeight)))
        {
            if (ImGui.IsWindowHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !ImGui.IsAnyItemHovered())
            {
                _triggerIndex = -1;
            }

            if (_script!.Triggers.Count == 0)
            {
                const string empty = "No triggers...";
                ImGuiX.SetTextCentered(empty);
                ImGui.Text(empty);
            }

            for (var i = 0; i < _script.Triggers.Count; i++)
            {
                var trigger = _script.Triggers[i];
                if (ImGui.Selectable(trigger.Stringify() + $"##{i}", _triggerIndex == i))
                {
                    _triggerIndex = i;
                }
            }

            ImGui.EndChild();
        }

        ImGui.Separator();

        if (ImGuiX.BeginChild("##TriggerForm", Vector2.Zero))
        {
            if (_triggerIndex >= 0 && _triggerIndex < _script!.Triggers.Count)
            {
                var t = _script.Triggers[_triggerIndex];

                var tEvent = t.Event;
                DrawEntityFields(t.Object, ref tEvent, "Trigger");
                if (!string.Equals(tEvent, t.Event, StringComparison.Ordinal))
                {
                    t.Event = tEvent;
                }

                var triggerEntry = FindEntry(t.Object.Type);
                var eventNames = triggerEntry != null
                    ? Array.ConvertAll(triggerEntry.Triggers, tr => tr.Name)
                    : Array.Empty<string>();

                if (eventNames.Length > 0)
                {
                    var eventIdx = Math.Max(0, Array.IndexOf(eventNames, t.Event));
                    var currentEvent = eventIdx < eventNames.Length ? eventNames[eventIdx] : "";
                    if (ImGui.BeginCombo("Event", currentEvent))
                    {
                        for (var ei = 0; ei < eventNames.Length; ei++)
                        {
                            var selected = ei == eventIdx;
                            if (ImGui.Selectable(eventNames[ei], selected))
                            {
                                using (_eddy.History.BeginScope("Change Trigger Event"))
                                {
                                    t.Event = eventNames[ei];
                                }
                            }

                            var evDesc = triggerEntry!.Triggers[ei].Description;
                            if (evDesc != null)
                            {
                                ImGui.SetItemTooltip(evDesc);
                            }

                            if (selected)
                            {
                                ImGui.SetItemDefaultFocus();
                            }
                        }

                        ImGui.EndCombo();
                    }
                }
                else
                {
                    ImGui.BeginDisabled();
                    var noEventsIdx = 0;
                    ImGui.Combo("Event", ref noEventsIdx, Items, 1);
                    ImGui.EndDisabled();
                }
            }

            ImGui.EndChild();
        }
    }

    private void DrawConditions()
    {
        ImGui.Text("Conditions (IF)");
        ImGui.Separator();

        if (ImGui.Button($"{Lucide.Plus} Add"))
        {
            using (_eddy.History.BeginScope("Add Condition"))
            {
                _script!.Conditions.Add(new ScriptCondition());
                _conditionIndex = _script.Conditions.Count - 1;
            }
        }

        ImGui.SameLine();
        ImGui.BeginDisabled(_conditionIndex == -1);
        if (ImGui.Button($"{Lucide.Copy} Clone"))
        {
            using (_eddy.History.BeginScope("Clone Condition"))
            {
                var clone = JsonSerializer.Deserialize<ScriptCondition>(
                    JsonSerializer.Serialize(_script!.Conditions[_conditionIndex]))!;
                _script.Conditions.Add(clone);
                _conditionIndex = _script.Conditions.Count - 1;
            }
        }

        ImGui.SameLine();
        if (ImGui.Button($"{Lucide.Trash2} Remove"))
        {
            using (_eddy.History.BeginScope("Remove Condition"))
            {
                _script!.Conditions.RemoveAt(_conditionIndex);
                _conditionIndex = -1;
            }
        }

        ImGui.EndDisabled();

        ImGui.Separator();

        if (ImGuiX.BeginChild("##ConditionList", new Vector2(0, -ConditionFormHeight)))
        {
            if (ImGui.IsWindowHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !ImGui.IsAnyItemHovered())
            {
                _conditionIndex = -1;
            }

            if (_script!.Conditions.Count == 0)
            {
                const string empty = "No conditions...";
                ImGuiX.SetTextCentered(empty);
                ImGui.Text(empty);
            }

            for (var i = 0; i < _script.Conditions.Count; i++)
            {
                var cond = _script.Conditions[i];
                if (ImGui.Selectable(cond.Stringify() + $"##{i}", _conditionIndex == i))
                {
                    _conditionIndex = i;
                }
            }

            ImGui.EndChild();
        }

        ImGui.Separator();

        if (ImGuiX.BeginChild("##ConditionForm", Vector2.Zero))
        {
            if (_conditionIndex >= 0 && _conditionIndex < _script!.Conditions.Count)
            {
                var c = _script.Conditions[_conditionIndex];

                var cProp = c.Property;
                DrawEntityFields(c.Object, ref cProp, "Condition");
                if (!string.Equals(cProp, c.Property, StringComparison.Ordinal))
                {
                    c.Property = cProp;
                }

                var condEntry = FindEntry(c.Object.Type);
                var propNames = condEntry != null
                    ? Array.ConvertAll(condEntry.Conditions, cd => cd.Name)
                    : Array.Empty<string>();

                if (propNames.Length > 0)
                {
                    var propIdx = Math.Max(0, Array.IndexOf(propNames, c.Property));
                    var currentProp = propIdx < propNames.Length ? propNames[propIdx] : "";
                    if (ImGui.BeginCombo("Property", currentProp))
                    {
                        for (var pi = 0; pi < propNames.Length; pi++)
                        {
                            var selected = pi == propIdx;
                            if (ImGui.Selectable(propNames[pi], selected))
                            {
                                using (_eddy.History.BeginScope("Change Condition Property"))
                                {
                                    c.Property = propNames[pi];
                                }
                            }

                            var propDesc = condEntry!.Conditions[pi].Description;
                            if (propDesc != null)
                            {
                                ImGui.SetItemTooltip(propDesc);
                            }

                            if (selected)
                            {
                                ImGui.SetItemDefaultFocus();
                            }
                        }

                        ImGui.EndCombo();
                    }
                }

                // Display symbols parallel to Enum.GetNames order
                var operatorNames = Enum.GetNames<ComparisonOperator>();
                var operatorDisplays = new[] { "?", "==", ">", ">=", "<", "<=", "!=" };
                var operatorIdx = Math.Max(0, Array.IndexOf(operatorNames, c.Operator.ToString()));
                if (ImGui.Combo("Operator", ref operatorIdx, operatorDisplays, operatorDisplays.Length))
                {
                    using (_eddy.History.BeginScope("Change Condition Operator"))
                    {
                        c.Operator = Enum.Parse<ComparisonOperator>(operatorNames[operatorIdx]);
                    }
                }

                var value = c.Value;
                if (ImGui.InputText("Value", ref value, 255))
                {
                    using (_eddy.History.BeginScope("Change Condition Value"))
                    {
                        c.Value = value;
                    }
                }
            }

            ImGui.EndChild();
        }
    }

    private void DrawActions()
    {
        ImGui.Text("Actions (WHAT)");
        ImGui.Separator();

        if (ImGui.Button($"{Lucide.Plus} Add"))
        {
            using (_eddy.History.BeginScope("Add Action"))
            {
                _script!.Actions.Add(new ScriptAction());
                _actionIndex = _script.Actions.Count - 1;
            }
        }

        ImGui.SameLine();
        ImGui.BeginDisabled(_actionIndex == -1);
        if (ImGui.Button($"{Lucide.Copy} Clone"))
        {
            using (_eddy.History.BeginScope("Clone Action"))
            {
                var clone = JsonSerializer.Deserialize<ScriptAction>(
                    JsonSerializer.Serialize(_script!.Actions[_actionIndex]))!;
                _script.Actions.Add(clone);
                _actionIndex = _script.Actions.Count - 1;
            }
        }

        ImGui.SameLine();
        if (ImGui.Button($"{Lucide.Trash2} Remove"))
        {
            using (_eddy.History.BeginScope("Remove Action"))
            {
                _script!.Actions.RemoveAt(_actionIndex);
                _actionIndex = -1;
            }
        }

        ImGui.EndDisabled();

        ImGui.Separator();

        if (ImGuiX.BeginChild("##ActionList", new Vector2(0, -ActionFormHeight)))
        {
            if (ImGui.IsWindowHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !ImGui.IsAnyItemHovered())
            {
                _actionIndex = -1;
            }

            if (_script!.Actions.Count == 0)
            {
                const string empty = "No actions...";
                ImGuiX.SetTextCentered(empty);
                ImGui.Text(empty);
            }

            for (var i = 0; i < _script.Actions.Count; i++)
            {
                var action = _script.Actions[i];
                if (ImGui.Selectable(action.Stringify() + $"##{i}", _actionIndex == i))
                {
                    _actionIndex = i;
                }
            }

            ImGui.EndChild();
        }

        ImGui.Separator();

        if (ImGuiX.BeginChild("##ActionForm", Vector2.Zero, ImGuiChildFlags.None, ImGuiWindowFlags.HorizontalScrollbar))
        {
            if (_actionIndex >= 0 && _actionIndex < _script!.Actions.Count)
            {
                var a = _script.Actions[_actionIndex];

                var aOp = a.Operation;
                DrawEntityFields(a.Object, ref aOp, "Action");
                if (!string.Equals(aOp, a.Operation, StringComparison.Ordinal))
                {
                    a.Operation = aOp;
                }

                var actionEntry = FindEntry(a.Object.Type);
                var opNames = actionEntry != null
                    ? Array.ConvertAll(actionEntry.Actions, ac => ac.Name)
                    : Array.Empty<string>();

                if (opNames.Length > 0)
                {
                    var opIdx = Math.Max(0, Array.IndexOf(opNames, a.Operation));
                    var currentOp = opIdx < opNames.Length ? opNames[opIdx] : "";
                    if (ImGui.BeginCombo("Operation", currentOp))
                    {
                        for (var oi = 0; oi < opNames.Length; oi++)
                        {
                            var selected = oi == opIdx;
                            if (ImGui.Selectable(opNames[oi], selected))
                            {
                                using (_eddy.History.BeginScope("Change Action Operation"))
                                {
                                    a.Operation = opNames[oi];
                                    var newActionDef = actionEntry!.Actions[oi];
                                    var expectedCount = newActionDef.Parameters.Length;
                                    var newArgs = new string[expectedCount];
                                    for (var i = 0; i < expectedCount; i++)
                                    {
                                        newArgs[i] = i < a.Arguments.Length ? a.Arguments[i] : "";
                                    }

                                    a.Arguments = newArgs;
                                }
                            }

                            var opDesc = actionEntry!.Actions[oi].Description;
                            if (opDesc != null)
                            {
                                ImGui.SetItemTooltip(opDesc);
                            }

                            if (selected)
                            {
                                ImGui.SetItemDefaultFocus();
                            }
                        }

                        ImGui.EndCombo();
                    }
                }

                var killSwitch = a.Killswitch;
                if (ImGui.Checkbox("Kill-switch", ref killSwitch))
                {
                    using (_eddy.History.BeginScope("Change Action Kill-switch"))
                    {
                        a.Killswitch = killSwitch;
                    }
                }

                ImGui.SameLine();

                var blocking = a.Blocking;
                if (ImGui.Checkbox("Stop-and-Wait Before", ref blocking))
                {
                    using (_eddy.History.BeginScope("Change Action Blocking"))
                    {
                        a.Blocking = blocking;
                    }
                }

                var currentActionDef = actionEntry?.Actions
                    .FirstOrDefault(ac => ac.Name == a.Operation);

                if (currentActionDef is { Parameters.Length: > 0 })
                {
                    if (a.Arguments.Length != currentActionDef.Parameters.Length)
                    {
                        var synced = new string[currentActionDef.Parameters.Length];
                        for (var i = 0; i < synced.Length; i++)
                        {
                            synced[i] = i < a.Arguments.Length ? a.Arguments[i] : "";
                        }

                        a.Arguments = synced;
                    }

                    ImGui.SeparatorText("Arguments");

                    for (var i = 0; i < currentActionDef.Parameters.Length; i++)
                    {
                        var param = currentActionDef.Parameters[i];
                        var arg = a.Arguments[i];
                        if (ImGui.InputText($"{param.Name}##{i}", ref arg, 255))
                        {
                            using (_eddy.History.BeginScope($"Change Action Argument [{param.Name}]"))
                            {
                                a.Arguments[i] = arg;
                            }
                        }
                    }
                }
            }

            ImGui.EndChild();
        }
    }

    #endregion
}