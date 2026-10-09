# 7. UI Application (Avalonia + MVVM)

The UI is a **client** of the service. Closing it never affects mixing.

> **⚙ Reconcile** — If your UI design specifies screens or a different
> framework (WPF/WinUI/MAUI/web), keep the ViewModel + `IServiceClient` split
> below; only the Views change.

## 7.1 Project setup

```bash
dotnet new install Avalonia.Templates
dotnet new avalonia.mvvm -n XTouch.UI -o src/XTouch.UI
dotnet add src/XTouch.UI package CommunityToolkit.Mvvm
dotnet add src/XTouch.UI package Microsoft.AspNetCore.SignalR.Client
dotnet add src/XTouch.UI package Microsoft.Extensions.DependencyInjection
dotnet add src/XTouch.UI reference src/XTouch.Contracts
```

## 7.2 Screens

| Screen | Purpose | Key elements |
|--------|---------|--------------|
| **Dashboard** | At-a-glance health | Surface status (port, connected), target status cards, active profile, bank/layer, quick buttons (Reconnect, Self-test) |
| **Surface** | Live mirror of the X-Touch | 8 strips + master drawn like the hardware: fader, ring, LCD with color, buttons, meters; interactive (acts as a virtual surface) |
| **Mapping editor** | Edit profiles | Left: surface control tree. Center: binding form (target, parameter, converter, toggle/momentary). Right: target catalog search. Validation errors inline. |
| **Banks & layers** | Channel order and encoder layers | Drag-to-reorder channel list; layer list with assign-button picker |
| **Diagnostics** | Troubleshooting | Live log (filter by level/category), MIDI trace (hex + decoded), copy-to-clipboard, export logs |
| **Settings** | Connection settings | MIDI port pickers, target host/port, API URL/token, start-with-OS toggle |

Wireframe of the Surface screen:

```
+-- Surface ------------------------------------------ Bank 1 | Layer: PAN --+
|  [Kick ] [Snare] [HH   ] [Tom 1] [Tom 2] [OH L ] [OH R ] [Bass ]   [MAIN ] |  LCD line 1 (name, colored)
|  [-3.5 ] [ -inf] [ +0.0] [-10.0] [ -6.2] [ -8.0] [ -8.0] [ -2.0]   [ 0.0 ] |  LCD line 2 (value)
|    (o)     (o)     (o)     (o)     (o)     (o)     (o)     (o)             |  encoder rings
|   R  S    R  S    R  S    R  S    R  S    R  S    R  S    R  S             |  rec / solo
|   M SEL   M SEL   M SEL   M SEL   M SEL   M SEL   M SEL   M SEL            |  mute / select
|   |# |    |# |    |# |    |# |    |# |    |# |    |# |    |# |     | |     |  meter + fader
|   |#=|    |# |    |#=|    |# |    |#=|    |# |    |# |    |#=|     |=|     |
|                                   [< BANK] [BANK >] [< CH] [CH >]          |
+----------------------------------------------------------------------------+
```

## 7.3 Service client

One class wraps SignalR + REST and is the only network code in the UI.

```csharp
namespace XTouch.UI.Services;

public interface IServiceClient
{
    event Action<StatusDto>? StatusChanged;
    event Action<SurfaceDto>? SurfaceChanged;
    event Action<LogEntryDto>? LogReceived;
    event Action<bool>? ConnectionChanged;

    Task StartAsync(CancellationToken ct);
    Task SetBankAsync(int bank);
    Task MoveFaderAsync(int strip, double value);
    Task PressButtonAsync(int note, bool down);
    Task<IReadOnlyList<ProfileSummaryDto>> GetProfilesAsync();
    Task<Profile?> GetProfileAsync(string id);
    Task<ValidationResult> SaveProfileAsync(string id, Profile profile);
}

public sealed class ServiceClient : IServiceClient, IAsyncDisposable
{
    private readonly HubConnection _hub;
    private readonly HttpClient _http;

    public ServiceClient(UiSettings settings)
    {
        _http = new HttpClient { BaseAddress = new Uri(settings.ServiceUrl) };
        if (settings.Token is { } t) _http.DefaultRequestHeaders.Authorization = new("Bearer", t);

        _hub = new HubConnectionBuilder()
            .WithUrl(new Uri(_http.BaseAddress, "hub"), o => o.AccessTokenProvider = () => Task.FromResult(settings.Token))
            .WithAutomaticReconnect(new ForeverRetryPolicy())          // service may start after UI
            .Build();

        _hub.On<StatusDto>("Status",   s => StatusChanged?.Invoke(s));
        _hub.On<SurfaceDto>("Surface", s => SurfaceChanged?.Invoke(s));
        _hub.On<LogEntryDto>("Log",    l => LogReceived?.Invoke(l));
        _hub.Reconnected  += _ => { ConnectionChanged?.Invoke(true);  return Task.CompletedTask; };
        _hub.Reconnecting += _ => { ConnectionChanged?.Invoke(false); return Task.CompletedTask; };
    }

    public async Task StartAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await _hub.StartAsync(ct); ConnectionChanged?.Invoke(true); break; }
            catch when (!ct.IsCancellationRequested) { await Task.Delay(2000, ct); }
        }
        StatusChanged?.Invoke(await _hub.InvokeAsync<StatusDto>("GetStatus", ct));
        SurfaceChanged?.Invoke(await _hub.InvokeAsync<SurfaceDto>("GetSurface", ct));
    }

    public Task MoveFaderAsync(int strip, double v) => _hub.SendAsync("MoveFader", strip, v);
    // ... remaining members follow the same pattern

    public async ValueTask DisposeAsync() { await _hub.DisposeAsync(); _http.Dispose(); }
}

sealed class ForeverRetryPolicy : IRetryPolicy
{
    public TimeSpan? NextRetryDelay(RetryContext c) => TimeSpan.FromSeconds(Math.Min(10, 1 + c.PreviousRetryCount));
}
```

## 7.4 ViewModels

SignalR callbacks arrive on a background thread — marshal to the UI thread.

```csharp
public partial class SurfaceViewModel : ObservableObject
{
    private readonly IServiceClient _client;
    public ObservableCollection<StripViewModel> Strips { get; } =
        new(Enumerable.Range(0, 8).Select(i => new StripViewModel(i)));

    [ObservableProperty] private string _display = "";
    [ObservableProperty] private bool _isServiceConnected;

    public SurfaceViewModel(IServiceClient client)
    {
        _client = client;
        client.SurfaceChanged += s => Dispatcher.UIThread.Post(() => Apply(s));
        client.ConnectionChanged += c => Dispatcher.UIThread.Post(() => IsServiceConnected = c);
        foreach (var strip in Strips) strip.FaderDragged += (i, v) => _ = _client.MoveFaderAsync(i, v);
    }

    private void Apply(SurfaceDto s)
    {
        Display = s.Display;
        for (int i = 0; i < Strips.Count; i++) Strips[i].Apply(s.Strips[i]);
    }

    [RelayCommand] private Task BankLeft()  => _client.PressButtonAsync(0x2E, true);
    [RelayCommand] private Task BankRight() => _client.PressButtonAsync(0x2F, true);
}

public partial class StripViewModel(int index) : ObservableObject
{
    public int Index { get; } = index;
    public event Action<int, double>? FaderDragged;

    [ObservableProperty] private double _fader;
    [ObservableProperty] private string _top = "";
    [ObservableProperty] private string _bottom = "";
    [ObservableProperty] private string _color = "Off";
    [ObservableProperty] private bool _mute, _solo, _rec, _select;
    [ObservableProperty] private int _meter;
    [ObservableProperty] private int _ringPosition;

    private bool _userDragging;
    public void BeginDrag() => _userDragging = true;
    public void EndDrag()   => _userDragging = false;

    partial void OnFaderChanged(double value) { if (_userDragging) FaderDragged?.Invoke(Index, value); }

    public void Apply(StripDto d)
    {
        if (!_userDragging) Fader = d.Fader;     // same "touch" rule as the hardware
        Top = d.Top; Bottom = d.Bottom; Color = d.Color;
        Mute = d.Mute; Solo = d.Solo; Rec = d.Rec; Select = d.Select;
        Meter = d.Meter; RingPosition = d.RingPosition;
    }
}
```

The on-screen fader obeys the **same touch rule** as the hardware: while the
user drags it, incoming updates do not move it.

## 7.5 Custom controls

Build these as Avalonia `TemplatedControl`s or `Control`s with `Render`
overrides (cheap to redraw at 30 Hz):

| Control | Properties |
|---------|------------|
| `FaderControl` | `Value` (0–1, two-way), `IsTouched`, raises `DragStarted/DragCompleted` |
| `RingControl` | `Position` (0–11), `Mode`, `Center` — draws 11 LED dots on an arc |
| `ScribbleStrip` | `Top`, `Bottom`, `BackColor` (maps 8 X-Touch colors to brushes), monospace font |
| `MeterBar` | `Level` (0–12), `Clip` |
| `LedButton` | `Label`, `State` (Off/On/Blink — blink via a shared 2 Hz animation clock) |

## 7.6 Mapping editor UX rules

- Edits are made to a **draft** copy; "Save" PUTs it, "Revert" reloads.
- Validation errors come from the service (single source of truth) and are
  shown next to the offending binding.
- "Learn" mode: user clicks a binding, then touches a hardware control; the
  UI subscribes to the MIDI trace and fills in the control ID.
- Unsaved-changes guard on navigation and window close.

## 7.7 App lifetime

- Single instance (named mutex) — a second launch focuses the first window.
- Tray icon with status color (green: all connected, amber: target missing,
  red: surface missing). Closing the window minimizes to tray.
- UI settings (service URL, token, window layout) stored in
  `%APPDATA%/XTouchMiddleware/ui.json` (or `~/.config/XTouchMiddleware/ui.json`).
