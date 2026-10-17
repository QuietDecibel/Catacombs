using System.Collections.Concurrent;
using XTouch.Abstractions.Targets;

namespace XTouch.Core.Targets;

/// <summary>
/// In-memory target that stores values and echoes every change back. Useful for demos without
/// a mixer and for tests.
/// </summary>
public sealed class LoopbackTarget(string id = "loopback") : ITargetAdapter
{
    private readonly ConcurrentDictionary<string, ParameterValue> _values = new();
    private Action<TargetEvent>? _sink;

    public string Id { get; } = id;
    public string DisplayName => "Loopback (no hardware)";
    public bool IsConnected => _sink is not null;
    public TargetCatalog Catalog => TargetCatalog.Empty;
    public ConcurrentQueue<TargetCommand> Received { get; } = new();

    public async Task RunAsync(Action<TargetEvent> sink, Action<bool> connectionChanged, CancellationToken ct)
    {
        _sink = sink;
        connectionChanged(true);
        try { await Task.Delay(Timeout.Infinite, ct); }
        catch (OperationCanceledException) { }
        _sink = null;
    }

    public void Send(TargetCommand command)
    {
        Received.Enqueue(command);
        switch (command)
        {
            case SetParameter s:
                _values[s.Address.Value] = s.Value;
                _sink?.Invoke(new ParameterChanged(s.Address, s.Value));
                break;
            case QueryParameter q when _values.TryGetValue(q.Address.Value, out var v):
                _sink?.Invoke(new ParameterChanged(q.Address, v));
                break;
        }
    }

    /// <summary>Simulates a change made on the target itself.</summary>
    public void SimulateRemoteChange(ParameterAddress address, ParameterValue value)
    {
        _values[address.Value] = value;
        _sink?.Invoke(new ParameterChanged(address, value));
    }

    public void SyncAll() { }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
