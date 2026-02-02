using Microsoft.Xna.Framework;

namespace FezEditor;

public class FezEditor : Game
{
    [STAThread]
    private static void Main(string[] args)
    {
        using var editor = new FezEditor();
        editor.Run();
    }
    
    private readonly GraphicsDeviceManager _deviceManager;
    
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
    
    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);
        base.Draw(gameTime);
    }
}