using System.Runtime.InteropServices;
using FezEditor.Structure;
using FezEditor.Tools;
using ImGuiNET;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SDL3;
using Serilog;

namespace FezEditor.Services;

/// <summary>
/// Renders Dear ImGui user interfaces by integrating ImGui.NET with the FNA graphics pipeline.
/// </summary>
[UsedImplicitly]
public partial class ImGuiService : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<ImGuiService>();

    private static readonly Color ClearColor = new(0.2f, 0.2f, 0.294f);

    public float AutoDisplayScale { get; private set; }

    private const float FallbackFrameTime = 1f / 60f;

    private const float WheelDelta = 120f;

    private readonly Game _game;

    private readonly AppStorageService _storage;

    private readonly InputService _input;

    private Texture2D _fontTexture = null!;

    private readonly Dictionary<string, LanguageFont> _languageFonts = new();

    private ImFontPtr _defaultFont;

    private readonly List<GCHandle> _fontDataHandles = [];

    private readonly List<nint> _fontGlyphRangeAllocations = [];

    private readonly BasicEffect _basicEffect;

    private readonly RasterizerState _rasterizerState;

    private float _previousScrollWheelValue;

    private byte[] _vertexData = [];

    private VertexBuffer? _vertexBuffer;

    private int _vertexBufferSize;

    private byte[] _indexData = [];

    private IndexBuffer? _indexBuffer;

    private int _indexBufferSize;

    private bool _gameWindowFocused = true;

    private float _displayScale;

    private float? _pendingDisplayScale;

    private bool _fontAtlasDirty;

    public ImGuiService(Game game)
    {
        _game = game;
        _storage = game.GetService<AppStorageService>();
        _input = game.GetService<InputService>();

        // Set up the context
        {
            var context = ImGui.CreateContext();
            ImGui.SetCurrentContext(context);
            PopulateKeyMappings();
            TextInputEXT.TextInput += HandleInput;
            TextInputEXT.StartTextInput();
            ImGuiX.Bind = BindTexture;
            ImGuiX.Unbind = UnbindTexture;
            ImGuiX.GetTexture = GetBoundTexture;
        }

        // Disable mouse if window not focused
        {
            _game.Deactivated += (_, _) => _gameWindowFocused = false;
            _game.Activated += (_, _) => _gameWindowFocused = true;
        }

        // Resolve display scale: user override takes priority over auto-detection
        {
            var storage = game.GetService<AppStorageService>();
            AutoDisplayScale = GetDisplayScale(_game.Window.Handle);
            _displayScale = storage.DisplayScale ?? AutoDisplayScale;
            Logger.Information("Display scale - {0:F2} (auto={1:F2}, override={2})",
                _displayScale, AutoDisplayScale,
                storage.DisplayScale.HasValue ? storage.DisplayScale.Value.ToString("F2") : "none");
        }

        BuildFontAtlas();
        ApplyStyleScale();

        // Initialize rendering
        {
            _basicEffect = new BasicEffect(_game.GraphicsDevice)
            {
                World = Matrix.Identity,
                View = Matrix.Identity,
                TextureEnabled = true,
                VertexColorEnabled = true
            };

            _rasterizerState = new RasterizerState
            {
                CullMode = CullMode.None,
                DepthBias = 0,
                FillMode = FillMode.Solid,
                MultiSampleAntiAlias = false,
                ScissorTestEnable = true,
                SlopeScaleDepthBias = 0
            };
        }

        Logger.Information("Dear ImGui Version - {0}", ImGui.GetVersion());
    }

    /// <summary>
    /// Updates ImGui input state and begins a new ImGui frame.
    /// </summary>
    /// <remarks>
    /// Call this before rendering ImGui UI.
    /// </remarks>
    /// <param name="gameTime">Current game timing information.</param>
    public void BeforeLayout(GameTime gameTime)
    {
        var currentAuto = GetDisplayScale(_game.Window.Handle);
        if (MathF.Abs(currentAuto - AutoDisplayScale) > 0.01f)
        {
            AutoDisplayScale = currentAuto;
            if (!_storage.DisplayScale.HasValue)
            {
                _pendingDisplayScale = currentAuto;
            }
        }

        var scaleChanged = _pendingDisplayScale.HasValue;
        if (scaleChanged)
        {
            _displayScale = _pendingDisplayScale!.Value;
            _pendingDisplayScale = null;
            _fontAtlasDirty = true;
        }

        var io = ImGui.GetIO();
        if (_fontAtlasDirty)
        {
            _fontAtlasDirty = false;
            if (UnbindTexture(_fontTexture))
            {
                _fontTexture.Dispose();
            }

            io.Fonts.Clear();
            BuildFontAtlas();
            if (scaleChanged)
            {
                ApplyStyleScale();
            }
        }

        var delta = (float)gameTime.ElapsedGameTime.TotalSeconds;
        io.DeltaTime = delta > 0f ? delta : FallbackFrameTime;

        // Update inputs
        if (_gameWindowFocused)
        {
            var mouse = _input.CurrentMouseState;
            io.AddMousePosEvent(mouse.X, mouse.Y);
            io.AddMouseButtonEvent(0, mouse.LeftButton == ButtonState.Pressed);
            io.AddMouseButtonEvent(1, mouse.RightButton == ButtonState.Pressed);
            io.AddMouseButtonEvent(2, mouse.MiddleButton == ButtonState.Pressed);
            io.AddMouseButtonEvent(3, mouse.XButton1 == ButtonState.Pressed);
            io.AddMouseButtonEvent(4, mouse.XButton2 == ButtonState.Pressed);
            io.AddMouseWheelEvent(0, (mouse.ScrollWheelValue - _previousScrollWheelValue) / WheelDelta);
            _previousScrollWheelValue = mouse.ScrollWheelValue;

            var keyboard = _input.CurrentKeyboardState;
            foreach (var key in KeyMappings.Keys)
            {
                io.AddKeyEvent(KeyMappings[key], keyboard.IsKeyDown(key));
            }
        }

        io.DisplaySize = new NVector2
        {
            X = _game.GraphicsDevice.PresentationParameters.BackBufferWidth,
            Y = _game.GraphicsDevice.PresentationParameters.BackBufferHeight
        };
        io.DisplayFramebufferScale = new NVector2(1, 1);

        _game.GraphicsDevice.Clear(ClearColor);
        ImGui.NewFrame();
    }

    /// <summary>
    /// Renders the ImGui draw data to the screen. Call this after all ImGui UI code.
    /// </summary>
    public void AfterLayout()
    {
        ImGui.Render();
        CleanupDeadTextures();

        // Setup render state: alpha-blending enabled, no face culling, no depth testing, scissor enabled, vertex/texcoord/color pointers
        var lastViewport = _game.GraphicsDevice.Viewport;
        var lastScissorBox = _game.GraphicsDevice.ScissorRectangle;
        var lastRasterizer = _game.GraphicsDevice.RasterizerState;
        var lastDepthStencil = _game.GraphicsDevice.DepthStencilState;
        var lastBlendFactor = _game.GraphicsDevice.BlendFactor;
        var lastBlendState = _game.GraphicsDevice.BlendState;
        var samplerState = _game.GraphicsDevice.SamplerStates[0];

        _game.GraphicsDevice.BlendFactor = Color.White;
        _game.GraphicsDevice.BlendState = BlendState.NonPremultiplied;
        _game.GraphicsDevice.RasterizerState = _rasterizerState;
        _game.GraphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
        _game.GraphicsDevice.SamplerStates[0] = SamplerState.PointClamp;

        // Handle cases of screen coordinates != from framebuffer coordinates (e.g. retina displays)
        var io = ImGui.GetIO();
        var drawData = ImGui.GetDrawData();
        drawData.ScaleClipRects(io.DisplayFramebufferScale);

        // Setup projection
        _game.GraphicsDevice.Viewport = new Viewport(0, 0, _game.GraphicsDevice.PresentationParameters.BackBufferWidth,
            _game.GraphicsDevice.PresentationParameters.BackBufferHeight);

        UpdateBuffers(drawData);
        RenderCommandLists(drawData);

        // Restore modified state
        _game.GraphicsDevice.Viewport = lastViewport;
        _game.GraphicsDevice.ScissorRectangle = lastScissorBox;
        _game.GraphicsDevice.RasterizerState = lastRasterizer;
        _game.GraphicsDevice.DepthStencilState = lastDepthStencil;
        _game.GraphicsDevice.BlendState = lastBlendState;
        _game.GraphicsDevice.BlendFactor = lastBlendFactor;
        _game.GraphicsDevice.SamplerStates[0] = samplerState;
    }

    private unsafe void UpdateBuffers(ImDrawDataPtr drawData)
    {
        if (drawData.TotalVtxCount == 0)
        {
            return;
        }

        // Expand buffers if we need more room
        if (drawData.TotalVtxCount > _vertexBufferSize)
        {
            _vertexBuffer?.Dispose();

            _vertexBufferSize = (int)(drawData.TotalVtxCount * 1.5f);
            _vertexBuffer = new VertexBuffer(_game.GraphicsDevice, DrawVertDeclaration.Declaration, _vertexBufferSize,
                BufferUsage.None);
            _vertexData = new byte[_vertexBufferSize * DrawVertDeclaration.Size];
        }

        if (drawData.TotalIdxCount > _indexBufferSize)
        {
            _indexBuffer?.Dispose();

            _indexBufferSize = (int)(drawData.TotalIdxCount * 1.5f);
            _indexBuffer = new IndexBuffer(_game.GraphicsDevice, IndexElementSize.SixteenBits, _indexBufferSize,
                BufferUsage.None);
            _indexData = new byte[_indexBufferSize * sizeof(ushort)];
        }

        // Copy ImGui's vertices and indices to a set of managed byte arrays
        var vtxOffset = 0;
        var idxOffset = 0;

        for (var n = 0; n < drawData.CmdListsCount; n++)
        {
            var cmdList = drawData.CmdLists[n];

            fixed (void* vtxDstPtr = &_vertexData[vtxOffset * DrawVertDeclaration.Size])
            fixed (void* idxDstPtr = &_indexData[idxOffset * sizeof(ushort)])
            {
                Buffer.MemoryCopy((void*)cmdList.VtxBuffer.Data, vtxDstPtr, _vertexData.Length,
                    cmdList.VtxBuffer.Size * DrawVertDeclaration.Size);
                Buffer.MemoryCopy((void*)cmdList.IdxBuffer.Data, idxDstPtr, _indexData.Length,
                    cmdList.IdxBuffer.Size * sizeof(ushort));
            }

            vtxOffset += cmdList.VtxBuffer.Size;
            idxOffset += cmdList.IdxBuffer.Size;
        }

        // Copy the managed byte arrays to the gpu vertex- and index buffers
        _vertexBuffer?.SetData(_vertexData, 0, drawData.TotalVtxCount * DrawVertDeclaration.Size);
        _indexBuffer?.SetData(_indexData, 0, drawData.TotalIdxCount * sizeof(ushort));
    }

    private void RenderCommandLists(ImDrawDataPtr drawData)
    {
        _game.GraphicsDevice.SetVertexBuffer(_vertexBuffer);
        _game.GraphicsDevice.Indices = _indexBuffer;

        var io = ImGui.GetIO();
        var vtxOffset = 0;
        var idxOffset = 0;

        for (var n = 0; n < drawData.CmdListsCount; n++)
        {
            var cmdList = drawData.CmdLists[n];
            for (var cmdi = 0; cmdi < cmdList.CmdBuffer.Size; cmdi++)
            {
                var drawCmd = cmdList.CmdBuffer[cmdi];
                if (drawCmd.ElemCount == 0)
                {
                    continue;
                }

                var texture = GetBoundTexture(drawCmd.TextureId);
                if (texture == null)
                {
                    throw new InvalidOperationException(
                        $"Could not find a texture with id '{drawCmd.TextureId}', please check your bindings");
                }

                _game.GraphicsDevice.ScissorRectangle = new Rectangle(
                    (int)drawCmd.ClipRect.X,
                    (int)drawCmd.ClipRect.Y,
                    (int)(drawCmd.ClipRect.Z - drawCmd.ClipRect.X),
                    (int)(drawCmd.ClipRect.W - drawCmd.ClipRect.Y)
                );

                _basicEffect.Projection =
                    Matrix.CreateOrthographicOffCenter(0f, io.DisplaySize.X, io.DisplaySize.Y, 0f, -1f, 1f);
                _basicEffect.Texture = texture;

                foreach (var pass in _basicEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _game.GraphicsDevice.DrawIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        (int)drawCmd.VtxOffset + vtxOffset,
                        0,
                        cmdList.VtxBuffer.Size,
                        (int)drawCmd.IdxOffset + idxOffset,
                        (int)drawCmd.ElemCount / 3
                    );
                }
            }

            vtxOffset += cmdList.VtxBuffer.Size;
            idxOffset += cmdList.IdxBuffer.Size;
        }
    }

    /// <summary>
    /// Handles text input events and forwards them to ImGui, excluding tab characters.
    /// </summary>
    /// <param name="c">The input character to process.</param>
    private static void HandleInput(char c)
    {
        if (c != '\t')
        {
            ImGui.GetIO().AddInputCharacter(c);
        }
    }

    /// <summary>
    /// Releases all graphics resources including textures, meshes, and effects.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        FreeFontAtlasAllocations();
        if (UnbindTexture(_fontTexture))
        {
            _fontTexture.Dispose();
        }
    }

    public void SetDisplayScale(float scale)
    {
        _pendingDisplayScale = scale;
    }

    private void ApplyStyleScale()
    {
        SetupStyle();
        ImGui.GetStyle().ScaleAllSizes(_displayScale);
    }

    private unsafe void BuildFontAtlas()
    {
        var io = ImGui.GetIO();
        _defaultFont = LoadFont("Fonts/ProggyForever", io.Fonts.GetGlyphRangesDefault(), size: 13f);
        LoadIconsFont("Fonts/Lucide", Lucide.IconMin, Lucide.IconMax, size: 16f, yOffset: 4f);
        LoadIconsFont("Fonts/AtIcons", AtIcons.IconMin, AtIcons.IconMax, size: 13f, yOffset: 0f);
        LoadLanguageFonts(size: 24f);

        io.Fonts.GetTexDataAsRGBA32(
            out byte* pixelData,
            out var width,
            out var height,
            out var bytesPerPixel);

        io.Fonts.ClearInputData();
        FreeFontAtlasAllocations();

        _fontTexture = new Texture2D(_game.GraphicsDevice, width, height, false, SurfaceFormat.Color);
        _fontTexture.SetDataPointerEXT(0, null, (nint)pixelData, width * height * bytesPerPixel);
        io.Fonts.SetTexID(BindTexture(_fontTexture));
        io.Fonts.ClearTexData();
    }

    private void LoadLanguageFonts(float size)
    {
        var io = ImGui.GetIO();
        foreach (var (path, languageFont) in _languageFonts)
        {
            languageFont.Ptr = LoadFont(path, languageFont.Language.GetGlyphRange(io.Fonts), size);
        }
    }

    public void AcquireLanguageFont(Language language)
    {
        var path = language.GetFont();
        if (_languageFonts.TryGetValue(path, out var font))
        {
            font.ReferenceCount++;
        }
        else
        {
            _languageFonts[path] = new LanguageFont(language);
            _fontAtlasDirty = true;
        }
    }

    public ImFontPtr GetLanguageFont(Language language)
    {
        var path = language.GetFont();
        return _languageFonts.TryGetValue(path, out var font) && font.Ptr.HasValue
            ? font.Ptr.Value
            : _defaultFont;
    }

    public void ReleaseLanguageFont(Language language)
    {
        var path = language.GetFont();
        if (!_languageFonts.TryGetValue(path, out var font))
        {
            return;
        }

        font.ReferenceCount--;
        if (font.ReferenceCount == 0)
        {
            _languageFonts.Remove(path);
            _fontAtlasDirty = true;
        }
    }

    /// <summary>
    /// Loads font into ImGui from game content.
    /// </summary>
    private unsafe ImFontPtr LoadFont(string path, nint glyphRanges, float size)
    {
        Logger.Debug("Loading font - {0}", path);
        var io = ImGui.GetIO();
        var content = _game.GetService<ContentService>().Global;
        var data = content.LoadBytes(path);
        var dataHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
        var config = ImGuiNative.ImFontConfig_ImFontConfig();
        config->MergeMode = 0;
        config->FontDataOwnedByAtlas = 0;
        var font = io.Fonts.AddFontFromMemoryTTF(
            dataHandle.AddrOfPinnedObject(), data.Length, size * _displayScale, config, glyphRanges);
        _fontDataHandles.Add(dataHandle);
        ImGuiNative.ImFontConfig_destroy(config);
        return font;
    }

    /// <summary>
    /// Loads icons font into ImGui from game content.
    /// </summary>
    /// <remarks>
    /// This method merges icons into previously loaded font.
    /// </remarks>
    private unsafe void LoadIconsFont(string path, ushort min, ushort max, float size, float yOffset)
    {
        Logger.Debug("Loading icons font - {0}", path);
        var io = ImGui.GetIO();
        var content = _game.GetService<ContentService>().Global;
        var data = content.LoadBytes(path);
        var dataHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
        var config = ImGuiNative.ImFontConfig_ImFontConfig();
        config->MergeMode = 1;
        config->FontDataOwnedByAtlas = 0;
        config->GlyphMinAdvanceX = size * _displayScale;
        config->GlyphOffset = new NVector2(0, yOffset);

        var rangesPtr = CopyGlyphRanges(min, max);
        io.Fonts.AddFontFromMemoryTTF(
            dataHandle.AddrOfPinnedObject(), data.Length, size * _displayScale, config, rangesPtr);

        _fontDataHandles.Add(dataHandle);
        _fontGlyphRangeAllocations.Add(rangesPtr);
        ImGuiNative.ImFontConfig_destroy(config);
    }

    private static unsafe nint CopyGlyphRanges(ushort min, ushort max)
    {
        var ptr = (ushort*)Marshal.AllocHGlobal(sizeof(ushort) * 3);
        ptr[0] = min;
        ptr[1] = max;
        ptr[2] = 0;
        return (nint)ptr;
    }

    private void FreeFontAtlasAllocations()
    {
        foreach (var handle in _fontDataHandles)
        {
            handle.Free();
        }

        foreach (var allocation in _fontGlyphRangeAllocations)
        {
            Marshal.FreeHGlobal(allocation);
        }

        _fontDataHandles.Clear();
        _fontGlyphRangeAllocations.Clear();
    }

    private static float GetDisplayScale(IntPtr windowHandle)
    {
#if __MACOS__
        return SDL.SDL_GetWindowDisplayScale(windowHandle);
#else
        return SDL.SDL_GetDisplayContentScale(SDL.SDL_GetDisplayForWindow(windowHandle));
#endif
    }

    private static class DrawVertDeclaration
    {
        public static readonly VertexDeclaration Declaration;

        public static readonly int Size;

        static DrawVertDeclaration()
        {
            unsafe { Size = sizeof(ImDrawVert); }

            Declaration = new VertexDeclaration(
                Size,

                // Position
                new VertexElement(0, VertexElementFormat.Vector2, VertexElementUsage.Position, 0),

                // UV
                new VertexElement(8, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),

                // Color
                new VertexElement(16, VertexElementFormat.Color, VertexElementUsage.Color, 0)
            );
        }
    }

    private sealed class LanguageFont(Language language)
    {
        public Language Language { get; } = language;

        public ImFontPtr? Ptr { get; set; }

        public int ReferenceCount { get; set; } = 1;
    }
}