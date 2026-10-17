namespace XTouch.Abstractions.Mcu;

/// <summary>Mackie Control (X-Touch "MC" mode) protocol numbers. See docs/xtouch-middleware/03-xtouch-protocol-reference.md.</summary>
public static class McuConstants
{
    public const byte DeviceIdMain = 0x14;
    public const byte DeviceIdExtender = 0x15;

    public const int Strips = 8;
    public const int MasterFader = 8;

    // ----- Notes (buttons / LEDs) -----
    public const int RecBase = 0x00;
    public const int SoloBase = 0x08;
    public const int MuteBase = 0x10;
    public const int SelectBase = 0x18;
    public const int VPotPushBase = 0x20;

    public const int AssignTrack = 0x28;
    public const int AssignSend = 0x29;
    public const int AssignPan = 0x2A;
    public const int AssignPlugin = 0x2B;
    public const int AssignEq = 0x2C;
    public const int AssignInstrument = 0x2D;

    public const int BankLeft = 0x2E;
    public const int BankRight = 0x2F;
    public const int ChannelLeft = 0x30;
    public const int ChannelRight = 0x31;
    public const int Flip = 0x32;
    public const int GlobalView = 0x33;
    public const int NameValue = 0x34;
    public const int SmpteBeats = 0x35;
    public const int F1 = 0x36; // F1..F8 = 0x36..0x3D

    public const int Shift = 0x46;
    public const int Option = 0x47;
    public const int Control = 0x48;
    public const int Alt = 0x49;

    public const int Save = 0x50;
    public const int Undo = 0x51;
    public const int Cancel = 0x52;
    public const int Enter = 0x53;
    public const int Marker = 0x54;
    public const int Nudge = 0x55;
    public const int Cycle = 0x56;
    public const int Drop = 0x57;
    public const int Replace = 0x58;
    public const int Click = 0x59;
    public const int GlobalSolo = 0x5A;

    public const int Rewind = 0x5B;
    public const int FastForward = 0x5C;
    public const int Stop = 0x5D;
    public const int Play = 0x5E;
    public const int Record = 0x5F;

    public const int CursorUp = 0x60;
    public const int CursorDown = 0x61;
    public const int CursorLeft = 0x62;
    public const int CursorRight = 0x63;
    public const int Zoom = 0x64;
    public const int Scrub = 0x65;

    public const int FaderTouchBase = 0x68; // 0x68..0x70 (master = 0x70)
    public const int SmpteLed = 0x71;
    public const int BeatsLed = 0x72;
    public const int RudeSoloLed = 0x73;
    public const int LastLedNote = 0x73;

    // ----- Control changes -----
    public const int VPotBase = 0x10;   // input, 0x10..0x17
    public const int RingBase = 0x30;   // output, 0x30..0x37
    public const int Jog = 0x3C;
    public const int SevenSegBase = 0x40; // 0x40..0x49 timecode (right to left), 0x4A..0x4B assignment (right, left)
    public const int SevenSegDigits = 12;

    // ----- SysEx -----
    public const byte LcdWrite = 0x12;
    public const byte LcdColors = 0x72;
    public const int LcdCellWidth = 7;
    public const int LcdLineLength = 56;

    // ----- Meters -----
    public const int MeterMax = 0x0C;
}
