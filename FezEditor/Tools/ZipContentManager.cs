using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework.Content;

namespace FezEditor.Tools;

public class ZipContentManager : ContentManager, IContentManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    private readonly ZipArchive _archive;

    public ZipContentManager(IServiceProvider serviceProvider, string zipPath)
        : base(serviceProvider)
    {
        _archive = ZipFile.OpenRead(zipPath);
        CheckContentsVersion();
    }

    public T LoadJson<T>(string assetName)
    {
        var path = Path.ChangeExtension(assetName, ".json");
        var entry = _archive.GetEntry(path)!;
        using var stream = entry.Open();
        return JsonSerializer.Deserialize<T>(stream, JsonOptions)!;
    }

    public byte[] LoadBytes(string assetName)
    {
        // DeflateStream doesn't support Length property
        using var stream = LoadStream(assetName);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    public Stream LoadStream(string assetName)
    {
        var entry = _archive.GetEntry(assetName);
        if (entry != null)
        {
            return entry.Open();
        }

        foreach (var entry1 in _archive.Entries)
        {
            var path = Path.ChangeExtension(entry1.FullName, null);
            if (path.Equals(assetName, StringComparison.Ordinal))
            {
                return entry1.Open();
            }
        }

        throw new FileNotFoundException();
    }

    protected override Stream OpenStream(string assetName)
    {
        using var stream = LoadStream(assetName);
        var memory = new MemoryStream();
        stream.CopyTo(memory);
        memory.Position = 0;
        return memory;
    }

    private void CheckContentsVersion()
    {
        var entry = _archive.GetEntry(".version")!;

        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        var rule = reader.ReadToEnd().Trim();

        string op;
        string versionStr;

        if (rule.StartsWith(">="))
        {
            op = ">=";
            versionStr = rule[2..].Trim();
        }
        else if (rule.StartsWith("<="))
        {
            op = "<=";
            versionStr = rule[2..].Trim();
        }
        else if (rule.StartsWith("=="))
        {
            op = "==";
            versionStr = rule[2..].Trim();
        }
        else if (rule.StartsWith('>'))
        {
            op = ">";
            versionStr = rule[1..].Trim();
        }
        else if (rule.StartsWith('<'))
        {
            op = "<";
            versionStr = rule[1..].Trim();
        }
        else
        {
            op = "==";
            versionStr = rule;
        }

        var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
        var contentsVersion = Version.Parse(versionStr);

        var compatible = op switch
        {
            ">=" => contentsVersion >= assemblyVersion,
            "<=" => contentsVersion <= assemblyVersion,
            "==" => contentsVersion == assemblyVersion,
            ">" => contentsVersion > assemblyVersion,
            "<" => contentsVersion < assemblyVersion,
            _ => false
        };

        if (!compatible)
        {
            throw new NotSupportedException($"Invalid version: {rule}, requires: >={assemblyVersion}");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _archive.Dispose();
        }

        base.Dispose(disposing);
    }
}