using Microsoft.Xna.Framework;
using Serilog;

namespace FezEditor;

public static class Logging
{
    private const string LogTemplate =
        "({Timestamp:HH:mm:ss.fff}) [{SourceContext}] {Level} : {Message:lj}{NewLine}{Exception}";
    
    public static void Initialize()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(outputTemplate: LogTemplate)
            .CreateLogger();

        var logger = Log.ForContext("SourceContext", "FNA");
        FNALoggerEXT.LogInfo = msg => logger.Information("{Message}", msg);
        FNALoggerEXT.LogWarn = msg => logger.Warning("{Message}", msg);
        FNALoggerEXT.LogError = msg => logger.Error("{Message}", msg);
    }

    public static ILogger Create<T>()
    {
        return Log.ForContext("SourceContext", typeof(T).Name);
    }
}