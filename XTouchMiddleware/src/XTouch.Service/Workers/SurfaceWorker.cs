using Microsoft.Extensions.Options;
using XTouch.Abstractions;
using XTouch.Core.Engine;
using XTouch.Surface;
using XTouch.Surface.Mcu;
using XTouch.Surface.Transport;

namespace XTouch.Service.Workers;

/// <summary>Owns the surface connection: connect/reconnect, decode input into the engine, run the renderer.</summary>
public sealed class SurfaceWorker(
    IMidiTransport transport, EngineChannel engine, FeedbackRenderer renderer,
    IOptions<SurfaceOptions> options, ILogger<SurfaceWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        transport.MessageReceived += raw => engine.Post(new FromSurface(McuDecoder.Decode(raw.Span)));
        transport.ConnectionChanged += connected =>
        {
            if (connected) renderer.Invalidate();
            engine.Post(new SurfaceConnection(connected, connected ? transport.Name : null));
            if (!connected) log.LogWarning("Surface disconnected");
        };

        log.LogInformation("Surface transport: {Kind} (port match '{Port}')", options.Value.Transport, options.Value.InputPortName);
        var render = renderer.RunAsync(ct);
        var dryWet = (transport as TracingTransport)?.Inner as DryWetMidiTransport;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!transport.IsConnected) await transport.ConnectAsync(ct);
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
                if (dryWet is not null && dryWet.IsConnected && dryWet.PortVanished()) dryWet.Disconnect();
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                log.LogError(ex, "Surface worker error");
                await Task.Delay(2000, CancellationToken.None);
            }
        }
        await render;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        if (options.Value.BlankOnShutdown && transport.IsConnected)
        {
            foreach (var msg in SurfaceReset.Messages()) transport.Send(msg);
            log.LogInformation("Surface blanked");
        }
    }
}
