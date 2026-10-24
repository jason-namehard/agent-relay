using System.IO;
using System.Text.Json;
using AgentRelay.Core;

namespace AgentRelay.Desktop;

public static class RelayLocation
{
    private sealed record Location(string Home);
    public static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AgentRelay", "relay.location.json");

    public static string? Load()
    {
        if (!File.Exists(FilePath)) return null;
        try
        {
            var value = RelayJson.Read<Location>(File.ReadAllText(FilePath));
            if (string.IsNullOrWhiteSpace(value.Home) || !Path.IsPathFullyQualified(value.Home))
                throw new RelayException("invalid_relay_location", $"中转站位置记录无效：{FilePath}");
            return value.Home;
        }
        catch (JsonException error)
        {
            throw new RelayException("invalid_relay_location", $"无法解析中转站位置记录 {FilePath}：{error.Message}");
        }
    }

    public static void Save(string home)
    {
        var value = new Location(Path.GetFullPath(home));
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, RelayJson.Write(value));
        File.Move(temporary, FilePath, true);
    }
}
