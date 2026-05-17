using FezEditor.Services;
using FezEditor.Tools;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Chris;

internal class PickTool : TextureTool
{
    private readonly AppStorageService _storage;

    private readonly Color[] _palette;


    public PickTool(Game game, IChrisEditor chris) : base(game, chris)
    {
        _storage = game.GetService<AppStorageService>();
        _palette = LoadPalette();
    }

    protected override void TestConditions()
    {
        if (Chris.CurrentTool != ChrisTool.Pick)
        {
            return;
        }

        if (ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            Chris.CurrentTool = ChrisTool.Select;
        }
    }

    public override void DrawOverlay()
    {
        if (!ImGui.BeginPopup("##PaintPicker"))
        {
            return;
        }

        if (Chris.CurrentPaintMode is PaintMode.Emission)
        {
            int paintAlpha = Chris.PaintColor.A;
            ImGui.SliderInt("##PaintEmissionValue", ref paintAlpha, byte.MinValue, byte.MaxValue);
            Chris.PaintColor = new Color(Chris.PaintColor.R, Chris.PaintColor.G, Chris.PaintColor.B, paintAlpha);
            ImGui.EndPopup();
            return;
        }

        var paintColor = Chris.PaintColor;

        if (ImGuiX.ColorPicker3("##Picker", ref paintColor,
                ImGuiColorEditFlags.NoSidePreview | ImGuiColorEditFlags.NoSmallPreview))
        {
            Chris.PaintColor = paintColor;
        }

        ImGui.Separator();
        ImGui.Text("Palette");
        for (var n = 0; n < _palette.Length; n++)
        {
            ImGui.PushID(n);
            if (n % 8 != 0)
            {
                ImGui.SameLine(0f, ImGui.GetStyle().ItemSpacing.Y);
            }

            const ImGuiColorEditFlags paletteFlags = ImGuiColorEditFlags.NoAlpha | ImGuiColorEditFlags.NoPicker |
                                                     ImGuiColorEditFlags.NoTooltip;
            if (ImGuiX.ColorButton("##Palette", _palette[n], paletteFlags, new Vector2(24, 24)))
            {
                Chris.PaintColor = new Color(_palette[n].R, _palette[n].G, _palette[n].B, Chris.PaintColor.A);
            }

            if (ImGui.BeginDragDropTarget())
            {
                var changed = false;
                unsafe
                {
                    var payload = ImGui.AcceptDragDropPayload("_COL3F");
                    if (payload.NativePtr != null)
                    {
                        var data = (float*)payload.Data;
                        _palette[n] = new Color(data[0], data[1], data[2], 1f);
                        changed = true;
                    }

                    payload = ImGui.AcceptDragDropPayload("_COL4F");
                    if (payload.NativePtr != null)
                    {
                        var data = (float*)payload.Data;
                        _palette[n] = new Color(data[0], data[1], data[2], data[3]);
                        changed = true;
                    }
                }

                if (changed)
                {
                    _storage.SavePaintPalette(_palette);
                }

                ImGui.EndDragDropTarget();
            }

            ImGui.PopID();
        }

        ImGui.EndPopup();
    }

    protected override void Act()
    {
        StatusService.AddHints(
            ("LMB", "Pick Color")
        );

        if (!Chris.Hit.HasValue || !ImGui.IsMouseClicked(ImGuiMouseButton.Left) || !Chris.IsViewportHovered)
        {
            return;
        }

        var face = Chris.Hit!.Value;
        Chris.PaintColor = GetTrixelColor(face);
        Chris.CurrentTool = ChrisTool.Paint;
    }

    protected override bool IsToolAllowed(ChrisTool tool)
    {
        return tool == ChrisTool.Pick;
    }

    private Color[] LoadPalette()
    {
        var palette = new Color[32];
        var generated = false;

        for (var n = 0; n < palette.Length; n++)
        {
            if (n < _storage.PaintPalette.Length)
            {
                palette[n] = _storage.PaintPalette[n];
            }
            else
            {
                ImGui.ColorConvertHSVtoRGB(n / 31f, 0.8f, 0.8f, out var r, out var g, out var b);
                palette[n] = new Color(r, g, b, 1f);
                generated = true;
            }
        }

        if (generated)
        {
            _storage.SavePaintPalette(palette);
        }

        return palette;
    }
}