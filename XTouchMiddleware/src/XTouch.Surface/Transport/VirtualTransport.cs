using XTouch.Abstractions;
using XTouch.Surface.Mcu;

namespace XTouch.Surface.Transport;

/// <summary>
/// In-memory surface. Used when no hardware is present (the UI acts as the surface) and in tests.
/// Keeps the most recent outgoing messages for inspection.
/// </summary>
public sealed class VirtualTransport : IMidiTransport
{
    private readonly List<byte[]> _sent = [];
    private readonly Lock _gate = new();

    public string Name => "Virtual X-Touch";
    public bool IsConnected { get; private set; }
    public int MaxRetained { get; set; } = 10_000;
    public event Action<ReadOnlyMemory<byte>>? MessageReceived;
    public event Action<bool>? ConnectionChanged;

    public Task ConnectAsync(CancellationToken ct)
    {
        if (!IsConnected)
        {
            IsConnected = true;
            ConnectionChanged?.Invoke(true);
        }
        return Task.CompletedTask;
    }

    public void Send(ReadOnlySpan<byte> message)
    {
        if (!IsConnected) return;
        lock (_gate)
        {
            _sent.Add(message.ToArray());
            if (_sent.Count > MaxRetained) _sent.RemoveRange(0, _sent.Count - MaxRetained);
        }
    }

    public IReadOnlyList<byte[]> Sent { get { lock (_gate) return [.. _sent]; } }

    public void ClearSent() { lock (_gate) _sent.Clear(); }

    // ----- Simulate the operator -----
    public void Inject(params byte[] message) => MessageReceived?.Invoke(message);
    public void TouchFader(int strip, bool touched) => Inject(0x90, (byte)(0x68 + strip), (byte)(touched ? 0x7F : 0));
    public void MoveFader(int strip, double value) => Inject(McuEncoder.Fader(strip, value));
    public void Press(int note) { Inject(0x90, (byte)note, 0x7F); Inject(0x90, (byte)note, 0x00); }
    public void Turn(int strip, int delta) =>
        Inject(0xB0, (byte)(0x10 + strip), (byte)(delta < 0 ? 0x40 | Math.Min(-delta, 63) : Math.Min(delta, 63)));

    public void Unplug()
    {
        if (!IsConnected) return;
        IsConnected = false;
        ConnectionChanged?.Invoke(false);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
