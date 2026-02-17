using FezEditor.Components;
using FezEditor.Services;
using FezEditor.Tools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Serilog;

namespace FezEditor;

public class FezEditor : Game
{
    private static readonly ILogger Logger = Logging.Create<FezEditor>();

#if DEBUG
    public const bool IsDebugBuild = true;
#else
    public const bool IsDebugBuild = false;
#endif
    
    private readonly GraphicsDeviceManager _deviceManager;
    
    private ContentService _content = null!;
    
    private ImGuiService _imGui = null!;
    
    private RenderingService _rendering = null!;
    
    private InputService _input = null!;
    
    private EditorService _editor = null!;

    [STAThread]
    private static void Main(string[] args)
    {
        Logging.Initialize();
        Environment.SetEnvironmentVariable("FNA3D_FORCE_DRIVER", "OpenGL");
        try
        {
            using var editor = new FezEditor();
            editor.Run();
        }
        catch (Exception e)
        {
            Logger.Fatal(e, "Unhandled Exception");
        }
    }
    
    private FezEditor()
    {
        _deviceManager = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720,
            IsFullScreen = false,
            SynchronizeWithVerticalRetrace = true
        };
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
    }

    protected override void Initialize()
    {
        RepackerExtensions.Gd = GraphicsDevice;

        _content = this.CreateService<ContentService>();
        _imGui = this.CreateService<ImGuiService>();
        _rendering = this.CreateService<RenderingService>();
        this.CreateService<ResourceService>();
        _input = this.CreateService<InputService>();
        _editor = this.CreateService<EditorService>();

        Content = (ContentManager)_content.Global;
        this.AddComponent(new MenuBar(this));
        this.AddComponent(new FileBrowser(this));
        this.AddComponent(new MainLayout(this));
        _editor.OpenEditor(new WelcomeComponent(this));

        base.Initialize();
    }

    protected override void Update(GameTime gameTime)
    {
        _input.Update();
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        _rendering.Draw(gameTime);
        _imGui.BeforeLayout(gameTime);
        base.Draw(gameTime);
        _imGui.AfterLayout();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        this.RemoveServices();
    }
}