using System.Globalization;
using Serilog.Events;

namespace FezEditor.Tools;

public class Args
{
    public LogEventLevel LogLevel { get; private set; } = LogEventLevel.Information;

    public static Args Parse(string[] args)
    {
        var result = new Args();
        var queue = new Queue<string>(args);

        while (queue.Count > 0)
        {
            switch (queue.Dequeue().ToLower(CultureInfo.InvariantCulture))
            {
                case "--log-level":
                    if (queue.Count > 0 && Enum.TryParse<LogEventLevel>(queue.Dequeue(), true, out var level))
                    {
                        result.LogLevel = level;
                    }
                    break;
            }
        }

        return result;
    }
}
