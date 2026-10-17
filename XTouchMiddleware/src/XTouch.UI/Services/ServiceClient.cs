using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.SignalR.Client;
using XTouch.Contracts;

namespace XTouch.UI.Services;

public interface IServiceClient
{
    event Action<StatusDto>? StatusChanged;
    event Action<SurfaceDto>? SurfaceChanged;
    event Action<IReadOnlyList<LogEntryDto>>? LogsReceived;
    event Action<IReadOnlyList<MidiTraceDto>>? TraceReceived;
    event Action<bool>? ConnectionChanged;

    bool IsConnected { get; }
    string ServiceUrl { get; }

    Task ConnectAsync(UiSettings settings, CancellationToken ct = default);

    Task ShiftBankAsync(int delta);
    Task SetLayerAsync(string layerId);
    Task PressButtonAsync(int note, bool down);
    Task MoveFaderAsync(int strip, double value);
    Task TurnEncoderAsync(int strip, int delta);
    Task RunSelfTestAsync();
    Task<IReadOnlyList<LogEntryDto>> SubscribeLogsAsync(bool on);
    Task SetTraceAsync(bool on);

    Task<IReadOnlyList<ProfileSummaryDto>> GetProfilesAsync();
    Task<string?> GetProfileJsonAsync(string id);
    Task<ValidationResultDto> ValidateProfileAsync(string json);
    Task<ValidationResultDto> SaveProfileAsync(string id, string json);
    Task<ValidationResultDto> ActivateProfileAsync(string id);
    Task<ValidationResultDto> DeleteProfileAsync(string id);
    Task<MidiPortsDto> GetMidiPortsAsync();
}

/// <summary>The UI's only network code: SignalR for live state and commands, HTTP for profiles and settings.</summary>
public sealed class ServiceClient : IServiceClient, IAsyncDisposable
{
    private HubConnection? _hub;
    private HttpClient? _http;
    private CancellationTokenSource? _connectCts;

    public event Action<StatusDto>? StatusChanged;
    public event Action<SurfaceDto>? SurfaceChanged;
    public event Action<IReadOnlyList<LogEntryDto>>? LogsReceived;
    public event Action<IReadOnlyList<MidiTraceDto>>? TraceReceived;
    public event Action<bool>? ConnectionChanged;

    public bool IsConnected => _hub?.State == HubConnectionState.Connected;
    public string ServiceUrl { get; private set; } = "";

    /// <summary>(Re)connects to the service and keeps retrying until it answers; SignalR then reconnects automatically.</summary>
    public async Task ConnectAsync(UiSettings settings, CancellationToken ct = default)
    {
        if (_connectCts is not null) await _connectCts.CancelAsync();
        _connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = _connectCts.Token;

        if (_hub is not null) await _hub.DisposeAsync();
        _http?.Dispose();

        ServiceUrl = settings.ServiceUrl.TrimEnd('/') + "/";
        _http = new HttpClient { BaseAddress = new Uri(ServiceUrl), Timeout = TimeSpan.FromSeconds(10) };
        if (!string.IsNullOrEmpty(settings.Token)) _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.Token);

        var hub = new HubConnectionBuilder()
            .WithUrl(new Uri(_http.BaseAddress, ApiInfo.HubPath.TrimStart('/')), o =>
            {
                if (!string.IsNullOrEmpty(settings.Token)) o.AccessTokenProvider = () => Task.FromResult<string?>(settings.Token);
            })
            .WithAutomaticReconnect(new ForeverRetryPolicy())
            .Build();

        hub.On<StatusDto>(HubEvents.Status, s => StatusChanged?.Invoke(s));
        hub.On<SurfaceDto>(HubEvents.Surface, s => SurfaceChanged?.Invoke(s));
        hub.On<LogEntryDto[]>(HubEvents.Logs, l => LogsReceived?.Invoke(l));
        hub.On<MidiTraceDto[]>(HubEvents.Trace, t => TraceReceived?.Invoke(t));
        hub.Reconnecting += _ => { ConnectionChanged?.Invoke(false); return Task.CompletedTask; };
        hub.Reconnected += async _ => { ConnectionChanged?.Invoke(true); await PullStateAsync(hub); };
        hub.Closed += _ => { ConnectionChanged?.Invoke(false); return Task.CompletedTask; };
        _hub = hub;
        ConnectionChanged?.Invoke(false);

        while (!token.IsCancellationRequested)
        {
            try
            {
                await hub.StartAsync(token);
                ConnectionChanged?.Invoke(true);
                await PullStateAsync(hub);
                return;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (Exception)
            {
                await Task.Delay(2000, token).ContinueWith(_ => { }, TaskScheduler.Default); // service not running yet
            }
        }
    }

    private async Task PullStateAsync(HubConnection hub)
    {
        StatusChanged?.Invoke(await hub.InvokeAsync<StatusDto>("GetStatus"));
        SurfaceChanged?.Invoke(await hub.InvokeAsync<SurfaceDto>("GetSurface"));
    }

    private Task Send(string method, params object?[] args) =>
        IsConnected ? _hub!.SendCoreAsync(method, args) : Task.CompletedTask;

    public Task ShiftBankAsync(int delta) => Send("ShiftBank", delta);
    public Task SetLayerAsync(string layerId) => Send("SetLayer", layerId);
    public Task PressButtonAsync(int note, bool down) => Send("PressButton", note, down);
    public Task MoveFaderAsync(int strip, double value) => Send("MoveFader", strip, value);
    public Task TurnEncoderAsync(int strip, int delta) => Send("TurnEncoder", strip, delta);
    public Task RunSelfTestAsync() => Send("RunSelfTest");
    public Task SetTraceAsync(bool on) => IsConnected ? _hub!.InvokeAsync("SetTrace", on) : Task.CompletedTask;

    public async Task<IReadOnlyList<LogEntryDto>> SubscribeLogsAsync(bool on) =>
        IsConnected ? await _hub!.InvokeAsync<List<LogEntryDto>>("SubscribeLogs", on) : [];

    private HttpClient Http => _http ?? throw new InvalidOperationException("Not connected");

    public async Task<IReadOnlyList<ProfileSummaryDto>> GetProfilesAsync() =>
        await Http.GetFromJsonAsync<List<ProfileSummaryDto>>("api/profiles") ?? [];

    public async Task<string?> GetProfileJsonAsync(string id)
    {
        var res = await Http.GetAsync($"api/profiles/{Uri.EscapeDataString(id)}");
        return res.IsSuccessStatusCode ? await res.Content.ReadAsStringAsync() : null;
    }

    public Task<ValidationResultDto> ValidateProfileAsync(string json) =>
        ReadResult(Http.PostAsync("api/profiles/validate", new StringContent(json, Encoding.UTF8, "application/json")));

    public Task<ValidationResultDto> SaveProfileAsync(string id, string json) =>
        ReadResult(Http.PutAsync($"api/profiles/{Uri.EscapeDataString(id)}", new StringContent(json, Encoding.UTF8, "application/json")));

    public Task<ValidationResultDto> ActivateProfileAsync(string id) =>
        ReadResult(Http.PostAsync($"api/profiles/{Uri.EscapeDataString(id)}/activate", null));

    public Task<ValidationResultDto> DeleteProfileAsync(string id) =>
        ReadResult(Http.DeleteAsync($"api/profiles/{Uri.EscapeDataString(id)}"));

    public async Task<MidiPortsDto> GetMidiPortsAsync() =>
        await Http.GetFromJsonAsync<MidiPortsDto>("api/midi/ports") ?? new MidiPortsDto([], []);

    private static async Task<ValidationResultDto> ReadResult(Task<HttpResponseMessage> request)
    {
        try
        {
            using var res = await request;
            if (res.Content.Headers.ContentLength is > 0)
            {
                try
                {
                    if (await res.Content.ReadFromJsonAsync<ValidationResultDto>() is { } r) return r;
                }
                catch (System.Text.Json.JsonException) { }
            }
            return res.IsSuccessStatusCode
                ? new ValidationResultDto(true, [])
                : new ValidationResultDto(false, [$"{(int)res.StatusCode} {res.ReasonPhrase}"]);
        }
        catch (HttpRequestException ex)
        {
            return new ValidationResultDto(false, [ex.Message]);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _connectCts?.Cancel();
        if (_hub is not null) await _hub.DisposeAsync();
        _http?.Dispose();
    }

    private sealed class ForeverRetryPolicy : IRetryPolicy
    {
        public TimeSpan? NextRetryDelay(RetryContext retryContext) =>
            TimeSpan.FromSeconds(Math.Min(10, 1 + retryContext.PreviousRetryCount));
    }
}
