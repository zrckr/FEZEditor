using FezEditor.Components;
using FezEditor.Services;
using FezEditor.Tools;
using Microsoft.Xna.Framework;
using Serilog;

namespace FezEditor;

public class FezEditor : Game
{
    private static readonly ILogger Logger = Logging.Create<FezEditor>();
    
    private readonly GraphicsDeviceManager _deviceManager;
    
    private IRenderingService _rendering = null!;

    private IImGuiService _imGui = null!;
    
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
        _imGui = this.CreateService<IImGuiService, ImGuiService>();
        _rendering = this.CreateService<IRenderingService, RenderingService>();
        this.CreateService<IStateService, StateService>();
        
        this.CreateComponent<MenuBar>();
        this.CreateComponent<FileBrowser>();
        this.CreateComponent<StatusBar>();
        this.CreateComponent<MainLayout>();
        this.CreateComponent<WelcomeScreen>();

        base.Initialize();
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