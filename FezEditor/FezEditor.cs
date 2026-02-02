using Microsoft.Xna.Framework;
using Serilog;
using Serilog.Core;

namespace FezEditor;

public class FezEditor : Game
{
    private static readonly ILogger Logger = Logging.Create<FezEditor>();
    
    private readonly GraphicsDeviceManager _deviceManager;
    
    [STAThread]
    private static void Main(string[] args)
    {
        Logging.Initialize();
        using var editor = new FezEditor();
        editor.Run();
    }
    
    private FezEditor()
    {
        _deviceManager = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720,
            IsFullScreen = false,
            SynchronizeWithVerticalRetrace = true,
        };
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
    }

    protected override void Initialize()
    {
        base.Initialize();
        Logger.Information("Hello, world!");
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);
        base.Draw(gameTime);
    }
}