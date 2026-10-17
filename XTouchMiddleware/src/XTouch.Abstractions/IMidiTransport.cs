namespace XTouch.Abstractions;

/// <summary>Raw MIDI byte I/O to a control surface.</summary>
public interface IMidiTransport : IAsyncDisposable
{
    string Name { get; }
    bool IsConnected { get; }

    /// <summary>Raised with one complete MIDI message per call (SysEx includes F0..F7). May fire on any thread.</summary>
    event Action<ReadOnlyMemory<byte>>? MessageReceived;

    event Action<bool>? ConnectionChanged;

    /// <summary>Attempts to connect until connected or cancelled.</summary>
    Task ConnectAsync(CancellationToken ct);

    /// <summary>Sends one complete MIDI message. Thread-safe; silently ignored when disconnected.</summary>
    void Send(ReadOnlySpan<byte> message);
}
