using XTouch.Abstractions;
using XTouch.Surface.Mcu;

namespace XTouch.Surface.Tests;

public class McuDecoderTests
{
    [Theory]
    [InlineData(new byte[] { 0xE2, 0x7F, 0x7F }, 2, 1.0)]
    [InlineData(new byte[] { 0xE0, 0x00, 0x00 }, 0, 0.0)]
    [InlineData(new byte[] { 0xE8, 0x7F, 0x7F }, 8, 1.0)]
    public void Decodes_faders(byte[] bytes, int strip, double value) =>
        Assert.Equal(new FaderMoved(strip, value), McuDecoder.Decode(bytes));

    [Fact]
    public void Fader_round_trips_through_encoder()
    {
        var decoded = (FaderMoved)McuDecoder.Decode(McuEncoder.Fader(5, 0.3));
        Assert.Equal(5, decoded.Strip);
        Assert.Equal(0.3, decoded.Value, 3);
    }

    [Theory]
    [InlineData(0x68, 0)]
    [InlineData(0x6F, 7)]
    [InlineData(0x70, 8)]
    public void Decodes_fader_touch(int note, int strip)
    {
        Assert.Equal(new FaderTouched(strip, true), McuDecoder.Decode([0x90, (byte)note, 0x7F]));
        Assert.Equal(new FaderTouched(strip, false), McuDecoder.Decode([0x90, (byte)note, 0x00]));
        Assert.Equal(new FaderTouched(strip, false), McuDecoder.Decode([0x80, (byte)note, 0x40]));
    }

    [Theory]
    [InlineData(0x10, true)]
    [InlineData(0x5E, true)]
    public void Decodes_buttons(int note, bool pressed) =>
        Assert.Equal(new ButtonChanged(note, pressed), McuDecoder.Decode([0x90, (byte)note, 0x7F]));

    [Fact]
    public void Note_off_and_zero_velocity_mean_release()
    {
        Assert.Equal(new ButtonChanged(0x10, false), McuDecoder.Decode([0x90, 0x10, 0x00]));
        Assert.Equal(new ButtonChanged(0x10, false), McuDecoder.Decode([0x80, 0x10, 0x7F]));
    }

    [Theory]
    [InlineData(0x01, 1)]
    [InlineData(0x3F, 63)]
    [InlineData(0x41, -1)]
    [InlineData(0x42, -2)]
    [InlineData(0x7F, -63)]
    public void Decodes_encoder_deltas(int value, int delta) =>
        Assert.Equal(new EncoderTurned(3, delta), McuDecoder.Decode([0xB0, 0x13, (byte)value]));

    [Fact]
    public void Decodes_jog() => Assert.Equal(new JogTurned(-3), McuDecoder.Decode([0xB0, 0x3C, 0x43]));

    [Fact]
    public void Unknown_messages_are_reported_not_thrown()
    {
        Assert.IsType<UnknownMessage>(McuDecoder.Decode([0xF0, 0x00, 0xF7]));
        Assert.IsType<UnknownMessage>(McuDecoder.Decode([0xB0, 0x50, 0x01]));
        Assert.IsType<UnknownMessage>(McuDecoder.Decode([]));
        Assert.IsType<UnknownMessage>(McuDecoder.Decode([0x90, 0x10]));
    }
}
