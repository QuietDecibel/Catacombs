using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using XTouch.Core.Conversion;
using XTouch.Core.Profiles;

namespace XTouch.Core.Tests;

public class ProfileTests
{
    [Fact]
    public void Default_profile_is_valid_and_round_trips_through_json()
    {
        var doc = DefaultProfiles.X32();
        Assert.Empty(ProfileCompiler.Validate(doc));
        var reparsed = ProfileDocument.Parse(doc.ToJson());
        Assert.Empty(ProfileCompiler.Validate(reparsed));
        Assert.Equal(doc.Channels.Count, reparsed.Channels.Count);
        Assert.Equal(56, ProfileCompiler.Compile(reparsed).ChannelCount);
    }

    [Fact]
    public void Placeholders_expand()
    {
        var ch = new ChannelDefinition { Key = "ch/07", Vars = { ["solo"] = "7" } };
        Assert.Equal("ch/07/mix/fader", ProfileCompiler.Expand("{key}/mix/fader", ch, 6));
        Assert.Equal("x/07", ProfileCompiler.Expand("x/{n:00}", ch, 6));
        Assert.Equal("-stat/solosw/07", ProfileCompiler.Expand("-stat/solosw/{solo:00}", ch, 6));
        Assert.Throws<ProfileValidationException>(() => ProfileCompiler.Expand("{nope}", ch, 0));
    }

    [Fact]
    public void Validation_reports_problems()
    {
        var doc = DefaultProfiles.X32();
        doc.Id = "bad id!";
        doc.Layers.Add(new LayerDefinition { Id = "pan", Button = "PAN", Encoder = new() { Address = "x", Converter = "nope" } });
        doc.Buttons["NOT_A_BUTTON"] = new BindingDefinition { Address = "a" };
        doc.DefaultLayer = "missing";
        var errors = ProfileCompiler.Validate(doc);
        Assert.Contains(errors, e => e.Contains("id may contain"));
        Assert.Contains(errors, e => e.Contains("duplicate id 'pan'"));
        Assert.Contains(errors, e => e.Contains("used by another layer"));
        Assert.Contains(errors, e => e.Contains("unknown converter 'nope'"));
        Assert.Contains(errors, e => e.Contains("unknown button 'NOT_A_BUTTON'"));
        Assert.Contains(errors, e => e.Contains("defaultLayer"));
    }

    [Fact]
    public void Json_with_comments_is_accepted()
    {
        var json = DefaultProfiles.X32().ToJson().Insert(1, "\n  // a comment\n");
        Assert.Equal("x32-default", ProfileDocument.Parse(json).Id);
    }

    [Fact]
    public void Store_writes_defaults_saves_and_rejects_invalid()
    {
        var dir = Path.Combine(Path.GetTempPath(), "xtouch-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var store = new ProfileStore(Options.Create(new ProfileOptions { Directory = dir }), NullLogger<ProfileStore>.Instance);
            Assert.Contains(store.List(), p => p.Id == "x32-default" && p.IsValid);
            Assert.Equal("x32-default", store.ActiveId);

            var doc = DefaultProfiles.X32();
            doc.Id = "copy";
            store.Save("copy", doc);
            Assert.NotNull(store.Get("copy"));

            doc.Channels.Clear();
            Assert.Throws<ProfileValidationException>(() => store.Save("copy", doc));
            Assert.Throws<ProfileValidationException>(() => store.Save("other", DefaultProfiles.X32()));
            Assert.Throws<ArgumentException>(() => store.PathFor("../evil"));
            Assert.True(store.Delete("copy"));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Theory]
    [InlineData(0.75, 0.0)]
    [InlineData(1.0, 10.0)]
    [InlineData(0.5, -10.0)]
    [InlineData(0.25, -30.0)]
    [InlineData(0.0625, -60.0)]
    public void X32_fader_law(double f, double db)
    {
        Assert.Equal(db, X32FaderConverter.ToDb(f), 6);
        Assert.Equal(f, X32FaderConverter.FromDb(db), 6);
    }

    [Fact]
    public void Converter_formats_fit_a_cell()
    {
        foreach (var name in ValueConverters.Names)
        {
            var c = ValueConverters.Get(name);
            foreach (var v in new[] { 0.0, 0.01, 0.5, 0.99, 1.0 })
                Assert.True(c.Format(v).Length <= 7, $"{name}({v}) = '{c.Format(v)}'");
        }
    }

    [Fact]
    public void Inverted_toggle()
    {
        var c = ValueConverters.Get("inverted-toggle");
        Assert.Equal(0, c.ToTarget(1).Number);   // muted → mix/on 0
        Assert.Equal(1, c.ToNormalized(new(0)));
    }
}
