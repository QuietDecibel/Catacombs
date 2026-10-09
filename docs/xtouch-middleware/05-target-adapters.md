# 5. Target Adapters

A **target adapter** connects the engine to one controllable system. The engine
only speaks in *parameter addresses* and *normalized values*; adapters
translate those to a wire protocol.

> **⚙ Reconcile** — If your architecture names a specific target (a DAW, a
> software mixer, a particular console), implement it as an adapter using the
> contract below; the X32 adapter is a worked example, not a requirement.

## 5.1 Contract

```csharp
namespace XTouch.Abstractions;

/// <summary>Adapter-specific path, e.g. "ch/03/fader". Opaque to the engine.</summary>
public readonly record struct ParameterAddress(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct ParameterValue(double Normalized, string? Text = null);

public abstract record TargetCommand;
public sealed record SetParameter(ParameterAddress Address, ParameterValue Value) : TargetCommand;
public sealed record QueryParameter(ParameterAddress Address) : TargetCommand;
public sealed record Trigger(ParameterAddress Address) : TargetCommand;          // e.g. transport play

public abstract record TargetEvent;
public sealed record ParameterChanged(ParameterAddress Address, ParameterValue Value) : TargetEvent;
public sealed record MeterFrame(IReadOnlyDictionary<string, double> LevelsDbfs) : TargetEvent;

public interface ITargetAdapter : IAsyncDisposable
{
    string Id { get; }                       // "x32", "daw", ...
    string DisplayName { get; }
    bool IsConnected { get; }

    /// <summary>Static description used by the UI mapping editor (channels, parameters, value ranges).</summary>
    TargetCatalog Catalog { get; }

    /// <summary>Run until cancelled: connect, subscribe, receive, reconnect. Push events to <paramref name="sink"/>.</summary>
    Task RunAsync(Action<TargetEvent> sink, Action<bool> connectionChanged, CancellationToken ct);

    /// <summary>Non-blocking, thread-safe. Called from the engine loop.</summary>
    void Send(TargetCommand command);

    /// <summary>Request current values for everything in the catalog (used after connect).</summary>
    Task SyncAllAsync(CancellationToken ct);
}

public sealed record TargetCatalog(IReadOnlyList<CatalogChannel> Channels, IReadOnlyList<CatalogParameter> Globals);
public sealed record CatalogChannel(string Key, string DefaultName, IReadOnlyList<CatalogParameter> Parameters);
public sealed record CatalogParameter(string Key, string Address, ParameterKind Kind, string? Unit = null);
public enum ParameterKind { Continuous, Toggle, Trigger, Text, Enum }
```

Rules every adapter must follow:
1. `Send` must never block the engine. Queue to an internal channel and write
   from the adapter's own task.
2. Report every remote change via `sink` — including echoes of our own
   commands. The engine is designed to absorb echoes (§2.3).
3. Own reconnection. On reconnect, call `SyncAllAsync` so the surface is
   repainted with real values.
4. Translate values with an `IValueConverter` so the engine never knows units.

## 5.2 Value converters

```csharp
public interface IValueConverter
{
    ParameterValue ToTarget(double normalized);   // 0..1 → target value (+ display text)
    double FromTarget(ParameterValue value);       // target → 0..1
}

/// <summary>Behringer X32/M32 fader law: float 0..1 ⇄ dB (four linear segments).</summary>
public sealed class X32FaderConverter : IValueConverter
{
    public ParameterValue ToTarget(double f) => new(f, Format(ToDb(f)));
    public double FromTarget(ParameterValue v) => v.Normalized;

    public static double ToDb(double f) => f switch
    {
        <= 0       => double.NegativeInfinity,
        >= 0.5     => f * 40 - 30,      // -10 .. +10 dB
        >= 0.25    => f * 80 - 50,      // -30 .. -10 dB
        >= 0.0625  => f * 160 - 70,     // -60 .. -30 dB
        _          => f * 480 - 90      // -90 .. -60 dB
    };

    private static string Format(double db) =>
        double.IsNegativeInfinity(db) ? " -inf " : $"{db,5:+0.0;-0.0}";
}

public sealed class PanConverter : IValueConverter   // 0..1 ⇄ L100..C..R100
{
    public ParameterValue ToTarget(double v)
    {
        int p = (int)Math.Round((v - 0.5) * 200);
        return new(v, p == 0 ? "  <C>  " : p < 0 ? $" L{-p,3}  " : $" R{p,3}  ");
    }
    public double FromTarget(ParameterValue v) => v.Normalized;
}

public sealed class InvertedToggleConverter : IValueConverter   // X32 "mix/on": 1 = unmuted
{
    public ParameterValue ToTarget(double muted) => new(muted >= 0.5 ? 0 : 1, muted >= 0.5 ? "MUTE" : "");
    public double FromTarget(ParameterValue v) => v.Normalized >= 0.5 ? 0 : 1;
}
```

## 5.3 Reference adapter: Behringer X32 / M32 (OSC over UDP)

### Protocol facts

| Item | Value |
|------|-------|
| Transport | UDP, console port **10023** |
| Subscribe to changes | Send `/xremote` (no args) at least every **9 s**; the console then pushes every change made elsewhere |
| Query a value | Send the address with no arguments; the console replies with the value |
| Channel fader | `/ch/NN/mix/fader` `,f` 0.0–1.0 (`NN` = 01–32) |
| Channel on (unmuted) | `/ch/NN/mix/on` `,i` 0 = muted, 1 = on |
| Pan | `/ch/NN/mix/pan` `,f` 0.0–1.0 |
| Name | `/ch/NN/config/name` `,s` (max 12 chars) |
| Color | `/ch/NN/config/color` `,i` 0–15 (0–7 = off, red, green, yellow, blue, magenta, cyan, white; 8–15 inverted) |
| Main LR fader | `/main/st/mix/fader` `,f` |
| Buses / DCAs | `/bus/NN/mix/fader`, `/dca/N/fader` |
| Meters | `/meters` with a meter-bank argument (e.g. `/meters/1`); console streams blobs of little-endian floats for ~10 s, renew periodically |

The X32 color index 0–7 matches the X-Touch scribble color index exactly
(`x32Color & 0x07`), which makes colors free to mirror.

### Minimal OSC codec

OSC is simple enough that a ~60-line codec avoids a dependency (a NuGet OSC
library is fine too).

```csharp
namespace XTouch.Targets.X32;

using System.Buffers.Binary;
using System.Text;

public static class Osc
{
    public static byte[] Message(string address, params object[] args)
    {
        using var ms = new MemoryStream();
        WriteString(ms, address);
        WriteString(ms, "," + string.Concat(args.Select(a => a switch
            { float => 'f', int => 'i', string => 's', _ => throw new ArgumentException(a.GetType().Name) })));
        Span<byte> buf = stackalloc byte[4];
        foreach (var a in args)
        {
            switch (a)
            {
                case float f: BinaryPrimitives.WriteSingleBigEndian(buf, f); ms.Write(buf); break;
                case int i:   BinaryPrimitives.WriteInt32BigEndian(buf, i);  ms.Write(buf); break;
                case string s: WriteString(ms, s); break;
            }
        }
        return ms.ToArray();
    }

    public static (string Address, object[] Args) Parse(ReadOnlySpan<byte> p)
    {
        int pos = 0;
        string address = ReadString(p, ref pos);
        string tags = pos < p.Length ? ReadString(p, ref pos) : ",";
        var args = new List<object>();
        foreach (char t in tags.AsSpan(1))
        {
            switch (t)
            {
                case 'f': args.Add(BinaryPrimitives.ReadSingleBigEndian(p[pos..])); pos += 4; break;
                case 'i': args.Add(BinaryPrimitives.ReadInt32BigEndian(p[pos..]));  pos += 4; break;
                case 's': args.Add(ReadString(p, ref pos)); break;
                case 'b':
                    int len = BinaryPrimitives.ReadInt32BigEndian(p[pos..]); pos += 4;
                    args.Add(p.Slice(pos, len).ToArray()); pos += (len + 3) & ~3; break;
                default: return (address, args.ToArray());   // unknown tag: stop parsing
            }
        }
        return (address, args.ToArray());
    }

    private static void WriteString(Stream s, string v)
    {
        var bytes = Encoding.ASCII.GetBytes(v);
        s.Write(bytes);
        s.Write(new byte[4 - bytes.Length % 4]);           // null terminator + pad to 4
    }

    private static string ReadString(ReadOnlySpan<byte> p, ref int pos)
    {
        int end = p[pos..].IndexOf((byte)0);
        string s = Encoding.ASCII.GetString(p.Slice(pos, end));
        pos += (end + 4) & ~3;
        return s;
    }
}
```

### Adapter skeleton

```csharp
public sealed class X32Options { public string Host { get; set; } = ""; public int Port { get; set; } = 10023; public int Channels { get; set; } = 32; }

public sealed class X32Adapter(IOptions<X32Options> options, ILogger<X32Adapter> log) : ITargetAdapter
{
    private readonly X32Options _opt = options.Value;
    private readonly UdpClient _udp = new(0);
    private readonly Channel<byte[]> _outbox = Channel.CreateUnbounded<byte[]>(new() { SingleReader = true });
    private DateTime _lastRx = DateTime.MinValue;

    public string Id => "x32";
    public string DisplayName => $"X32 @ {_opt.Host}";
    public bool IsConnected => DateTime.UtcNow - _lastRx < TimeSpan.FromSeconds(12);
    public TargetCatalog Catalog => X32Catalog.Build(_opt.Channels);

    public void Send(TargetCommand cmd)
    {
        byte[] packet = cmd switch
        {
            SetParameter { Address.Value: var a, Value: var v } when a.EndsWith("/on")     => Osc.Message("/" + a, (int)v.Normalized),
            SetParameter { Address.Value: var a, Value: var v } when a.EndsWith("/color")  => Osc.Message("/" + a, (int)v.Normalized),
            SetParameter { Address.Value: var a, Value: var v } when a.EndsWith("/name")   => Osc.Message("/" + a, v.Text ?? ""),
            SetParameter { Address.Value: var a, Value: var v }                            => Osc.Message("/" + a, (float)v.Normalized),
            QueryParameter q => Osc.Message("/" + q.Address.Value),
            _ => throw new NotSupportedException(cmd.GetType().Name)
        };
        _outbox.Writer.TryWrite(packet);
    }

    public async Task RunAsync(Action<TargetEvent> sink, Action<bool> connectionChanged, CancellationToken ct)
    {
        _udp.Connect(_opt.Host, _opt.Port);
        var sender    = SendLoop(ct);
        var keepAlive = KeepAliveLoop(connectionChanged, ct);

        while (!ct.IsCancellationRequested)
        {
            UdpReceiveResult r;
            try { r = await _udp.ReceiveAsync(ct); }
            catch (SocketException ex) { log.LogDebug(ex, "X32 unreachable"); await Task.Delay(1000, ct); continue; }

            bool wasConnected = IsConnected;
            _lastRx = DateTime.UtcNow;
            if (!wasConnected) { connectionChanged(true); _ = SyncAllAsync(ct); }

            var (address, args) = Osc.Parse(r.Buffer);
            if (args.Length == 0) continue;
            var value = args[0] switch
            {
                float f  => new ParameterValue(f),
                int i    => new ParameterValue(i),
                string s => new ParameterValue(0, s),
                _        => (ParameterValue?)null
            };
            if (value is { } v) sink(new ParameterChanged(new(address.TrimStart('/')), v));
            // "/meters/..." blobs → decode floats → sink(new MeterFrame(...))
        }
        await Task.WhenAll(sender, keepAlive);
    }

    private async Task SendLoop(CancellationToken ct)
    {
        await foreach (var p in _outbox.Reader.ReadAllAsync(ct))
            try { await _udp.SendAsync(p, ct); } catch (SocketException ex) { log.LogDebug(ex, "send failed"); }
    }

    private async Task KeepAliveLoop(Action<bool> connectionChanged, CancellationToken ct)
    {
        using var t = new PeriodicTimer(TimeSpan.FromSeconds(8));
        bool last = false;
        do
        {
            _outbox.Writer.TryWrite(Osc.Message("/xremote"));
            _outbox.Writer.TryWrite(Osc.Message("/meters", "/meters/1"));
            if (last && !IsConnected) connectionChanged(false);
            last = IsConnected;
        } while (await t.WaitForNextTickAsync(ct));
    }

    public Task SyncAllAsync(CancellationToken ct)
    {
        foreach (var ch in Catalog.Channels)
            foreach (var p in ch.Parameters)
                Send(new QueryParameter(new(p.Address)));
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() { _udp.Dispose(); return ValueTask.CompletedTask; }
}
```

Registration helper:

```csharp
public static IServiceCollection AddX32Adapter(this IServiceCollection s, IConfiguration cfg)
{
    s.Configure<X32Options>(cfg);
    s.AddSingleton<X32Adapter>();
    s.AddSingleton<ITargetAdapter>(sp => sp.GetRequiredService<X32Adapter>());
    return s;
}
```

`TargetsWorker` resolves all `ITargetAdapter` instances and runs each
`RunAsync`, forwarding events into the engine channel as `FromTarget`.

## 5.4 Other adapters

| Target | Protocol | Notes |
|--------|----------|-------|
| Behringer Wing | OSC (UDP 2223) / native binary | Different address tree; same pattern as X32. |
| DAW (Reaper, Cubase, Ableton…) | MIDI to a virtual port (loopMIDI on Windows, IAC on macOS) | The middleware can act as an MCU *host* to the surface while presenting a simpler, remapped MIDI device to the DAW. Reaper also supports OSC. |
| VoiceMeeter | VoiceMeeter Remote API (native DLL, P/Invoke) | Windows only; poll `IsParametersDirty` at ~30 Hz for changes. |
| OBS Studio | obs-websocket v5 (JSON over WebSocket) | Use for scene/mute control, not audio levels. |
| Custom hardware | Whatever it speaks | Implement `ITargetAdapter`; nothing else changes. |

## 5.5 Adapter test kit

Each adapter project ships a fake peer for tests:

- **X32**: a `FakeX32` UDP server (bind to `127.0.0.1:0`) that stores values,
  answers queries, and pushes changes to the last client that sent `/xremote`.
- Shared contract tests (`TargetAdapterContractTests<TAdapter>`) assert: send
  never blocks; reconnect triggers `SyncAllAsync`; echoes arrive as
  `ParameterChanged`; dispose is idempotent.
