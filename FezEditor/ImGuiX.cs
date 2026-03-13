// @formatter:off
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using FezEditor.Structure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

// ReSharper disable CheckNamespace
namespace ImGuiNET;

public static class ImGuiX
{
    #region Texture Bindings

    public static Func<Texture2D, IntPtr> Bind { get; set; } = null!;

    public static Func<Texture2D, bool> Unbind { get; set; } = null!;

    public static Func<IntPtr, Texture2D?> GetTexture { get; set; } = null!;

    #endregion

    #region Texture / Image

    public static void Image(Texture2D texture)
        => ImGui.Image(Bind(texture), new NVector2(texture.Width, texture.Height));

    public static void Image(Texture2D texture, Vector2 size)
        => ImGui.Image(Bind(texture), size.ToNumerics());

    public static void Image(Texture2D texture, Vector2 size, Vector2 uv0, Vector2 uv1)
        => ImGui.Image(Bind(texture), size.ToNumerics(), uv0.ToNumerics(), uv1.ToNumerics());

    public static void Image(Texture2D texture, Vector2 size, Vector2 uv0, Vector2 uv1, Color tintCol)
        => ImGui.Image(Bind(texture), size.ToNumerics(), uv0.ToNumerics(), uv1.ToNumerics(), tintCol.ToNumerics4());

    public static void Image(Texture2D texture, Vector2 size, Vector2 uv0, Vector2 uv1, Color tintCol, Color borderCol)
        => ImGui.Image(Bind(texture), size.ToNumerics(), uv0.ToNumerics(), uv1.ToNumerics(), tintCol.ToNumerics4(), borderCol.ToNumerics4());

    public static bool ImageButton(string strId, Texture2D texture)
        => ImGui.ImageButton(strId, Bind(texture), new NVector2(texture.Width, texture.Height));

    public static bool ImageButton(string strId, Texture2D texture, Vector2 size)
        => ImGui.ImageButton(strId, Bind(texture), size.ToNumerics());

    public static bool ImageButton(string strId, Texture2D texture, Vector2 size, Vector2 uv0, Vector2 uv1)
        => ImGui.ImageButton(strId, Bind(texture), size.ToNumerics(), uv0.ToNumerics(), uv1.ToNumerics());

    public static bool ImageButton(string strId, Texture2D texture, Vector2 size, Vector2 uv0, Vector2 uv1, Color bgCol, Color tintCol)
        => ImGui.ImageButton(strId, Bind(texture), size.ToNumerics(), uv0.ToNumerics(), uv1.ToNumerics(), bgCol.ToNumerics4(), tintCol.ToNumerics4());

    #endregion

    #region Color Edit / Picker

    public static bool ColorEdit3(string label, ref Color col)
    {
        var v = col.ToNumerics3();
        var result = ImGui.ColorEdit3(label, ref v);
        if (result) col = v.ToXnaColor();
        return result;
    }

    public static bool ColorEdit3(string label, ref Color col, ImGuiColorEditFlags flags)
    {
        var v = col.ToNumerics3();
        var result = ImGui.ColorEdit3(label, ref v, flags);
        if (result) col = v.ToXnaColor();
        return result;
    }

    public static bool ColorEdit4(string label, ref Color col)
    {
        var v = col.ToNumerics4();
        var result = ImGui.ColorEdit4(label, ref v);
        if (result) col = v.ToXnaColor();
        return result;
    }

    public static bool ColorEdit4(string label, ref Color col, ImGuiColorEditFlags flags)
    {
        var v = col.ToNumerics4();
        var result = ImGui.ColorEdit4(label, ref v, flags);
        if (result) col = v.ToXnaColor();
        return result;
    }

    public static bool ColorPicker3(string label, ref Color col)
    {
        var v = col.ToNumerics3();
        var result = ImGui.ColorPicker3(label, ref v);
        if (result) col = v.ToXnaColor();
        return result;
    }

    public static bool ColorPicker3(string label, ref Color col, ImGuiColorEditFlags flags)
    {
        var v = col.ToNumerics3();
        var result = ImGui.ColorPicker3(label, ref v, flags);
        if (result) col = v.ToXnaColor();
        return result;
    }

    public static bool ColorPicker4(string label, ref Color col)
    {
        var v = col.ToNumerics4();
        var result = ImGui.ColorPicker4(label, ref v);
        if (result) col = v.ToXnaColor();
        return result;
    }

    public static bool ColorPicker4(string label, ref Color col, ImGuiColorEditFlags flags)
    {
        var v = col.ToNumerics4();
        var result = ImGui.ColorPicker4(label, ref v, flags);
        if (result) col = v.ToXnaColor();
        return result;
    }

    public static bool ColorButton(string descId, Color col)
        => ImGui.ColorButton(descId, col.ToNumerics4());

    public static bool ColorButton(string descId, Color col, ImGuiColorEditFlags flags)
        => ImGui.ColorButton(descId, col.ToNumerics4(), flags);

    public static bool ColorButton(string descId, Color col, ImGuiColorEditFlags flags, Vector2 size)
        => ImGui.ColorButton(descId, col.ToNumerics4(), flags, size.ToNumerics());

    #endregion

    #region Drag Float Vector

    public static bool DragFloat2(string label, ref Vector2 v)
    {
        var nv = v.ToNumerics();
        var result = ImGui.DragFloat2(label, ref nv);
        if (result) v = nv.ToXna();
        return result;
    }

    public static bool DragFloat2(string label, ref Vector2 v, float vSpeed, float vMin = 0f, float vMax = 0f)
    {
        var nv = v.ToNumerics();
        var result = ImGui.DragFloat2(label, ref nv, vSpeed, vMin, vMax);
        if (result) v = nv.ToXna();
        return result;
    }

    public static bool DragFloat3(string label, ref Vector3 v)
    {
        var nv = v.ToNumerics();
        var result = ImGui.DragFloat3(label, ref nv);
        if (result) v = nv.ToXna();
        return result;
    }

    public static bool DragFloat3(string label, ref Vector3 v, float vSpeed, float vMin = 0f, float vMax = 0f)
    {
        var nv = v.ToNumerics();
        var result = ImGui.DragFloat3(label, ref nv, vSpeed, vMin, vMax);
        if (result) v = nv.ToXna();
        return result;
    }

    public static bool DragFloat4(string label, ref Vector4 v)
    {
        var nv = v.ToNumerics();
        var result = ImGui.DragFloat4(label, ref nv);
        if (result) v = nv.ToXna();
        return result;
    }

    public static bool DragFloat4(string label, ref Vector4 v, float vSpeed, float vMin = 0f, float vMax = 0f)
    {
        var nv = v.ToNumerics();
        var result = ImGui.DragFloat4(label, ref nv, vSpeed, vMin, vMax);
        if (result) v = nv.ToXna();
        return result;
    }

    #endregion

    #region Slider Float Vector

    public static bool SliderFloat2(string label, ref Vector2 v, float vMin, float vMax)
    {
        var nv = v.ToNumerics();
        var result = ImGui.SliderFloat2(label, ref nv, vMin, vMax);
        if (result) v = nv.ToXna();
        return result;
    }

    public static bool SliderFloat3(string label, ref Vector3 v, float vMin, float vMax)
    {
        var nv = v.ToNumerics();
        var result = ImGui.SliderFloat3(label, ref nv, vMin, vMax);
        if (result) v = nv.ToXna();
        return result;
    }

    public static bool SliderFloat4(string label, ref Vector4 v, float vMin, float vMax)
    {
        var nv = v.ToNumerics();
        var result = ImGui.SliderFloat4(label, ref nv, vMin, vMax);
        if (result) v = nv.ToXna();
        return result;
    }

    #endregion

    #region Input Float Vector

    public static bool InputFloat2(string label, ref Vector2 v)
    {
        var nv = v.ToNumerics();
        var result = ImGui.InputFloat2(label, ref nv);
        if (result) v = nv.ToXna();
        return result;
    }

    public static bool InputFloat3(string label, ref Vector3 v)
    {
        var nv = v.ToNumerics();
        var result = ImGui.InputFloat3(label, ref nv);
        if (result) v = nv.ToXna();
        return result;
    }

    public static bool InputFloat4(string label, ref Vector4 v)
    {
        var nv = v.ToNumerics();
        var result = ImGui.InputFloat4(label, ref nv);
        if (result) v = nv.ToXna();
        return result;
    }

    #endregion

    #region Text

    public static void TextColored(Color col, string fmt)
        => ImGui.TextColored(col.ToNumerics4(), fmt);

    #endregion

    #region Style

    public static void PushStyleColor(ImGuiCol idx, Color col)
        => ImGui.PushStyleColor(idx, col.ToNumerics4());

    public static void PushStyleVar(ImGuiStyleVar idx, Vector2 val)
        => ImGui.PushStyleVar(idx, val.ToNumerics());

    #endregion

    #region Layout / Positioning

    public static void SetNextWindowPos(Vector2 pos)
        => ImGui.SetNextWindowPos(pos.ToNumerics());

    public static void SetNextWindowPos(Vector2 pos, ImGuiCond cond)
        => ImGui.SetNextWindowPos(pos.ToNumerics(), cond);

    public static void SetNextWindowPos(Vector2 pos, ImGuiCond cond, Vector2 pivot)
        => ImGui.SetNextWindowPos(pos.ToNumerics(), cond, pivot.ToNumerics());

    public static void SetNextWindowSize(Vector2 size)
        => ImGui.SetNextWindowSize(size.ToNumerics());

    public static void SetNextWindowSize(Vector2 size, ImGuiCond cond)
        => ImGui.SetNextWindowSize(size.ToNumerics(), cond);

    public static void SetNextWindowContentSize(Vector2 size)
        => ImGui.SetNextWindowContentSize(size.ToNumerics());

    public static void SetNextWindowSizeConstraints(Vector2 sizeMin, Vector2 sizeMax)
        => ImGui.SetNextWindowSizeConstraints(sizeMin.ToNumerics(), sizeMax.ToNumerics());

    public static void SetCursorPos(Vector2 localPos)
        => ImGui.SetCursorPos(localPos.ToNumerics());

    public static void SetCursorScreenPos(Vector2 pos)
        => ImGui.SetCursorScreenPos(pos.ToNumerics());

    public static Vector2 GetCursorPos()
        => ImGui.GetCursorPos().ToXna();

    public static Vector2 GetCursorScreenPos()
        => ImGui.GetCursorScreenPos().ToXna();

    public static Vector2 GetCursorStartPos()
        => ImGui.GetCursorStartPos().ToXna();

    public static Vector2 GetWindowPos()
        => ImGui.GetWindowPos().ToXna();

    public static Vector2 GetWindowSize()
        => ImGui.GetWindowSize().ToXna();

    public static Vector2 GetContentRegionAvail()
        => ImGui.GetContentRegionAvail().ToXna();

    public static Vector2 GetContentRegionMax()
        => ImGui.GetContentRegionMax().ToXna();

    public static Vector2 GetItemRectMin()
        => ImGui.GetItemRectMin().ToXna();

    public static Vector2 GetItemRectMax()
        => ImGui.GetItemRectMax().ToXna();

    public static Vector2 GetItemRectSize()
        => ImGui.GetItemRectSize().ToXna();

    #endregion

    #region Widgets with Vector2 size

    public static bool Button(string label, Vector2 size)
        => ImGui.Button(label, size.ToNumerics());

    public static bool InvisibleButton(string strId, Vector2 size)
        => ImGui.InvisibleButton(strId, size.ToNumerics());

    public static bool InvisibleButton(string strId, Vector2 size, ImGuiButtonFlags flags)
        => ImGui.InvisibleButton(strId, size.ToNumerics(), flags);

    public static bool Selectable(string label, bool selected, ImGuiSelectableFlags flags, Vector2 size)
        => ImGui.Selectable(label, selected, flags, size.ToNumerics());

    public static bool Selectable(string label, ref bool pSelected, ImGuiSelectableFlags flags, Vector2 size)
        => ImGui.Selectable(label, ref pSelected, flags, size.ToNumerics());

    public static void Dummy(Vector2 size)
        => ImGui.Dummy(size.ToNumerics());

    public static bool BeginChild(string strId, Vector2 size)
        => ImGui.BeginChild(strId, size.ToNumerics());

    public static bool BeginChild(string strId, Vector2 size, ImGuiChildFlags childFlags)
        => ImGui.BeginChild(strId, size.ToNumerics(), childFlags);

    public static bool BeginChild(string strId, Vector2 size, ImGuiChildFlags childFlags, ImGuiWindowFlags windowFlags)
        => ImGui.BeginChild(strId, size.ToNumerics(), childFlags, windowFlags);

    public static bool BeginListBox(string label, Vector2 size)
        => ImGui.BeginListBox(label, size.ToNumerics());

    public static void ProgressBar(float fraction, Vector2 sizeArg)
        => ImGui.ProgressBar(fraction, sizeArg.ToNumerics());

    public static void ProgressBar(float fraction, Vector2 sizeArg, string overlay)
        => ImGui.ProgressBar(fraction, sizeArg.ToNumerics(), overlay);

    #endregion

    #region Mouse

    public static Vector2 GetMousePos()
        => ImGui.GetMousePos().ToXna();

    public static Vector2 GetMouseDragDelta()
        => ImGui.GetMouseDragDelta().ToXna();

    public static Vector2 GetMouseDragDelta(ImGuiMouseButton button)
        => ImGui.GetMouseDragDelta(button).ToXna();

    public static Vector2 GetMouseDragDelta(ImGuiMouseButton button, float lockThreshold)
        => ImGui.GetMouseDragDelta(button, lockThreshold).ToXna();

    public static bool IsMouseHoveringRect(Vector2 rMin, Vector2 rMax)
        => ImGui.IsMouseHoveringRect(rMin.ToNumerics(), rMax.ToNumerics());

    public static bool IsMouseHoveringRect(Vector2 rMin, Vector2 rMax, bool clip)
        => ImGui.IsMouseHoveringRect(rMin.ToNumerics(), rMax.ToNumerics(), clip);

    #endregion

    #region Misc

    public static Vector2 CalcTextSize(string text)
        => ImGui.CalcTextSize(text).ToXna();

    public static bool IsRectVisible(Vector2 size)
        => ImGui.IsRectVisible(size.ToNumerics());

    public static bool IsRectVisible(Vector2 rectMin, Vector2 rectMax)
        => ImGui.IsRectVisible(rectMin.ToNumerics(), rectMax.ToNumerics());

    public static void PushClipRect(Vector2 clipRectMin, Vector2 clipRectMax, bool intersectWithCurrentClipRect)
        => ImGui.PushClipRect(clipRectMin.ToNumerics(), clipRectMax.ToNumerics(), intersectWithCurrentClipRect);

    public static void SetWindowPos(Vector2 pos)
        => ImGui.SetWindowPos(pos.ToNumerics());

    public static void SetWindowPos(Vector2 pos, ImGuiCond cond)
        => ImGui.SetWindowPos(pos.ToNumerics(), cond);

    public static void SetWindowSize(Vector2 size)
        => ImGui.SetWindowSize(size.ToNumerics());

    public static void SetWindowSize(Vector2 size, ImGuiCond cond)
        => ImGui.SetWindowSize(size.ToNumerics(), cond);

    #endregion

    #region Extensions

    // XNA -> System.Numerics
    public static NVector2 ToNumerics(this Vector2 v) => new(v.X, v.Y);
    public static NVector3 ToNumerics(this Vector3 v) => new(v.X, v.Y, v.Z);
    public static NVector4 ToNumerics(this Vector4 v) => new(v.X, v.Y, v.Z, v.W);
    public static NVector3 ToNumerics3(this Color c) => c.ToVector3().ToNumerics();
    public static NVector4 ToNumerics4(this Color c) => c.ToVector4().ToNumerics();

    // System.Numerics -> XNA
    public static Vector2 ToXna(this NVector2 v) => new(v.X, v.Y);
    public static Vector3 ToXna(this NVector3 v) => new(v.X, v.Y, v.Z);
    public static Vector4 ToXna(this NVector4 v) => new(v.X, v.Y, v.Z, v.W);
    public static Color ToXnaColor(this NVector3 v) => new(v.X, v.Y, v.Z);
    public static Color ToXnaColor(this NVector4 v) => new(v.X, v.Y, v.Z, v.W);

    #endregion

    #region Additions

    public static void Hyperlink(string label, string url)
    {
        ImGui.Text(label);
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        ImGui.GetWindowDrawList().AddLine(
            new NVector2(min.X, max.Y), max,
            new Color(100, 150, 255, 255).PackedValue);

        if (ImGui.IsItemClicked())
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                url = url.Replace("&", "^&");
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
            else
            {
                throw new NotSupportedException("Unsupported OS");
            }
        }
    }

    public static void SetNextWindowCentered()
    {
        var center = ImGui.GetMainViewport().GetCenter();
        ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, NVector2.One / 2f);
    }

    public static void SetTextCentered(string text)
    {
        var textSize = ImGui.CalcTextSize(text);
        var windowSize = ImGui.GetContentRegionAvail();
        ImGui.SetCursorPos((windowSize - textSize) * 0.5f);
    }

    public static bool InputTextMultiline(string label, ref string input, uint maxLength, Vector2 size)
    {
        return ImGui.InputTextMultiline(label, ref input, maxLength, size.ToNumerics());
    }

    public static bool TimeSpanInput(string label, ref TimeSpan timeSpan)
    {
        const ImGuiChildFlags flags = ImGuiChildFlags.Border | ImGuiChildFlags.AutoResizeY;
        var hash = timeSpan.GetHashCode();

        var hours = timeSpan.Hours;
        var minutes = timeSpan.Minutes;
        var seconds = timeSpan.Seconds;
        var milliseconds = timeSpan.Milliseconds;

        var changed = false;
        ImGui.Text(label);
        ImGui.SameLine();

        var header = $"{timeSpan.ToString("g", CultureInfo.InvariantCulture)}##Header_{hash}";
        if (ImGui.CollapsingHeader(header))
        {
            if (BeginChild($"##Child_{hash}", Vector2.Zero, flags))
            {
                if (ImGui.InputInt("Hours", ref hours))
                {
                    changed = true;
                }

                if (ImGui.InputInt("Minutes", ref minutes))
                {
                    changed = true;
                }

                if (ImGui.InputInt("Seconds", ref seconds))
                {
                    changed = true;
                }

                if (ImGui.InputInt("Millis", ref milliseconds))
                {
                    changed = true;
                }

                ImGui.EndChild();
            }
        }

        if (changed)
        {
            timeSpan = new TimeSpan(0, hours, minutes, seconds, milliseconds);
        }

        return changed;
    }

    public static bool DateTimeInput(string label, ref DateTime dateTime)
    {
        const ImGuiChildFlags flags = ImGuiChildFlags.Border | ImGuiChildFlags.AutoResizeY;
        var hash = dateTime.GetHashCode();
        var year = dateTime.Year;
        var month = dateTime.Month;
        var day = dateTime.Day;
        var hour = dateTime.Hour;
        var minute = dateTime.Minute;
        var second = dateTime.Second;

        var changed = false;
        ImGui.Text(label);
        ImGui.SameLine();

        var header = $"{dateTime.ToString("s", CultureInfo.InvariantCulture)}##Header_{hash}";
        if (ImGui.CollapsingHeader(header))
        {
            if (BeginChild($"##Child_{hash}", Vector2.Zero, flags))
            {
                if (ImGui.InputInt("Year", ref year))
                {
                    changed = true;
                }

                if (ImGui.InputInt("Month", ref month))
                {
                    changed = true;
                }

                if (ImGui.InputInt("Day", ref day))
                {
                    changed = true;
                }

                if (ImGui.InputInt("Hour", ref hour))
                {
                    changed = true;
                }

                if (ImGui.InputInt("Minute", ref minute))
                {
                    changed = true;
                }

                if (ImGui.InputInt("Second", ref second))
                {
                    changed = true;
                }

                ImGui.EndChild();
            }
        }

        if (changed)
        {
            year = Math.Clamp(year, 1, 9999);
            month = Math.Clamp(month, 1, 12);
            day = Math.Clamp(day, 1, DateTime.DaysInMonth(year, month));
            hour = Math.Clamp(hour, 0, 23);
            minute = Math.Clamp(minute, 0, 59);
            second = Math.Clamp(second, 0, 59);

            dateTime = new DateTime(year, month, day, hour, minute, second);
        }

        return changed;
    }

    public delegate bool RenderItem<T>(int index, ref T item);

    public static bool EditableList<T>(string label, ref List<T> items, RenderItem<T> renderItem, Func<T> createNew)
    {
        const ImGuiChildFlags flags = ImGuiChildFlags.Border | ImGuiChildFlags.AutoResizeY;
        var hash = label.GetHashCode();

        var changed = false;
        ImGui.Text(label);
        ImGui.SameLine();

        var count = "item" + (items.Count is > 1 or 0 ? "s" : "");
        var header = $"List ({items.Count} {count})##Header_{hash}";
        if (ImGui.CollapsingHeader(header))
        {
            if (BeginChild($"##Child_{hash}", Vector2.Zero, flags))
            {
                if (BeginListBox($"##ListBox_{hash}", new Vector2(-1, 0)))
                {
                    for (var i = 0; i < items.Count; i++)
                    {
                        ImGui.PushID(i);
                        ImGui.SetNextItemWidth(-48);

                        var item = items[i];
                        if (renderItem(i, ref item))
                        {
                            items[i] = item;
                            changed = true;
                        }

                        ImGui.SameLine();
                        if (ImGui.Button(Icons.Close))
                        {
                            items.RemoveAt(i);
                            i--;
                            changed = true;
                        }

                        ImGui.PopID();
                    }

                    ImGui.EndListBox();
                }

                if (ImGui.Button($"{Icons.Add} Add"))
                {
                    items.Add(createNew());
                    changed = true;
                }

                ImGui.EndChild();
            }
        }

        return changed;
    }

    public delegate bool RenderKeyValuePair<in K, V>(K key, ref V value) where K : IEquatable<K>;

    public delegate bool RenderNewKey<K>(ref K key) where K : IEquatable<K>;

    public static bool EditableDict<K, V>(string label, ref Dictionary<K, V> items,
        RenderKeyValuePair<K, V> renderItem,
        RenderNewKey<K> renderNewKey,
        Func<V> createDefaultValue) where K : IEquatable<K>
    {
        const ImGuiChildFlags flags = ImGuiChildFlags.Border | ImGuiChildFlags.AutoResizeY;
        var hash = label.GetHashCode();

        var changed = false;
        ImGui.Text(label);
        ImGui.SameLine();

        var count = "key" + (items.Count is > 1 or 0 ? "s" : "");
        var header = $"Dictionary ({items.Count} {count})##Header_{hash}";
        if (ImGui.CollapsingHeader(header))
        {
            if (BeginChild($"##Child_{hash}", Vector2.Zero, flags))
            {
                if (BeginListBox($"##ListBox_{hash}", new Vector2(-1, 0)))
                {
                    var keys = items.Keys.ToList();

                    for (var i = 0; i < keys.Count; i++)
                    {
                        var key = keys[i];
                        var value = items[key];

                        ImGui.PushID(i);
                        ImGui.SetNextItemWidth(-48);

                        if (renderItem(key, ref value))
                        {
                            items[key] = value;
                            changed = true;
                        }

                        ImGui.SameLine();
                        if (ImGui.Button(Icons.Close))
                        {
                            items.Remove(key);
                            changed = true;
                        }

                        ImGui.PopID();
                    }

                    ImGui.EndListBox();
                }

                // New entry input
                K newKey = default!;
                ImGui.Button($"{Icons.Add} Add New Key");
                ImGui.SameLine();

                if (renderNewKey(ref newKey))
                {
                    if (!items.ContainsKey(newKey))
                    {
                        items.Add(newKey, createDefaultValue());
                        changed = true;
                    }
                }

                ImGui.EndChild();
            }
        }

        return changed;
    }

    public static bool SelectableWithImage(Texture2D texture, Vector2 size, string label, bool selected)
    {
        var itemHeight = Math.Max(size.Y, ImGui.GetTextLineHeight());

        // Selectable
        var clicked = Selectable($"##{label}_sel", selected, ImGuiSelectableFlags.None, new Vector2(0, itemHeight));

        // Go back to draw image and text
        ImGui.SameLine(0, 0);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (itemHeight - size.Y) * 0.5f);
        Image(texture, size);

        ImGui.SameLine();
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - (itemHeight - size.Y) * 0.5f + (itemHeight - ImGui.GetTextLineHeight()) * 0.5f);
        ImGui.Text(label);

        return clicked;
    }

    public static bool SelectableWithImage(Texture2D texture, Vector2 size, Vector2 uv0, Vector2 uv1, string label, bool selected)
    {
        var itemHeight = Math.Max(size.Y, ImGui.GetTextLineHeight());

        // Selectable
        var clicked = Selectable($"##{label}_sel", selected, ImGuiSelectableFlags.None, new Vector2(0, itemHeight));

        // Go back to draw image and text
        ImGui.SameLine(0, 0);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (itemHeight - size.Y) * 0.5f);
        Image(texture, size,  uv0, uv1);

        ImGui.SameLine();
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - (itemHeight - size.Y) * 0.5f + (itemHeight - ImGui.GetTextLineHeight()) * 0.5f);
        ImGui.Text(label);

        return clicked;
    }

    public static void DrawStats(Vector2 position, Dictionary<string, string> stats)
    {
        var dl = ImGui.GetWindowDrawList();
        var pos = position.ToNumerics();
        var lineHeight = ImGui.GetTextLineHeight();
        var padding = new NVector2(4, 4);

        // Measure max width
        var maxWidth = 0f;
        foreach (var (key, value) in stats)
        {
            var textWidth = ImGui.CalcTextSize($"{key}: {value}").X;
            if (textWidth > maxWidth) maxWidth = textWidth;
        }

        // Draw background
        var bgMin = pos - padding;
        var bgMax = pos + new NVector2(maxWidth, lineHeight * stats.Count) + padding;
        dl.AddRectFilled(bgMin, bgMax, 0xAA000000, 0f);

        // Draw text
        var i = 0;
        foreach (var (key, value) in stats)
        {
            dl.AddText(pos + new NVector2(0, lineHeight * i), 0xFFFFFFFF, $"{key}: {value}");
            i++;
        }
    }

    public static void DrawTime(Vector2 position, TimeSpan time)
    {
        var dl = ImGui.GetWindowDrawList();
        var lineHeight = ImGui.GetTextLineHeight();
        var padding = new NVector2(4, 4);

        var text = $@"{Icons.Clockface} {time:hh\:mm}";
        var textSize = ImGui.CalcTextSize(text);

        // Center horizontally around the given position
        var pos = new NVector2(position.X - textSize.X / 2f, position.Y);

        // Draw background
        var bgMin = pos - padding;
        var bgMax = pos + textSize with { Y = lineHeight } + padding;
        dl.AddRectFilled(bgMin, bgMax, 0xAA000000, 0f);

        // Draw text
        dl.AddText(pos, 0xFFFFFFFF, text);
    }

    #endregion

    #region Fonts

    public static class Fonts
    {
        public static ImFontPtr NotoSans { get; set; } = null!;

        public static ImFontPtr NotoSansJp { get; set; } = null!;

        public static ImFontPtr NotoSansKr { get; set; } = null!;

        public static ImFontPtr NotoSansTc { get; set; } = null!;
    }

    #endregion
}