# 3. X-Touch Protocol Reference (MC / Mackie Control mode)

The X-Touch implements the **Mackie Control Universal (MCU)** protocol when
booted in **MC mode** (hold the mode-select button at power-up and choose *MC*,
interface *USB*). Everything below is plain MIDI on **channel 1** unless noted.

> **Verify on your unit.** The core MCU map is stable and widely documented.
> X-Touch–specific extensions (scribble-strip colors, Ethernet/Xctl) depend on
> firmware version. Use the MIDI trace (see [Testing](09-testing-and-simulation.md))
> to confirm before relying on them. Keep all numbers in one file
> (`McuConstants.cs`) so corrections are one-line changes.

Notation: `9n` = Note On on channel *n+1*, `Bn` = Control Change, `En` = Pitch Bend,
`Dn` = Channel Pressure. Hex unless stated.

## 3.1 Faders (motorized)

| Direction | Message | Meaning |
|-----------|---------|---------|
| Surface → host | `E0+s LSB MSB` | Fader *s* (0–7 strips, 8 = master) moved. 14-bit value 0–16383. |
| Host → surface | `E0+s LSB MSB` | Move motor to position. |
| Surface → host | `90 68+s 7F` / `90 68+s 00` | Fader *s* **touched** / **released** (notes 0x68–0x70). |

```csharp
// decode
int value14 = (msb << 7) | lsb;             // 0..16383
double normalized = value14 / 16383.0;
// encode
int v = (int)Math.Round(Math.Clamp(normalized, 0, 1) * 16383);
byte lsb = (byte)(v & 0x7F), msb = (byte)((v >> 7) & 0x7F);
```

**Motor rules (critical):**
1. Never send a position to a fader whose touch note is down.
2. On release, send the authoritative value once (the X-Touch may otherwise
   snap back to the last value it *received*).
3. Coalesce: if 20 updates arrive in 10 ms, send only the last.

## 3.2 Buttons and LEDs

Buttons send **Note On**, velocity `7F` on press and `00` on release (some
firmware sends Note Off on release — accept both).

LED feedback uses the same note number:

| Velocity sent by host | LED |
|-----------------------|-----|
| `00` | Off |
| `01` | Blinking |
| `7F` | On |

### Note map

| Note (hex) | MCU name | Notes |
|------------|----------|-------|
| 00–07 | REC/ARM strip 1–8 | |
| 08–0F | SOLO strip 1–8 | |
| 10–17 | MUTE strip 1–8 | |
| 18–1F | SELECT strip 1–8 | |
| 20–27 | V-Pot push strip 1–8 | Encoder click |
| 28 | Assign: Track | X-Touch label: TRACK |
| 29 | Assign: Send | SEND |
| 2A | Assign: Pan/Surround | PAN/SURROUND |
| 2B | Assign: Plug-in | PLUG-IN |
| 2C | Assign: EQ | EQ |
| 2D | Assign: Instrument | INST |
| 2E | Fader Bank ◄ | BANK ◄ |
| 2F | Fader Bank ► | BANK ► |
| 30 | Channel ◄ | CHANNEL ◄ |
| 31 | Channel ► | CHANNEL ► |
| 32 | Flip | FLIP |
| 33 | Global View | GLOBAL VIEW |
| 34 | Name/Value | DISPLAY NAME/VALUE |
| 35 | SMPTE/Beats | SMPTE/BEATS |
| 36–3D | F1–F8 | |
| 3E–45 | View: MIDI Tracks, Inputs, Audio Tracks, Audio Inst, Aux, Buses, Outputs, User | |
| 46 | Shift | |
| 47 | Option | |
| 48 | Control | |
| 49 | Alt / Cmd | |
| 4A | Automation: Read/Off | |
| 4B | Write | |
| 4C | Trim | |
| 4D | Touch | |
| 4E | Latch | |
| 4F | Group | |
| 50 | Save | |
| 51 | Undo | |
| 52 | Cancel | |
| 53 | Enter | |
| 54 | Marker | |
| 55 | Nudge | |
| 56 | Cycle | |
| 57 | Drop | |
| 58 | Replace | |
| 59 | Click | |
| 5A | Solo (global) | |
| 5B | Rewind | ◄◄ |
| 5C | Fast Forward | ►► |
| 5D | Stop | ■ |
| 5E | Play | ► |
| 5F | Record | ● |
| 60 | Cursor Up | |
| 61 | Cursor Down | |
| 62 | Cursor Left | |
| 63 | Cursor Right | |
| 64 | Zoom | Center of cursor pad |
| 65 | Scrub | |
| 66–67 | User Switch A/B | Footswitch jacks |
| 68–70 | Fader touch 1–8, Master | Input only |
| 71 | SMPTE LED | Output only |
| 72 | Beats LED | Output only |
| 73 | Rude Solo LED | Output only |

The X-Touch's silk-screen labels do not always match MCU names; the middleware
should treat note numbers as **control IDs** and let profiles assign meaning.

## 3.3 Rotary encoders (V-Pots)

**Input:** `B0 10+s vv` (CC 0x10–0x17) — relative:

| `vv` | Meaning |
|------|---------|
| `01`–`3F` | Clockwise, *vv* ticks |
| `41`–`7F` | Counter-clockwise, *vv & 0x3F* ticks |

```csharp
int delta = (vv & 0x40) != 0 ? -(vv & 0x3F) : (vv & 0x3F);
```

**Jog wheel:** `B0 3C vv`, same encoding.

**LED ring output:** `B0 30+s vv` (CC 0x30–0x37):

```
vv = 0 C M M P P P P
       │ └┬┘ └──┬──┘
       │  │     └── position 0..11 (0 = all ring LEDs off)
       │  └──────── mode: 0 = single dot, 1 = boost/cut (from center),
       │                  2 = wrap (fill from left), 3 = spread (width)
       └─────────── center LED on/off
```

```csharp
static byte EncodeRing(RingMode mode, int position, bool center) =>
    (byte)((center ? 0x40 : 0) | (((int)mode & 0x03) << 4) | Math.Clamp(position, 0, 11));

// Normalized value → ring position
static int RingPosition(double v, RingMode mode) => mode switch
{
    RingMode.Dot or RingMode.BoostCut => 1 + (int)Math.Round(v * 10), // 1..11
    RingMode.Wrap   => (int)Math.Round(v * 11),                      // 0..11
    RingMode.Spread => 1 + (int)Math.Round(v * 5),                   // 1..6
    _ => 0
};
```

## 3.4 Level meters

`D0 xy` (Channel Pressure, channel 1): **x** = strip 0–7 (high nibble),
**y** = level:

| y | Meaning |
|---|---------|
| 0–C | Level, 0 = off, C = top |
| E | Set overload (clip) LED |
| F | Clear overload LED |

Meters **decay on the device** (roughly 300 ms per step), so the host must
resend levels continuously (10–20 Hz) while audio is present.

```csharp
static byte[] Meter(int strip, int level) => [0xD0, (byte)((strip << 4) | Math.Clamp(level, 0, 0xC))];

// dBFS → MCU step (tune to taste)
static int DbToMeter(double db) => db switch
{
    >= 0   => 0xC, >= -2 => 0xB, >= -4 => 0xA, >= -6 => 0x9, >= -9 => 0x8,
    >= -12 => 0x7, >= -15 => 0x6, >= -20 => 0x5, >= -25 => 0x4,
    >= -30 => 0x3, >= -40 => 0x2, >= -50 => 0x1, _ => 0x0
};
```

## 3.5 Scribble strips (LCD)

Each strip has 2 lines × 7 characters. The whole LCD is one 112-character
buffer: **top line = offsets 0–55, bottom line = 56–111**.

```
F0 00 00 66 14 12 <offset> <ascii bytes...> F7
            │  │
            │  └── command 0x12 = LCD write
            └───── device ID: 0x14 = X-Touch / MCU, 0x15 = X-Touch Extender
```

```csharp
static byte[] LcdWrite(byte deviceId, int offset, string text)
{
    var ascii = text.Select(c => c is >= ' ' and <= '~' ? (byte)c : (byte)'?');
    return [0xF0, 0x00, 0x00, 0x66, deviceId, 0x12, (byte)offset, .. ascii, 0xF7];
}
// Strip s, top:    offset = s * 7
// Strip s, bottom: offset = 56 + s * 7
```

Always pad/truncate to exactly 7 chars per cell (`text.PadRight(7)[..7]`).
Write the whole 56-char line in one SysEx when several cells change — fewer
messages is faster than many small ones.

### Scribble-strip backlight color (X-Touch extension)

```
F0 00 00 66 14 72 c1 c2 c3 c4 c5 c6 c7 c8 F7
```

| c | Color | c | Color |
|---|-------|---|-------|
| 0 | Off (black) | 4 | Blue |
| 1 | Red | 5 | Magenta |
| 2 | Green | 6 | Cyan |
| 3 | Yellow | 7 | White |

All eight colors are sent at once. (Firmware-dependent — verify.) Some firmware
versions also accept bit 4/5 flags for inverted text; treat as optional.

## 3.6 7-segment display (timecode + assignment)

`B0 4x cc` where `x` selects the digit, **right to left**:

| CC | Digit |
|----|-------|
| 40–49 | Timecode digits 1 (rightmost) → 10 (leftmost) |
| 4A–4B | Assignment display right, left (the 2-digit "channel" display) |

Character code `cc` (6-bit, MCU 7-segment charset):

| Code | Characters |
|------|------------|
| 00–1F | `@ A B … Z [ \ ] ^ _` (ASCII 0x40–0x5F minus 0x40) |
| 20–3F | ASCII 0x20–0x3F (space, digits, punctuation) |
| +40 | Same character with the decimal point lit |

```csharp
static byte SevenSeg(char c, bool dot = false)
{
    c = char.ToUpperInvariant(c);
    int code = c switch
    {
        >= '@' and <= '_' => c - 0x40,
        >= ' ' and <= '?' => c,
        _ => ' '
    };
    return (byte)(code | (dot ? 0x40 : 0));
}
```

## 3.7 Device handshake (optional)

Genuine Mackie hardware performs a challenge/response on SysEx command `0x00`
(host query) → `0x01` (surface reply with serial + challenge) → `0x02` (host
response) → `0x03` (surface confirms). The X-Touch works **without** the
handshake; it is safe to ignore surface-originated `0x01`/`0x03` SysEx. A
useful "is anything there?" probe is:

```
F0 00 00 66 14 00 F7      // device query — expect an F0 00 00 66 14 01 ... reply
```

Use it as a liveness check, not a requirement.

## 3.8 Resetting the surface

On connect and on shutdown, send a "blank" state so the operator never sees
stale values:

1. All LED notes 0x00–0x73 → velocity 0.
2. All rings → 0.
3. All faders → 0 (or current mixer value on connect).
4. LCD: 112 spaces; colors all 0 (or 7 for white).
5. 7-segment: spaces.
6. Meters: `F` (clear overload) then `0`.

## 3.9 Other connection modes (future)

| Mode | Transport | Notes |
|------|-----------|-------|
| MC over 5-pin MIDI | DIN MIDI | Same protocol; needs a MIDI interface. ~3 KB/s bandwidth — throttling matters even more. |
| MC over Ethernet (RTP-MIDI) | AppleMIDI session | Use an RTP-MIDI driver (rtpMIDI on Windows) → appears as a normal MIDI port. |
| Xctl over Ethernet | UDP | Behringer's native protocol (used with X32). Different message set and a keep-alive requirement; document from captures before implementing. |
| HUI | MIDI | Different, older protocol; separate `ISurfaceProtocol` implementation. |

## 3.10 Bandwidth budget

USB MIDI on the X-Touch is fast, but the surface's internal processing is the
limit. Practical numbers that keep it responsive:

| Feedback type | Max rate per control | Notes |
|---------------|---------------------|-----|
| Fader motor | 50–100 Hz | Coalesced; only when not touched |
| LEDs / rings | On change | Coalesced per render tick |
| LCD | ≤ 20 Hz per line | Batch into full-line writes |
| Meters | 10–20 Hz | Continuous refresh while signal present |
| 7-segment | ≤ 20 Hz | Only changed digits |
