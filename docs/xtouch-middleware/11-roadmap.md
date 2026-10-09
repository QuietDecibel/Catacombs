# 11. Implementation Roadmap

Build in this order. Each milestone ends with something you can run and
demonstrate, and none needs the next one to be useful.

> **⚙ Reconcile** — Re-order to match your architecture's priorities, but keep
> M1–M3 first: everything else depends on a reliable surface layer.

| # | Milestone | Deliverables | Acceptance |
|---|-----------|--------------|------------|
| **M0** | Skeleton | Solution, projects, `Directory.Build.props`, CI build + test (GitHub Actions `dotnet test`) | `dotnet build` and `dotnet test` green in CI |
| **M1** | Protocol | `McuConstants`, `McuDecoder`, `McuEncoder` + data-driven tests | 100 % of chapter-3 tables covered by tests |
| **M2** | Talk to hardware | `DryWetMidiTransport`, console "echo" app: decoded events printed; fader move → LCD shows value | Real X-Touch: every control prints the right event; LCD/LED/ring/fader commands work; reconnect on replug |
| **M3** | Renderer | `SurfaceState`, `FeedbackRenderer` (diff, throttle, touch rule), `MeterRenderer`, surface reset | Unit tests from §9.3; self-test pattern runs on hardware |
| **M4** | Engine (no target) | `ControlEngine`, channel pipeline, banking, layers, `VirtualTransport`, an in-memory `LoopbackTarget` adapter | Faders/mutes/pans "stick" via loopback; banking repaints correctly |
| **M5** | Profiles | Object model, JSON loader, compiler, validator, hot reload | Edit JSON → surface changes without restart; invalid file rejected |
| **M6** | X32 adapter | OSC codec, `X32Adapter`, `FakeX32`, fader law, names/colors, `/xremote` keep-alive, meters | Bidirectional control with real console or X32-Edit emulator; integration tests green |
| **M7** | Service host | `Program.cs`, workers, health endpoint, Serilog, Windows Service/systemd support | Runs headless as an OS service; survives surface and target restarts |
| **M8** | API | SignalR hub, broadcaster, REST endpoints, token filter, `XTouch.Contracts` | Integration test with SignalR client; Postman/`curl` collection |
| **M9** | UI v1 | Dashboard, Surface mirror (interactive), Diagnostics (logs + MIDI trace), Settings | UI controls the mixer via virtual surface; reconnects when service restarts |
| **M10** | UI v2 | Mapping editor with Learn mode, Banks & layers screen | Build a new profile end-to-end without touching JSON |
| **M11** | Packaging | Installer, auto-start, file locations, upgrade path | Clean-machine install → working in < 5 minutes |
| **M12** | Hardening | Soak test, perf counters, hardware checklist (§9.5), docs update | All checklist items pass; 8-hour soak clean |

## Future work (post-v1)

- X-Touch Extender (second `IMidiTransport` + strip offset 8–15).
- Xctl over Ethernet transport (requires protocol capture and documentation first).
- Additional adapters: Wing, VoiceMeeter, OBS, DAW via virtual MIDI.
- Scripting: user macros in C# script or Lua, run inside the engine loop with a time budget.
- Snapshot/scene recall from the surface.
- Multi-surface sync (two X-Touches controlling the same mixer).

## Definition of done (every milestone)

- Code reviewed; analyzers and `TreatWarningsAsErrors` clean.
- Unit tests for new logic; integration test for new cross-component behavior.
- Docs in this folder updated if a contract, protocol detail or setting changed.
- No new blocking calls on the engine loop or MIDI callback (review checklist item).
