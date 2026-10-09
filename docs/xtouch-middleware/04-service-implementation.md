# 4. Service Implementation (C#)

This chapter builds the middleware service bottom-up. Each section is a
self-contained step you can implement and unit-test before moving on (the
[roadmap](11-roadmap.md) follows the same order).

> **⚙ Reconcile** — Type and namespace names are suggestions. If your
> architecture already defines equivalents, keep yours and use the bodies here.

## 4.1 Create the solution

```bash
dotnet new sln -n XTouchMiddleware
dotnet new classlib -n XTouch.Abstractions -o src/XTouch.Abstractions
dotnet new classlib -n XTouch.Surface      -o src/XTouch.Surface
dotnet new classlib -n XTouch.Core         -o src/XTouch.Core
dotnet new classlib -n XTouch.Targets.X32  -o src/XTouch.Targets.X32
dotnet new classlib -n XTouch.Contracts    -o src/XTouch.Contracts
dotnet new web      -n XTouch.Service      -o src/XTouch.Service
dotnet new xunit    -n XTouch.Surface.Tests -o tests/XTouch.Surface.Tests
dotnet new xunit    -n XTouch.Core.Tests    -o tests/XTouch.Core.Tests
dotnet sln add (Get-ChildItem -Recurse *.csproj)        # PowerShell
# or: dotnet sln add $(find . -name '*.csproj')          # bash

dotnet add src/XTouch.Surface package Melanchall.DryWetMidi
dotnet add src/XTouch.Service package Microsoft.Extensions.Hosting.WindowsServices
dotnet add src/XTouch.Service package Microsoft.Extensions.Hosting.Systemd
dotnet add src/XTouch.Service package Serilog.AspNetCore
```

Add a `Directory.Build.props` at the root so every project shares settings:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

## 4.2 Abstractions (`XTouch.Abstractions`)

```csharp
namespace XTouch.Abstractions;

// ---------- Surface input events ----------
public abstract record SurfaceEvent;
public sealed record FaderMoved(int Strip, double Value) : SurfaceEvent;        // Strip 0..8, Value 0..1
public sealed record FaderTouched(int Strip, bool IsTouched) : SurfaceEvent;
public sealed record ButtonChanged(int Note, bool IsPressed) : SurfaceEvent;    // MCU note number
public sealed record EncoderTurned(int Strip, int Delta) : SurfaceEvent;        // +cw / -ccw ticks
public sealed record JogTurned(int Delta) : SurfaceEvent;
public sealed record UnknownMessage(byte[] Raw) : SurfaceEvent;

// ---------- Feedback primitives ----------
public enum LedState : byte { Off = 0x00, Blink = 0x01, On = 0x7F }
public enum RingMode : byte { Dot = 0, BoostCut = 1, Wrap = 2, Spread = 3 }
public readonly record struct RingState(RingMode Mode, int Position, bool Center);
public enum LcdColor : byte { Off, Red, Green, Yellow, Blue, Magenta, Cyan, White }

// ---------- Transport ----------
public interface IMidiTransport : IAsyncDisposable
{
    string Name { get; }
    bool IsConnected { get; }
    event Action<ReadOnlyMemory<byte>>? MessageReceived;   // one complete MIDI message per call
    event Action<bool>? ConnectionChanged;
    Task ConnectAsync(CancellationToken ct);
    void Send(ReadOnlySpan<byte> message);
}
```

Target-side abstractions are defined in [Target Adapters](05-target-adapters.md).

## 4.3 MCU constants

Keep every magic number in one place. Put this file in `XTouch.Abstractions`
because the engine (Core) needs the note numbers for banking buttons and LEDs,
and Core must not reference Surface.

```csharp
namespace XTouch.Abstractions.Mcu;

public static class McuConstants
{
    public const byte DeviceIdMain = 0x14;
    public const byte DeviceIdExtender = 0x15;
    public static ReadOnlySpan<byte> SysExHeader => [0xF0, 0x00, 0x00, 0x66];

    public const int Strips = 8;
    public const int MasterFader = 8;

    // Notes
    public const int RecBase = 0x00, SoloBase = 0x08, MuteBase = 0x10, SelectBase = 0x18;
    public const int VPotPushBase = 0x20;
    public const int BankLeft = 0x2E, BankRight = 0x2F, ChannelLeft = 0x30, ChannelRight = 0x31;
    public const int Flip = 0x32, NameValue = 0x34, Shift = 0x46;
    public const int Rewind = 0x5B, FastForward = 0x5C, Stop = 0x5D, Play = 0x5E, Record = 0x5F;
    public const int FaderTouchBase = 0x68;              // 0x68..0x70
    public const int LastLedNote = 0x73;

    // Control changes
    public const int VPotBase = 0x10;                    // input 0x10..0x17
    public const int RingBase = 0x30;                    // output 0x30..0x37
    public const int Jog = 0x3C;
    public const int SevenSegBase = 0x40;                // 0x40..0x4B

    // SysEx commands
    public const byte LcdWrite = 0x12;
    public const byte LcdColors = 0x72;
    public const int LcdCellWidth = 7;
    public const int LcdLineLength = 56;
}
```

## 4.4 Decoder (bytes → `SurfaceEvent`)

Pure function, no state, trivially unit-testable.

```csharp
namespace XTouch.Surface.Mcu;

using XTouch.Abstractions;
using XTouch.Abstractions.Mcu;
using static XTouch.Abstractions.Mcu.McuConstants;

public static class McuDecoder
{
    public static SurfaceEvent Decode(ReadOnlySpan<byte> m)
    {
        if (m.Length == 0) return new UnknownMessage([]);
        int status = m[0] & 0xF0, channel = m[0] & 0x0F;

        switch (status)
        {
            case 0xE0 when m.Length >= 3 && channel <= MasterFader:
                return new FaderMoved(channel, ((m[2] << 7) | m[1]) / 16383.0);

            case 0x90 when m.Length >= 3:
            case 0x80 when m.Length >= 3:
            {
                int note = m[1];
                bool down = status == 0x90 && m[2] > 0;
                if (note is >= FaderTouchBase and <= FaderTouchBase + MasterFader)
                    return new FaderTouched(note - FaderTouchBase, down);
                return new ButtonChanged(note, down);
            }

            case 0xB0 when m.Length >= 3:
            {
                int cc = m[1], v = m[2];
                int delta = (v & 0x40) != 0 ? -(v & 0x3F) : (v & 0x3F);
                if (cc is >= VPotBase and < VPotBase + Strips) return new EncoderTurned(cc - VPotBase, delta);
                if (cc == Jog) return new JogTurned(delta);
                break;
            }
        }
        return new UnknownMessage(m.ToArray());
    }
}
```

Unit test example:

```csharp
[Theory]
[InlineData(new byte[] { 0xE2, 0x7F, 0x7F }, 2, 1.0)]
[InlineData(new byte[] { 0xE0, 0x00, 0x00 }, 0, 0.0)]
public void Decodes_fader(byte[] bytes, int strip, double value) =>
    McuDecoder.Decode(bytes).Should().Be(new FaderMoved(strip, value));

[Fact]
public void Decodes_ccw_encoder() =>
    McuDecoder.Decode([0xB0, 0x13, 0x42]).Should().Be(new EncoderTurned(3, -2));
```

## 4.5 Encoder (feedback → bytes)

```csharp
namespace XTouch.Surface.Mcu;

using System.Text;
using XTouch.Abstractions;
using XTouch.Abstractions.Mcu;
using static XTouch.Abstractions.Mcu.McuConstants;

public static class McuEncoder
{
    public static byte[] Fader(int strip, double value)
    {
        int v = (int)Math.Round(Math.Clamp(value, 0, 1) * 16383);
        return [(byte)(0xE0 | strip), (byte)(v & 0x7F), (byte)((v >> 7) & 0x7F)];
    }

    public static byte[] Led(int note, LedState state) => [0x90, (byte)note, (byte)state];

    public static byte[] Ring(int strip, RingState r) =>
        [0xB0, (byte)(RingBase + strip),
         (byte)((r.Center ? 0x40 : 0) | (((int)r.Mode & 3) << 4) | Math.Clamp(r.Position, 0, 11))];

    public static byte[] Meter(int strip, int level) =>
        [0xD0, (byte)((strip << 4) | Math.Clamp(level, 0, 0x0C))];

    public static byte[] LcdText(int offset, string text, byte deviceId = DeviceIdMain)
    {
        var bytes = Encoding.ASCII.GetBytes(Sanitize(text));
        return [0xF0, 0x00, 0x00, 0x66, deviceId, LcdWrite, (byte)offset, .. bytes, 0xF7];
    }

    public static byte[] LcdColorsMsg(ReadOnlySpan<LcdColor> colors, byte deviceId = DeviceIdMain)
    {
        var msg = new byte[7 + Strips];
        new byte[] { 0xF0, 0x00, 0x00, 0x66, deviceId, McuConstants.LcdColors }.CopyTo(msg, 0);
        for (int i = 0; i < Strips; i++) msg[6 + i] = (byte)colors[i];
        msg[^1] = 0xF7;
        return msg;
    }

    public static byte[] SevenSegment(int digitFromRight, char c, bool dot = false)
    {
        c = char.ToUpperInvariant(c);
        int code = c switch { >= '@' and <= '_' => c - 0x40, >= ' ' and <= '?' => c, _ => ' ' };
        return [0xB0, (byte)(SevenSegBase + digitFromRight), (byte)(code | (dot ? 0x40 : 0))];
    }

    private static string Sanitize(string s) =>
        string.Create(s.Length, s, (span, src) =>
        {
            for (int i = 0; i < src.Length; i++) span[i] = src[i] is >= ' ' and <= '~' ? src[i] : '?';
        });
}
```

Also add a small helper to `XTouch.Abstractions` that the engine uses to
format scribble-strip cells:

```csharp
namespace XTouch.Abstractions.Mcu;

public static class SurfaceText
{
    /// <summary>Pads/truncates to one 7-char cell and replaces non-printable chars.</summary>
    public static string Cell(string? s)
    {
        var cell = (s ?? "").PadRight(McuConstants.LcdCellWidth)[..McuConstants.LcdCellWidth];
        return string.Create(cell.Length, cell, (span, src) =>
        {
            for (int i = 0; i < src.Length; i++) span[i] = src[i] is >= ' ' and <= '~' ? src[i] : '?';
        });
    }
}
```

## 4.6 Transport (`DryWetMidiTransport`)

Responsibilities: find the ports by name, translate DryWetMIDI event objects to
raw bytes and back, and reconnect when the device disappears.

```csharp
namespace XTouch.Surface.Transport;

using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XTouch.Abstractions;

public sealed class SurfaceOptions
{
    public string InputPortName { get; set; } = "X-TOUCH";   // substring match, case-insensitive
    public string OutputPortName { get; set; } = "X-TOUCH";
    public TimeSpan ReconnectInterval { get; set; } = TimeSpan.FromSeconds(2);
}

public sealed class DryWetMidiTransport(IOptions<SurfaceOptions> options, ILogger<DryWetMidiTransport> log)
    : IMidiTransport
{
    private readonly SurfaceOptions _opt = options.Value;
    private InputDevice? _in;
    private OutputDevice? _out;
    private readonly object _sendLock = new();

    public string Name => _opt.InputPortName;
    public bool IsConnected => _in is not null && _out is not null;
    public event Action<ReadOnlyMemory<byte>>? MessageReceived;
    public event Action<bool>? ConnectionChanged;

    public async Task ConnectAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && !TryOpen())
            await Task.Delay(_opt.ReconnectInterval, ct);
    }

    private bool TryOpen()
    {
        try
        {
            var inInfo = InputDevice.GetAll().FirstOrDefault(d => Match(d.Name, _opt.InputPortName));
            var outInfo = OutputDevice.GetAll().FirstOrDefault(d => Match(d.Name, _opt.OutputPortName));
            if (inInfo is null || outInfo is null) return false;

            _in = inInfo; _out = outInfo;
            _in.EventReceived += OnEvent;
            _in.ErrorOccurred += (_, e) => OnDeviceError(e.Exception);
            _out.ErrorOccurred += (_, e) => OnDeviceError(e.Exception);
            _in.StartEventsListening();

            log.LogInformation("Connected to surface {In} / {Out}", _in.Name, _out.Name);
            ConnectionChanged?.Invoke(true);
            return true;
        }
        catch (Exception ex)
        {
            log.LogDebug(ex, "Surface not available yet");
            Close();
            return false;
        }
    }

    private static bool Match(string name, string pattern) =>
        name.Contains(pattern, StringComparison.OrdinalIgnoreCase);

    private void OnEvent(object? sender, MidiEventReceivedEventArgs e)
    {
        byte[]? raw = e.Event switch
        {
            NoteOnEvent n  => [(byte)(0x90 | n.Channel), n.NoteNumber, n.Velocity],
            NoteOffEvent n => [(byte)(0x80 | n.Channel), n.NoteNumber, n.Velocity],
            ControlChangeEvent c => [(byte)(0xB0 | c.Channel), c.ControlNumber, c.ControlValue],
            PitchBendEvent p => [(byte)(0xE0 | p.Channel), (byte)(p.PitchValue & 0x7F), (byte)(p.PitchValue >> 7)],
            ChannelAftertouchEvent a => [(byte)(0xD0 | a.Channel), a.AftertouchValue],
            NormalSysExEvent s => [0xF0, .. s.Data],               // Data already ends with F7
            _ => null
        };
        if (raw is not null) MessageReceived?.Invoke(raw);
    }

    public void Send(ReadOnlySpan<byte> m)
    {
        var dev = _out;
        if (dev is null || m.Length == 0) return;
        MidiEvent ev = (m[0] & 0xF0) switch
        {
            0x90 => new NoteOnEvent((SevenBitNumber)m[1], (SevenBitNumber)m[2]) { Channel = (FourBitNumber)(m[0] & 0x0F) },
            0x80 => new NoteOffEvent((SevenBitNumber)m[1], (SevenBitNumber)m[2]) { Channel = (FourBitNumber)(m[0] & 0x0F) },
            0xB0 => new ControlChangeEvent((SevenBitNumber)m[1], (SevenBitNumber)m[2]) { Channel = (FourBitNumber)(m[0] & 0x0F) },
            0xE0 => new PitchBendEvent((ushort)((m[2] << 7) | m[1])) { Channel = (FourBitNumber)(m[0] & 0x0F) },
            0xD0 => new ChannelAftertouchEvent((SevenBitNumber)m[1]) { Channel = (FourBitNumber)(m[0] & 0x0F) },
            0xF0 => new NormalSysExEvent(m[1..].ToArray()),          // strip F0, keep F7
            _ => throw new ArgumentException($"Unsupported status 0x{m[0]:X2}")
        };
        try { lock (_sendLock) dev.SendEvent(ev); }
        catch (Exception ex) { OnDeviceError(ex); }
    }

    private void OnDeviceError(Exception ex)
    {
        log.LogWarning(ex, "Surface I/O error — closing and reconnecting");
        Close();
        ConnectionChanged?.Invoke(false);
    }

    private void Close()
    {
        try { _in?.StopEventsListening(); } catch { /* device already gone */ }
        _in?.Dispose(); _out?.Dispose();
        _in = null; _out = null;
    }

    public ValueTask DisposeAsync() { Close(); return ValueTask.CompletedTask; }
}
```

Notes
- On Windows a MIDI port can be opened by **one** application at a time. If
  the DAW or Behringer's own tools hold it, `TryOpen` keeps failing — surface
  that clearly in the UI ("port in use").
- Unplug detection: DryWetMIDI raises `ErrorOccurred` on most platforms. As a
  belt-and-braces check, the `SurfaceWorker` (§4.9) also re-enumerates devices
  every few seconds and treats a vanished port as a disconnect.

## 4.7 Engine inputs and the event channel

Everything the engine reacts to is an `EngineInput`:

```csharp
namespace XTouch.Core.Engine;

public abstract record EngineInput;
public sealed record FromSurface(SurfaceEvent Event) : EngineInput;
public sealed record FromTarget(string AdapterId, TargetEvent Event) : EngineInput;
public sealed record SurfaceConnection(bool Connected) : EngineInput;
public sealed record TargetConnection(string AdapterId, bool Connected) : EngineInput;
public sealed record UiCommand(object Command) : EngineInput;   // e.g. SetBank, LoadProfile
```

```csharp
// registration (Program.cs)
builder.Services.AddSingleton(Channel.CreateBounded<EngineInput>(new BoundedChannelOptions(4096)
{
    SingleReader = true,
    FullMode = BoundedChannelFullMode.DropOldest     // never block a MIDI callback
}));
```

`DropOldest` is acceptable because fader/encoder streams are self-correcting
(the next value supersedes the last) and 4096 is far above any realistic burst.
Log a warning if the channel is ever more than 50 % full.

## 4.8 Control engine

The engine is the single owner of `MixerState`, `SurfaceState`, bank and layer.

```csharp
namespace XTouch.Core.Engine;

public sealed class ControlEngine(
    Channel<EngineInput> inputs,
    ITargetRegistry targets,
    IProfileProvider profiles,
    IFeedbackSink feedback,            // implemented by FeedbackRenderer
    IStateBroadcaster broadcaster,     // implemented by the SignalR layer
    ILogger<ControlEngine> log)
{
    private readonly MixerState _mixer = new();
    private readonly SurfaceState _surface = new();
    private Profile _profile = profiles.Current;
    private int _bankOffset;           // first target channel shown on strip 0 (0-based)
    private string _layer = "pan";     // active encoder layer

    public async Task RunAsync(CancellationToken ct)
    {
        await foreach (var input in inputs.Reader.ReadAllAsync(ct))
        {
            Handle(input);
            while (inputs.Reader.TryRead(out var more)) Handle(more);   // drain burst, publish once
            feedback.Publish(_surface.Snapshot());
            broadcaster.Publish(_surface.Snapshot(), _mixer.Snapshot(), _bankOffset, _layer);
        }
    }

    private void Handle(EngineInput input)
    {
        try
        {
            switch (input)
            {
                case FromSurface { Event: var e }: OnSurface(e); break;
                case FromTarget t: OnTarget(t); break;
                case SurfaceConnection { Connected: true }: RepaintAll(); break;
                case UiCommand c: OnUi(c.Command); break;
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Engine failed handling {Input}", input);   // never kill the loop
        }
    }

    private void OnSurface(SurfaceEvent e)
    {
        switch (e)
        {
            case FaderTouched t:
                _surface.FaderTouched[t.Strip] = t.IsTouched;
                break;

            case FaderMoved f:
                _surface.Faders[f.Strip] = f.Value;                      // keeps desired == hand position
                Send(_profile.ResolveFader(f.Strip, _bankOffset), f.Value);
                break;

            case EncoderTurned enc:
            {
                var binding = _profile.ResolveEncoder(enc.Strip, _bankOffset, _layer);
                if (binding is null) break;
                double current = _mixer.Get(binding.Address)?.Normalized ?? binding.Default;
                double next = Math.Clamp(current + enc.Delta * binding.Step, 0, 1);
                _mixer.Set(binding.Address, next);                       // optimistic
                Send(binding, next);
                UpdateStripFeedback(enc.Strip);
                break;
            }

            case ButtonChanged { IsPressed: true } b:
                OnButtonPressed(b.Note);
                break;
        }
    }

    private void OnButtonPressed(int note)
    {
        switch (note)
        {
            case McuConstants.BankLeft:     ShiftBank(-8); return;
            case McuConstants.BankRight:    ShiftBank(+8); return;
            case McuConstants.ChannelLeft:  ShiftBank(-1); return;
            case McuConstants.ChannelRight: ShiftBank(+1); return;
        }
        if (_profile.ResolveLayerButton(note) is { } layer) { _layer = layer; RepaintAll(); return; }

        var binding = _profile.ResolveButton(note, _bankOffset);
        if (binding is null) return;
        bool current = (_mixer.Get(binding.Address)?.Normalized ?? 0) >= 0.5;
        Send(binding, binding.IsToggle ? (current ? 0 : 1) : 1);
    }

    private void OnTarget(FromTarget t)
    {
        if (t.Event is ParameterChanged p && _mixer.Set(p.Address, p.Value))
        {
            // Which strip (if any) is showing this parameter in the current bank?
            foreach (int strip in _profile.StripsShowing(p.Address, _bankOffset))
                UpdateStripFeedback(strip);
        }
        else if (t.Event is MeterFrame m)
        {
            for (int s = 0; s < McuConstants.Strips; s++)
                _surface.Meters[s] = _profile.MeterFor(s, _bankOffset, m);
        }
    }

    private void ShiftBank(int delta)
    {
        int max = Math.Max(0, _profile.ChannelCount - McuConstants.Strips);
        _bankOffset = Math.Clamp(_bankOffset + delta, 0, max);
        RepaintAll();
    }

    private void RepaintAll()
    {
        for (int s = 0; s < McuConstants.Strips; s++) UpdateStripFeedback(s);
        UpdateMasterAndGlobalLeds();
        _surface.Display = $"BANK {_bankOffset / 8 + 1,2}".PadLeft(12);
    }

    private void UpdateStripFeedback(int strip)
    {
        var view = _profile.BuildStripView(strip, _bankOffset, _layer, _mixer);  // pure function
        _surface.Faders[strip]   = view.Fader;
        _surface.Rings[strip]    = view.Ring;
        _surface.LcdTop[strip]   = SurfaceText.Cell(view.Name);
        _surface.LcdBottom[strip]= SurfaceText.Cell(view.ValueText);
        _surface.LcdColors[strip]= view.Color;
        _surface.Leds[McuConstants.MuteBase + strip]   = view.Muted  ? LedState.On : LedState.Off;
        _surface.Leds[McuConstants.SoloBase + strip]   = view.Soloed ? LedState.On : LedState.Off;
        _surface.Leds[McuConstants.SelectBase + strip] = view.Selected ? LedState.On : LedState.Off;
    }

    private void Send(Binding? binding, double normalized)
    {
        if (binding is null) return;
        targets.Get(binding.AdapterId)?.Send(new SetParameter(binding.Address, binding.Converter.ToTarget(normalized)));
    }

    // OnUi, UpdateMasterAndGlobalLeds omitted for brevity
}
```

Design notes
- **Optimistic updates**: after sending to the target, the engine updates
  `MixerState` immediately so encoder acceleration feels instant. The target's
  echo then confirms (or corrects) the value.
- **Pure view builder**: `Profile.BuildStripView` turns (strip, bank, layer,
  mixer state) into what the strip should show. It has no side effects and is
  the most heavily unit-tested function in the system.
- **Fader while touched**: `FaderMoved` writes `_surface.Faders` to the hand
  position, so when the renderer resumes after release it sends that same
  value — no jump.

## 4.9 Feedback renderer

```csharp
namespace XTouch.Surface;

// IFeedbackSink and SurfaceSnapshot live in XTouch.Abstractions so Core can use them:
//   public interface IFeedbackSink { void Publish(SurfaceSnapshot desired); }

public sealed class FeedbackRenderer(IMidiTransport transport, ILogger<FeedbackRenderer> log)
    : IFeedbackSink
{
    private SurfaceSnapshot? _desired;          // latest from engine (atomic swap)
    private SurfaceSnapshot _sent = SurfaceSnapshot.Unknown;  // every field "unknown" → forces send
    private readonly bool[] _wasTouched = new bool[9];

    public void Publish(SurfaceSnapshot desired) => Volatile.Write(ref _desired, desired);

    /// <summary>Forget what was sent, so the next tick repaints everything (call on connect).</summary>
    public void Invalidate() => _sent = SurfaceSnapshot.Unknown;

    public async Task RunAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(10));   // ~100 Hz
        while (await timer.WaitForNextTickAsync(ct))
        {
            if (!transport.IsConnected) continue;
            var d = Volatile.Read(ref _desired);
            if (d is null) continue;
            try { Render(d); } catch (Exception ex) { log.LogWarning(ex, "Render failed"); }
        }
    }

    private void Render(SurfaceSnapshot d)
    {
        var next = _sent.ToBuilder();

        for (int i = 0; i <= McuConstants.MasterFader; i++)
        {
            bool released = _wasTouched[i] && !d.FaderTouched[i];
            _wasTouched[i] = d.FaderTouched[i];
            if (d.FaderTouched[i]) continue;                                   // hand on fader: hands off
            if (released || !Near(_sent.Faders[i], d.Faders[i]))
            {
                transport.Send(McuEncoder.Fader(i, d.Faders[i]));
                next.Faders[i] = d.Faders[i];
            }
        }

        for (int n = 0; n <= McuConstants.LastLedNote; n++)
            if (_sent.Leds[n] != d.Leds[n]) { transport.Send(McuEncoder.Led(n, d.Leds[n])); next.Leds[n] = d.Leds[n]; }

        for (int s = 0; s < McuConstants.Strips; s++)
            if (_sent.Rings[s] != d.Rings[s]) { transport.Send(McuEncoder.Ring(s, d.Rings[s])); next.Rings[s] = d.Rings[s]; }

        RenderLcdLine(d.LcdTop, _sent.LcdTop, next.LcdTop, offset: 0);
        RenderLcdLine(d.LcdBottom, _sent.LcdBottom, next.LcdBottom, offset: McuConstants.LcdLineLength);

        if (!d.LcdColors.SequenceEqual(_sent.LcdColors))
        { transport.Send(McuEncoder.LcdColorsMsg(d.LcdColors.AsSpan())); d.LcdColors.CopyTo(next.LcdColors, 0); }

        RenderSevenSegment(d.Display, _sent.Display, next);
        _sent = next.Build();
    }

    // Sends the whole 56-char line in one SysEx if any cell changed (cheaper than 8 messages).
    private void RenderLcdLine(string[] desired, string[] sent, string[] next, int offset)
    {
        if (desired.SequenceEqual(sent)) return;
        transport.Send(McuEncoder.LcdText(offset, string.Concat(desired)));
        desired.CopyTo(next, 0);
    }

    private static bool Near(double a, double b) => Math.Abs(a - b) < 1.0 / 1024;  // ~10-bit fader resolution
    // RenderSevenSegment: diff 12 chars, send changed digits right-to-left. Omitted.
}
```

Meters are sent by a separate `MeterRenderer` loop at 15 Hz that sends every
strip's level each tick (not diffed — they decay on the device).

## 4.10 Wiring workers

```csharp
// SurfaceWorker: owns connect/reconnect and forwards input to the engine
public sealed class SurfaceWorker(
    IMidiTransport transport, Channel<EngineInput> inputs, FeedbackRenderer renderer,
    ILogger<SurfaceWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        transport.MessageReceived += raw => inputs.Writer.TryWrite(new FromSurface(McuDecoder.Decode(raw.Span)));
        transport.ConnectionChanged += connected =>
        {
            if (connected) renderer.Invalidate();
            inputs.Writer.TryWrite(new SurfaceConnection(connected));
        };

        var render = renderer.RunAsync(ct);
        while (!ct.IsCancellationRequested)
        {
            if (!transport.IsConnected) await transport.ConnectAsync(ct);
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
        }
        await render;
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        foreach (var msg in SurfaceReset.Messages()) transport.Send(msg);  // blank the surface (§3.8)
        await base.StopAsync(ct);
    }
}

public sealed class EngineWorker(ControlEngine engine) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken ct) => engine.RunAsync(ct);
}
```

## 4.11 `Program.cs`

```csharp
using System.Threading.Channels;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));
builder.Services.AddWindowsService(o => o.ServiceName = "XTouch Middleware");
builder.Services.AddSystemd();

builder.Services.Configure<SurfaceOptions>(builder.Configuration.GetSection("Surface"));
builder.Services.Configure<ApiOptions>(builder.Configuration.GetSection("Api"));

builder.Services.AddSingleton(Channel.CreateBounded<EngineInput>(new BoundedChannelOptions(4096)
    { SingleReader = true, FullMode = BoundedChannelFullMode.DropOldest }));

builder.Services.AddSingleton<IMidiTransport, DryWetMidiTransport>();
builder.Services.AddSingleton<FeedbackRenderer>();
builder.Services.AddSingleton<IFeedbackSink>(sp => sp.GetRequiredService<FeedbackRenderer>());
builder.Services.AddSingleton<IProfileProvider, FileProfileProvider>();
builder.Services.AddSingleton<ITargetRegistry, TargetRegistry>();
builder.Services.AddSingleton<IStateBroadcaster, HubStateBroadcaster>();
builder.Services.AddSingleton<ControlEngine>();
builder.Services.AddX32Adapter(builder.Configuration.GetSection("Targets:X32"));

builder.Services.AddHostedService<EngineWorker>();
builder.Services.AddHostedService<SurfaceWorker>();
builder.Services.AddHostedService<TargetsWorker>();
builder.Services.AddHostedService<MeterWorker>();

builder.Services.AddSignalR();
builder.WebHost.UseUrls(builder.Configuration["Api:Url"] ?? "http://127.0.0.1:5280");

var app = builder.Build();
app.MapHub<SurfaceHub>("/hub");
app.MapApiEndpoints();               // REST, see 06-service-api.md
app.MapGet("/health", (IMidiTransport t, ITargetRegistry r) =>
    Results.Ok(new { surface = t.IsConnected, targets = r.Status() }));

app.Run();
```

`appsettings.json`:

```json
{
  "Surface": { "InputPortName": "X-TOUCH", "OutputPortName": "X-TOUCH" },
  "Targets": { "X32": { "Host": "192.168.1.50", "Port": 10023 } },
  "Profiles": { "Directory": "profiles", "Active": "x32-default" },
  "Api": { "Url": "http://127.0.0.1:5280", "Token": null },
  "Serilog": {
    "MinimumLevel": { "Default": "Information", "Override": { "XTouch.Surface.Trace": "Warning" } },
    "WriteTo": [ { "Name": "Console" }, { "Name": "File", "Args": { "path": "logs/xtouch-.log", "rollingInterval": "Day" } } ]
  }
}
```

## 4.12 Pitfalls checklist

| Pitfall | Symptom | Prevention |
|---------|---------|------------|
| Writing state from MIDI callback | Random flicker, race bugs | Callback only enqueues (§2.4) |
| Driving a touched fader | Fader fights the hand | Renderer skips touched faders (§4.9) |
| Not resending on release | Fader snaps to an old value | Force send on touch-release |
| Unbounded queues | Lag grows during heavy moves | Bounded channel + coalescing renderer |
| One SysEx per LCD cell | Sluggish bank switching | Whole-line LCD writes |
| Meters sent only on change | Meters die after ~1 s | Continuous 15 Hz refresh |
| Exceptions escaping a loop | Service stops responding | try/catch per input and per tick |
| Port held by another app | Never connects | Show "port in use" in UI; document closing the DAW |
