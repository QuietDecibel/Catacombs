using System.Collections.Immutable;
using XTouch.Abstractions.Mcu;

namespace XTouch.Abstractions;

/// <summary>
/// What the surface SHOULD look like right now. Mutable; owned by the engine loop only.
/// Use <see cref="Snapshot"/> to hand a copy to other threads.
/// </summary>
public sealed class SurfaceState
{
    public const int FaderCount = McuConstants.Strips + 1;
    public const int LedCount = McuConstants.LastLedNote + 1;

    public double[] Faders { get; } = new double[FaderCount];
    public bool[] FaderTouched { get; } = new bool[FaderCount];
    public LedState[] Leds { get; } = new LedState[LedCount];
    public RingState[] Rings { get; } = new RingState[McuConstants.Strips];
    public string[] LcdTop { get; } = Enumerable.Repeat(Blank, McuConstants.Strips).ToArray();
    public string[] LcdBottom { get; } = Enumerable.Repeat(Blank, McuConstants.Strips).ToArray();
    public LcdColor[] LcdColors { get; } = new LcdColor[McuConstants.Strips];
    public int[] Meters { get; } = new int[McuConstants.Strips];

    /// <summary>
    /// 7-segment display, 12 characters: [0..1] = assignment display, [2..11] = timecode digits, left to right.
    /// A '.' directly after a character is not supported; use <see cref="DisplayDots"/>.
    /// </summary>
    public string Display { get; set; } = new(' ', McuConstants.SevenSegDigits);
    public bool[] DisplayDots { get; } = new bool[McuConstants.SevenSegDigits];

    private const string Blank = "       ";

    public SurfaceSnapshot Snapshot() => new(
        [.. Faders], [.. FaderTouched], [.. Leds], [.. Rings],
        [.. LcdTop], [.. LcdBottom], [.. LcdColors], [.. Meters],
        Display.PadRight(McuConstants.SevenSegDigits)[..McuConstants.SevenSegDigits], [.. DisplayDots]);
}

/// <summary>Immutable copy of <see cref="SurfaceState"/>, safe to share between threads.</summary>
public sealed record SurfaceSnapshot(
    ImmutableArray<double> Faders,
    ImmutableArray<bool> FaderTouched,
    ImmutableArray<LedState> Leds,
    ImmutableArray<RingState> Rings,
    ImmutableArray<string> LcdTop,
    ImmutableArray<string> LcdBottom,
    ImmutableArray<LcdColor> LcdColors,
    ImmutableArray<int> Meters,
    string Display,
    ImmutableArray<bool> DisplayDots)
{
    public static SurfaceSnapshot Blank { get; } = new SurfaceState().Snapshot();
}
