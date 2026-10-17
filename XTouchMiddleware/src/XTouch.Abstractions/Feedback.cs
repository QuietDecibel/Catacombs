namespace XTouch.Abstractions;

public enum LedState : byte { Off = 0x00, Blink = 0x01, On = 0x7F }

public enum RingMode : byte { Dot = 0, BoostCut = 1, Wrap = 2, Spread = 3 }

/// <summary>Encoder LED ring. Position 0 = all off, 1..11 = LED index.</summary>
public readonly record struct RingState(RingMode Mode, int Position, bool Center)
{
    public static readonly RingState Off = new(RingMode.Dot, 0, false);

    /// <summary>Converts a normalized value to a ring position for the given mode.</summary>
    public static RingState FromValue(double value, RingMode mode)
    {
        value = Math.Clamp(value, 0, 1);
        int position = mode switch
        {
            RingMode.Wrap => (int)Math.Round(value * 11),
            RingMode.Spread => 1 + (int)Math.Round(value * 5),
            _ => 1 + (int)Math.Round(value * 10),
        };
        return new RingState(mode, position, false);
    }
}

/// <summary>X-Touch scribble strip backlight colors (same order as X32 channel colors 0..7).</summary>
public enum LcdColor : byte { Off = 0, Red = 1, Green = 2, Yellow = 3, Blue = 4, Magenta = 5, Cyan = 6, White = 7 }

/// <summary>Receives the desired surface state from the engine. Implemented by the feedback renderer.</summary>
public interface IFeedbackSink
{
    void Publish(SurfaceSnapshot desired);
}
