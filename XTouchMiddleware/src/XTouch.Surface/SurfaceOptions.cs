namespace XTouch.Surface;

public enum SurfaceTransportKind { Midi, Virtual }

public sealed class SurfaceOptions
{
    /// <summary>"Midi" for a real X-Touch, "Virtual" to run without hardware (UI surface only).</summary>
    public SurfaceTransportKind Transport { get; set; } = SurfaceTransportKind.Midi;

    /// <summary>Substring of the MIDI input port name (case-insensitive).</summary>
    public string InputPortName { get; set; } = "X-TOUCH";

    /// <summary>Substring of the MIDI output port name (case-insensitive).</summary>
    public string OutputPortName { get; set; } = "X-TOUCH";

    public TimeSpan ReconnectInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Feedback render tick (fader motors, LEDs, LCD).</summary>
    public TimeSpan RenderInterval { get; set; } = TimeSpan.FromMilliseconds(10);

    /// <summary>Meter refresh interval. Meters decay on the device and must be resent.</summary>
    public TimeSpan MeterInterval { get; set; } = TimeSpan.FromMilliseconds(66);

    /// <summary>Blank the surface when the service stops.</summary>
    public bool BlankOnShutdown { get; set; } = true;
}
