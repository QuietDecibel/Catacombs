# 10. Deployment & Operations

## 10.1 Build & publish

```bash
# Service: self-contained, single file, per OS
dotnet publish src/XTouch.Service -c Release -r win-x64   --self-contained -p:PublishSingleFile=true -o out/service/win-x64
dotnet publish src/XTouch.Service -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true -o out/service/osx-arm64
dotnet publish src/XTouch.Service -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o out/service/linux-x64

# UI
dotnet publish src/XTouch.UI -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o out/ui/win-x64
```

DryWetMIDI ships native helper libraries for device I/O; verify they are
present next to the single-file executable (set
`IncludeNativeLibrariesForSelfExtract=true` if needed) and test on a clean machine.

## 10.2 Install as an OS service

### Windows

```powershell
sc.exe create "XTouchMiddleware" binPath= "C:\Program Files\XTouchMiddleware\XTouch.Service.exe" start= auto
sc.exe failure "XTouchMiddleware" reset= 86400 actions= restart/5000/restart/5000/restart/30000
sc.exe start "XTouchMiddleware"
```

> **Windows MIDI caveat:** some USB MIDI drivers are not accessible from
> session 0 (where services run). If the service cannot see the X-Touch port
> but the console app can, run the service as a **per-user startup app**
> (Task Scheduler "At log on", or a Startup shortcut with `--minimized`) instead
> of a true Windows Service. Decide this early on your target machines.

An installer (WiX v5 or Inno Setup) should: copy files, register the service
or startup task, add a firewall rule only if the API is exposed beyond
localhost, and create Start-menu shortcuts for the UI.

### Linux (systemd)

```ini
# /etc/systemd/system/xtouch-middleware.service
[Unit]
Description=X-Touch Middleware
After=network-online.target sound.target

[Service]
Type=notify
ExecStart=/opt/xtouch-middleware/XTouch.Service
WorkingDirectory=/opt/xtouch-middleware
User=xtouch
SupplementaryGroups=audio
Restart=always
RestartSec=3

[Install]
WantedBy=multi-user.target
```

> DryWetMIDI's device API targets Windows and macOS. For Linux, implement
> `IMidiTransport` over ALSA (e.g. via a small P/Invoke wrapper or a library
> such as RtMidi bindings). The rest of the system is unchanged.

### macOS (launchd)

Install a `~/Library/LaunchAgents/com.yourorg.xtouch.plist` LaunchAgent (user
session — required for CoreMIDI access) with `KeepAlive=true`.

## 10.3 File locations

| Item | Windows | macOS / Linux |
|------|---------|---------------|
| Binaries | `%ProgramFiles%\XTouchMiddleware` | `/Applications/…` / `/opt/xtouch-middleware` |
| Settings + profiles | `%ProgramData%\XTouchMiddleware` | `~/Library/Application Support/XTouchMiddleware` / `/etc/xtouch-middleware` |
| Logs | `%ProgramData%\XTouchMiddleware\logs` | `~/Library/Logs/XTouchMiddleware` / `/var/log/xtouch-middleware` |

Keep settings/profiles outside the install folder so upgrades never overwrite them.

## 10.4 Logging

| Category | Default level | Content |
|----------|---------------|---------|
| `XTouch.Surface` | Information | Connect/disconnect, port names |
| `XTouch.Surface.Trace` | Warning (off) | Every MIDI message (only when enabled) |
| `XTouch.Core` | Information | Profile load, bank/layer changes, engine errors |
| `XTouch.Targets.*` | Information | Target connect/disconnect, protocol errors |
| `Microsoft.AspNetCore` | Warning | |

Rolling daily files, keep 14 days. The UI's Diagnostics screen streams the
same entries live.

## 10.5 Troubleshooting guide

| Symptom | Likely cause | Fix |
|---------|-------------|-----|
| Surface never connects | Wrong mode on X-Touch, port held by DAW, port name mismatch | Boot X-Touch in **MC / USB**; close other apps; pick port in Settings |
| Faders jitter or fight the hand | Touch rule bypassed, or target echoes at a different resolution | Check renderer skips touched faders; widen `Near()` tolerance |
| Fader snaps back on release | No final value sent after release | Renderer must force-send on touch release |
| LCD shows garbage | Non-ASCII chars, wrong device ID | Sanitize text; use 0x14 for X-Touch, 0x15 for Extender |
| Scribble strips stay white | Firmware lacks color SysEx | Update X-Touch firmware; confirm with MIDI trace |
| Meters flicker to zero | Refresh rate too low | Send meters at ≥ 10 Hz |
| Mixer changes not shown | `/xremote` not renewed / firewall | Keep-alive < 10 s; allow UDP 10023 |
| UI says "service offline" | Service stopped or different port/token | Check `/health` in browser; match `Api:Url` |

## 10.6 Upgrades

1. Stop service (surface blanks).
2. Replace binaries; never touch the data folder.
3. Run profile migrations if `profile.schemaVersion` increased (keep a
   `ProfileMigrator` with one step per version).
4. Start service; UI checks `apiVersion` compatibility on reconnect.
