using XTouch.Abstractions;
using XTouch.Abstractions.Targets;
using XTouch.Core.Conversion;

namespace XTouch.Core.Profiles;

public sealed record Binding(ParameterAddress Address, IValueConverter Converter, ButtonMode Mode = ButtonMode.Toggle, double TriggerValue = 1);

public sealed record EncoderBinding(ParameterAddress Address, IValueConverter Converter, RingMode Ring, double Step, double Default);

public sealed record CompiledChannel(
    int Index, string Key, string Label, int? Meter,
    Binding? Fader, Binding? Mute, Binding? Solo, Binding? Rec, Binding? Name, Binding? Color,
    IReadOnlyDictionary<string, EncoderBinding> Encoders);

public sealed record CompiledLayer(string Id, string Label, int? ButtonNote);

/// <summary>A validated profile with every placeholder expanded. Immutable; safe to share.</summary>
public sealed class CompiledProfile
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Target { get; init; }
    public required IReadOnlyList<CompiledChannel> Channels { get; init; }
    public required IReadOnlyList<CompiledLayer> Layers { get; init; }
    public required string DefaultLayer { get; init; }
    public Binding? Master { get; init; }
    public required string MasterLabel { get; init; }
    /// <summary>MCU note → binding for global buttons.</summary>
    public required IReadOnlyDictionary<int, Binding> Buttons { get; init; }
    /// <summary>Address → indexes of channels that use it (for routing target changes to strips).</summary>
    public required IReadOnlyDictionary<string, int[]> ChannelsByAddress { get; init; }
    public required ProfileDocument Source { get; init; }

    public int ChannelCount => Channels.Count;

    public CompiledLayer? LayerByNote(int note) => Layers.FirstOrDefault(l => l.ButtonNote == note);

    public IEnumerable<ParameterAddress> AllAddresses()
    {
        var set = new HashSet<string>(ChannelsByAddress.Keys);
        if (Master is not null) set.Add(Master.Address.Value);
        foreach (var b in Buttons.Values) set.Add(b.Address.Value);
        return set.Select(a => new ParameterAddress(a));
    }
}
