using System.Text.RegularExpressions;
using XTouch.Abstractions.Mcu;
using XTouch.Core.Conversion;

namespace XTouch.Core.Profiles;

public sealed class ProfileValidationException(IReadOnlyList<string> errors)
    : Exception("Invalid profile: " + string.Join("; ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}

/// <summary>Validates a <see cref="ProfileDocument"/> and expands it into a <see cref="CompiledProfile"/>.</summary>
public static partial class ProfileCompiler
{
    [GeneratedRegex(@"\{(?<name>[A-Za-z0-9_]+)(:(?<fmt>[^}]+))?\}")]
    private static partial Regex Placeholder();

    public static IReadOnlyList<string> Validate(ProfileDocument doc)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(doc.Id)) errors.Add("id is required");
        else if (!IdPattern().IsMatch(doc.Id)) errors.Add("id may contain only letters, digits, '-' and '_'");
        if (string.IsNullOrWhiteSpace(doc.Target)) errors.Add("target is required");
        if (doc.Channels.Count == 0) errors.Add("at least one channel is required");

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < doc.Channels.Count; i++)
        {
            var c = doc.Channels[i];
            if (string.IsNullOrWhiteSpace(c.Key)) errors.Add($"channels[{i}].key is required");
            else if (!keys.Add(c.Key)) errors.Add($"channels[{i}]: duplicate key '{c.Key}'");
            if (c.Meter is < 0) errors.Add($"channels[{i}].meter must be >= 0");
        }

        CheckBinding(doc.Strip.Fader, "strip.fader", errors);
        CheckBinding(doc.Strip.Mute, "strip.mute", errors);
        CheckBinding(doc.Strip.Solo, "strip.solo", errors);
        CheckBinding(doc.Strip.Rec, "strip.rec", errors);
        CheckBinding(doc.Strip.Name, "strip.name", errors);
        CheckBinding(doc.Strip.Color, "strip.color", errors);
        CheckBinding(doc.Master, "master", errors);

        var layerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var layerButtons = new HashSet<int>();
        for (int i = 0; i < doc.Layers.Count; i++)
        {
            var l = doc.Layers[i];
            if (string.IsNullOrWhiteSpace(l.Id)) errors.Add($"layers[{i}].id is required");
            else if (!layerIds.Add(l.Id)) errors.Add($"layers[{i}]: duplicate id '{l.Id}'");
            if (l.Button is not null)
            {
                if (!ButtonNames.TryGetNote(l.Button, out int note)) errors.Add($"layers[{i}].button: unknown button '{l.Button}'");
                else if (!layerButtons.Add(note)) errors.Add($"layers[{i}].button: '{l.Button}' is used by another layer");
            }
            if (string.IsNullOrWhiteSpace(l.Encoder.Address)) errors.Add($"layers[{i}].encoder.address is required");
            if (!ValueConverters.TryGet(l.Encoder.Converter, out _)) errors.Add($"layers[{i}].encoder.converter: unknown converter '{l.Encoder.Converter}'");
            if (l.Encoder.Step is <= 0 or > 1) errors.Add($"layers[{i}].encoder.step must be in (0, 1]");
            if (l.Encoder.Default is < 0 or > 1) errors.Add($"layers[{i}].encoder.default must be in [0, 1]");
        }
        if (doc.DefaultLayer is not null && !layerIds.Contains(doc.DefaultLayer))
            errors.Add($"defaultLayer '{doc.DefaultLayer}' is not one of the layers");

        foreach (var (name, binding) in doc.Buttons)
        {
            if (!ButtonNames.TryGetNote(name, out int note)) errors.Add($"buttons: unknown button '{name}'");
            else if (layerButtons.Contains(note)) errors.Add($"buttons: '{name}' is already used to select a layer");
            CheckBinding(binding, $"buttons.{name}", errors);
        }

        return errors;
    }

    public static CompiledProfile Compile(ProfileDocument doc)
    {
        var errors = Validate(doc);
        if (errors.Count > 0) throw new ProfileValidationException(errors);

        var channels = new List<CompiledChannel>(doc.Channels.Count);
        var byAddress = new Dictionary<string, List<int>>(StringComparer.Ordinal);

        for (int i = 0; i < doc.Channels.Count; i++)
        {
            var def = doc.Channels[i];
            Binding? B(BindingDefinition? b, ButtonMode defaultMode = ButtonMode.Toggle)
            {
                if (b is null) return null;
                var address = Expand(b.Address, def, i);
                Index(byAddress, address, i);
                return new Binding(address, ValueConverters.Get(b.Converter), b.Mode ?? defaultMode, b.Value ?? 1);
            }

            var encoders = new Dictionary<string, EncoderBinding>(StringComparer.OrdinalIgnoreCase);
            foreach (var layer in doc.Layers)
            {
                var address = Expand(layer.Encoder.Address, def, i);
                Index(byAddress, address, i);
                encoders[layer.Id] = new EncoderBinding(address, ValueConverters.Get(layer.Encoder.Converter),
                    layer.Encoder.Ring, layer.Encoder.Step, layer.Encoder.Default);
            }

            channels.Add(new CompiledChannel(
                i, def.Key, def.Label ?? def.Key, def.Meter,
                B(doc.Strip.Fader), B(doc.Strip.Mute), B(doc.Strip.Solo), B(doc.Strip.Rec), B(doc.Strip.Name), B(doc.Strip.Color),
                encoders));
        }

        var layers = doc.Layers.Select(l => new CompiledLayer(
            l.Id, l.Label ?? l.Id.ToUpperInvariant(),
            l.Button is not null && ButtonNames.TryGetNote(l.Button, out int n) ? n : null)).ToList();

        var buttons = new Dictionary<int, Binding>();
        foreach (var (name, b) in doc.Buttons)
        {
            ButtonNames.TryGetNote(name, out int note);
            buttons[note] = new Binding(b.Address, ValueConverters.Get(b.Converter ?? "toggle"), b.Mode ?? ButtonMode.Toggle, b.Value ?? 1);
        }

        return new CompiledProfile
        {
            Id = doc.Id,
            Name = string.IsNullOrWhiteSpace(doc.Name) ? doc.Id : doc.Name,
            Target = doc.Target,
            Channels = channels,
            Layers = layers,
            DefaultLayer = doc.DefaultLayer ?? layers.FirstOrDefault()?.Id ?? "",
            Master = doc.Master is null ? null : new Binding(doc.Master.Address, ValueConverters.Get(doc.Master.Converter)),
            MasterLabel = doc.MasterLabel,
            Buttons = buttons,
            ChannelsByAddress = byAddress.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray(), StringComparer.Ordinal),
            Source = doc,
        };
    }

    /// <summary>Expands {key}, {n}, {n:00} and channel vars in an address template.</summary>
    public static string Expand(string template, ChannelDefinition channel, int index) =>
        Placeholder().Replace(template, m =>
        {
            var name = m.Groups["name"].Value;
            var fmt = m.Groups["fmt"].Success ? m.Groups["fmt"].Value : null;
            if (name.Equals("key", StringComparison.OrdinalIgnoreCase)) return channel.Key;
            if (name.Equals("n", StringComparison.OrdinalIgnoreCase)) return (index + 1).ToString(fmt);
            if (channel.Vars.TryGetValue(name, out var v))
                return fmt is not null && int.TryParse(v, out int iv) ? iv.ToString(fmt) : v;
            throw new ProfileValidationException([$"channel '{channel.Key}': unknown placeholder '{{{name}}}' in '{template}'"]);
        });

    private static void Index(Dictionary<string, List<int>> map, string address, int channel)
    {
        if (!map.TryGetValue(address, out var list)) map[address] = list = [];
        if (!list.Contains(channel)) list.Add(channel);
    }

    private static void CheckBinding(BindingDefinition? b, string path, List<string> errors)
    {
        if (b is null) return;
        if (string.IsNullOrWhiteSpace(b.Address)) errors.Add($"{path}.address is required");
        if (!ValueConverters.TryGet(b.Converter, out _)) errors.Add($"{path}.converter: unknown converter '{b.Converter}'");
    }

    [GeneratedRegex("^[A-Za-z0-9_-]+$")]
    private static partial Regex IdPattern();
}
