using XTouch.Abstractions;
using XTouch.Surface.Mcu;

namespace XTouch.Surface.Tests;

public class McuEncoderTests
{
    [Fact]
    public void Fader_full_scale() => Assert.Equal([0xE3, 0x7F, 0x7F], McuEncoder.Fader(3, 1.0));

    [Fact]
    public void Fader_clamps() => Assert.Equal([0xE0, 0x00, 0x00], McuEncoder.Fader(0, -0.5));

    [Theory]
    [InlineData(LedState.Off, 0x00)]
    [InlineData(LedState.Blink, 0x01)]
    [InlineData(LedState.On, 0x7F)]
    public void Led(LedState state, byte velocity) => Assert.Equal([0x90, 0x5E, velocity], McuEncoder.Led(0x5E, state));

    [Fact]
    public void Ring_packs_mode_position_and_center() =>
        Assert.Equal([0xB0, 0x32, 0x40 | (1 << 4) | 6], McuEncoder.Ring(2, new RingState(RingMode.BoostCut, 6, true)));

    [Fact]
    public void Meter_packs_strip_and_level() => Assert.Equal([0xD0, 0x5C], McuEncoder.Meter(5, 20));

    [Fact]
    public void Lcd_text_has_header_offset_and_terminator()
    {
        var msg = McuEncoder.LcdText(56, "Hi");
        Assert.Equal([0xF0, 0x00, 0x00, 0x66, 0x14, 0x12, 56, (byte)'H', (byte)'i', 0xF7], msg);
    }

    [Fact]
    public void Lcd_text_replaces_non_ascii() => Assert.Equal((byte)'?', McuEncoder.LcdText(0, "é")[7]);

    [Fact]
    public void Lcd_colors()
    {
        var colors = new[] { LcdColor.Red, LcdColor.Green, LcdColor.Yellow, LcdColor.Blue, LcdColor.Magenta, LcdColor.Cyan, LcdColor.White, LcdColor.Off };
        Assert.Equal([0xF0, 0x00, 0x00, 0x66, 0x14, 0x72, 1, 2, 3, 4, 5, 6, 7, 0, 0xF7], McuEncoder.LcdColorsMessage(colors));
    }

    [Theory]
    [InlineData(0, 0x4B)]
    [InlineData(1, 0x4A)]
    [InlineData(2, 0x49)]
    [InlineData(11, 0x40)]
    public void Seven_segment_digit_positions(int index, int cc) => Assert.Equal(cc, McuEncoder.SevenSegment(index, '0')[1]);

    [Theory]
    [InlineData('A', false, 0x01)]
    [InlineData('a', false, 0x01)]
    [InlineData('0', false, 0x30)]
    [InlineData('9', true, 0x79)]
    [InlineData(' ', false, 0x20)]
    [InlineData('~', false, 0x20)]
    public void Seven_segment_charset(char c, bool dot, int code) => Assert.Equal(code, McuEncoder.SevenSegmentCode(c, dot));
}
