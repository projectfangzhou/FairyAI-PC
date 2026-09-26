using System.Windows;

namespace MyAiAssistant.Services;

/// <summary>
/// Screen adaptation: detects screen size, aspect ratio, and device type.
/// Tablets get PC-like UI behavior (Live2D always visible on large screens).
/// </summary>
public class ScreenAdapterService
{
    public double ScreenWidth => SystemParameters.PrimaryScreenWidth;
    public double ScreenHeight => SystemParameters.PrimaryScreenHeight;
    public double AspectRatio => ScreenWidth / ScreenHeight;
    public double DiagonalInches => EstimateDiagonal();

    /// <summary>Is this a tablet / large-screen device?</summary>
    public bool IsTablet => DiagonalInches >= 10.0 || ScreenWidth >= 1800;

    /// <summary>Is this a phone-sized screen?</summary>
    public bool IsPhone => DiagonalInches < 6.5 && ScreenWidth < 800;

    /// <summary>Get device type name.</summary>
    public string DeviceType => IsTablet ? "Tablet" : IsPhone ? "Phone" : "PC";

    /// <summary>Get recommended Live2D size.</summary>
    public (double Width, double Height) GetLive2DSize()
    {
        if (IsTablet) return (400, 500);
        if (IsPhone) return (250, 320);
        return (350, 450);
    }

    /// <summary>Get screen description for AI.</summary>
    public string GetScreenDescription() =>
        $"{DeviceType}, {ScreenWidth:F0}x{ScreenHeight:F0}, ratio {AspectRatio:F2}, ~{DiagonalInches:F1}in";

    private double EstimateDiagonal()
    {
        // Approximate diagonal in inches (assumes 96 DPI)
        var diagPixels = Math.Sqrt(ScreenWidth * ScreenWidth + ScreenHeight * ScreenHeight);
        return diagPixels / 96.0;
    }
}
