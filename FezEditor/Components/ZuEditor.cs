using FezEditor.Structure;
using FezEditor.Tools;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Components;

public class ZuEditor : EditorComponent
{
    public override object Asset => _font;

    private readonly FezFont _font;

    private Texture2D _fontTexture = null!;
    
    private IntPtr _fontTexturePtr;
    
    private ImFontPtr _charactersFont;

    private int _selectedIndex = -1;

    private int _lastSelectedIndex = -1;

    private float _baseZoom = 1.0f;

    private float _zoom = 1.0f;

    private float _zoomPercent = 100f;

    private NVector2 _pan = NVector2.Zero;

    private bool _needsFit = true;

    private int _fitFrameDelay = 2;

    private bool _showPreview;

    private string _previewText = "The quick brown fox jumps over the lazy dog.\n" +
                                  "ABCDEFGHIJKLMNOPQRSTUVWXYZ\n" +
                                  "abcdefghijklmnopqrstuvwxyz\n" +
                                  "0123456789 !@#$%^&*()_+-=[]{}|;':\",./<>?";

    private float _previewScale = 2.0f;

    private bool _showKerning = true;

    private bool _showGlyphBounds = true; 

    public ZuEditor(Game game, string title, FezFont font) : base(game, title)
    {
        _font = font;
        History.Track(font);
    }

    public override void LoadContent()
    {
        _fontTexture = RepackerExtensions.ConvertToTexture2D(_font.Texture);
        _fontTexturePtr = ImGuiX.Bind(_fontTexture);
        
        if (Title.Contains("japanese", StringComparison.OrdinalIgnoreCase))
        {
            _charactersFont = ImGuiX.Fonts.NotoSansJp;
        }
        else if (Title.Contains("korean", StringComparison.OrdinalIgnoreCase))
        {
            _charactersFont = ImGuiX.Fonts.NotoSansKr;
        }
        else if (Title.Contains("chinese", StringComparison.OrdinalIgnoreCase))
        {
            _charactersFont = ImGuiX.Fonts.NotoSansTc;
        }
        else
        {
            _charactersFont = ImGuiX.Fonts.NotoSans;
        }
    }

    public override void Draw()
    {
        ImGuiX.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 8));

        if (ImGuiX.BeginChild("##Left", new Vector2(340f, 0), ImGuiChildFlags.Border))
        {
            DrawLeftPane();
            ImGui.EndChild();
        }

        ImGui.SameLine();

        if (ImGuiX.BeginChild("##Right", Vector2.Zero, ImGuiChildFlags.Border))
        {
            DrawTexturePane();
            ImGui.EndChild();
        }

        ImGui.PopStyleVar();

        if (_showPreview)
        {
            DrawPreviewWindow();
        }
    }
    
    #region Left Pane

    private void DrawLeftPane()
    {
        DrawFontProperties();
        ImGui.Spacing();
        DrawCharacterList();
        ImGui.Spacing();
        DrawCharacterEditor();
    }

    private void DrawFontProperties()
    {
        ImGui.SeparatorText("Font Properties");
        {
            var lineSpacing = _font.LineSpacing;
            ImGui.SetNextItemWidth(120f);
            if (ImGui.InputInt("Line Spacing", ref lineSpacing))
            {
                using (History.BeginScope("Edit Line Spacing"))
                {
                    _font.LineSpacing = Math.Max(1, lineSpacing);
                }
            }

            var spacing = _font.Spacing;
            ImGui.SetNextItemWidth(120f);
            if (ImGui.InputFloat("Spacing", ref spacing, 0.5f, 1f, "%.1f"))
            {
                using (History.BeginScope("Edit Spacing"))
                {
                    _font.Spacing = spacing;
                }
            }

            var defStr = _font.DefaultCharacter == '\u0000'
                ? string.Empty
                : _font.DefaultCharacter.ToString();

            ImGui.SetNextItemWidth(60f);
            if (ImGui.InputText("Default Char", ref defStr, 2))
            {
                using (History.BeginScope("Edit Default Char"))
                {
                    _font.DefaultCharacter = defStr.Length > 0 ? defStr[0] : '\u0000';
                }
            }

            ImGui.SameLine();
            ImGui.TextDisabled($"U+{(int)_font.DefaultCharacter:X4}");
        }
    }

    private void DrawCharacterList()
    {
        var count = _font.Characters.Count;
        ImGui.SeparatorText($"Characters ({count})");

        var selectionChanged = _selectedIndex != _lastSelectedIndex;

        if (ImGuiX.BeginListBox("##CharList", new Vector2(-1f, 240f)))
        {
            for (var i = 0; i < count; i++)
            {
                var character = _font.Characters[i];
                var selected = i == _selectedIndex;

                var label = !char.IsControl(character)
                    ? $"[{character}]  U+{(int)character:X4}  #{i}"
                    : $"[ctrl]  U+{(int)character:X4}  #{i}";

                ImGui.PushFont(_charactersFont);
                if (ImGui.Selectable($"{label}###{i}", selected, ImGuiSelectableFlags.SpanAllColumns))
                {
                    _selectedIndex = i;
                }
                ImGui.PopFont();

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                    if (selectionChanged)
                    {
                        ImGui.SetScrollHereY(0.5f);
                    }
                }
            }

            ImGui.EndListBox();
        }

        _lastSelectedIndex = _selectedIndex;

        if (ImGuiX.Button($"{Icons.Add} Add", Vector2.Zero))
        {
            AddCharacter('?');
        }

        ImGui.SameLine();
        ImGui.BeginDisabled(_selectedIndex < 0);

        if (ImGuiX.Button($"{Icons.Copy} Duplicate", Vector2.Zero))
        {
            DuplicateSelected();
        }

        ImGui.SameLine();

        if (ImGuiX.Button($"{Icons.Remove} Remove", Vector2.Zero))
        {
            DeleteSelected();
        }

        ImGui.EndDisabled();
    }

    private void DrawCharacterEditor()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _font.Characters.Count)
        {
            ImGui.TextDisabled("No character selected.");
            return;
        }

        ImGui.SeparatorText("Edit Character");

        // Character value
        var character = _font.Characters[_selectedIndex];
        var str = char.IsControl(character) ? "" : character.ToString();

        ImGui.SetNextItemWidth(50f);
        ImGui.PushFont(_charactersFont);
        if (ImGui.InputText("##Character", ref str, 2) && str.Length > 0)
        {
            SetCharacter(_selectedIndex, str[0]);
        }
        ImGui.PopFont();

        ImGui.SameLine();
        ImGui.Text("Character");

        ImGui.SameLine();
        ImGui.TextDisabled($"U+{(int)character:X4}");

        // GlyphBounds
        ImGui.SeparatorText("Glyph Bounds  (atlas UV rect)");
        ImGui.PushID("gb");
        EditRectInList(_font.GlyphBounds, _selectedIndex);
        ImGui.PopID();

        // Cropping
        ImGui.SeparatorText("Cropping  (render offset)");
        ImGui.PushID("cr");
        EditRectInList(_font.Cropping, _selectedIndex);
        ImGui.PopID();

        // KerningData
        ImGui.SeparatorText("Kerning Data");
        if (_selectedIndex < _font.KerningData.Count)
        {
            var kerning = _font.KerningData[_selectedIndex].ToXna();

            var x = kerning.X;
            if (ImGui.InputFloat("Left Bear.", ref x, 1f, 1f, "%.0f"))
            {
                _font.KerningData[_selectedIndex] = (kerning with { X = x }).ToRepacker();
            }

            var y = kerning.Y;
            if (ImGui.InputFloat("Advance", ref y, 1f, 1f, "%.0f"))
            {
                _font.KerningData[_selectedIndex] = (kerning with { Y = y }).ToRepacker();
            }

            var z = kerning.Z;
            if (ImGui.InputFloat("Right Bear.", ref z, 1f, 1f, "%.0f"))
            {
                _font.KerningData[_selectedIndex] = (kerning with { Z = z }).ToRepacker();
            }

            DrawKerningBar(kerning);
        }
    }

    // Edit a Rectangle inside a List<Rectangle> in-place
    private static void EditRectInList(List<RRectangle> list, int index)
    {
        // Grow list to cover index if needed
        while (list.Count <= index)
        {
            list.Add(new RRectangle(0, 0, 0, 0));
        }

        var r = list[index];
        int x = r.X, y = r.Y, w = r.Width, h = r.Height;

        ImGui.SetNextItemWidth(80f);
        ImGui.InputInt("X##r", ref x);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(80f);
        ImGui.InputInt("Y##r", ref y);
        ImGui.SetNextItemWidth(80f);
        ImGui.InputInt("W##r", ref w);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(80f);
        ImGui.InputInt("H##r", ref h);

        list[index] = new RRectangle(x, y, w, h);
    }

    private static void DrawKerningBar(Vector3 k)
    {
        const float barH = 16f;
        const float maxBarWidth = 300f;

        var left = k.X;
        var adv = k.Y;
        var right = k.Z;

        var totalUnits = MathF.Abs(left) + adv + MathF.Abs(right);
        var scale = totalUnits > 0 ? MathF.Min(4f, maxBarWidth / totalUnits) : 4f;

        var leftPad = MathF.Max(0, -left * scale);
        var totalW = Math.Max(leftPad + MathF.Max(0, left * scale) + adv * scale + MathF.Max(0, right * scale), 80f);

        ImGui.Spacing();
        ImGui.TextDisabled("Preview:");

        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();

        dl.AddRectFilled(pos, pos + new NVector2(totalW, barH), Color.Black.PackedValue);

        var cx = pos.X + leftPad;

        if (left != 0f)
        {
            var col = left > 0 ? Color.DarkOrange : Color.DarkBlue;
            var x0 = left > 0 ? cx : cx + left * scale;
            var x1 = left > 0 ? cx + left * scale : cx;
            dl.AddRectFilled(new NVector2(x0, pos.Y), new NVector2(x1, pos.Y + barH), col.PackedValue);
            cx += left * scale;
        }

        dl.AddRectFilled(new NVector2(cx, pos.Y), new NVector2(cx + adv * scale, pos.Y + barH), Color.Green.PackedValue);
        cx += adv * scale;

        if (right != 0f)
        {
            var col = right > 0 ? Color.LightBlue : Color.Orange;
            var x0 = right > 0 ? cx : cx + right * scale;
            var x1 = right > 0 ? cx + right * scale : cx;
            dl.AddRectFilled(new NVector2(x0, pos.Y), new NVector2(x1, pos.Y + barH), col.PackedValue);
        }

        dl.AddRect(pos, pos + new NVector2(totalW, barH), Color.Gray.PackedValue);

        ImGui.Dummy(new NVector2(totalW, barH));
        ImGui.TextDisabled($"L:{left:F0}  Adv:{adv:F0}  R:{right:F0}");
    }
    
    #endregion
    
    #region Texture Pane

    private void DrawTexturePane()
    {
        ImGui.SeparatorText("Atlas Texture");

        DrawToolbar();
        ImGui.Separator();

        if (!ImGuiX.BeginChild("##TextureCanvas", Vector2.Zero, ImGuiChildFlags.None))
        {
            ImGui.EndChild();
            return;
        }

        var canvasPos = ImGui.GetCursorScreenPos();
        var canvasSize = ImGui.GetContentRegionAvail();

        if (canvasSize.X < 1f || canvasSize.Y < 1f)
        {
            ImGui.EndChild();
            return;
        }

        HandleAutoFit(canvasSize);
        HandleInput(canvasPos, canvasSize);
        RenderTexture(canvasPos, canvasSize);

        ImGui.EndChild();
    }

    private void DrawToolbar()
    {
        ImGui.Text("Zoom:");
        ImGui.SameLine();

        ImGui.SetNextItemWidth(120f);
        if (ImGui.SliderFloat("##Zoom", ref _zoomPercent, 10f, 800f, "%.0f%%"))
        {
            _zoom = _baseZoom * (_zoomPercent / 100f);
        }

        ImGui.SameLine();
        if (ImGui.Button("Reset Zoom"))
        {
            _needsFit = true;
            _fitFrameDelay = 0;
        }
        
        ImGui.SameLine();
        if (ImGui.Button(_showPreview ? "Hide Preview" : "Show Preview"))
        {
            _showPreview = !_showPreview;
        }

        if (_font.Texture is { Width: > 0 } tex)
        {
            var sizeText = $"Texture Size: {tex.Width}x{tex.Height}px";
            var textWidth = ImGui.CalcTextSize(sizeText).X;
            var availWidth = ImGui.GetContentRegionAvail().X;
            ImGui.SameLine(ImGui.GetCursorPosX() + availWidth - textWidth);
            ImGui.TextDisabled(sizeText);
        }
    }

    private void HandleAutoFit(NVector2 canvasSize)
    {
        if (!_needsFit) return;

        if (_fitFrameDelay > 0)
        {
            _fitFrameDelay--;
        }
        else
        {
            FitToView(canvasSize);
            _needsFit = false;
        }
    }

    private void HandleInput(NVector2 canvasPos, NVector2 canvasSize)
    {
        ImGui.InvisibleButton("##Canvas", canvasSize,
            ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonRight);

        if (!ImGui.IsItemHovered()) return;

        var io = ImGui.GetIO();

        if (ImGui.IsMouseDragging(ImGuiMouseButton.Right))
        {
            _pan += io.MouseDelta;
            ClampPan(canvasSize);
        }

        if (io.MouseWheel != 0f)
        {
            var before = _zoom;
            _zoomPercent = Math.Clamp(_zoomPercent * MathF.Pow(1.1f, io.MouseWheel), 10f, 800f);
            _zoom = _baseZoom * (_zoomPercent / 100f);
            var rel = io.MousePos - canvasPos;
            _pan = rel - (rel - _pan) * (_zoom / before);
            ClampPan(canvasSize);
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            var atlasPixel = (io.MousePos - canvasPos - _pan) / _zoom;
            var hit = HitTest(atlasPixel);
            _selectedIndex = hit;
        }
    }

    private void ClampPan(NVector2 canvasSize)
    {
        var imgSize = new NVector2(_fontTexture.Width, _fontTexture.Height) * _zoom;
        var maxPan = canvasSize * 2f;
        var minPan = -imgSize - canvasSize;
        _pan = NVector2.Clamp(_pan, minPan, maxPan);
    }

    private void RenderTexture(NVector2 canvasPos, NVector2 canvasSize)
    {
        var dl = ImGui.GetWindowDrawList();
        var imgPos = new NVector2(MathF.Round(canvasPos.X + _pan.X), MathF.Round(canvasPos.Y + _pan.Y));
        var imgSize = new NVector2(_fontTexture.Width, _fontTexture.Height) * _zoom;

        dl.PushClipRect(canvasPos, canvasPos + canvasSize, true);

        DrawCheckerboard(dl, imgPos, imgSize);
        dl.AddImage(_fontTexturePtr, imgPos, imgPos + imgSize, new NVector2(0, 0), new NVector2(1, 1));

        for (var i = 0; i < _font.Characters.Count; i++)
        {
            DrawGlyphRect(dl, i, imgPos);
        }

        dl.PopClipRect();
    }

    private void DrawGlyphRect(ImDrawListPtr dl, int index, NVector2 imgPos)
    {
        if (index >= _font.GlyphBounds.Count)
        {
            return;
        }

        var gb = _font.GlyphBounds[index];
        var sel = index == _selectedIndex;

        var rMin = imgPos + new NVector2(gb.X, gb.Y) * _zoom;
        var rMax = rMin + new NVector2(gb.Width, gb.Height) * _zoom;

        var fill = sel ? 0x3300FFFFu : 0x22FF8800u;
        var border = sel ? 0xFF00FFFFu : 0xAAFF8800u;
        var text = sel ? 0xFF00FFFFu : 0xFFFFAA00u;

        dl.AddRectFilled(rMin, rMax, fill);
        dl.AddRect(rMin, rMax, border, 0f, ImDrawFlags.None, sel ? 2f : 1f);

        // Draw cropping overlay if selected
        if (sel && index < _font.Cropping.Count)
        {
            var crop = _font.Cropping[index];

            // The cropping offset from origin
            var cropMin = rMin + new NVector2(crop.X, crop.Y) * _zoom;
            var cropMax = cropMin + new NVector2(crop.Width, crop.Height) * _zoom;

            // Draw cropping rectangle in bright green
            dl.AddRect(cropMin, cropMax, Color.Lime.PackedValue, 0f, ImDrawFlags.None, 1.5f);

            // Draw crosshair at render origin to show the reference point
            var crossSize = 4f * _zoom;
            dl.AddLine(
                new NVector2(rMin.X - crossSize, rMin.Y),
                new NVector2(rMin.X + crossSize, rMin.Y),
                Color.White.PackedValue, 1f);
            dl.AddLine(
                new NVector2(rMin.X, rMin.Y - crossSize),
                new NVector2(rMin.X, rMin.Y + crossSize),
                Color.White.PackedValue, 1f);
        }

        // Label — only if rect is large enough
        if (gb.Width * _zoom > 8f && index < _font.Characters.Count)
        {
            var ch = _font.Characters[index];
            var lbl = char.IsControl(ch) ? "?" : ch.ToString();
            dl.AddText(rMin + new NVector2(2f, 1f), text, lbl);
        }
    }

    private static void DrawCheckerboard(ImDrawListPtr dl, NVector2 pos, NVector2 size)
    {
        const float cellSize = 16f;
        const int maxCells = 10000;

        var clipMin = dl.GetClipRectMin();
        var clipMax = dl.GetClipRectMax();
        var visibleMin = NVector2.Max(new NVector2(pos.X, pos.Y), clipMin);
        var visibleMax = NVector2.Min(pos + size, clipMax);

        if (visibleMin.X >= visibleMax.X || visibleMin.Y >= visibleMax.Y)
            return;

        var startCol = (int)MathF.Floor((visibleMin.X - pos.X) / cellSize);
        var endCol = (int)MathF.Ceiling((visibleMax.X - pos.X) / cellSize);
        var startRow = (int)MathF.Floor((visibleMin.Y - pos.Y) / cellSize);
        var endRow = (int)MathF.Ceiling((visibleMax.Y - pos.Y) / cellSize);
        var totalCells = (endCol - startCol) * (endRow - startRow);

        if (totalCells > maxCells)
        {
            dl.AddRectFilled(visibleMin, visibleMax, Color.Gray.PackedValue);
            return;
        }

        for (var r = startRow; r < endRow; r++)
        {
            for (var c = startCol; c < endCol; c++)
            {
                var color = (r + c) % 2 == 0 ? Color.DarkGray : Color.LightGray;
                var cellMin = pos + new NVector2(c, r) * cellSize;
                var cellMax = NVector2.Min(cellMin + new NVector2(cellSize), pos + size);
                cellMin = NVector2.Max(cellMin, pos);

                dl.AddRectFilled(cellMin, cellMax, color.PackedValue);
            }
        }
    }
    
    private void FitToView(NVector2 canvasSize)
    {
        var texW = _fontTexture.Width;
        var texH = _fontTexture.Height;

        var scaleX = canvasSize.X / texW;
        var scaleY = canvasSize.Y / texH;

        _baseZoom = Math.Min(scaleX, scaleY) * 0.95f;
        _zoomPercent = 100f;
        _zoom = _baseZoom;
        _pan = (canvasSize - new NVector2(texW, texH) * _zoom) * 0.5f;
    }

    private int HitTest(NVector2 atlasPixel)
    {
        for (var i = _font.GlyphBounds.Count - 1; i >= 0; i--)
        {
            var r = _font.GlyphBounds[i];
            if (atlasPixel.X >= r.X && atlasPixel.X <= r.X + r.Width &&
                atlasPixel.Y >= r.Y && atlasPixel.Y <= r.Y + r.Height)
            {
                return i;
            }
        }

        return -1;
    }
    
    #endregion
    
    #region Preview Window

    private void DrawPreviewWindow()
    {
        ImGui.SetNextWindowSize(new NVector2(800, 600), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin($"Font Preview##{Title}", ref _showPreview, ImGuiWindowFlags.None))
        {
            ImGui.End();
            return;
        }

        ImGui.Text("Preview Text:");
        ImGui.PushFont(_charactersFont);
        ImGui.InputTextMultiline("##PreviewText", ref _previewText, 1024, new NVector2(-1, 0));
        ImGui.PopFont();
        
        ImGui.Text("Scale:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(200f);
        ImGui.SliderFloat("##PreviewScale", ref _previewScale, 0.5f, 8.0f, "%.1fx");

        ImGui.SameLine();
        ImGui.Checkbox("Show Glyph Bounds", ref _showGlyphBounds);
        ImGui.SameLine();
        ImGui.Checkbox("Show Kerning", ref _showKerning);

        ImGui.Separator();

        if (ImGuiX.BeginChild("##PreviewCanvas", Vector2.Zero, ImGuiChildFlags.Border))
        {
            var canvasPos = ImGui.GetCursorScreenPos();
            var canvasSize = ImGui.GetContentRegionAvail();

            if (canvasSize is { X: > 1f, Y: > 1f })
            {
                var dl = ImGui.GetWindowDrawList();
                dl.PushClipRect(canvasPos, canvasPos + canvasSize, true);
                dl.AddRectFilled(canvasPos, canvasPos + canvasSize, Color.Black.PackedValue);
                DrawPreviewText(dl, canvasPos + new NVector2(10, 10));
                dl.PopClipRect();
            }

            ImGui.EndChild();
        }

        ImGui.End();
    }

    private void DrawPreviewText(ImDrawListPtr dl, NVector2 startPos)
    {
        var pos = startPos;
        var lineHeight = _font.LineSpacing * _previewScale;

        foreach (var ch in _previewText)
        {
            switch (ch)
            {
                case '\n':
                    pos.X = startPos.X;
                    pos.Y += lineHeight;
                    continue;
                
                case '\r':
                    continue;
            }

            var charIndex = _font.Characters.IndexOf(ch);
            if (charIndex < 0)
            {
                // Use default character if available
                if (_font.DefaultCharacter != '\u0000')
                {
                    charIndex = _font.Characters.IndexOf(_font.DefaultCharacter);
                }

                if (charIndex < 0)
                {
                    // Skip character if not found
                    continue;
                }
            }

            // Get glyph data
            if (charIndex >= _font.GlyphBounds.Count)
            {
                continue;
            }

            var glyphBounds = _font.GlyphBounds[charIndex];
            var cropping = charIndex < _font.Cropping.Count
                ? _font.Cropping[charIndex]
                : new RRectangle(0, 0, glyphBounds.Width, glyphBounds.Height);

            var kerning = charIndex < _font.KerningData.Count
                ? _font.KerningData[charIndex].ToXna()
                : new Vector3(0, glyphBounds.Width, 0);

            var charStartX = pos.X;

            // Calculate render position with kerning
            var renderX = pos.X + (kerning.X + cropping.X) * _previewScale;
            var renderY = pos.Y + cropping.Y * _previewScale;

            // Calculate UV coordinates from atlas
            var texW = (float)_fontTexture.Width;
            var texH = (float)_fontTexture.Height;
            var uv0 = new NVector2(glyphBounds.X / texW, glyphBounds.Y / texH);
            var uv1 = new NVector2((glyphBounds.X + glyphBounds.Width) / texW, (glyphBounds.Y + glyphBounds.Height) / texH);

            // Draw glyph from atlas
            var renderPos = new NVector2(renderX, renderY);
            var renderSize = new NVector2(glyphBounds.Width * _previewScale, glyphBounds.Height * _previewScale);

            if (glyphBounds is { Width: > 0, Height: > 0 })
            {
                dl.AddImage(_fontTexturePtr, renderPos, renderPos + renderSize, uv0, uv1);
            }

            // Draw glyph bounds (actual rendered area)
            if (_showGlyphBounds && glyphBounds is { Width: > 0, Height: > 0 })
            {
                dl.AddRect(
                    renderPos,
                    renderPos + renderSize,
                    0xFFFFFF40u, // Yellow
                    0f, ImDrawFlags.None, 1f);
            }

            // Draw kerning box (entire advance area)
            if (_showKerning)
            {
                var advanceWidth = kerning.Y * _previewScale;
                var boxTop = pos.Y - 2f;
                var boxBottom = pos.Y + lineHeight + 2f;
                dl.AddRect(
                    new NVector2(charStartX, boxTop),
                    new NVector2(charStartX + advanceWidth, boxBottom),
                    0x80FF8040u, // Semi-transparent orange
                    0f, ImDrawFlags.None, 1f);
            }

            // Advance position (includes spacing)
            pos.X += (kerning.Y + _font.Spacing) * _previewScale;
        }
    }

    #endregion
    
    #region Updates
    
    private void AddCharacter(char character)
    {
        using (History.BeginScope("Add New Character"))
        {
            _font.Characters.Add(character);
            _font.GlyphBounds.Add(new RRectangle(0, 0, 8, 8));
            _font.Cropping.Add(new RRectangle(0, 0, 8, 12));
            _font.KerningData.Add(new RVector3(0, 8, 0));
            _selectedIndex = _font.Characters.Count - 1;
        }
    }

    private void SetCharacter(int selectedIndex, char character)
    {
        using (History.BeginScope("Set a Character"))
        {
            _font.Characters[selectedIndex] = character;
        }
    }

    private void DuplicateSelected()
    {
        if (_selectedIndex < 0)
        {
            return;
        }
        
        using (History.BeginScope("Duplicate Selected Characters"))
        {
            _font.Characters.Add(_font.Characters[_selectedIndex]);

            _font.GlyphBounds.Add(_selectedIndex < _font.GlyphBounds.Count
                ? _font.GlyphBounds[_selectedIndex]
                : new RRectangle(0, 0, 0, 0)
            );

            _font.Cropping.Add(_selectedIndex < _font.Cropping.Count
                ? _font.Cropping[_selectedIndex] 
                : new RRectangle(0, 0, 0, 0)
            );

            _font.KerningData.Add(_selectedIndex < _font.KerningData.Count
                ? _font.KerningData[_selectedIndex] 
                : RVector3.Zero
            );

            _selectedIndex = _font.Characters.Count - 1;
        }
    }

    private void DeleteSelected()
    {
        if (_selectedIndex < 0)
        {
            return;
        }
        
        using (History.BeginScope("Delete Selected Characters"))
        {
            RemoveAt(_font.Characters, _selectedIndex);
            RemoveAt(_font.GlyphBounds, _selectedIndex);
            RemoveAt(_font.Cropping, _selectedIndex);
            RemoveAt(_font.KerningData, _selectedIndex);
            _selectedIndex = Math.Min(_selectedIndex, _font.Characters.Count - 1);
        }
    }

    private static void RemoveAt<T>(List<T> list, int i)
    {
        if (i < list.Count) list.RemoveAt(i);
    }

    #endregion
}