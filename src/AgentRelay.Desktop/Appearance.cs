using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media;
using AgentRelay.Core;

namespace AgentRelay.Desktop;

public sealed record DesktopPreferences(
    [property: JsonRequired] string Language = "zh-CN",
    [property: JsonRequired] string Palette = "sage",
    string? Accent = null);

public sealed record AppearanceOption(string Id, string Label);

public static class Appearance
{
    public static IReadOnlyList<AppearanceOption> Languages { get; } = Array.AsReadOnly(new[]
    {
        new AppearanceOption("en", "English"), new AppearanceOption("zh-CN", "简体中文"),
        new AppearanceOption("zh-TW", "繁體中文"), new AppearanceOption("ja", "日本語"),
        new AppearanceOption("ko", "한국어"), new AppearanceOption("es", "Español")
    });

    public static IReadOnlyList<AppearanceOption> Palettes { get; } = Array.AsReadOnly(new[]
    {
        new AppearanceOption("sage", "Sage"), new AppearanceOption("clay", "Clay"),
        new AppearanceOption("mist", "Mist"), new AppearanceOption("rose", "Rose")
    });

    public static DesktopPreferences Load(string home)
    {
        var path = Path.Combine(home, "desktop.preferences.json");
        if (!File.Exists(path)) return new DesktopPreferences(SystemLanguage());
        DesktopPreferences preferences;
        try
        {
            preferences = RelayJson.Read<DesktopPreferences>(File.ReadAllText(path));
        }
        catch (JsonException error)
        {
            throw new RelayException("invalid_desktop_preferences", $"Invalid desktop preferences in {path}: {error.Message}");
        }
        Validate(preferences);
        return preferences;
    }

    public static void Save(string home, DesktopPreferences preferences)
    {
        Validate(preferences);
        Directory.CreateDirectory(home);
        var path = Path.Combine(home, "desktop.preferences.json");
        var temporary = path + ".relay-" + Guid.NewGuid() + ".tmp";
        try
        {
            File.WriteAllText(temporary, RelayJson.Write(preferences));
            File.Move(temporary, path, true);
        }
        finally { File.Delete(temporary); }
    }

    public static void Apply(Window window, DesktopPreferences preferences)
    {
        Validate(preferences);
        var colors = preferences.Palette switch
        {
            "sage" => new[] { "#F1F2EC", "#E7EBE1", "#516654", "#DFE7DB", "#303E36" },
            "clay" => new[] { "#F4EFE9", "#EEE3D9", "#855C48", "#EFDFD1", "#493B34" },
            "mist" => new[] { "#EEF1F3", "#E2E9ED", "#526B7E", "#DDE7EE", "#303E49" },
            "rose" => new[] { "#F5EFF0", "#EFE2E5", "#865C69", "#EFDEE3", "#48363D" },
            _ => throw new InvalidOperationException("Validated palette was not found.")
        };
        var accent = ParseColor(preferences.Accent ?? colors[2]);
        var accentText = ReadableAccent(accent);
        Set("PageBrush", ParseColor(colors[0]));
        Set("SurfaceBrush", ParseColor("#FDFCF9"));
        Set("SurfaceAltBrush", ParseColor(colors[1]));
        Set("TextBrush", ParseColor("#292E2B"));
        Set("MutedBrush", ParseColor("#5B645F"));
        Set("BorderBrush", ParseColor("#BFC5BD"));
        Set("AccentBrush", accentText);
        Set("AccentSoftBrush", preferences.Accent is null ? ParseColor(colors[3]) : Blend(accent, ParseColor("#FDFCF9"), 0.16));
        Set("NavBrush", ParseColor(colors[4]));
        Set("NavTextBrush", ParseColor("#F4F3ED"));
        Set("AllowedBrush", ParseColor("#47694F"));
        Set("DeniedBrush", ParseColor("#9B4B4B"));
        Set("UnavailableBrush", ParseColor("#71736F"));

        void Set(string key, Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            window.Resources[key] = brush;
        }
    }

    private static void Validate(DesktopPreferences preferences)
    {
        // Preferences are editable user input; invalid values must produce an actionable error.
        if (!Languages.Any(option => option.Id == preferences.Language))
            throw new RelayException("invalid_desktop_language", $"Unknown desktop language: {preferences.Language}. Supported: en, zh-CN, zh-TW, ja, ko, es.");
        if (!Palettes.Any(option => option.Id == preferences.Palette))
            throw new RelayException("invalid_desktop_palette", $"Unknown desktop palette: {preferences.Palette}. Supported: sage, clay, mist, rose.");
        if (preferences.Accent is not null && (preferences.Accent.Length != 7 || preferences.Accent[0] != '#' ||
            !preferences.Accent.Skip(1).All(Uri.IsHexDigit)))
            throw new RelayException("invalid_desktop_accent", "Desktop accent must use #RRGGBB, for example #637A68.");
    }

    private static string SystemLanguage()
    {
        var culture = CultureInfo.CurrentUICulture;
        var exact = Languages.FirstOrDefault(option => option.Id == culture.Name);
        if (exact is not null) return exact.Id;
        if (culture.TwoLetterISOLanguageName == "zh")
            return culture.Name.Contains("Hant", StringComparison.OrdinalIgnoreCase) ||
                culture.Name.EndsWith("-HK", StringComparison.OrdinalIgnoreCase) ||
                culture.Name.EndsWith("-MO", StringComparison.OrdinalIgnoreCase) ? "zh-TW" : "zh-CN";
        return Languages.Any(option => option.Id == culture.TwoLetterISOLanguageName) ? culture.TwoLetterISOLanguageName : "en";
    }

    private static Color ParseColor(string color) => Color.FromRgb(
        byte.Parse(color.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
        byte.Parse(color.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
        byte.Parse(color.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));

    private static Color Blend(Color first, Color second, double amount) => Color.FromRgb(
        (byte)Math.Round(first.R * amount + second.R * (1 - amount)),
        (byte)Math.Round(first.G * amount + second.G * (1 - amount)),
        (byte)Math.Round(first.B * amount + second.B * (1 - amount)));

    private static Color ReadableAccent(Color color)
    {
        // Accent is used for white button labels and links on a pale surface.
        // Darken only when needed to preserve at least 4.5:1 contrast with white.
        while (Luminance(color) > 0.1833) color = Blend(color, Colors.Black, 0.94);
        return color;
    }

    private static double Luminance(Color color)
    {
        static double Linear(byte channel)
        {
            var value = channel / 255d;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
    }
}
