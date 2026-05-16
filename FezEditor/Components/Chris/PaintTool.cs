using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Chris;

internal class PaintTool : BaseTool
{
    private readonly AppStorageService _storage;

    private readonly Color[] _palette;


    public PaintTool(Game game, IChrisEditor chris) : base(game, chris)
    {
        _storage = game.GetService<AppStorageService>();
        _palette = LoadPalette();
    }

    protected override void TestConditions()
    {
        if (Chris.CurrentTool != ChrisTool.Paint)
        {
            return;
        }

        var cancelEsc = ImGui.IsKeyPressed(ImGuiKey.Escape);
        var clickOnEmpty = Chris.IsViewportHovered &&
                           ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !Chris.Hit.HasValue;
        if (cancelEsc || clickOnEmpty)
        {
            Chris.SelectedFaces.Clear();
            Chris.SelectionOrientation = null;
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

    private IDisposable? _paintScope;

    protected override void Act()
    {
        StatusService.AddHints(
            ("LMB", "Paint")
        );

        if (!Chris.Hit.HasValue || !Chris.IsViewportHovered)
        {
            return;
        }

        if (Chris.SelectedFaces.Count > 0 &&
            Chris.SelectedFaces.Contains(Chris.Hit.Value) &&
            ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            using (Chris.History.BeginScope("Paint Trixel Selection"))
            {
                foreach (var face in Chris.SelectedFaces)
                {
                    PaintTrixel(face);
                }
            }

            return;
        }

        if (Chris.SelectedFaces.Count < 1 &&
            ImGui.IsMouseClicked(ImGuiMouseButton.Left) ||
            ImGui.IsMouseDragging(ImGuiMouseButton.Left))
        {
            _paintScope ??= Chris.History.BeginScope("Paint Single Trixels");
            PaintTrixel(Chris.Hit.Value);
        }

        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            _paintScope?.Dispose();
            _paintScope = null;
        }
    }

    protected override bool IsToolAllowed(ChrisTool tool)
    {
        return tool == ChrisTool.Paint;
    }

    private void PaintTrixel(TrixelFace face)
    {
        var obj = Chris.Obj;
        var color = Chris.PaintColor;

        var (lx, y) = face.Face switch
        {
            FaceOrientation.Front => (face.Emplacement.X, obj.Height - 1 - face.Emplacement.Y),
            FaceOrientation.Right => (obj.Depth - 1 - face.Emplacement.Z, obj.Height - 1 - face.Emplacement.Y),
            FaceOrientation.Back => (obj.Width - 1 - face.Emplacement.X, obj.Height - 1 - face.Emplacement.Y),
            FaceOrientation.Left => (face.Emplacement.Z, obj.Height - 1 - face.Emplacement.Y),
            FaceOrientation.Top => (face.Emplacement.X, face.Emplacement.Z),
            FaceOrientation.Down => (face.Emplacement.X, obj.Depth - 1 - face.Emplacement.Z),
            _ => throw new InvalidOperationException()
        };

        var faceIndex = Array.IndexOf(FaceExtensions.NaturalOrder, face.Face);
        var x = faceIndex * obj.Texture.Width / 6 + lx;
        var idx = (y * obj.Texture.Width + x) * 4;

        if (Chris.CurrentPaintMode is PaintMode.Color)
        {
            obj.Texture.TextureData[idx + 0] = color.R;
            obj.Texture.TextureData[idx + 1] = color.G;
            obj.Texture.TextureData[idx + 2] = color.B;
        }
        else if (Chris.CurrentPaintMode is PaintMode.Emission)
        {
            obj.Texture.TextureData[idx + 3] = color.A;
        }

        Chris.Trixels.Texture!.SetData(obj.Texture.TextureData);
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