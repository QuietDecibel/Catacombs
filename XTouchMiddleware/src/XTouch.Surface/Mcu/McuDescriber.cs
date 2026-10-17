using XTouch.Abstractions;
using XTouch.Abstractions.Mcu;

namespace XTouch.Surface.Mcu;

/// <summary>Human-readable descriptions of MIDI messages for the MIDI trace.</summary>
public static class McuDescriber
{
    public static string DescribeInput(ReadOnlySpan<byte> m) => McuDecoder.Decode(m) switch
    {
        FaderMoved f => $"Fader {StripName(f.Strip)} = {f.Value:0.000}",
        FaderTouched t => $"Fader {StripName(t.Strip)} {(t.IsTouched ? "touched" : "released")}",
        ButtonChanged b => $"{ButtonLabel(b.Note)} {(b.IsPressed ? "pressed" : "released")}",
        EncoderTurned e => $"Encoder {e.Strip + 1} {e.Delta:+0;-0}",
        JogTurned j => $"Jog {j.Delta:+0;-0}",
        _ => "?",
    };

    public static string DescribeOutput(ReadOnlySpan<byte> m)
    {
        if (m.Length == 0) return "";
        int status = m[0] & 0xF0;
        return status switch
        {
            0xE0 when m.Length >= 3 => $"Motor {StripName(m[0] & 0x0F)} → {((m[2] << 7) | m[1]) / 16383.0:0.000}",
            0x90 when m.Length >= 3 => $"LED {ButtonLabel(m[1])} = {(LedState)m[2]}",
            0xB0 when m.Length >= 3 && m[1] is >= McuConstants.RingBase and < McuConstants.RingBase + 8 =>
                $"Ring {m[1] - McuConstants.RingBase + 1} mode={(m[2] >> 4) & 3} pos={m[2] & 0x0F}{((m[2] & 0x40) != 0 ? " +center" : "")}",
            0xB0 when m.Length >= 3 && m[1] is >= McuConstants.SevenSegBase and < McuConstants.SevenSegBase + 12 =>
                $"7-seg CC {m[1]:X2} = {m[2]:X2}",
            0xD0 when m.Length >= 2 => $"Meter {(m[1] >> 4) + 1} = {m[1] & 0x0F}",
            0xF0 when m.Length > 6 && m[5] == McuConstants.LcdWrite =>
                $"LCD @{m[6]} \"{System.Text.Encoding.ASCII.GetString(m[7..^1])}\"",
            0xF0 when m.Length > 6 && m[5] == McuConstants.LcdColors => "LCD colors " + string.Join(",", m[6..^1].ToArray()),
            _ => "",
        };
    }

    private static string StripName(int strip) => strip == McuConstants.MasterFader ? "Master" : (strip + 1).ToString();

    private static string ButtonLabel(int note) => note switch
    {
        < McuConstants.SoloBase => $"REC {note + 1}",
        < McuConstants.MuteBase => $"SOLO {note - McuConstants.SoloBase + 1}",
        < McuConstants.SelectBase => $"MUTE {note - McuConstants.MuteBase + 1}",
        < McuConstants.VPotPushBase => $"SELECT {note - McuConstants.SelectBase + 1}",
        < McuConstants.AssignTrack => $"VPOT {note - McuConstants.VPotPushBase + 1}",
        _ => ButtonNames.NameOf(note),
    };
}
