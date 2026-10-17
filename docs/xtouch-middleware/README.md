# X-Touch Middleware — C# Developer Documentation

This folder documents how to build a **middleware service** and a **desktop UI**
in C# (.NET) that sit between a **Behringer X-Touch** control surface and one or
more audio targets (a digital mixer, a DAW, a software mixer, etc.).

```
┌──────────────┐  USB/Ethernet MIDI  ┌──────────────────────┐  OSC / MIDI / API  ┌──────────────────┐
│ Behringer    │ ◄─────────────────► │  XTouch Middleware   │ ◄────────────────► │ Target(s): X32,  │
│ X-Touch      │   (Mackie Control)  │  Service (.NET)      │                    │ DAW, VoiceMeeter │
└──────────────┘                     └──────────┬───────────┘                    └──────────────────┘
                                                │ SignalR + REST (localhost)
                                     ┌──────────▼───────────┐
                                     │  Config / Monitor UI │
                                     │  (Avalonia, .NET)    │
                                     └──────────────────────┘
```

> **Your existing architecture.** These documents were written without access to
> your architecture files (they are not in this repository). Where your design
> differs, treat your design as authoritative and use these pages as the
> implementation guide for the parts they cover. Sections that are most likely
> to need reconciling are marked **⚙ Reconcile**.

## Implementation

A working implementation of these documents lives in
[`XTouchMiddleware/`](../../XTouchMiddleware/README.md): the service, the X32
adapter (with a built-in X32 emulator for running without hardware), the
Avalonia UI and the tests. Where the code and these pages differ in detail, the
code and its README are current.

## Reading order

| # | Document | What it covers |
|---|----------|----------------|
| 1 | [Overview & Requirements](01-overview-and-requirements.md) | Goals, scope, functional and non-functional requirements |
| 2 | [Architecture](02-architecture.md) | Layers, solution layout, data flow, threading model |
| 3 | [X-Touch Protocol Reference](03-xtouch-protocol-reference.md) | Mackie Control (MCU) MIDI map for every control and display on the X-Touch |
| 4 | [Service Implementation](04-service-implementation.md) | Step-by-step C# for the hosted service: transport, decoder, engine, feedback renderer |
| 5 | [Target Adapters](05-target-adapters.md) | Plug-in interface for mixers/DAWs, with a Behringer X32/M32 OSC example |
| 6 | [Service API (IPC)](06-service-api.md) | SignalR hub + REST contract used by the UI |
| 7 | [UI Application](07-ui-application.md) | Avalonia MVVM desktop app: screens, view models, live surface mirror |
| 8 | [Configuration & Mapping](08-configuration-and-mapping.md) | Profiles, layers, bank layouts, JSON schema |
| 9 | [Testing & Simulation](09-testing-and-simulation.md) | Unit tests, virtual X-Touch, loopback, hardware test checklist |
| 10 | [Deployment & Operations](10-deployment.md) | Windows Service / systemd, logging, upgrades, troubleshooting |
| 11 | [Implementation Roadmap](11-roadmap.md) | Milestones in build order with acceptance criteria |

## Technology choices at a glance

| Concern | Choice | Why |
|---------|--------|-----|
| Runtime | .NET 10 (LTS) | Long-term support, cross-platform, `System.Threading.Channels`, AOT-friendly |
| Service host | ASP.NET Core Generic Host (`WebApplication`) | One host for background workers, SignalR, REST, DI, logging, config |
| Run as OS service | `Microsoft.Extensions.Hosting.WindowsServices` / `.Systemd` | Same binary runs as a console app, Windows Service or systemd unit |
| MIDI I/O | [DryWetMIDI](https://github.com/melanchall/drywetmidi) (`Melanchall.DryWetMidi`) | Mature, cross-platform (Windows/macOS) device I/O, SysEx support |
| Target protocol | OSC over UDP (adapter-specific) | Native protocol of X32/M32/Wing and many software mixers |
| UI ↔ service | SignalR (real-time) + minimal REST (config) | Push-based state updates, auto-reconnect, typed client |
| UI framework | Avalonia UI 11 + CommunityToolkit.Mvvm | Cross-platform XAML desktop, MVVM source generators |
| Logging | `Microsoft.Extensions.Logging` + Serilog sinks | Structured logs to file + console + UI live log |
| Tests | xUnit + FluentAssertions + NSubstitute | Standard .NET stack |

If your architecture is Windows-only, WPF or WinUI 3 can replace Avalonia and the
Windows MIDI Services API can replace DryWetMIDI; the layering in these docs is
unchanged.
