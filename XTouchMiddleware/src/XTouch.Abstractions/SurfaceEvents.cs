namespace XTouch.Abstractions;

/// <summary>Input coming from the control surface, already decoded from MIDI.</summary>
public abstract record SurfaceEvent;

/// <summary>Fader moved. Strip 0..7 = channel strips, 8 = master. Value is normalized 0..1.</summary>
public sealed record FaderMoved(int Strip, double Value) : SurfaceEvent;

public sealed record FaderTouched(int Strip, bool IsTouched) : SurfaceEvent;

/// <summary>Button pressed/released. Note is the MCU note number (see <see cref="Mcu.McuConstants"/>).</summary>
public sealed record ButtonChanged(int Note, bool IsPressed) : SurfaceEvent;

/// <summary>Encoder turned. Positive delta = clockwise ticks.</summary>
public sealed record EncoderTurned(int Strip, int Delta) : SurfaceEvent;

public sealed record JogTurned(int Delta) : SurfaceEvent;

public sealed record UnknownMessage(byte[] Raw) : SurfaceEvent;
