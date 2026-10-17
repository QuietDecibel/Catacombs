using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XTouch.Abstractions;

namespace XTouch.Surface.Transport;

/// <summary>USB/DIN MIDI transport using DryWetMIDI (Windows and macOS).</summary>
public sealed class DryWetMidiTransport(IOptions<SurfaceOptions> options, ILogger<DryWetMidiTransport> log)
    : IMidiTransport
{
    private readonly SurfaceOptions _opt = options.Value;
    private readonly Lock _gate = new();
    private InputDevice? _in;
    private OutputDevice? _out;
    private bool _platformWarned;

    public string Name => _in?.Name ?? _opt.InputPortName;
    public bool IsConnected { get { lock (_gate) return _in is not null && _out is not null; } }
    public event Action<ReadOnlyMemory<byte>>? MessageReceived;
    public event Action<bool>? ConnectionChanged;

    public static (IReadOnlyList<string> Inputs, IReadOnlyList<string> Outputs) ListPorts()
    {
        try
        {
            return (InputDevice.GetAll().Select(d => d.Name).ToList(), OutputDevice.GetAll().Select(d => d.Name).ToList());
        }
        catch (Exception)
        {
            return ([], []);
        }
    }

    public async Task ConnectAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && !TryOpen())
            await Task.Delay(_opt.ReconnectInterval, ct);
    }

    /// <summary>True if the port we are connected to has disappeared from the system.</summary>
    public bool PortVanished()
    {
        string? name;
        lock (_gate) name = _in?.Name;
        if (name is null) return false;
        try { return InputDevice.GetAll().All(d => d.Name != name); }
        catch { return false; }
    }

    private bool TryOpen()
    {
        try
        {
            var input = InputDevice.GetAll().FirstOrDefault(d => Match(d.Name, _opt.InputPortName));
            var output = OutputDevice.GetAll().FirstOrDefault(d => Match(d.Name, _opt.OutputPortName));
            if (input is null || output is null)
            {
                input?.Dispose();
                output?.Dispose();
                return false;
            }

            input.EventReceived += OnEvent;
            input.ErrorOccurred += (_, e) => OnDeviceError(e.Exception);
            output.ErrorOccurred += (_, e) => OnDeviceError(e.Exception);
            input.StartEventsListening();
            lock (_gate) { _in = input; _out = output; }

            log.LogInformation("Connected to surface: in={In} out={Out}", input.Name, output.Name);
            ConnectionChanged?.Invoke(true);
            return true;
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException or TypeInitializationException or DllNotFoundException)
        {
            if (!_platformWarned)
            {
                log.LogError(ex, "MIDI devices are not supported on this platform. Use Surface:Transport=Virtual, or run on Windows/macOS.");
                _platformWarned = true;
            }
            return false;
        }
        catch (Exception ex)
        {
            log.LogDebug(ex, "Surface not available yet");
            Close();
            return false;
        }
    }

    private static bool Match(string name, string pattern) => name.Contains(pattern, StringComparison.OrdinalIgnoreCase);

    private void OnEvent(object? sender, MidiEventReceivedEventArgs e)
    {
        byte[]? raw = e.Event switch
        {
            NoteOnEvent n => [(byte)(0x90 | n.Channel), n.NoteNumber, n.Velocity],
            NoteOffEvent n => [(byte)(0x80 | n.Channel), n.NoteNumber, n.Velocity],
            ControlChangeEvent c => [(byte)(0xB0 | c.Channel), c.ControlNumber, c.ControlValue],
            PitchBendEvent p => [(byte)(0xE0 | p.Channel), (byte)(p.PitchValue & 0x7F), (byte)(p.PitchValue >> 7)],
            ChannelAftertouchEvent a => [(byte)(0xD0 | a.Channel), a.AftertouchValue],
            NormalSysExEvent s => [0xF0, .. s.Data], // Data already ends with F7
            _ => null,
        };
        if (raw is not null) MessageReceived?.Invoke(raw);
    }

    public void Send(ReadOnlySpan<byte> m)
    {
        if (m.Length == 0) return;
        MidiEvent ev;
        try
        {
            var ch = (FourBitNumber)(m[0] & 0x0F);
            ev = (m[0] & 0xF0) switch
            {
                0x90 => new NoteOnEvent((SevenBitNumber)m[1], (SevenBitNumber)m[2]) { Channel = ch },
                0x80 => new NoteOffEvent((SevenBitNumber)m[1], (SevenBitNumber)m[2]) { Channel = ch },
                0xB0 => new ControlChangeEvent((SevenBitNumber)m[1], (SevenBitNumber)m[2]) { Channel = ch },
                0xE0 => new PitchBendEvent((ushort)((m[2] << 7) | m[1])) { Channel = ch },
                0xD0 => new ChannelAftertouchEvent((SevenBitNumber)m[1]) { Channel = ch },
                0xF0 => new NormalSysExEvent(m[1..].ToArray()), // strip F0, keep F7
                _ => throw new ArgumentException($"Unsupported status 0x{m[0]:X2}"),
            };
        }
        catch (ArgumentException ex)
        {
            log.LogWarning(ex, "Dropping malformed outgoing MIDI message");
            return;
        }

        try
        {
            lock (_gate) _out?.SendEvent(ev);
        }
        catch (Exception ex)
        {
            OnDeviceError(ex);
        }
    }

    private void OnDeviceError(Exception ex)
    {
        bool wasConnected = IsConnected;
        log.LogWarning(ex, "Surface I/O error — closing and reconnecting");
        Close();
        if (wasConnected) ConnectionChanged?.Invoke(false);
    }

    /// <summary>Closes the ports; the surface worker reconnects.</summary>
    public void Disconnect()
    {
        bool wasConnected = IsConnected;
        Close();
        if (wasConnected) ConnectionChanged?.Invoke(false);
    }

    private void Close()
    {
        InputDevice? input; OutputDevice? output;
        lock (_gate) { input = _in; output = _out; _in = null; _out = null; }
        try { input?.StopEventsListening(); } catch { /* device already gone */ }
        try { input?.Dispose(); } catch { /* ignore */ }
        try { output?.Dispose(); } catch { /* ignore */ }
    }

    public ValueTask DisposeAsync()
    {
        Close();
        return ValueTask.CompletedTask;
    }
}
