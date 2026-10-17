using XTouch.Abstractions;
using XTouch.Abstractions.Targets;

namespace XTouch.Core.Engine;

public sealed record StripInfo(int Strip, int? ChannelIndex, string ChannelKey, string Label);

/// <summary>Engine state for the API/UI. Immutable.</summary>
public sealed record EngineStatus(
    bool SurfaceConnected,
    string? SurfacePort,
    IReadOnlyDictionary<string, bool> Targets,
    string ProfileId,
    string ProfileName,
    int BankOffset,
    int ChannelCount,
    string Layer,
    IReadOnlyList<(string Id, string Label)> Layers,
    IReadOnlyList<StripInfo> Strips,
    bool ShowEncoderValues)
{
    public static EngineStatus Empty { get; } = new(false, null, new Dictionary<string, bool>(), "", "", 0, 0, "", [], [], false);
}

/// <summary>Receives engine output for the API layer. Called on the engine thread; must not block.</summary>
public interface IStateBroadcaster
{
    void Publish(SurfaceSnapshot surface, EngineStatus status, IReadOnlyDictionary<string, ParameterValue>? mixerChanges);
}

public sealed class NullStateBroadcaster : IStateBroadcaster
{
    public void Publish(SurfaceSnapshot surface, EngineStatus status, IReadOnlyDictionary<string, ParameterValue>? mixerChanges) { }
}

public interface ITargetRegistry
{
    ITargetAdapter? Get(string id);
    IReadOnlyList<ITargetAdapter> All { get; }
}

public sealed class TargetRegistry(IEnumerable<ITargetAdapter> adapters) : ITargetRegistry
{
    private readonly IReadOnlyList<ITargetAdapter> _all = adapters.ToList();
    public ITargetAdapter? Get(string id) => _all.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));
    public IReadOnlyList<ITargetAdapter> All => _all;
}
