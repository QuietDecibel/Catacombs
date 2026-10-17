using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XTouch.Abstractions.Targets;

namespace XTouch.Targets.X32;

/// <summary>
/// Behringer X32/M32 over OSC/UDP. Subscribes to remote changes with /xremote, requests meters,
/// and uses /info as a heartbeat to detect the console.
/// </summary>
public sealed class X32Adapter : ITargetAdapter
{
    private static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(7);

    private readonly X32Options _opt;
    private readonly ILogger<X32Adapter> _log;
    private readonly Channel<byte[]> _outbox = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(4096)
    {
        SingleReader = true,
        FullMode = BoundedChannelFullMode.DropOldest,
    });

    // OSC type observed per address, so we reply in the console's own type (int vs float vs string).
    private readonly ConcurrentDictionary<string, char> _types = new(StringComparer.Ordinal);
    private long _lastRxTicks;
    private Func<string>? _hostOverride;

    public X32Adapter(IOptions<X32Options> options, ILogger<X32Adapter> log)
    {
        _opt = options.Value;
        _log = log;
    }

    public string Id => "x32";
    public string DisplayName => $"Behringer X32 @ {Host}:{Port}";
    public bool IsConnected => DateTime.UtcNow.Ticks - Interlocked.Read(ref _lastRxTicks) < ConnectionTimeout.Ticks;
    public TargetCatalog Catalog { get; } = X32Catalog.Build();

    private string Host => _hostOverride?.Invoke() ?? _opt.Host;
    public int Port { get; set; }

    /// <summary>Points the adapter at another endpoint (used for the built-in emulator).</summary>
    public void UseEndpoint(Func<string> host, int port)
    {
        _hostOverride = host;
        Port = port;
    }

    public async Task RunAsync(Action<TargetEvent> sink, Action<bool> connectionChanged, CancellationToken ct)
    {
        if (Port == 0) Port = _opt.Port;
        if (string.IsNullOrWhiteSpace(Host))
        {
            _log.LogWarning("X32 adapter disabled: set Targets:X32:Host or Targets:X32:Emulator=true");
            return;
        }

        while (!ct.IsCancellationRequested)
        {
            using var udp = new UdpClient(Host.Contains(':') ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork);
            try
            {
                udp.Connect(Host, Port);
                _log.LogInformation("X32 adapter sending to {Host}:{Port}", Host, Port);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var send = SendLoop(udp, linked.Token);
                var keepAlive = KeepAliveLoop(connectionChanged, linked.Token);
                try
                {
                    await ReceiveLoop(udp, sink, connectionChanged, linked.Token);
                }
                finally
                {
                    linked.Cancel();
                    await Task.WhenAll(send, keepAlive).ContinueWith(_ => { }, TaskScheduler.Default);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "X32 connection error; retrying");
                await Task.Delay(2000, ct).ContinueWith(_ => { }, TaskScheduler.Default);
            }
        }
    }

    private async Task ReceiveLoop(UdpClient udp, Action<TargetEvent> sink, Action<bool> connectionChanged, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            UdpReceiveResult r;
            try
            {
                r = await udp.ReceiveAsync(ct);
            }
            catch (SocketException ex) when (ex.SocketErrorCode is SocketError.ConnectionReset or SocketError.ConnectionRefused)
            {
                await Task.Delay(500, ct); // ICMP port unreachable: console not there yet
                continue;
            }

            bool wasConnected = IsConnected;
            Interlocked.Exchange(ref _lastRxTicks, DateTime.UtcNow.Ticks);
            if (!wasConnected)
            {
                _log.LogInformation("X32 console responding at {Host}:{Port}", Host, Port);
                connectionChanged(true);
            }

            if (!Osc.TryParse(r.Buffer, out var address, out var args) || args.Length == 0) continue;

            if (address.StartsWith("/meters/", StringComparison.Ordinal))
            {
                if (args[0] is byte[] blob) sink(new MeterFrame(X32Meters.Decode(blob)));
                continue;
            }
            if (address is "/info" or "/status" or "/xinfo") continue;

            ParameterValue? value = args[0] switch
            {
                float f => new ParameterValue(f),
                int i => new ParameterValue(i),
                string s => ParameterValue.OfText(s),
                _ => null,
            };
            if (value is null) continue;
            var key = address.TrimStart('/');
            _types[key] = args[0] switch { float => 'f', int => 'i', _ => 's' };
            sink(new ParameterChanged(key, value.Value));
        }
    }

    private async Task SendLoop(UdpClient udp, CancellationToken ct)
    {
        int burst = 0;
        await foreach (var packet in _outbox.Reader.ReadAllAsync(ct))
        {
            try
            {
                await udp.SendAsync(packet, ct);
            }
            catch (SocketException ex)
            {
                _log.LogDebug(ex, "X32 send failed");
            }
            // Pace large bursts (e.g. a full resync) so the console's UDP buffer does not overflow.
            if (_outbox.Reader.Count > 0 && ++burst % 32 == 0) await Task.Delay(2, ct);
            else if (_outbox.Reader.Count == 0) burst = 0;
        }
    }

    private async Task KeepAliveLoop(Action<bool> connectionChanged, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(KeepAliveInterval);
        bool last = false;
        do
        {
            Enqueue(Osc.Message("/xremote"));
            Enqueue(Osc.Message("/meters", _opt.MeterBank));
            Enqueue(Osc.Message("/info"));
            bool now = IsConnected;
            if (last && !now)
            {
                _log.LogWarning("X32 console stopped responding");
                connectionChanged(false);
            }
            last = now;
        } while (await timer.WaitForNextTickAsync(ct));
    }

    public void Send(TargetCommand command)
    {
        switch (command)
        {
            case SetParameter s:
                Enqueue(Osc.Message("/" + s.Address.Value, Encode(s.Address.Value, s.Value)));
                break;
            case QueryParameter q:
                Enqueue(Osc.Message("/" + q.Address.Value));
                break;
        }
    }

    private object Encode(string address, ParameterValue v)
    {
        char type = _types.TryGetValue(address, out var t) ? t : GuessType(address, v);
        return type switch
        {
            'i' => (object)(int)Math.Round(v.Number),
            's' => v.Text ?? "",
            _ => (float)v.Number,
        };
    }

    /// <summary>Type for addresses we have not heard from the console yet.</summary>
    public static char GuessType(string address, ParameterValue v)
    {
        if (v.Text is not null) return 's';
        if (address.EndsWith("/on", StringComparison.Ordinal) || address.EndsWith("/color", StringComparison.Ordinal)
            || address.Contains("solosw", StringComparison.Ordinal) || address.StartsWith("config/mute/", StringComparison.Ordinal)
            || address.EndsWith("/source", StringComparison.Ordinal) || address.EndsWith("/icon", StringComparison.Ordinal))
            return 'i';
        return 'f';
    }

    private void Enqueue(byte[] packet) => _outbox.Writer.TryWrite(packet);

    public void SyncAll()
    {
        foreach (var ch in Catalog.Channels)
            foreach (var p in ch.Parameters)
                Send(new QueryParameter(p.Address));
    }

    public ValueTask DisposeAsync()
    {
        _outbox.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
