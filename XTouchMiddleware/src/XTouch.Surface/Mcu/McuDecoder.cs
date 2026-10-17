using XTouch.Abstractions;
using static XTouch.Abstractions.Mcu.McuConstants;

namespace XTouch.Surface.Mcu;

/// <summary>Turns raw MC-mode MIDI messages into <see cref="SurfaceEvent"/>s. Pure and stateless.</summary>
public static class McuDecoder
{
    public static SurfaceEvent Decode(ReadOnlySpan<byte> m)
    {
        if (m.Length == 0) return new UnknownMessage([]);
        int status = m[0] & 0xF0, channel = m[0] & 0x0F;

        switch (status)
        {
            case 0xE0 when m.Length >= 3 && channel <= MasterFader:
                return new FaderMoved(channel, ((m[2] << 7) | m[1]) / 16383.0);

            case 0x90 when m.Length >= 3:
            case 0x80 when m.Length >= 3:
            {
                int note = m[1];
                bool down = status == 0x90 && m[2] > 0;
                if (note is >= FaderTouchBase and <= FaderTouchBase + MasterFader)
                    return new FaderTouched(note - FaderTouchBase, down);
                return new ButtonChanged(note, down);
            }

            case 0xB0 when m.Length >= 3:
            {
                int cc = m[1], v = m[2];
                int delta = (v & 0x40) != 0 ? -(v & 0x3F) : (v & 0x3F);
                if (cc is >= VPotBase and < VPotBase + Strips) return new EncoderTurned(cc - VPotBase, delta);
                if (cc == Jog) return new JogTurned(delta);
                break;
            }
        }
        return new UnknownMessage(m.ToArray());
    }
}
