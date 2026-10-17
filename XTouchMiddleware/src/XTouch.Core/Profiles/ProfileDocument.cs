using System.Text.Json;
using System.Text.Json.Serialization;

namespace XTouch.Core.Profiles;

/// <summary>
/// A profile as stored on disk (JSON). Strings in addresses may contain placeholders:
/// {key} (channel key), {n} / {n:00} (1-based position in the channel list) and {name} for any entry in the channel's "vars".
/// See docs/xtouch-middleware/08-configuration-and-mapping.md.
/// </summary>
public sealed class ProfileDocument
{
    public int SchemaVersion { get; set; } = 1;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Target { get; set; } = "x32";
    public List<ChannelDefinition> Channels { get; set; } = [];
    public StripTemplate Strip { get; set; } = new();
    public List<LayerDefinition> Layers { get; set; } = [];
    public string? DefaultLayer { get; set; }
    public BindingDefinition? Master { get; set; }
    public string MasterLabel { get; set; } = "MAIN";
    public Dictionary<string, BindingDefinition> Buttons { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static ProfileDocument Parse(string json) =>
        JsonSerializer.Deserialize<ProfileDocument>(json, JsonOptions) ?? throw new JsonException("Empty profile");

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
}

public sealed class ChannelDefinition
{
    /// <summary>Unique key, usually the target's address prefix (e.g. "ch/01").</summary>
    public string Key { get; set; } = "";

    /// <summary>Fallback scribble-strip name when the target does not provide one.</summary>
    public string? Label { get; set; }

    /// <summary>Index into the target's meter frame, if metered.</summary>
    public int? Meter { get; set; }

    public Dictionary<string, string> Vars { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class StripTemplate
{
    public BindingDefinition? Fader { get; set; }
    public BindingDefinition? Mute { get; set; }
    public BindingDefinition? Solo { get; set; }
    public BindingDefinition? Rec { get; set; }
    /// <summary>Text parameter shown on the top LCD line.</summary>
    public BindingDefinition? Name { get; set; }
    /// <summary>Numeric parameter whose value 0..7 (mod 8) selects the scribble-strip color.</summary>
    public BindingDefinition? Color { get; set; }
}

public enum ButtonMode { Toggle, Momentary, Trigger }

public sealed class BindingDefinition
{
    public string Address { get; set; } = "";
    public string? Converter { get; set; }
    public ButtonMode? Mode { get; set; }
    /// <summary>Value sent for <see cref="ButtonMode.Trigger"/> (normalized). Default 1.</summary>
    public double? Value { get; set; }
}

public sealed class LayerDefinition
{
    public string Id { get; set; } = "";
    public string? Label { get; set; }
    /// <summary>Button name that activates the layer (e.g. "PAN", "SEND", "F1").</summary>
    public string? Button { get; set; }
    public EncoderDefinition Encoder { get; set; } = new();
}

public sealed class EncoderDefinition
{
    public string Address { get; set; } = "";
    public string? Converter { get; set; }
    public Abstractions.RingMode Ring { get; set; } = Abstractions.RingMode.Dot;
    public double Step { get; set; } = 0.01;
    public double Default { get; set; }
}
