using System.Windows.Media;

namespace Strate.Brand;

public sealed class RoleSet
{
    public required Color Primary { get; init; }
    public required Color OnPrimary { get; init; }
    public required Color Secondary { get; init; }
    public required Color Surface { get; init; }
    public required Color OnSurface { get; init; }
    public required Color OnSurfaceVariant { get; init; }
    public required Color SurfaceContainerLow { get; init; }
    public required Color SurfaceContainerHigh { get; init; }
    public required Color SurfaceContainerHighest { get; init; }
    public required Color Outline { get; init; }
    public required Color OutlineVariant { get; init; }
    public required Color Error { get; init; }
    public required Color InverseSurface { get; init; }
}

public static class StrateColors
{
    public static RoleSet Light { get; } = new()
    {
        Primary = C(0x06, 0x67, 0x7F),
        OnPrimary = C(0xFF, 0xFF, 0xFF),
        Secondary = C(0x4C, 0x62, 0x6A),
        Surface = C(0xF5, 0xFA, 0xFD),
        OnSurface = C(0x17, 0x1C, 0x1F),
        OnSurfaceVariant = C(0x40, 0x48, 0x4C),
        SurfaceContainerLow = C(0xEF, 0xF4, 0xF7),
        SurfaceContainerHigh = C(0xE4, 0xE9, 0xEC),
        SurfaceContainerHighest = C(0xDE, 0xE3, 0xE6),
        Outline = C(0x70, 0x78, 0x7C),
        OutlineVariant = C(0xBF, 0xC8, 0xCC),
        Error = C(0xBA, 0x1A, 0x1A),
        InverseSurface = C(0x2C, 0x31, 0x34)
    };

    public static RoleSet Dark { get; } = new()
    {
        Primary = C(0x88, 0xD1, 0xEB),
        OnPrimary = C(0x00, 0x35, 0x43),
        Secondary = C(0xB3, 0xCA, 0xD4),
        Surface = C(0x0F, 0x14, 0x16),
        OnSurface = C(0xDE, 0xE3, 0xE6),
        OnSurfaceVariant = C(0xBF, 0xC8, 0xCC),
        SurfaceContainerLow = C(0x17, 0x1C, 0x1F),
        SurfaceContainerHigh = C(0x25, 0x2B, 0x2D),
        SurfaceContainerHighest = C(0x30, 0x36, 0x38),
        Outline = C(0x8A, 0x92, 0x96),
        OutlineVariant = C(0x40, 0x48, 0x4C),
        Error = C(0xFF, 0xB4, 0xAB),
        InverseSurface = C(0xDE, 0xE3, 0xE6)
    };

    private static Color C(byte r, byte g, byte b) => Color.FromRgb(r, g, b);
}
