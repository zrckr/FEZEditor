using FezEditor.Structure;
using FezEditor.Tools;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Components;

public class TexViewer : EditorComponent
{
    private static readonly Color CheckerLight = Color.LightGray;

    private static readonly Color CheckerDark = Color.DarkGray;

    private const float MinZoom = 0.25f;

    private const float MaxZoom = 64f;

    private const int CheckerSize = 16;

    private const float ThumbSize = 96f;

    private const float CellSpacing = 8f;

    private const float CellSize = ThumbSize + CellSpacing;

    private int CurrentFrame
    {
        get => _currentFrame;
        set
        {
            _currentFrame = value;
            _scrollToFrame = true;
        }
    }

    private readonly RTexture2D? _texture;

    private readonly RAnimatedTexture? _animatedTexture;

    private Texture2D _xnaTexture = null!;

    private Vector2 _pan;

    private float _zoom;

    private float? _initialZoom;

    private bool _playing;

    private bool _showTimeline;

    private TempTextureTracker? _tracker;

    private int _currentFrame;

    private bool _scrollToFrame;

    private TimeSpan _frameTimer;

    public TexViewer(Game game, string title, RTexture2D texture) : base(game, title)
    {
        _texture = texture;
    }

    public TexViewer(Game game, string title, RAnimatedTexture texture) : base(game, title)
    {
        _animatedTexture = texture;
    }

    public override void LoadContent()
    {
        if (_texture != null)
        {
            _xnaTexture = RepackerExtensions.ConvertToTexture2D(_texture);
        }
        else if (_animatedTexture != null)
        {
            _xnaTexture = RepackerExtensions.ConvertToTexture2D(_animatedTexture);
        }

        var fullPath = ResourceService.GetFullPath(Title);
        if (!string.IsNullOrEmpty(fullPath))
        {
            _tracker = new TempTextureTracker(Game, fullPath);
            _tracker.Changed += OnTrackedTextureChanged;
        }
    }

    public override void Update(GameTime gameTime)
    {
        if (_animatedTexture is { Frames.Count: > 0 } && _playing)
        {
            _frameTimer += gameTime.ElapsedGameTime;
            if (_frameTimer >= _animatedTexture.Frames[CurrentFrame].Duration)
            {
                _frameTimer = TimeSpan.Zero;
                CurrentFrame = (CurrentFrame + 1) % _animatedTexture.Frames.Count;
            }
        }
    }

    public override void Draw()
    {
        var frame = new Rectangle();
        if (_animatedTexture is { Frames.Count: > 0 })
        {
            var rect = _animatedTexture.Frames[CurrentFrame].Rectangle;
            frame.X = rect.X;
            frame.Y = rect.Y;
            frame.Width = rect.Width;
            frame.Height = rect.Height;
        }
        else
        {
            frame.Width = _xnaTexture.Width;
            frame.Height = _xnaTexture.Height;
        }

        DrawToolbar(frame);
        ImGui.Separator();

        var avail = ImGuiX.GetContentRegionAvail();
        if (avail.X <= 0 || avail.Y <= 0)
        {
            return;
        }

        if (_initialZoom == null && frame.Height > 0)
        {
            _initialZoom = MathHelper.Clamp(avail.Y / (frame.Height * 2f), MinZoom, MaxZoom);
            _zoom = _initialZoom.Value;
        }

        const ImGuiButtonFlags flags = ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonMiddle;
        ImGuiX.InvisibleButton("##canvas", avail, flags);

        var canvasMin = ImGuiX.GetItemRectMin();
        var canvasMax = canvasMin + avail;
        var canvasCenter = canvasMin + avail * 0.5f;

        InputService.IsViewportHovered = ImGui.IsItemHovered();
        if (InputService.CaptureScrollWheelDelta(out var scrollDelta))
        {
            var mousePos = ImGuiX.GetMousePos();
            var mouseBeforeZoom = (mousePos - canvasCenter - _pan) / _zoom;

            var zoomFactor = scrollDelta > 0 ? 1.2f : 1f / 1.2f;
            _zoom = MathHelper.Clamp(_zoom * zoomFactor, MinZoom, MaxZoom);

            // Adjust pan so the zoom is centered on mouse
            var mouseAfterZoom = mouseBeforeZoom * _zoom;
            _pan = mousePos - canvasCenter - mouseAfterZoom;
        }

        if (ImGui.IsItemActive() && (ImGui.IsMouseDragging(ImGuiMouseButton.Middle) ||
                                     ImGui.IsMouseDragging(ImGuiMouseButton.Left)))
        {
            var delta = ImGui.GetIO().MouseDelta;
            _pan.X += delta.X;
            _pan.Y += delta.Y;
        }

        var imageSize = new Vector2(frame.Width, frame.Height) * _zoom;
        var imageMin = canvasCenter + _pan - imageSize * 0.5f;
        var imageMax = imageMin + imageSize;

        var dl = ImGui.GetWindowDrawList();
        dl.PushClipRect(canvasMin.ToNumerics(), canvasMax.ToNumerics(), true);

        DrawCheckerBackground(dl, imageMin, imageMax, canvasMin, canvasMax);

        var texPtr = ImGuiX.Bind(_xnaTexture);
        var uv0 = new NVector2
        {
            X = (float)frame.X / _xnaTexture.Width,
            Y = (float)frame.Y / _xnaTexture.Height
        };
        var uv1 = new NVector2
        {
            X = (float)(frame.X + frame.Width) / _xnaTexture.Width,
            Y = (float)(frame.Y + frame.Height) / _xnaTexture.Height
        };
        dl.AddImage(texPtr, imageMin.ToNumerics(), imageMax.ToNumerics(), uv0, uv1);
        dl.PopClipRect();

        DrawTimelineWindow();
    }

    private void DrawToolbar(Rectangle frame)
    {
        if (ImGui.Button($"{Icons.ZoomIn} Zoom In"))
        {
            _zoom = MathHelper.Min(_zoom * 2f, MaxZoom);
        }

        ImGui.SameLine();
        if (ImGui.Button($"{Icons.ZoomOut} Zoom Out"))
        {
            _zoom = MathHelper.Max(_zoom / 2f, MinZoom);
        }

        ImGui.SameLine();
        if (ImGui.Button($"{Icons.ScreenNormal} Reset View"))
        {
            _zoom = _initialZoom!.Value;
            _pan = Vector2.Zero;
        }

        ImGui.SameLine();
        ImGui.Text($"{_zoom:P0}%");

        ImGui.SameLine();
        ImGui.Text("|");
        ImGui.SameLine();
        ImGui.Text($"Size: {frame.Width}x{frame.Height} px");

        if (_animatedTexture is { Frames.Count: > 0 })
        {
            ImGui.SameLine();
            ImGui.Text("|");

            var icon = _showTimeline ? Icons.EyeClosed : Icons.Eye;
            ImGui.SameLine();
            if (ImGui.Button($"{icon} Timeline"))
            {
                _showTimeline = !_showTimeline;
            }

            ImGui.SameLine();
            ImGui.Text($"Frame: {CurrentFrame + 1} / {_animatedTexture.Frames.Count}");

            ImGui.SameLine();
            ImGui.Text("|");
            ImGui.SameLine();
            var totalMs = _animatedTexture.Frames.Sum(f => f.Duration.TotalMilliseconds);
            ImGui.Text($"Total: {totalMs:F0} ms");
        }
    }

    private void DrawTimelineWindow()
    {
        if (!_showTimeline || _animatedTexture is not { Frames.Count: > 0 })
        {
            return;
        }

        ImGuiX.SetNextWindowSize(new Vector2(480, 400), ImGuiCond.Appearing);

        if (ImGui.Begin($"Timeline##{Title}", ref _showTimeline, ImGuiWindowFlags.NoCollapse))
        {
            var frameCount = _animatedTexture.Frames.Count;

            #region Playback controls

            if (ImGui.Button(Icons.DebugStepBack))
            {
                _playing = false;
                CurrentFrame = (CurrentFrame - 1 + frameCount) % frameCount;
                _frameTimer = TimeSpan.Zero;
            }

            ImGui.SameLine();
            if (ImGui.Button(_playing ? Icons.DebugPause : Icons.Play))
            {
                _playing = !_playing;
            }

            ImGui.SameLine();
            if (ImGui.Button(Icons.Stop))
            {
                _playing = false;
                CurrentFrame = 0;
                _frameTimer = TimeSpan.Zero;
            }

            ImGui.SameLine();
            if (ImGui.Button(Icons.DebugStepOver))
            {
                _playing = false;
                CurrentFrame = (CurrentFrame + 1) % frameCount;
                _frameTimer = TimeSpan.Zero;
            }

            ImGui.SameLine();
            ImGui.SetNextItemWidth(80);
            var displayFrame = CurrentFrame + 1;
            if (ImGui.InputInt($"/ {frameCount}", ref displayFrame, 0, 0))
            {
                CurrentFrame = Math.Clamp(displayFrame - 1, 0, frameCount - 1);
                _frameTimer = TimeSpan.Zero;
            }

            #endregion

            ImGui.Separator();

            ImGui.TextWrapped(
                $"{Icons.Info} To edit this animation, use Aseprite or another sprite editor. " +
                $"Export the texture, edit it externally, and changes will be detected automatically " +
                $"when you return to this window.");

            if (ImGui.Button($"{Icons.FolderOpened} Open in File Manager"))
            {
                ResourceService.OpenInFileManager(Title);
            }

            ImGui.Separator();

            #region Frame grid

            if (ImGui.BeginChild("##grid", NVector2.Zero, ImGuiChildFlags.None,
                    ImGuiWindowFlags.AlwaysVerticalScrollbar))
            {
                var texPtr = ImGuiX.Bind(_xnaTexture);
                var availWidth = ImGui.GetContentRegionAvail().X;
                var columns = MathHelper.Max((int)(availWidth / CellSize), 1);

                for (var i = 0; i < frameCount; i++)
                {
                    var f = _animatedTexture.Frames[i];
                    var isSelected = i == CurrentFrame;

                    ImGui.PushID(i);
                    ImGui.BeginGroup();

                    // Compute thumbnail size preserving aspect ratio
                    var aspect = (float)f.Rectangle.Width / f.Rectangle.Height;
                    float thumbW, thumbH;
                    if (aspect >= 1f)
                    {
                        thumbW = ThumbSize;
                        thumbH = ThumbSize / aspect;
                    }
                    else
                    {
                        thumbH = ThumbSize;
                        thumbW = ThumbSize * aspect;
                    }

                    // Highlight selected frame and autoscroll
                    if (isSelected)
                    {
                        var cursorPos = ImGui.GetCursorScreenPos();
                        var dl = ImGui.GetWindowDrawList();
                        var highlightMax = new NVector2(cursorPos.X + ThumbSize, cursorPos.Y + ThumbSize);
                        dl.AddRect(cursorPos, highlightMax, Color.LightGray.PackedValue);
                        if (_scrollToFrame)
                        {
                            ImGui.SetScrollHereY();
                            _scrollToFrame = false;
                        }
                    }

                    // Center thumbnail within the cell
                    var padX = (ThumbSize - thumbW) * 0.5f;
                    var padY = (ThumbSize - thumbH) * 0.5f;
                    var cursor = ImGui.GetCursorPos();
                    ImGui.SetCursorPos(new NVector2(cursor.X + padX, cursor.Y + padY));

                    var uv0 = new NVector2(
                        (float)f.Rectangle.X / _xnaTexture.Width,
                        (float)f.Rectangle.Y / _xnaTexture.Height);
                    var uv1 = new NVector2(
                        (float)(f.Rectangle.X + f.Rectangle.Width) / _xnaTexture.Width,
                        (float)(f.Rectangle.Y + f.Rectangle.Height) / _xnaTexture.Height);
                    ImGui.Image(texPtr, new NVector2(thumbW, thumbH), uv0, uv1);

                    // Restore cursor for the invisible click target over the whole cell
                    ImGui.SetCursorPos(cursor);
                    if (ImGui.InvisibleButton("##sel", new NVector2(ThumbSize, ThumbSize)))
                    {
                        CurrentFrame = i;
                        _frameTimer = TimeSpan.Zero;
                        _playing = false;
                    }

                    // Label: index and duration
                    var label = $"#{i + 1} ({f.Duration.TotalMilliseconds:F0} ms)";
                    var textSize = ImGui.CalcTextSize(label);
                    var labelPad = (ThumbSize - textSize.X) * 0.5f;
                    if (labelPad > 0)
                    {
                        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + labelPad);
                    }

                    ImGui.TextUnformatted(label);
                    ImGui.EndGroup();
                    ImGui.PopID();

                    // Flow into columns with spacing
                    if ((i + 1) % columns != 0 && i + 1 < frameCount)
                    {
                        ImGui.SameLine(0, CellSpacing);
                    }
                }
            }

            ImGui.EndChild();

            #endregion
        }

        ImGui.End();
    }

    private static void DrawCheckerBackground(ImDrawListPtr dl, Vector2 imageMin, Vector2 imageMax, Vector2 clipMin,
        Vector2 clipMax)
    {
        // Clamp checker region to visible area
        var visMin = new Vector2(
            MathHelper.Max(imageMin.X, clipMin.X),
            MathHelper.Max(imageMin.Y, clipMin.Y));
        var visMax = new Vector2(
            MathHelper.Min(imageMax.X, clipMax.X),
            MathHelper.Min(imageMax.Y, clipMax.Y));

        if (visMin.X >= visMax.X || visMin.Y >= visMax.Y)
        {
            return;
        }

        // Fill the whole image area with the light color first
        dl.AddRectFilled(visMin.ToNumerics(), visMax.ToNumerics(), CheckerLight.PackedValue);

        // Then draw dark checker squares on top
        var startCol = (int)MathF.Floor((visMin.X - imageMin.X) / CheckerSize);
        var startRow = (int)MathF.Floor((visMin.Y - imageMin.Y) / CheckerSize);
        var endCol = (int)MathF.Ceiling((visMax.X - imageMin.X) / CheckerSize);
        var endRow = (int)MathF.Ceiling((visMax.Y - imageMin.Y) / CheckerSize);

        for (var row = startRow; row < endRow; row++)
        {
            for (var col = startCol; col < endCol; col++)
            {
                if ((row + col) % 2 == 0)
                {
                    continue;
                }

                var rectMin = new NVector2(
                    MathHelper.Max(imageMin.X + col * CheckerSize, visMin.X),
                    MathHelper.Max(imageMin.Y + row * CheckerSize, visMin.Y));
                var rectMax = new NVector2(
                    MathHelper.Min(imageMin.X + (col + 1) * CheckerSize, visMax.X),
                    MathHelper.Min(imageMin.Y + (row + 1) * CheckerSize, visMax.Y));

                dl.AddRectFilled(rectMin, rectMax, CheckerDark.PackedValue);
            }
        }
    }

    private void OnTrackedTextureChanged(Texture2D newTexture)
    {
        _xnaTexture.Dispose();
        _xnaTexture = newTexture;
    }

    public override void Dispose()
    {
        _tracker?.Dispose();
        _xnaTexture.Dispose();
        base.Dispose();
    }
}