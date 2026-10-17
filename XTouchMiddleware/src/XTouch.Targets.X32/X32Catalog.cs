using XTouch.Abstractions.Targets;

namespace XTouch.Targets.X32;

/// <summary>The parts of the X32 address tree this adapter knows how to present in the UI.</summary>
public static class X32Catalog
{
    public static TargetCatalog Build()
    {
        var channels = new List<CatalogChannel>();
        void Add(string prefix, int count, string label, bool hasTrim)
        {
            for (int i = 1; i <= count; i++)
            {
                var key = $"{prefix}/{i:00}";
                var ps = new List<CatalogParameter>
                {
                    new("fader", $"{key}/mix/fader", ParameterKind.Continuous),
                    new("on", $"{key}/mix/on", ParameterKind.Toggle),
                    new("pan", $"{key}/mix/pan", ParameterKind.Continuous),
                    new("name", $"{key}/config/name", ParameterKind.Text),
                    new("color", $"{key}/config/color", ParameterKind.Enum),
                };
                if (hasTrim) ps.Add(new("trim", $"{key}/preamp/trim", ParameterKind.Continuous));
                channels.Add(new CatalogChannel(key, $"{label} {i}", ps));
            }
        }
        Add("ch", 32, "Ch", hasTrim: true);
        Add("auxin", 8, "Aux", hasTrim: true);
        Add("bus", 16, "Bus", hasTrim: false);

        var globals = new List<CatalogParameter> { new("main", "main/st/mix/fader", ParameterKind.Continuous) };
        for (int g = 1; g <= 6; g++) globals.Add(new($"mutegroup{g}", $"config/mute/{g}", ParameterKind.Toggle));
        return new TargetCatalog(channels, globals);
    }
}
