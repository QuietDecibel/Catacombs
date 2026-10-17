using XTouch.Abstractions.Targets;

namespace XTouch.Targets.X32.Tests;

public class OscTests
{
    [Fact]
    public void Message_without_args()
    {
        // "/xremote" is 8 chars → padded to 12; "," → padded to 4
        var msg = Osc.Message("/xremote");
        Assert.Equal(16, msg.Length);
        Assert.True(Osc.TryParse(msg, out var a, out var args));
        Assert.Equal("/xremote", a);
        Assert.Empty(args);
    }

    [Fact]
    public void Round_trips_all_types()
    {
        var blob = new byte[] { 1, 2, 3, 4, 5 };
        var msg = Osc.Message("/ch/01/mix/fader", 0.75f, 3, "Kick", blob);
        Assert.Equal(0, msg.Length % 4);
        Assert.True(Osc.TryParse(msg, out var address, out var args));
        Assert.Equal("/ch/01/mix/fader", address);
        Assert.Equal(0.75f, args[0]);
        Assert.Equal(3, args[1]);
        Assert.Equal("Kick", args[2]);
        Assert.Equal(blob, args[3]);
    }

    [Fact]
    public void Float_is_big_endian() =>
        Assert.Equal(new byte[] { 0x3F, 0x40, 0x00, 0x00 }, Osc.Message("/a", 0.75f)[^4..]);

    [Fact]
    public void Garbage_is_rejected()
    {
        Assert.False(Osc.TryParse([1, 2, 3], out _, out _));
        Assert.False(Osc.TryParse("#bundle\0"u8.ToArray(), out _, out _));
    }

    [Fact]
    public void Meter_blob_round_trips()
    {
        float[] levels = [0f, 0.5f, 1f];
        Assert.Equal(levels, X32Meters.Decode(X32Meters.Encode(levels)));
        Assert.Empty(X32Meters.Decode([1]));
    }

    [Theory]
    [InlineData("ch/01/mix/on", 'i')]
    [InlineData("ch/01/config/color", 'i')]
    [InlineData("-stat/solosw/05", 'i')]
    [InlineData("config/mute/2", 'i')]
    [InlineData("ch/01/mix/fader", 'f')]
    public void Guesses_types(string address, char type) => Assert.Equal(type, X32Adapter.GuessType(address, new ParameterValue(1)));
}
