using FezEditor.Services;
using FezEditor.Tools;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Chris;

internal class PickTool : TextureTool
{
    private const int CommonCount = 12;

    private const int RecentCount = 12;

    private readonly AppStorageService _storage;

    private Color[] _commonColors = new Color[CommonCount];

    private byte[]? _lastTextureData;

    private readonly Color[] _recentColors = new Color[RecentCount];

    private int _recentCount;

    private ChrisTool _lastTextureTool = ChrisTool.Paint;

    public PickTool(Game game, IChrisEditor chris) : base(game, chris)
    {
        _storage = game.GetService<AppStorageService>();
        LoadRecentColors();
    }

    protected override void TestConditions()
    {
        var textureData = Chris.Obj.Texture.TextureData;
        if (!ReferenceEquals(textureData, _lastTextureData))
        {
            _lastTextureData = textureData;
            _commonColors = ComputeCommonColors(textureData);
        }

        if (Chris.CurrentTool != ChrisTool.Pick && Chris.CurrentTool.IsTextureTool())
        {
            _lastTextureTool = Chris.CurrentTool;
        }
    }

    public override void DrawOverlay()
    {
        if (Chris.CurrentTool == ChrisTool.Pick && !ImGui.IsPopupOpen("##PaintPicker"))
        {
            Chris.CurrentTool = _lastTextureTool;
        }

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

        ImGui.Text("Select a color from color palette");
        ImGui.Text("or click on trixel to pick it.");
        ImGui.Separator();

        var paintColor = Chris.PaintColor;

        if (ImGuiX.ColorPicker3("##Picker", ref paintColor,
                ImGuiColorEditFlags.NoSidePreview | ImGuiColorEditFlags.NoSmallPreview))
        {
            Chris.PaintColor = paintColor;
        }

        const ImGuiColorEditFlags swatchFlags = ImGuiColorEditFlags.NoAlpha | ImGuiColorEditFlags.NoPicker |
                                                ImGuiColorEditFlags.NoTooltip;
        var spacing = ImGui.GetStyle().ItemSpacing.Y;

        ImGui.Separator();
        ImGui.Text("Common");
        for (var n = 0; n < CommonCount; n++)
        {
            ImGui.PushID(1000 + n);
            if (n != 0)
            {
                ImGui.SameLine(0f, spacing);
            }

            if (ImGuiX.ColorButton("##Common", _commonColors[n], swatchFlags, new Vector2(24, 24)))
            {
                var c = _commonColors[n];
                Chris.PaintColor = new Color(c.R, c.G, c.B, Chris.PaintColor.A);
            }

            ImGui.PopID();
        }

        ImGui.Text("Recent");
        for (var n = 0; n < RecentCount; n++)
        {
            ImGui.PushID(2000 + n);
            if (n != 0)
            {
                ImGui.SameLine(0f, spacing);
            }

            if (n >= _recentCount)
            {
                ImGui.BeginDisabled();
                ImGuiX.ColorButton("##Recent", Color.Transparent, swatchFlags, new Vector2(24, 24));
                ImGui.EndDisabled();
            }
            else if (ImGuiX.ColorButton("##Recent", _recentColors[n], swatchFlags, new Vector2(24, 24)))
            {
                var c = _recentColors[n];
                Chris.PaintColor = new Color(c.R, c.G, c.B, Chris.PaintColor.A);
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
        var picked = GetTrixelColor(face);
        Chris.PaintColor = picked;
        PushRecentColor(new Color(picked.R, picked.G, picked.B, byte.MaxValue));
    }

    protected override bool IsToolAllowed(ChrisTool tool)
    {
        return tool == ChrisTool.Pick;
    }

    private void PushRecentColor(Color color)
    {
        if (_recentCount > 0 && _recentColors[0] == color)
        {
            return;
        }

        var limit = Math.Min(_recentCount, RecentCount - 1);
        for (var i = limit; i > 0; i--)
        {
            _recentColors[i] = _recentColors[i - 1];
        }

        _recentColors[0] = color;
        if (_recentCount < RecentCount)
        {
            _recentCount++;
        }

        _storage.SavePaintPalette(_recentColors[..RecentCount]);
    }

    private void LoadRecentColors()
    {
        _recentCount = 0;
        for (var n = 0; n < RecentCount && n < _storage.PaintPalette.Length; n++)
        {
            if (_storage.PaintPalette[n] != default)
            {
                _recentColors[n] = _storage.PaintPalette[n];
                _recentCount++;
            }
        }
    }

    private static Color[] ComputeCommonColors(byte[] textureData)
    {
        var counts = new Dictionary<(byte r, byte g, byte b), int>();
        for (var i = 0; i + 3 < textureData.Length; i += 4)
        {
            var key = (textureData[i], textureData[i + 1], textureData[i + 2]);
            counts.TryGetValue(key, out var existing);
            counts[key] = existing + 1;
        }

        var result = new Color[CommonCount];
        var n = 0;
        foreach (var (color, _) in counts.OrderByDescending(kv => kv.Value))
        {
            if (n < CommonCount)
            {
                result[n++] = new Color(color.r, color.g, color.b, byte.MaxValue);
            }
        }

        return result;
    }
}