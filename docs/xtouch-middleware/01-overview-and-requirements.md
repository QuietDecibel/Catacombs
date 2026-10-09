# 1. Overview & Requirements

## 1.1 Purpose

The Behringer X-Touch is a "dumb" control surface: it reports what the operator
touches and displays whatever the host tells it to. It has no knowledge of the
mixer it is controlling. The **middleware** supplies that knowledge:

1. **Input translation** – turns surface gestures (fader moves, button presses,
   encoder turns) into commands for a target (e.g. *set channel 5 fader to −10 dB*).
2. **Feedback** – mirrors the target's state back onto the surface (motor fader
   positions, button LEDs, encoder rings, scribble-strip names/colors, meters,
   7-segment display).
3. **Mapping & banking** – lets the user decide which target parameter each
   control drives, and page through more channels than the 8 physical strips.
4. **Management UI** – a desktop app to configure mappings, watch live state and
   diagnose problems.

## 1.2 Hardware summary (X-Touch, full size)

| Element | Count | Notes |
|---------|-------|-------|
| Motorized touch-sensitive faders (100 mm) | 9 | 8 channel + 1 master |
| Rotary encoders with LED ring + push | 8 | Endless, relative output |
| Scribble strips (LCD, RGB backlight) | 8 | 2 lines × 7 characters each |
| Per-strip buttons | 4 × 8 | REC, SOLO, MUTE, SELECT (all illuminated) |
| Level meters | 8 | LED ladder, driven by host |
| Function / assign / transport / navigation buttons | ~60 | Illuminated |
| Jog/shuttle wheel | 1 | Relative output |
| 7-segment display | 12 digits | Timecode + 2-digit assignment display |
| Connections | USB (class-compliant MIDI), 5-pin MIDI, Ethernet (RTP-MIDI / Xctl) | |
| Modes | MC (Mackie Control), HUI, MIDI, Xctl | Selected on the unit at power-up |

The **X-Touch Extender** (8 strips, no master/transport) uses the same protocol
with a different SysEx device ID and can be added as a second surface.

The **X-Touch Compact / Mini** use a different (plain MIDI CC/note) layout and are
out of scope for v1 but fit the same adapter model (see §2.5).

## 1.3 Scope

**In scope (v1)**
- One X-Touch in **MC mode over USB MIDI**.
- One or more target adapters; reference adapter for **Behringer X32/M32 (OSC)**.
- 8-strip banking across N target channels; a master fader.
- Full feedback: faders, LEDs, rings, scribble text + color, meters, display.
- Desktop UI: device status, mapping editor, live surface mirror, logs.
- Runs as a background service with auto-reconnect to both surface and target.

**Out of scope (v1, candidates for later)**
- HUI mode, Xctl-over-Ethernet mode, multiple extenders.
- Cloud/remote access; multi-user editing.
- Scripting engine for user macros (designed for, not built — see roadmap).

## 1.4 Functional requirements

| ID | Requirement |
|----|-------------|
| FR-1 | Detect the X-Touch MIDI ports by name; reconnect automatically on unplug/replug. |
| FR-2 | Decode every MC-mode input (faders, touch, buttons, encoders, encoder push, jog) into typed events. |
| FR-3 | Encode all feedback (fader, LED on/off/blink, ring, LCD text, LCD color, meters, 7-segment). |
| FR-4 | While a fader is **touched**, never drive its motor; on release, send the authoritative position. |
| FR-5 | Map any surface control to any target parameter via a declarative profile (JSON). |
| FR-6 | Support banks (page by 8 / shift by 1) and layers (e.g. encoder controls pan vs. gain vs. sends). |
| FR-7 | Reflect target-side changes (made on the mixer or another app) on the surface within 50 ms. |
| FR-8 | On (re)connection, fully repaint the surface from current state. |
| FR-9 | Expose live state, connection status, logs and profile CRUD to the UI over a local API. |
| FR-10 | Hot-reload profiles without restarting the service. |

## 1.5 Non-functional requirements

| ID | Requirement |
|----|-------------|
| NFR-1 | **Latency**: surface input → target command ≤ 5 ms in-process (p99). |
| NFR-2 | **Throughput**: handle 9 faders moving simultaneously without dropping the final value (coalesce, never queue unboundedly). |
| NFR-3 | **No feedback loops**: echoes from the target must not fight the operator's hand. |
| NFR-4 | **Resilience**: a crashed/disconnected target or surface never crashes the service. |
| NFR-5 | **Footprint**: < 100 MB RAM, < 2 % CPU idle on a typical laptop. |
| NFR-6 | **Security**: API bound to `localhost` by default; optional token if exposed on LAN. |
| NFR-7 | **Observability**: structured logs, MIDI trace toggle, health endpoint. |
| NFR-8 | **Testability**: core logic runs without hardware (virtual surface + fake target). |

## 1.6 Glossary

| Term | Meaning |
|------|---------|
| **Surface** | The X-Touch hardware (or a virtual stand-in). |
| **Target** | The device/software being controlled (mixer, DAW…). |
| **Adapter** | C# component that speaks one target's protocol. |
| **Strip** | One vertical channel column on the surface (0–7), plus master (8). |
| **Bank** | Offset that maps the 8 physical strips to target channels. |
| **Layer** | Mode that changes what a control does (e.g. encoder = Pan / Gain / Send 1). |
| **Profile** | A saved set of mappings, banks and layers (JSON). |
| **Feedback** | Messages from middleware to surface that update motors/LEDs/displays. |
| **MCU** | Mackie Control Universal protocol, the X-Touch "MC" mode. |
