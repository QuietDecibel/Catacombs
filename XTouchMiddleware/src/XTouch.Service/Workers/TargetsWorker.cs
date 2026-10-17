using Microsoft.Extensions.Options;
using XTouch.Abstractions.Targets;
using XTouch.Core.Engine;
using XTouch.Targets.X32;

namespace XTouch.Service.Workers;

/// <summary>Runs every target adapter (and the X32 emulator when enabled), forwarding events into the engine.</summary>
public sealed class TargetsWorker(
    ITargetRegistry targets, EngineChannel engine, IOptions<X32Options> x32Options,
    ILoggerFactory loggers, ILogger<TargetsWorker> log) : BackgroundService
{
    private X32Emulator? _emulator;

    /// <summary>The built-in X32 emulator, when enabled.</summary>
    public X32Emulator? Emulator => _emulator;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        StartEmulatorIfEnabled();

        var runs = targets.All.Select(adapter => Task.Run(async () =>
        {
            try
            {
                await adapter.RunAsync(
                    e => engine.Post(new FromTarget(adapter.Id, e)),
                    connected => engine.Post(new TargetConnection(adapter.Id, connected)),
                    ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                log.LogError(ex, "Target adapter {Id} stopped unexpectedly", adapter.Id);
            }
        }, ct)).ToList();

        await Task.WhenAll(runs);
    }

    private void StartEmulatorIfEnabled()
    {
        var opt = x32Options.Value;
        if (!opt.Emulator || targets.Get("x32") is not X32Adapter adapter) return;
        try
        {
            _emulator = new X32Emulator(opt.EmulatorPort, loggers.CreateLogger<X32Emulator>());
        }
        catch (System.Net.Sockets.SocketException)
        {
            _emulator = new X32Emulator(0, loggers.CreateLogger<X32Emulator>()); // preferred port busy
        }
        _emulator.Start();
        adapter.UseEndpoint(() => "127.0.0.1", _emulator.Port);
        log.LogInformation("X32 emulator running on 127.0.0.1:{Port}", _emulator.Port);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        if (Interlocked.Exchange(ref _emulator, null) is { } emulator) await emulator.DisposeAsync();
    }
}
