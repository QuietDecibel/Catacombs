# 6. Service API (UI ↔ Service)

The UI is a separate process. It talks to the service through:

- **SignalR hub** at `/hub` — real-time state push + commands.
- **REST** under `/api` — profiles, settings, diagnostics (request/response).

All DTOs live in `XTouch.Contracts`, referenced by both the service and the UI.

## 6.1 Contracts

```csharp
namespace XTouch.Contracts;

public sealed record StatusDto(bool SurfaceConnected, string? SurfacePort,
                               IReadOnlyList<TargetStatusDto> Targets, string ActiveProfile, int Bank, string Layer);
public sealed record TargetStatusDto(string Id, string DisplayName, bool Connected);

public sealed record StripDto(int Index, double Fader, bool Touched, string Top, string Bottom,
                              string Color, int RingPosition, string RingMode,
                              bool Rec, bool Solo, bool Mute, bool Select, int Meter);

public sealed record SurfaceDto(IReadOnlyList<StripDto> Strips, double Master, string Display,
                                IReadOnlyDictionary<int, string> Leds);   // note → "On"/"Off"/"Blink"

public sealed record LogEntryDto(DateTimeOffset Time, string Level, string Category, string Message);
public sealed record MidiTraceDto(DateTimeOffset Time, string Direction, string Hex, string Decoded);

public sealed record ProfileSummaryDto(string Id, string Name, DateTimeOffset Modified);
```

## 6.2 SignalR hub

### Server → client (push)

```csharp
public interface ISurfaceClient
{
    Task Status(StatusDto status);              // on any connection/bank/layer/profile change
    Task Surface(SurfaceDto surface);           // throttled to 30 Hz; full snapshot (small: ~2 KB)
    Task Log(LogEntryDto entry);                // only to clients in group "logs"
    Task MidiTrace(MidiTraceDto entry);         // only to clients in group "trace"
}
```

### Client → server (invoke)

```csharp
public sealed class SurfaceHub(Channel<EngineInput> inputs, IStateCache cache) : Hub<ISurfaceClient>
{
    public StatusDto GetStatus() => cache.Status;
    public SurfaceDto GetSurface() => cache.Surface;

    public void SetBank(int bank)            => inputs.Writer.TryWrite(new UiCommand(new SetBankCommand(bank)));
    public void SetLayer(string layer)       => inputs.Writer.TryWrite(new UiCommand(new SetLayerCommand(layer)));
    public void ActivateProfile(string id)   => inputs.Writer.TryWrite(new UiCommand(new LoadProfileCommand(id)));

    // Virtual surface: lets the UI act as an on-screen X-Touch (great for testing without hardware)
    public void PressButton(int note, bool down) => inputs.Writer.TryWrite(new FromSurface(new ButtonChanged(note, down)));
    public void MoveFader(int strip, double v)   => inputs.Writer.TryWrite(new FromSurface(new FaderMoved(strip, v)));
    public void TurnEncoder(int strip, int d)    => inputs.Writer.TryWrite(new FromSurface(new EncoderTurned(strip, d)));

    public Task SubscribeLogs(bool on)  => on ? Groups.AddToGroupAsync(Context.ConnectionId, "logs")
                                              : Groups.RemoveFromGroupAsync(Context.ConnectionId, "logs");
    public Task SubscribeTrace(bool on) => on ? Groups.AddToGroupAsync(Context.ConnectionId, "trace")
                                              : Groups.RemoveFromGroupAsync(Context.ConnectionId, "trace");
}
```

### Broadcaster (engine → hub)

The engine calls `IStateBroadcaster.Publish` after every batch. The hub
broadcaster stores the latest snapshot and a timer pushes it at most 30×/s,
so a fast fader move never floods the UI.

```csharp
public sealed class HubStateBroadcaster(IHubContext<SurfaceHub, ISurfaceClient> hub)
    : IStateBroadcaster, IStateCache, IHostedService
{
    private SurfaceDto? _pending;
    private Timer? _timer;
    public StatusDto Status { get; private set; } = StatusDto.Empty;
    public SurfaceDto Surface { get; private set; } = SurfaceDto.Empty;

    public void Publish(SurfaceSnapshot s, MixerSnapshot m, int bank, string layer)
    {
        var dto = DtoMapper.ToDto(s);
        Surface = dto;
        Volatile.Write(ref _pending, dto);
        // Status changes (bank/layer/connection) are rare: push immediately when changed.
    }

    public Task StartAsync(CancellationToken _)
    {
        _timer = new Timer(_ =>
        {
            var dto = Interlocked.Exchange(ref _pending, null);
            if (dto is not null) _ = hub.Clients.All.Surface(dto);
        }, null, 0, 33);
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken _) { _timer?.Dispose(); return Task.CompletedTask; }
}
```

Register it once as a singleton and expose it through all three interfaces:

```csharp
builder.Services.AddSingleton<HubStateBroadcaster>();
builder.Services.AddSingleton<IStateBroadcaster>(sp => sp.GetRequiredService<HubStateBroadcaster>());
builder.Services.AddSingleton<IStateCache>(sp => sp.GetRequiredService<HubStateBroadcaster>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<HubStateBroadcaster>());
```

## 6.3 REST endpoints

| Method | Path | Body / Result | Purpose |
|--------|------|---------------|---------|
| GET | `/health` | `{ surface, targets }` | Liveness for monitoring |
| GET | `/api/status` | `StatusDto` | Same as hub `GetStatus` |
| GET | `/api/midi/ports` | `{ inputs: [], outputs: [] }` | Port picker in settings |
| GET | `/api/profiles` | `ProfileSummaryDto[]` | List profiles |
| GET | `/api/profiles/{id}` | Profile JSON | Load for editing |
| PUT | `/api/profiles/{id}` | Profile JSON → 204 / 400 with validation errors | Save (hot-reloads if active) |
| DELETE | `/api/profiles/{id}` | 204 | Delete (not the active one → 409) |
| POST | `/api/profiles/{id}/activate` | 202 | Switch profile |
| GET | `/api/targets/{id}/catalog` | `TargetCatalog` | Parameters for the mapping editor |
| GET | `/api/settings` / PUT | Settings JSON | Port names, target hosts, API options |
| POST | `/api/surface/test` | 202 | Run the LED/fader self-test pattern |

```csharp
public static class ApiEndpoints
{
    public static void MapApiEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api").AddEndpointFilter<TokenFilter>();

        api.MapGet("/status", (IStateCache c) => c.Status);
        api.MapGet("/profiles", (IProfileStore s) => s.List());
        api.MapGet("/profiles/{id}", (string id, IProfileStore s) =>
            s.Get(id) is { } p ? Results.Ok(p) : Results.NotFound());
        api.MapPut("/profiles/{id}", (string id, Profile p, IProfileStore s, ProfileValidator v) =>
        {
            var errors = v.Validate(p);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            s.Save(id, p);
            return Results.NoContent();
        });
        api.MapPost("/profiles/{id}/activate", (string id, Channel<EngineInput> ch) =>
        {
            ch.Writer.TryWrite(new UiCommand(new LoadProfileCommand(id)));
            return Results.Accepted();
        });
        // ...
    }
}
```

## 6.4 Security

- Default bind: `127.0.0.1` only. Nothing is reachable from the network.
- If `Api:Url` binds to a LAN address, require `Api:Token`: `TokenFilter`
  checks an `Authorization: Bearer <token>` header (REST) and the
  `access_token` query string (SignalR). Reject startup if bound to a non-loopback
  address without a token.
- Validate every profile on save (schema + references to real catalog entries)
  so a bad file can't crash the engine.

## 6.5 Versioning

Add `"apiVersion": 1` to `StatusDto`. The UI checks it on connect and shows a
clear "service is newer/older than UI" message instead of failing
mysteriously. Bump only on breaking changes.
