using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using XTouch.Abstractions;
using XTouch.Abstractions.Targets;
using XTouch.Contracts;
using XTouch.Core.Engine;
using XTouch.Service.Logging;
using XTouch.Surface;
using XTouch.Surface.Transport;

namespace XTouch.Service.Api;

/// <summary>
/// Bridges engine output to SignalR clients. The engine publishes on every batch; this class keeps
/// the latest snapshot and pushes it at most ~30 times per second. Logs and MIDI trace are batched too.
/// </summary>
public sealed class HubStateBroadcaster : IStateBroadcaster, IHostedService, IDisposable
{
    public const string LogsGroup = "logs";
    public const string TraceGroup = "trace";

    private readonly IHubContext<SurfaceHub> _hub;
    private readonly ITargetRegistry _targets;
    private readonly MidiTrace _trace;
    private readonly UiLogSink _logs;
    private readonly string _transport;
    private readonly Lock _gate = new();
    private readonly List<LogEntryDto> _pendingLogs = [];
    private readonly List<MidiTraceDto> _pendingTrace = [];

    private SurfaceSnapshot _surface = SurfaceSnapshot.Blank;
    private EngineStatus _engineStatus = EngineStatus.Empty;
    private SurfaceDto _surfaceDto = SurfaceDto.Empty;
    private StatusDto _statusDto = StatusDto.Empty;
    private int _surfaceDirty, _statusDirty;
    private Timer? _timer;

    public HubStateBroadcaster(IHubContext<SurfaceHub> hub, ITargetRegistry targets, MidiTrace trace, UiLogSink logs, IOptions<SurfaceOptions> surface)
    {
        _hub = hub;
        _targets = targets;
        _trace = trace;
        _logs = logs;
        _transport = surface.Value.Transport.ToString();
        _logs.Entry += e => { lock (_gate) if (_pendingLogs.Count < 1000) _pendingLogs.Add(e); };
        _trace.Entry += e => { lock (_gate) if (_pendingTrace.Count < 2000) _pendingTrace.Add(new MidiTraceDto(e.Time, e.Direction, e.Hex, e.Description)); };
    }

    public StatusDto Status { get { lock (_gate) return _statusDto; } }
    public SurfaceDto Surface { get { lock (_gate) return _surfaceDto; } }

    public void Publish(SurfaceSnapshot surface, EngineStatus status, IReadOnlyDictionary<string, ParameterValue>? mixerChanges)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(surface, _surface))
            {
                _surface = surface;
                _surfaceDto = DtoMapper.ToDto(surface, status);
                _surfaceDirty = 1;
            }
            if (!ReferenceEquals(status, _engineStatus))
            {
                _engineStatus = status;
                _statusDto = DtoMapper.ToDto(status, _targets, _transport, _trace.Enabled);
                _statusDirty = 1;
            }
        }
    }

    /// <summary>Re-sends status (e.g. after the trace switch changes).</summary>
    public void RefreshStatus()
    {
        lock (_gate)
        {
            _statusDto = DtoMapper.ToDto(_engineStatus, _targets, _transport, _trace.Enabled);
            _statusDirty = 1;
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(_ => Flush(), null, 0, 33);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        return Task.CompletedTask;
    }

    private int _flushing;

    private void Flush()
    {
        if (Interlocked.Exchange(ref _flushing, 1) == 1) return;
        try
        {
            SurfaceDto? surface = null; StatusDto? status = null;
            LogEntryDto[] logs = []; MidiTraceDto[] trace = [];
            lock (_gate)
            {
                if (_surfaceDirty == 1) { surface = _surfaceDto; _surfaceDirty = 0; }
                if (_statusDirty == 1) { status = _statusDto; _statusDirty = 0; }
                if (_pendingLogs.Count > 0) { logs = [.. _pendingLogs]; _pendingLogs.Clear(); }
                if (_pendingTrace.Count > 0) { trace = [.. _pendingTrace]; _pendingTrace.Clear(); }
            }
            var tasks = new List<Task>(4);
            if (status is not null) tasks.Add(_hub.Clients.All.SendAsync(HubEvents.Status, status));
            if (surface is not null) tasks.Add(_hub.Clients.All.SendAsync(HubEvents.Surface, surface));
            if (logs.Length > 0) tasks.Add(_hub.Clients.Group(LogsGroup).SendAsync(HubEvents.Logs, logs));
            if (trace.Length > 0) tasks.Add(_hub.Clients.Group(TraceGroup).SendAsync(HubEvents.Trace, trace));
            if (tasks.Count > 0) Task.WaitAll([.. tasks], TimeSpan.FromSeconds(2));
        }
        catch (Exception)
        {
            // A slow or vanished client must never break broadcasting.
        }
        finally
        {
            Interlocked.Exchange(ref _flushing, 0);
        }
    }

    public void Dispose() => _timer?.Dispose();
}
