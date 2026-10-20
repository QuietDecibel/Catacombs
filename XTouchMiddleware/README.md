# X-Touch Middleware

A background service and desktop app that connect a **Behringer X-Touch**
control surface (Mackie Control "MC" mode) to an audio target. The first target
is a **Behringer X32/M32** over OSC. The design is described in
[`docs/xtouch-middleware`](../docs/xtouch-middleware/README.md).

```
X-Touch ⇄ (USB MIDI) ⇄ XTouch.Service ⇄ (OSC/UDP) ⇄ X32 / M32
                              ⇅ SignalR + REST (localhost:5280)
                          XTouch.UI (Avalonia desktop app)
```

What works:

- **Faders** move the mapped channel. Motor faders follow changes made on the mixer, and never fight your hand while you touch them.
- **Mute, solo, rec and select buttons** work on every strip, with LED feedback.
- **Encoders** control pan, trim or sends 1–4, chosen with the PAN / TRACK / SEND / PLUG-IN / EQ / INST buttons. The LED rings and the push-to-reset work.
- **Scribble strips** show the mixer's channel names and colors, plus the fader or encoder value.
- **Meters, banking and the master fader** work: 56 channels in banks of 8 (BANK, CHANNEL and the jog wheel), meters from the console, and the master fader.
- **F1–F6** toggle the X32 mute groups.
- **Displays:** the 7-segment display shows the encoder layer and the visible channel range.
- **Profiles** are JSON mappings with validation. Edits apply immediately, with no restart.
- **Reconnects** happen automatically when the X-Touch is replugged or the console reboots.
- **Desktop UI:** a live on-screen copy of the surface that also works as a virtual X-Touch, a profile editor, the service log, MIDI trace and connection settings.

## Quick start without hardware (demo mode)

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
# Terminal 1 – service with a virtual surface and the built-in X32 emulator
cd XTouchMiddleware
DOTNET_ENVIRONMENT=Demo dotnet run --project src/XTouch.Service
#   PowerShell:  $env:DOTNET_ENVIRONMENT="Demo"; dotnet run --project src/XTouch.Service

# Terminal 2 – desktop UI
dotnet run --project src/XTouch.UI
```

The **Surface** tab shows 8 strips named Kick, Snare, HiHat… with moving
meters. In demo mode the emulator stands in for the console:

- Drag a fader and the value round-trips through the emulator.
- Drag or scroll an encoder to turn it.
- Click MUTE, BANK ►, the layer buttons and the other surface buttons as you would on the hardware.

## Desktop shortcut (Windows)

From the `XTouchMiddleware` folder, in PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Install-DesktopShortcut.ps1
```

This publishes the service and UI to `%LOCALAPPDATA%\Programs\XTouchMiddleware`
and puts an **X-Touch Middleware** shortcut with the golden-yellow icon on your
desktop. The UI starts the service automatically when it isn't already running
(turn this off with `"AutoStartService": false` in the UI's `ui.json`). Run the
script again after pulling updates; your settings and profiles are kept.
`-SkipPublish` only recreates the shortcut.

## Running with an X-Touch and an X32

1. Boot the X-Touch in **MC mode, USB interface**. Open its setup menu at power-up, choose **MC** and **USB**, then confirm. The X-Touch quick-start guide shows the button to hold; it is usually channel 1 SELECT.
2. Create `appsettings.Local.json` next to the service executable (`%LOCALAPPDATA%\Programs\XTouchMiddleware\service` if you used the shortcut script):
   ```json
   {
     "Surface": { "Transport": "Midi", "InputPortName": "X-TOUCH", "OutputPortName": "X-TOUCH" },
     "Targets": { "X32": { "Host": "192.168.1.50" } }
   }
   ```
   `InputPortName` and `OutputPortName` are case-insensitive substrings of the
   MIDI port names. The UI's **Settings** tab lists the ports the service can see.
3. Start the service, then the UI.

Notes:
- MIDI device I/O uses [DryWetMIDI](https://github.com/melanchall/drywetmidi), which supports **Windows and macOS**. On Linux, run with `"Transport": "Virtual"` (UI surface only) or add an ALSA transport. See [doc 10](../docs/xtouch-middleware/10-deployment.md).
- On Windows only one application can open a MIDI port at a time. Close DAWs or other tools that hold the X-Touch port.
- The X32 needs nothing enabled. The service talks to UDP port 10023 and renews its `/xremote` subscription every 3 s.

## Configuration

The service reads its settings in this order, later sources overriding earlier ones:
1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. `appsettings.Local.json`
4. Environment variables (`XTOUCH_Section__Key`)
5. Command line (`--Section:Key=value`)

| Setting | Default | Meaning |
|---------|---------|---------|
| `Surface:Transport` | `Midi` | `Midi` for a real X-Touch, `Virtual` for no hardware |
| `Surface:InputPortName` / `OutputPortName` | `X-TOUCH` | Port name substrings |
| `Surface:BlankOnShutdown` | `true` | Clear the surface when the service stops |
| `Targets:X32:Host` / `Port` | — / `10023` | Console address. Leave `Host` empty to disable |
| `Targets:X32:Emulator` | `false` | Run the built-in X32 emulator and connect to it |
| `Profiles:Directory` | `profiles` | Folder of profile JSON files, written on first run |
| `Profiles:Active` | `x32-default` | Profile used on first start. After that, the last one activated is used |
| `Api:Url` | `http://127.0.0.1:5280` | API address. A non-loopback address requires `Api:Token` |
| `Api:Token` | — | Bearer token for `/api` and `/hub` |

Relative paths resolve next to the executable. Logs go to `logs/xtouch-YYYYMMDD.log`.

## Profiles

Profiles map surface controls to target parameters. Edit them in the UI's
**Profiles** tab or in the JSON files directly; changes to the active profile
apply immediately. Invalid files are rejected and the running profile stays.
The format is documented in
[doc 08](../docs/xtouch-middleware/08-configuration-and-mapping.md).

## API

| Endpoint | Purpose |
|----------|---------|
| `GET /health` | Surface, target and profile status |
| `GET /api/status`, `GET /api/surface` | Engine status, full surface snapshot |
| `GET/PUT/DELETE /api/profiles/{id}`, `POST /api/profiles/{id}/activate`, `POST /api/profiles/validate` | Profile management |
| `GET /api/midi/ports`, `GET /api/targets`, `GET /api/targets/{id}/catalog`, `GET /api/reference` | Discovery |
| `POST /api/surface/selftest` | Light every LED and sweep every fader |
| SignalR `/hub` | Live `Status`, `Surface`, `Logs` and `Trace` pushes; commands such as `MoveFader`, `PressButton`, `TurnEncoder`, `ShiftBank`, `SetLayer` and `SetTrace` |

Contracts are in `src/XTouch.Contracts`.

## Projects

| Project | Contents |
|---------|----------|
| `XTouch.Abstractions` | Surface events, feedback state, MCU constants, target adapter contract |
| `XTouch.Surface` | MCU decoder and encoder, DryWetMIDI, virtual and tracing transports, feedback renderer |
| `XTouch.Core` | Control engine, profiles (model, compiler, store), value converters, loopback target |
| `XTouch.Targets.X32` | OSC codec, X32 adapter, X32 emulator |
| `XTouch.Contracts` | DTOs shared by the service and the UI |
| `XTouch.Service` | ASP.NET Core host: workers, SignalR hub, REST, logging |
| `XTouch.UI` | Avalonia desktop app |
| `tests/*` | Unit, adapter, integration (full service in-process) and headless UI tests |

## Tests

```bash
dotnet test XTouchMiddleware.slnx
```

The integration tests run the whole service with a virtual surface and the X32
emulator. The UI tests render the window headlessly. Set
`XTOUCH_SCREENSHOT_DIR` to keep the PNGs, or `XTOUCH_SERVICE_URL` to also run
the UI against a live service.

## Publishing and installing

```bash
dotnet publish src/XTouch.Service -c Release -r win-x64 --self-contained -o out/service
dotnet publish src/XTouch.UI      -c Release -r win-x64 --self-contained -o out/ui
```

Use a folder publish (not single-file) for the service, so DryWetMIDI's native
library sits next to the executable. To install the service on Windows:

```powershell
sc.exe create "XTouchMiddleware" binPath= "C:\Program Files\XTouchMiddleware\XTouch.Service.exe" start= auto
sc.exe start "XTouchMiddleware"
```

If the X-Touch's MIDI port is not visible from a Windows Service (session 0),
run `XTouch.Service.exe` as a per-user startup task instead. See
[doc 10](../docs/xtouch-middleware/10-deployment.md) for systemd, launchd and
troubleshooting.

## Known limitations

- The real X-Touch and X32 have not been tested yet. Everything was verified against the virtual surface and the X32 emulator. Run the hardware checklist in [doc 09](../docs/xtouch-middleware/09-testing-and-simulation.md#95-hardware-acceptance-checklist) before relying on it.
- Two X-Touch details depend on the firmware: the scribble-strip color SysEx (`0x72`) and the meter step scale. Confirm them with the MIDI trace on the Diagnostics tab.
- X32 meter indexes in the default profile assume `/meters/1` order: 32 inputs, then aux, then buses at 48+. Verify them against your console firmware.
- Not supported yet:
  - HUI mode, the Xctl/Ethernet mode, and the X-Touch Extender
  - a form-based mapping editor (profiles are edited as JSON with server-side validation)
  - a tray icon
