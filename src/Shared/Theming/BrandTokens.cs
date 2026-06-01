using Microsoft.UI;
using Windows.UI;

namespace LocalAiDemos.Shared.Theming;

/// <summary>
/// Brand colour tokens swapped per theme. The active palette is the one named
/// in <see cref="Settings.AppSettings.ThemeName"/>; consumers can also build
/// their own and assign at runtime.
/// </summary>
public sealed class BrandTokens
{
    public Color Accent { get; init; } = Color.FromArgb(0xFF, 0x4F, 0x46, 0xE5);   // indigo
    public Color AccentDim { get; init; } = Color.FromArgb(0xFF, 0x6D, 0x28, 0xD9); // purple
    public Color Background { get; init; } = Colors.Transparent;
    public string Name { get; init; } = "Default";

    public static BrandTokens Default => new();

    public static BrandTokens Houston => new()
    {
        Name = "Houston",
        Accent = Color.FromArgb(0xFF, 0xFF, 0x6B, 0x6B),
        AccentDim = Color.FromArgb(0xFF, 0xFF, 0x8A, 0x65),
    };

    public static BrandTokens EY => new()
    {
        Name = "EY",
        Accent = Color.FromArgb(0xFF, 0xFF, 0xE6, 0x00),
        AccentDim = Color.FromArgb(0xFF, 0x2E, 0x2E, 0x38),
    };

    public static BrandTokens CBA => new()
    {
        Name = "CBA",
        Accent = Color.FromArgb(0xFF, 0xFF, 0xCC, 0x00),
        AccentDim = Color.FromArgb(0xFF, 0xE6, 0x00, 0x23),
    };

    public static BrandTokens FromName(string? name) => name switch
    {
        "Houston" => Houston,
        "EY" => EY,
        "CBA" => CBA,
        _ => Default,
    };
}
