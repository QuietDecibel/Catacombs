using XTouch.Abstractions.Targets;

namespace XTouch.Core.State;

/// <summary>Last known value of every target parameter, in the target's own representation. Engine-loop only.</summary>
public sealed class MixerState
{
    private readonly Dictionary<string, ParameterValue> _values = new(StringComparer.Ordinal);

    public ParameterValue? Get(ParameterAddress a) => _values.TryGetValue(a.Value, out var v) ? v : null;

    /// <summary>Stores a value. Returns true if it changed.</summary>
    public bool Set(ParameterAddress a, ParameterValue v)
    {
        if (_values.TryGetValue(a.Value, out var old) && old == v) return false;
        _values[a.Value] = v;
        return true;
    }

    public int Count => _values.Count;

    public IReadOnlyDictionary<string, ParameterValue> Snapshot() => new Dictionary<string, ParameterValue>(_values);

    public void Clear() => _values.Clear();
}
