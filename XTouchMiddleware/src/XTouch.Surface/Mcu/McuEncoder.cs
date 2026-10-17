using System.Text;
using XTouch.Abstractions;
using XTouch.Abstractions.Mcu;
using static XTouch.Abstractions.Mcu.McuConstants;

namespace XTouch.Surface.Mcu;

/// <summary>Builds MC-mode feedback messages. Pure and stateless.</summary>
public static class McuEncoder
{
    public static byte[] Fader(int strip, double value)
    {
        int v = (int)Math.Round(Math.Clamp(value, 0, 1) * 16383);
        return [(byte)(0xE0 | strip), (byte)(v & 0x7F), (byte)((v >> 7) & 0x7F)];
    }

    public static byte[] Led(int note, LedState state) => [0x90, (byte)note, (byte)state];

    public static byte[] Ring(int strip, RingState r) =>
        [0xB0, (byte)(RingBase + strip),
         (byte)((r.Center ? 0x40 : 0) | (((int)r.Mode & 3) << 4) | Math.Clamp(r.Position, 0, 11))];

    /// <summary>Meter level 0..12, or 0x0E (set clip) / 0x0F (clear clip).</summary>
    public static byte[] Meter(int strip, int level)
    {
        int v = level is 0x0E or 0x0F ? level : Math.Clamp(level, 0, MeterMax);
        return [0xD0, (byte)((strip << 4) | v)];
    }

    public static byte[] LcdText(int offset, string text, byte deviceId = DeviceIdMain)
    {
        var bytes = Encoding.ASCII.GetBytes(SurfaceText.Fit(text, text.Length));
        return [0xF0, 0x00, 0x00, 0x66, deviceId, LcdWrite, (byte)offset, .. bytes, 0xF7];
    }

    public static byte[] LcdColorsMessage(IReadOnlyList<LcdColor> colors, byte deviceId = DeviceIdMain)
    {
        var msg = new byte[7 + Strips];
        msg[0] = 0xF0; msg[1] = 0x00; msg[2] = 0x00; msg[3] = 0x66; msg[4] = deviceId; msg[5] = McuConstants.LcdColors;
        for (int i = 0; i < Strips; i++) msg[6 + i] = (byte)((byte)colors[i] & 0x07);
        msg[^1] = 0xF7;
        return msg;
    }

    /// <summary>
    /// One 7-segment digit. <paramref name="index"/> is the position in the 12-char display string:
    /// 0..1 = assignment display (left, right), 2..11 = timecode digits (left to right).
    /// </summary>
    public static byte[] SevenSegment(int index, char c, bool dot = false)
    {
        int cc = index switch
        {
            0 => SevenSegBase + 11,                 // assignment left  = 0x4B
            1 => SevenSegBase + 10,                 // assignment right = 0x4A
            _ => SevenSegBase + (SevenSegDigits - 1 - index), // timecode: index 2 → 0x49 ... index 11 → 0x40
        };
        return [0xB0, (byte)cc, SevenSegmentCode(c, dot)];
    }

    public static byte SevenSegmentCode(char c, bool dot = false)
    {
        c = char.ToUpperInvariant(c);
        int code = c switch
        {
            >= '@' and <= '_' => c - 0x40,
            >= ' ' and <= '?' => c,
            _ => ' ',
        };
        return (byte)(code | (dot ? 0x40 : 0));
    }
}
