using System.Globalization;
using XTouch.Abstractions.Targets;

namespace XTouch.Core.Conversion;

/// <summary>Translates between the engine's normalized 0..1 values and a target's own values.</summary>
public interface IValueConverter
{
    ParameterValue ToTarget(double normalized);
    double ToNormalized(ParameterValue value);

    /// <summary>Short display text (fits a 7-char scribble-strip cell).</summary>
    string Format(double normalized);
}

public static class ValueConverters
{
    private static readonly Dictionary<string, IValueConverter> Registry = new(StringComparer.OrdinalIgnoreCase)
    {
        ["linear"] = new LinearConverter(),
        ["toggle"] = new ToggleConverter(inverted: false),
        ["inverted-toggle"] = new ToggleConverter(inverted: true),
        ["pan"] = new PanConverter(),
        ["x32-fader"] = new X32FaderConverter(),
        ["x32-trim"] = new RangeDbConverter(-18, 18),
        ["x32-gain"] = new RangeDbConverter(-12, 60),
    };

    public static IReadOnlyCollection<string> Names => Registry.Keys;

    public static bool TryGet(string? name, out IValueConverter converter) =>
        Registry.TryGetValue(string.IsNullOrWhiteSpace(name) ? "linear" : name, out converter!);

    public static IValueConverter Get(string? name) =>
        TryGet(name, out var c) ? c : throw new KeyNotFoundException($"Unknown converter '{name}'");
}

public sealed class LinearConverter : IValueConverter
{
    public ParameterValue ToTarget(double n) => new(Math.Clamp(n, 0, 1));
    public double ToNormalized(ParameterValue v) => Math.Clamp(v.Number, 0, 1);
    public string Format(double n) => $"{n * 100,4:0}%";
}

/// <summary>On/off. Normalized 1 = "active" (lit LED). Inverted: target 1 means inactive (e.g. X32 mix/on vs. mute).</summary>
public sealed class ToggleConverter(bool inverted) : IValueConverter
{
    public ParameterValue ToTarget(double n) => new((n >= 0.5) ^ inverted ? 1 : 0);
    public double ToNormalized(ParameterValue v) => (v.Number >= 0.5) ^ inverted ? 1 : 0;
    public string Format(double n) => n >= 0.5 ? "  ON" : "  OFF";
}

/// <summary>0..1 ⇄ L100 .. C .. R100.</summary>
public sealed class PanConverter : IValueConverter
{
    public ParameterValue ToTarget(double n) => new(Math.Clamp(n, 0, 1));
    public double ToNormalized(ParameterValue v) => Math.Clamp(v.Number, 0, 1);
    public string Format(double n)
    {
        int p = (int)Math.Round((Math.Clamp(n, 0, 1) - 0.5) * 200);
        return p == 0 ? "  <C>" : p < 0 ? $" L{-p,3}" : $" R{p,3}";
    }
}

/// <summary>Behringer X32/M32 fader law: float 0..1 ⇄ dB in four linear segments.</summary>
public sealed class X32FaderConverter : IValueConverter
{
    public ParameterValue ToTarget(double n) => new(Math.Clamp(n, 0, 1));
    public double ToNormalized(ParameterValue v) => Math.Clamp(v.Number, 0, 1);
    public string Format(double n) => FormatDb(ToDb(n));

    public static double ToDb(double f) => f switch
    {
        <= 0 => double.NegativeInfinity,
        >= 0.5 => f * 40 - 30,      // -10 .. +10 dB
        >= 0.25 => f * 80 - 50,     // -30 .. -10 dB
        >= 0.0625 => f * 160 - 70,  // -60 .. -30 dB
        _ => f * 480 - 90,          // -90 .. -60 dB
    };

    public static double FromDb(double db) => db switch
    {
        double.NegativeInfinity => 0,
        >= -10 => Math.Min(1, (db + 30) / 40),
        >= -30 => (db + 50) / 80,
        >= -60 => (db + 70) / 160,
        _ => Math.Max(0, (db + 90) / 480),
    };

    internal static string FormatDb(double db) =>
        double.IsNegativeInfinity(db) || db <= -89.9 ? "  -inf" : db.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture).PadLeft(6);
}

/// <summary>Linear 0..1 mapped onto a dB range (X32 trim/gain).</summary>
public sealed class RangeDbConverter(double minDb, double maxDb) : IValueConverter
{
    public ParameterValue ToTarget(double n) => new(Math.Clamp(n, 0, 1));
    public double ToNormalized(ParameterValue v) => Math.Clamp(v.Number, 0, 1);
    public string Format(double n) => X32FaderConverter.FormatDb(minDb + Math.Clamp(n, 0, 1) * (maxDb - minDb));
}
