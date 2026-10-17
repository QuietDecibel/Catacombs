namespace XTouch.Targets.X32;

public sealed class X32Options
{
    /// <summary>Console IP or host name. Empty = adapter disabled (unless <see cref="Emulator"/> is on).</summary>
    public string Host { get; set; } = "";
    public int Port { get; set; } = 10023;

    /// <summary>Run a built-in X32 emulator on localhost and connect to it (no console needed).</summary>
    public bool Emulator { get; set; }

    /// <summary>UDP port for the emulator. 0 = pick a free port.</summary>
    public int EmulatorPort { get; set; } = 10023;

    /// <summary>Meter bank requested from the console (/meters/1 = all channels).</summary>
    public string MeterBank { get; set; } = "/meters/1";
}
