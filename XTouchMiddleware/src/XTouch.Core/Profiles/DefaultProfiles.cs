using XTouch.Abstractions;

namespace XTouch.Core.Profiles;

/// <summary>Built-in profiles written to the profile directory on first run.</summary>
public static class DefaultProfiles
{
    /// <summary>
    /// Behringer X32/M32: 32 inputs, 8 aux inputs, 16 mix buses. Encoder layers: pan, trim, send 1-4.
    /// Solo numbers follow the X32 /-stat/solosw numbering; meter indexes follow /meters/1.
    /// </summary>
    public static ProfileDocument X32()
    {
        var channels = new List<ChannelDefinition>();
        for (int i = 1; i <= 32; i++)
            channels.Add(new() { Key = $"ch/{i:00}", Label = $"Ch {i}", Meter = i - 1, Vars = { ["solo"] = $"{i:00}" } });
        for (int i = 1; i <= 8; i++)
            channels.Add(new() { Key = $"auxin/{i:00}", Label = $"Aux {i}", Meter = 31 + i, Vars = { ["solo"] = $"{32 + i:00}" } });
        for (int i = 1; i <= 16; i++)
            channels.Add(new() { Key = $"bus/{i:00}", Label = $"Bus {i}", Meter = 47 + i, Vars = { ["solo"] = $"{48 + i:00}" } });

        var doc = new ProfileDocument
        {
            Id = "x32-default",
            Name = "X32 – Inputs, Aux, Buses",
            Target = "x32",
            Channels = channels,
            Strip = new StripTemplate
            {
                Fader = new() { Address = "{key}/mix/fader", Converter = "x32-fader" },
                Mute = new() { Address = "{key}/mix/on", Converter = "inverted-toggle", Mode = ButtonMode.Toggle },
                Solo = new() { Address = "-stat/solosw/{solo}", Converter = "toggle", Mode = ButtonMode.Toggle },
                Name = new() { Address = "{key}/config/name" },
                Color = new() { Address = "{key}/config/color" },
            },
            Layers =
            [
                new() { Id = "pan", Label = "PAN", Button = "PAN", Encoder = new() { Address = "{key}/mix/pan", Converter = "pan", Ring = RingMode.BoostCut, Step = 0.01, Default = 0.5 } },
                new() { Id = "trim", Label = "TRIM", Button = "TRACK", Encoder = new() { Address = "{key}/preamp/trim", Converter = "x32-trim", Ring = RingMode.BoostCut, Step = 0.01, Default = 0.5 } },
                new() { Id = "send1", Label = "SEND 1", Button = "SEND", Encoder = new() { Address = "{key}/mix/01/level", Converter = "x32-fader", Ring = RingMode.Wrap, Step = 0.01, Default = 0 } },
                new() { Id = "send2", Label = "SEND 2", Button = "PLUGIN", Encoder = new() { Address = "{key}/mix/02/level", Converter = "x32-fader", Ring = RingMode.Wrap, Step = 0.01, Default = 0 } },
                new() { Id = "send3", Label = "SEND 3", Button = "EQ", Encoder = new() { Address = "{key}/mix/03/level", Converter = "x32-fader", Ring = RingMode.Wrap, Step = 0.01, Default = 0 } },
                new() { Id = "send4", Label = "SEND 4", Button = "INST", Encoder = new() { Address = "{key}/mix/04/level", Converter = "x32-fader", Ring = RingMode.Wrap, Step = 0.01, Default = 0 } },
            ],
            DefaultLayer = "pan",
            Master = new() { Address = "main/st/mix/fader", Converter = "x32-fader" },
            MasterLabel = "MAIN LR",
        };
        for (int g = 1; g <= 6; g++)
            doc.Buttons[$"F{g}"] = new() { Address = $"config/mute/{g}", Converter = "toggle", Mode = ButtonMode.Toggle };
        return doc;
    }

    public static IEnumerable<ProfileDocument> All() => [X32()];
}
