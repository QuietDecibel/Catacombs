using XTouch.Abstractions;
using XTouch.Surface.Mcu;

namespace XTouch.Surface.Transport;

public sealed record MidiTraceEntry(DateTimeOffset Time, string Direction, byte[] Bytes, string Description)
{
    public string Hex => Convert.ToHexString(Bytes) is var h && h.Length > 0
        ? string.Join(' ', Enumerable.Range(0, h.Length / 2).Select(i => h.Substring(i * 2, 2)))
        : "";
}

/// <summary>Global MIDI trace switch. When enabled, every message in and out is reported.</summary>
public sealed class MidiTrace
{
    public volatile bool Enabled;
    public event Action<MidiTraceEntry>? Entry;
    internal void Write(string direction, ReadOnlySpan<byte> m, string description) =>
        Entry?.Invoke(new MidiTraceEntry(DateTimeOffset.Now, direction, m.ToArray(), description));
}

/// <summary>Decorator that reports traffic to <see cref="MidiTrace"/> when enabled.</summary>
public sealed class TracingTransport : IMidiTransport
{
    private readonly IMidiTransport _inner;
    private readonly MidiTrace _trace;

    public TracingTransport(IMidiTransport inner, MidiTrace trace)
    {
        _inner = inner;
        _trace = trace;
        _inner.MessageReceived += m =>
        {
            if (_trace.Enabled) _trace.Write("IN", m.Span, McuDescriber.DescribeInput(m.Span));
            MessageReceived?.Invoke(m);
        };
        _inner.ConnectionChanged += c => ConnectionChanged?.Invoke(c);
    }

    public IMidiTransport Inner => _inner;
    public string Name => _inner.Name;
    public bool IsConnected => _inner.IsConnected;
    public event Action<ReadOnlyMemory<byte>>? MessageReceived;
    public event Action<bool>? ConnectionChanged;

    public Task ConnectAsync(CancellationToken ct) => _inner.ConnectAsync(ct);

    public void Send(ReadOnlySpan<byte> message)
    {
        if (_trace.Enabled) _trace.Write("OUT", message, McuDescriber.DescribeOutput(message));
        _inner.Send(message);
    }

    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}
