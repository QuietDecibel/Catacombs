namespace XTouch.Abstractions.Mcu;

/// <summary>Readable names for MCU notes, used by profiles ("PAN", "F1", "PLAY", ...).</summary>
public static class ButtonNames
{
    private static readonly Dictionary<string, int> ByName = Build();
    private static readonly Dictionary<int, string> ByNote = ByName
        .GroupBy(kv => kv.Value).ToDictionary(g => g.Key, g => g.First().Key);

    public static bool TryGetNote(string name, out int note) => ByName.TryGetValue(name.Trim().ToUpperInvariant(), out note);

    public static string NameOf(int note) => ByNote.TryGetValue(note, out var n) ? n : $"NOTE_{note:X2}";

    public static IReadOnlyCollection<string> All => ByName.Keys;

    private static Dictionary<string, int> Build()
    {
        var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["TRACK"] = McuConstants.AssignTrack,
            ["SEND"] = McuConstants.AssignSend,
            ["PAN"] = McuConstants.AssignPan,
            ["PLUGIN"] = McuConstants.AssignPlugin,
            ["EQ"] = McuConstants.AssignEq,
            ["INST"] = McuConstants.AssignInstrument,
            ["BANK_LEFT"] = McuConstants.BankLeft,
            ["BANK_RIGHT"] = McuConstants.BankRight,
            ["CHANNEL_LEFT"] = McuConstants.ChannelLeft,
            ["CHANNEL_RIGHT"] = McuConstants.ChannelRight,
            ["FLIP"] = McuConstants.Flip,
            ["GLOBAL_VIEW"] = McuConstants.GlobalView,
            ["NAME_VALUE"] = McuConstants.NameValue,
            ["SMPTE_BEATS"] = McuConstants.SmpteBeats,
            ["MIDI_TRACKS"] = 0x3E,
            ["INPUTS"] = 0x3F,
            ["AUDIO_TRACKS"] = 0x40,
            ["AUDIO_INST"] = 0x41,
            ["AUX"] = 0x42,
            ["BUSES"] = 0x43,
            ["OUTPUTS"] = 0x44,
            ["USER"] = 0x45,
            ["SHIFT"] = McuConstants.Shift,
            ["OPTION"] = McuConstants.Option,
            ["CONTROL"] = McuConstants.Control,
            ["ALT"] = McuConstants.Alt,
            ["READ"] = 0x4A,
            ["WRITE"] = 0x4B,
            ["TRIM"] = 0x4C,
            ["TOUCH"] = 0x4D,
            ["LATCH"] = 0x4E,
            ["GROUP"] = 0x4F,
            ["SAVE"] = McuConstants.Save,
            ["UNDO"] = McuConstants.Undo,
            ["CANCEL"] = McuConstants.Cancel,
            ["ENTER"] = McuConstants.Enter,
            ["MARKER"] = McuConstants.Marker,
            ["NUDGE"] = McuConstants.Nudge,
            ["CYCLE"] = McuConstants.Cycle,
            ["DROP"] = McuConstants.Drop,
            ["REPLACE"] = McuConstants.Replace,
            ["CLICK"] = McuConstants.Click,
            ["SOLO"] = McuConstants.GlobalSolo,
            ["REWIND"] = McuConstants.Rewind,
            ["FAST_FORWARD"] = McuConstants.FastForward,
            ["STOP"] = McuConstants.Stop,
            ["PLAY"] = McuConstants.Play,
            ["RECORD"] = McuConstants.Record,
            ["UP"] = McuConstants.CursorUp,
            ["DOWN"] = McuConstants.CursorDown,
            ["LEFT"] = McuConstants.CursorLeft,
            ["RIGHT"] = McuConstants.CursorRight,
            ["ZOOM"] = McuConstants.Zoom,
            ["SCRUB"] = McuConstants.Scrub,
            ["USER_A"] = 0x66,
            ["USER_B"] = 0x67,
        };
        for (int i = 0; i < 8; i++) d[$"F{i + 1}"] = McuConstants.F1 + i;
        return d;
    }
}
