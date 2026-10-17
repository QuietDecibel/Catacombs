using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace XTouch.UI.Converters;

/// <summary>X-Touch scribble-strip color name → backlight brush.</summary>
public sealed class LcdColorToBrushConverter : IValueConverter
{
    public static readonly LcdColorToBrushConverter Instance = new();

    private static readonly Dictionary<string, IBrush> Brushes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Off"] = new SolidColorBrush(Color.Parse("#15171B")),
        ["Red"] = new SolidColorBrush(Color.Parse("#E5484D")),
        ["Green"] = new SolidColorBrush(Color.Parse("#30A46C")),
        ["Yellow"] = new SolidColorBrush(Color.Parse("#F5D90A")),
        ["Blue"] = new SolidColorBrush(Color.Parse("#3E63DD")),
        ["Magenta"] = new SolidColorBrush(Color.Parse("#D23197")),
        ["Cyan"] = new SolidColorBrush(Color.Parse("#05A2C2")),
        ["White"] = new SolidColorBrush(Color.Parse("#EDEEF0")),
    };

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Brushes.TryGetValue(value as string ?? "Off", out var b) ? b : Brushes["Off"];

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Text color that stays readable on each backlight.</summary>
public sealed class LcdColorToForegroundConverter : IValueConverter
{
    public static readonly LcdColorToForegroundConverter Instance = new();
    private static readonly IBrush Dark = new SolidColorBrush(Color.Parse("#111111"));
    private static readonly IBrush Light = new SolidColorBrush(Color.Parse("#E8E8E8"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        (value as string) is "Off" or "Blue" or "Magenta" or "Red" ? Light : Dark;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>bool → green/red status dot.</summary>
public sealed class BoolToStatusBrushConverter : IValueConverter
{
    public static readonly BoolToStatusBrushConverter Instance = new();
    private static readonly IBrush Ok = new SolidColorBrush(Color.Parse("#36D16B"));
    private static readonly IBrush Bad = new SolidColorBrush(Color.Parse("#FF4D4D"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is true ? Ok : Bad;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Log level → brush.</summary>
public sealed class LogLevelToBrushConverter : IValueConverter
{
    public static readonly LogLevelToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => (value as string) switch
    {
        "Error" or "Critical" => new SolidColorBrush(Color.Parse("#FF6B6B")),
        "Warning" => new SolidColorBrush(Color.Parse("#F2D33D")),
        _ => new SolidColorBrush(Color.Parse("#A9AEB8")),
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
