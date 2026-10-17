using XTouch.Abstractions;
using static XTouch.Abstractions.Mcu.McuConstants;

namespace XTouch.Surface.Mcu;

/// <summary>Messages that blank the whole surface (used on shutdown).</summary>
public static class SurfaceReset
{
    public static IEnumerable<byte[]> Messages()
    {
        for (int n = 0; n <= LastLedNote; n++) yield return McuEncoder.Led(n, LedState.Off);
        for (int s = 0; s < Strips; s++) yield return McuEncoder.Ring(s, RingState.Off);
        for (int s = 0; s <= MasterFader; s++) yield return McuEncoder.Fader(s, 0);
        for (int s = 0; s < Strips; s++) { yield return McuEncoder.Meter(s, 0x0F); yield return McuEncoder.Meter(s, 0); }
        yield return McuEncoder.LcdText(0, new string(' ', LcdLineLength * 2));
        yield return McuEncoder.LcdColorsMessage(Enumerable.Repeat(LcdColor.Off, Strips).ToArray());
        for (int i = 0; i < SevenSegDigits; i++) yield return McuEncoder.SevenSegment(i, ' ');
    }
}
