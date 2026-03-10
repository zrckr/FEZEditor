using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Serilog;

namespace FezEditor.Tools;

public class DirContentManager : ContentManager, IContentManager
{
    private static readonly ILogger Logger = Logging.Create<DirContentManager>();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    private readonly DirectoryInfo _directory;

    public DirContentManager(IServiceProvider serviceProvider, string rootDirectory)
        : base(serviceProvider, rootDirectory)
    {
        _directory = new DirectoryInfo(rootDirectory);
    }

    public T LoadJson<T>(string assetName)
    {
        var file = _directory.GetFiles($"{assetName}.json").Single();
        using var stream = file.OpenRead();
        return JsonSerializer.Deserialize<T>(stream, JsonOptions)!;
    }

    public byte[] LoadBytes(string assetName)
    {
        using var stream = LoadStream(assetName);
        var data = new byte[stream.Length];
        stream.ReadExactly(data);
        return data;
    }

    public Stream LoadStream(string assetName)
    {
        var file = _directory.GetFiles($"{assetName}.*").Single();
        return file.OpenRead();
    }

    protected override Stream OpenStream(string assetName)
    {
        Logger.Debug("Loading asset - {0}", assetName);
        return base.OpenStream(assetName);
    }
}