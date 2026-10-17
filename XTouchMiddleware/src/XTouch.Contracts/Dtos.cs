namespace XTouch.Contracts;

/// <summary>Bump when the UI ⇄ service contract changes incompatibly.</summary>
public static class ApiInfo
{
    public const int Version = 1;
    public const string HubPath = "/hub";
}

public sealed record TargetStatusDto(string Id, string DisplayName, bool Connected);

public sealed record LayerDto(string Id, string Label);

public sealed record StatusDto(
    int ApiVersion,
    bool SurfaceConnected,
    string? SurfacePort,
    string SurfaceTransport,
    IReadOnlyList<TargetStatusDto> Targets,
    string ProfileId,
    string ProfileName,
    int BankOffset,
    int ChannelCount,
    string Layer,
    IReadOnlyList<LayerDto> Layers,
    bool ShowEncoderValues,
    bool TraceEnabled)
{
    public static StatusDto Empty { get; } = new(ApiInfo.Version, false, null, "", [], "", "", 0, 0, "", [], false, false);
}

public sealed record StripDto(
    int Index,
    int? ChannelIndex,
    string ChannelKey,
    double Fader,
    bool Touched,
    string Top,
    string Bottom,
    string Color,
    int RingPosition,
    string RingMode,
    bool RingCenter,
    bool Rec,
    bool Solo,
    bool Mute,
    bool Select,
    int Meter);

/// <summary>Full surface snapshot. Leds holds the MCU LED value (0 = off, 1 = blink, 127 = on) indexed by note.</summary>
public sealed record SurfaceDto(
    IReadOnlyList<StripDto> Strips,
    double Master,
    bool MasterTouched,
    string Display,
    IReadOnlyList<int> Leds)
{
    public static SurfaceDto Empty { get; } = new([], 0, false, new string(' ', 12), []);
}

public sealed record LogEntryDto(DateTimeOffset Time, string Level, string Category, string Message);

public sealed record MidiTraceDto(DateTimeOffset Time, string Direction, string Hex, string Description);

public sealed record ProfileSummaryDto(string Id, string Name, string Target, DateTimeOffset Modified, bool IsValid, bool IsActive);

public sealed record ValidationResultDto(bool Ok, IReadOnlyList<string> Errors);

public sealed record MidiPortsDto(IReadOnlyList<string> Inputs, IReadOnlyList<string> Outputs);

/// <summary>Server → client hub method names.</summary>
public static class HubEvents
{
    public const string Status = "Status";
    public const string Surface = "Surface";
    public const string Logs = "Logs";
    public const string Trace = "Trace";
}

/// <summary>Well-known MCU notes the UI needs (mirrors XTouch.Abstractions.Mcu.McuConstants).</summary>
public static class SurfaceNotes
{
    public const int RecBase = 0x00, SoloBase = 0x08, MuteBase = 0x10, SelectBase = 0x18, VPotPushBase = 0x20;
    public const int BankLeft = 0x2E, BankRight = 0x2F, ChannelLeft = 0x30, ChannelRight = 0x31, NameValue = 0x34;
    public const int F1 = 0x36;
}
