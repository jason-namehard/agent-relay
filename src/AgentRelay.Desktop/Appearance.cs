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
    [property: JsonRequired] string Palette = "mist",
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
        new AppearanceOption("mist", "Mist"), new AppearanceOption("sage", "Sage"),
        new AppearanceOption("clay", "Clay"), new AppearanceOption("rose", "Rose")
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
        window.FontFamily=new FontFamily(preferences.Language is "zh-CN" or "zh-TW" ? "Microsoft YaHei UI, Segoe UI" : "Segoe UI");
        Validate(preferences);
        var colors = preferences.Palette switch
        {
            "sage" => new[] { "#E8EDEA", "#7C8F86", "#2E3A35" },
            "clay" => new[] { "#F1E7DC", "#A9825F", "#3A2F27" },
            "mist" => new[] { "#E6EBEF", "#6E8092", "#2C3742" },
            "rose" => new[] { "#F2E7E9", "#A8858C", "#38292D" },
            _ => throw new InvalidOperationException("Validated palette was not found.")
        };
        var accent = ParseColor(preferences.Accent ?? colors[1]);
        var accentText = ReadableAccent(accent);
        Set("PageBrush", ParseColor("#FAFAF9"));
        Set("SurfaceBrush", Colors.White);
        Set("SurfaceAltBrush", ParseColor("#F4F4F2"));
        Set("TextBrush", ParseColor("#262626"));
        Set("MutedBrush", ParseColor("#6B6B6B"));
        Set("BorderBrush", ParseColor("#E6E3DE"));
        Set("AccentBrush", accentText);
        Set("AccentSoftBrush", preferences.Accent is null ? ParseColor(colors[0]) : Blend(accent, Colors.White, 0.16));
        Set("NavBrush", ParseColor(colors[2]));
        Set("NavTextBrush", ParseColor("#F6F6F4"));
        Set("AllowedBrush", ParseColor("#4C7A5A"));
        Set("DeniedBrush", ParseColor("#A6473F"));
        Set("UnavailableBrush", ParseColor("#8A8A8A"));

        void Set(string key, Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            window.Resources[key] = brush;
        }
    }

    public static Color ParseAccent(string accent)
    {
        // The accent can be entered or pasted by the user; reject invalid color text at this boundary.
        if (accent.Length != 7 || accent[0] != '#' || !accent.Skip(1).All(Uri.IsHexDigit))
            throw new RelayException("invalid_desktop_accent", "Desktop accent must use #RRGGBB, for example #637A68.");
        return ParseColor(accent);
    }

    private static void Validate(DesktopPreferences preferences)
    {
        // Preferences are editable user input; invalid values must produce an actionable error.
        if (!Languages.Any(option => option.Id == preferences.Language))
            throw new RelayException("invalid_desktop_language", $"Unknown desktop language: {preferences.Language}. Supported: en, zh-CN, zh-TW, ja, ko, es.");
        if (!Palettes.Any(option => option.Id == preferences.Palette))
            throw new RelayException("invalid_desktop_palette", $"Unknown desktop palette: {preferences.Palette}. Supported: sage, clay, mist, rose.");
        if (preferences.Accent is not null) ParseAccent(preferences.Accent);
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
