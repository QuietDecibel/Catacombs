namespace XTouch.Abstractions.Targets;

/// <summary>Adapter-specific parameter path, e.g. "ch/03/mix/fader". Opaque to the engine.</summary>
public readonly record struct ParameterAddress(string Value)
{
    public override string ToString() => Value;
    public static implicit operator ParameterAddress(string value) => new(value);
}

/// <summary>A value in the target's own representation: numeric (Number) or text (Text).</summary>
public readonly record struct ParameterValue(double Number, string? Text = null)
{
    public static ParameterValue OfText(string text) => new(0, text);
    public override string ToString() => Text ?? Number.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
}

public abstract record TargetCommand;
public sealed record SetParameter(ParameterAddress Address, ParameterValue Value) : TargetCommand;
public sealed record QueryParameter(ParameterAddress Address) : TargetCommand;

public abstract record TargetEvent;
public sealed record ParameterChanged(ParameterAddress Address, ParameterValue Value) : TargetEvent;

/// <summary>Meter levels, linear 0..1, indexed by the adapter's meter numbering.</summary>
public sealed record MeterFrame(IReadOnlyList<float> Levels) : TargetEvent;

public enum ParameterKind { Continuous, Toggle, Text, Enum }

public sealed record CatalogParameter(string Key, string Address, ParameterKind Kind);
public sealed record CatalogChannel(string Key, string DefaultName, IReadOnlyList<CatalogParameter> Parameters);
public sealed record TargetCatalog(IReadOnlyList<CatalogChannel> Channels, IReadOnlyList<CatalogParameter> Globals)
{
    public static TargetCatalog Empty { get; } = new([], []);
}

/// <summary>Connects the engine to one controllable system (mixer, DAW, ...).</summary>
public interface ITargetAdapter : IAsyncDisposable
{
    string Id { get; }
    string DisplayName { get; }
    bool IsConnected { get; }
    TargetCatalog Catalog { get; }

    /// <summary>Runs until cancelled: connect, subscribe, receive, reconnect.</summary>
    Task RunAsync(Action<TargetEvent> sink, Action<bool> connectionChanged, CancellationToken ct);

    /// <summary>Non-blocking and thread-safe.</summary>
    void Send(TargetCommand command);

    /// <summary>Requests current values for everything in the catalog.</summary>
    void SyncAll();
}
