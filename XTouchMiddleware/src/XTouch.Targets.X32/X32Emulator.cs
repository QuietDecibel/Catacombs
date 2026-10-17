using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace XTouch.Targets.X32;

/// <summary>
/// A small X32 console emulator on localhost: stores parameter values, answers queries,
/// pushes changes to /xremote subscribers and streams synthetic meters. Lets the whole system
/// run without hardware, and is used by the tests.
/// </summary>
public sealed class X32Emulator : IAsyncDisposable
{
    private readonly UdpClient _udp;
    private readonly ILogger _log;
    private readonly ConcurrentDictionary<string, object> _values = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<IPEndPoint, DateTime> _remoteClients = new();
    private readonly ConcurrentDictionary<IPEndPoint, (string Bank, DateTime Until)> _meterClients = new();
    private readonly CancellationTokenSource _cts = new();
    private Task? _run;

    public int Port { get; }
    public bool AnimateMeters { get; set; } = true;
    public ConcurrentQueue<(string Address, object[] Args)> Received { get; } = new();

    public X32Emulator(int port = 0, ILogger? log = null)
    {
        _udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
        Port = ((IPEndPoint)_udp.Client.LocalEndPoint!).Port;
        _log = log ?? NullLogger.Instance;
        Seed();
    }

    public void Start() => _run ??= Task.WhenAll(ReceiveLoop(_cts.Token), MeterLoop(_cts.Token));

    public object? Get(string address) => _values.TryGetValue(address.TrimStart('/'), out var v) ? v : null;

    /// <summary>Simulates someone changing a value on the console: stores it and notifies subscribers.</summary>
    public void SetFromConsole(string address, object value)
    {
        address = address.TrimStart('/');
        _values[address] = value;
        Broadcast(address, value, except: null);
    }

    private void Seed()
    {
        string[] names = ["Kick", "Snare", "HiHat", "Tom 1", "Tom 2", "OH L", "OH R", "Bass", "Gtr 1", "Gtr 2", "Keys L", "Keys R", "Vox 1", "Vox 2", "Vox 3", "Click"];
        void Strip(string key, string name, int color, bool trim)
        {
            _values[$"{key}/mix/fader"] = 0.75f;
            _values[$"{key}/mix/on"] = 1;
            _values[$"{key}/mix/pan"] = 0.5f;
            _values[$"{key}/config/name"] = name;
            _values[$"{key}/config/color"] = color;
            if (trim) _values[$"{key}/preamp/trim"] = 0.5f;
            for (int s = 1; s <= 4; s++) _values[$"{key}/mix/{s:00}/level"] = 0f;
        }
        for (int i = 1; i <= 32; i++) Strip($"ch/{i:00}", i <= names.Length ? names[i - 1] : "", (i - 1) / 4 % 7 + 1, true);
        for (int i = 1; i <= 8; i++) Strip($"auxin/{i:00}", "", 6, true);
        for (int i = 1; i <= 16; i++) Strip($"bus/{i:00}", $"Mon {i}", 3, false);
        for (int i = 1; i <= 80; i++) _values[$"-stat/solosw/{i:00}"] = 0;
        for (int g = 1; g <= 6; g++) _values[$"config/mute/{g}"] = 0;
        _values["main/st/mix/fader"] = 0.75f;
    }

    private async Task ReceiveLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            UdpReceiveResult r;
            try { r = await _udp.ReceiveAsync(ct); }
            catch (OperationCanceledException) { break; }
            catch (SocketException) { continue; }
            catch (ObjectDisposedException) { break; }

            if (!Osc.TryParse(r.Buffer, out var address, out var args)) continue;
            Received.Enqueue((address, args));
            try { Handle(r.RemoteEndPoint, address, args); }
            catch (Exception ex) { _log.LogWarning(ex, "Emulator failed handling {Address}", address); }
        }
    }

    private void Handle(IPEndPoint from, string address, object[] args)
    {
        switch (address)
        {
            case "/xremote":
                _remoteClients[from] = DateTime.UtcNow.AddSeconds(10);
                return;
            case "/info":
                Reply(from, Osc.Message("/info", "V2.07", "X32 Emulator", "X32", "4.06"));
                return;
            case "/meters":
                if (args.Length > 0 && args[0] is string bank) _meterClients[from] = (bank, DateTime.UtcNow.AddSeconds(10));
                return;
        }

        var key = address.TrimStart('/');
        if (args.Length == 0)
        {
            if (_values.TryGetValue(key, out var v)) Reply(from, Osc.Message(address, v));
            return;
        }

        // Keep the console's own type for known addresses (the real X32 coerces too).
        object value = _values.TryGetValue(key, out var existing) ? Coerce(args[0], existing) : args[0];
        _values[key] = value;
        Broadcast(key, value, except: from); // the X32 does not echo a change back to its sender
    }

    private static object Coerce(object incoming, object existing) => (incoming, existing) switch
    {
        (float f, int) => (int)Math.Round(f),
        (int i, float) => (float)i,
        _ => incoming,
    };

    private void Broadcast(string key, object value, IPEndPoint? except)
    {
        var msg = Osc.Message("/" + key, value);
        var now = DateTime.UtcNow;
        foreach (var (ep, until) in _remoteClients)
        {
            if (until < now) { _remoteClients.TryRemove(ep, out _); continue; }
            if (except is not null && ep.Equals(except)) continue;
            Reply(ep, msg);
        }
    }

    private async Task MeterLoop(CancellationToken ct)
    {
        var levels = new float[96];
        var rng = new Random(42);
        double t = 0;
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
            while (await timer.WaitForNextTickAsync(ct))
            {
                t += 0.05;
                var now = DateTime.UtcNow;
                if (_meterClients.IsEmpty) continue;
                for (int i = 0; i < levels.Length; i++)
                {
                    if (!AnimateMeters) { levels[i] = 0; continue; }
                    string key = i < 32 ? $"ch/{i + 1:00}" : i < 40 ? $"auxin/{i - 31:00}" : i >= 48 && i < 64 ? $"bus/{i - 47:00}" : "";
                    bool on = key.Length > 0 && Get($"{key}/mix/on") is int on1 && on1 == 1;
                    float fader = key.Length > 0 && Get($"{key}/mix/fader") is float f ? f : 0;
                    double signal = 0.5 + 0.5 * Math.Sin(t * (1 + i % 5) + i);
                    levels[i] = on ? (float)(fader * signal * (0.6 + 0.4 * rng.NextDouble())) : 0;
                }
                foreach (var (ep, (bank, until)) in _meterClients)
                {
                    if (until < now) { _meterClients.TryRemove(ep, out _); continue; }
                    Reply(ep, Osc.Message(bank, X32Meters.Encode(levels)));
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    private void Reply(IPEndPoint to, byte[] packet)
    {
        try { _udp.Send(packet, packet.Length, to); }
        catch (SocketException) { /* client gone */ }
        catch (ObjectDisposedException) { }
    }

    private int _disposed;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
        _cts.Cancel();
        _udp.Dispose();
        if (_run is not null) await _run.ContinueWith(_ => { }, TaskScheduler.Default);
        _cts.Dispose();
    }
}
