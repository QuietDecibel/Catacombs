using XTouch.Core.Engine;

namespace XTouch.Service.Workers;

public sealed class EngineWorker(ControlEngine engine) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => engine.RunAsync(stoppingToken);
}
