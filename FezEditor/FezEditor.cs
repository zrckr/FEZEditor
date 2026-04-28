using FezEditor.Components;
using FezEditor.Scripting;
using FezEditor.Services;
using FezEditor.Tools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Serilog;

namespace FezEditor;

public class FezEditor : Game
{
    private static readonly ILogger Logger = Logging.Create<FezEditor>();

    public static readonly string Version = GetAssemblyVersion();

    public static readonly string Authors = GetAssemblyAuthors();

    public const string Commit = ThisAssembly.Git.Commit;

#if DEBUG
    public const bool IsDebugBuild = true;
#else
    public const bool IsDebugBuild = false;
#endif

    private readonly GraphicsDeviceManager _deviceManager;

    private AppStorageService _appStorage = null!;

    private ContentService _content = null!;

    private ImGuiService _imGui = null!;

    private RenderingService _rendering = null!;

    private ResourceService _resources = null!;

    private InputService _input = null!;

    private StatusService _status = null!;

    private EditorService _editor = null!;

    private static void Main(string[] args)
    {
        var parsedArgs = Args.Parse(args);
        Logging.Initialize(parsedArgs.LogLevel);
        Environment.SetEnvironmentVariable("FNA3D_FORCE_DRIVER", "OpenGL");
        Environment.SetEnvironmentVariable("FNA_GRAPHICS_ENABLE_HIGHDPI", "1");
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
            PreferredDepthStencilFormat = DepthFormat.Depth24Stencil8,
            IsFullScreen = false,
            SynchronizeWithVerticalRetrace = true
        };
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
    }

    protected override void Initialize()
    {
        Logger.Information("Version - {0} ({1})", Version, Commit);
        Logger.Information("Scripts - {0} entities", ScriptingApi.Entries.Length); // inits collection
        RepackerExtensions.Gd = GraphicsDevice;

        _appStorage = this.CreateService<AppStorageService>();
        _content = this.CreateService<ContentService>();
        _input = this.CreateService<InputService>();
        _imGui = this.CreateService<ImGuiService>();
        _rendering = this.CreateService<RenderingService>();
        _resources = this.CreateService<ResourceService>();
        _status = this.CreateService<StatusService>();
        _editor = this.CreateService<EditorService>();
        Content = (ContentManager)_content.Global;

        this.AddComponent(new MenuBar(this));
        this.AddComponent(new FileBrowser(this));
        this.AddComponent(new MainLayout(this));
        _editor.OpenEditor(new WelcomeComponent(this));

        _appStorage.LoadWindowState(_deviceManager);
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
        this.RemoveServices();
        base.Dispose(disposing);
    }

    private static string GetAssemblyVersion()
    {
        return ThisAssembly.Git.BaseVersion.Major + "." +
               ThisAssembly.Git.BaseVersion.Minor +
               (ThisAssembly.Git.BaseVersion.Patch != "0" ? "." + ThisAssembly.Git.BaseVersion.Patch : "");
    }

    private static string GetAssemblyAuthors()
    {
        var attrs = System.Reflection.Assembly
            .GetExecutingAssembly()
            .GetCustomAttributes(typeof(System.Reflection.AssemblyCompanyAttribute), false);
        return attrs.Length > 0
            ? ((System.Reflection.AssemblyCompanyAttribute)attrs[0]).Company
            : string.Empty;
    }
}