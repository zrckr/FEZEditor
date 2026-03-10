using System.Globalization;
using Microsoft.Xna.Framework;
using Serilog;
using Serilog.Events;

namespace FezEditor;

public static class Logging
{
    private const string LogTemplate =
        "({Timestamp:HH:mm:ss.fff}) [{SourceContext}] {Level} : {Message:lj}{NewLine}{Exception}";

    private const string DateTimeFormat = "yyyy-MM-dd_HH-mm-ss";

    public static void Initialize(LogEventLevel level = LogEventLevel.Information)
    {
        var logFile = Path.Combine(AppContext.BaseDirectory, "Logs",
            $"[{DateTime.Now:DateTimeFormat}] {level} Log.txt");

        CleanOldLogFiles(logFile);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(level)
            .WriteTo.Console(outputTemplate: LogTemplate)
            .WriteTo.File(logFile, outputTemplate: LogTemplate)
            .CreateLogger();

        var logger = Log.ForContext("SourceContext", "FNA");
        FNALoggerEXT.LogInfo = msg => logger.Information("{Message}", msg);
        FNALoggerEXT.LogWarn = msg => logger.Warning("{Message}", msg);
        FNALoggerEXT.LogError = msg => logger.Error("{Message}", msg);
    }

    private static void CleanOldLogFiles(string logFile)
    {
        var directory = Path.GetDirectoryName(logFile)!;
        if (Directory.Exists(directory))
        {
            var cutoff = DateTime.Now.AddDays(-3);
            foreach (var file in Directory.GetFiles(directory, "*.txt"))
            {
                if (TryParseLogFileDate(file, out var fileDate) && fileDate < cutoff)
                {
                    File.Delete(file);
                }
            }
        }
    }

    private static bool TryParseLogFileDate(string file, out DateTime date)
    {
        var name = Path.GetFileNameWithoutExtension(file);
        return DateTime.TryParseExact(name[1..20], DateTimeFormat,
            CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    public static ILogger Create<T>()
    {
        return Log.ForContext("SourceContext", typeof(T).Name);
    }
}