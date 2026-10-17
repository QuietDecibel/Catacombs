using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using XTouch.Abstractions;
using XTouch.Contracts;
using XTouch.Surface.Transport;
using XTouch.Targets.X32;

namespace XTouch.Integration.Tests;

/// <summary>Full service: virtual surface + built-in X32 emulator + REST + SignalR.</summary>
public sealed class ServiceTests : IAsyncLifetime
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "xtouch-it-" + Guid.NewGuid().ToString("N"));
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _http = null!;

    public Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Surface:Transport", "Virtual");
            b.UseSetting("Targets:X32:Emulator", "true");
            b.UseSetting("Targets:X32:EmulatorPort", "0");
            b.UseSetting("Profiles:Directory", _dir);
            b.UseSetting("Serilog:WriteTo:1:Args:path", Path.Combine(_dir, "logs", "x-.log"));
        });
        _http = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    private VirtualTransport Surface => (VirtualTransport)((TracingTransport)_factory.Services.GetRequiredService<IMidiTransport>()).Inner;

    private static async Task Until(Func<Task<bool>> condition, int timeoutMs = 8000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!await condition())
        {
            if (sw.ElapsedMilliseconds > timeoutMs) throw new TimeoutException();
            await Task.Delay(50);
        }
    }

    private async Task<SurfaceDto> SurfaceState() => (await _http.GetFromJsonAsync<SurfaceDto>("/api/surface"))!;

    [Fact]
    public async Task Starts_connects_and_shows_console_names()
    {
        await Until(async () => (await _http.GetFromJsonAsync<StatusDto>("/api/status"))!.Targets.All(t => t.Connected));
        await Until(async () => (await SurfaceState()).Strips[0].Top.Trim() == "Kick");
        var status = await _http.GetFromJsonAsync<StatusDto>("/api/status");
        Assert.True(status!.SurfaceConnected);
        Assert.Equal("x32-default", status.ProfileId);
    }

    [Fact]
    public async Task Hardware_fader_move_reaches_the_console()
    {
        await Until(async () => (await SurfaceState()).Strips[0].Top.Trim() == "Kick");
        Surface.TouchFader(2, true);
        Surface.MoveFader(2, 0.25);
        Surface.TouchFader(2, false);
        await Until(async () => Math.Abs((await SurfaceState()).Strips[2].Fader - 0.25) < 0.001);
        var emulator = _factory.Services.GetRequiredService<XTouch.Service.Workers.TargetsWorker>().Emulator!;
        await Until(() => Task.FromResult(emulator.Get("ch/03/mix/fader") is float f && Math.Abs(f - 0.25f) < 0.001f));
        Assert.Contains(Surface.Sent, m => m[0] == 0xF0 && m[5] == 0x12); // LCD was written to the hardware
    }

    [Fact]
    public async Task SignalR_pushes_surface_updates_and_accepts_commands()
    {
        var hub = new HubConnectionBuilder()
            .WithUrl(new Uri(_http.BaseAddress!, ApiInfo.HubPath), o => o.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler())
            .Build();
        var surfaces = new List<SurfaceDto>();
        hub.On<SurfaceDto>(HubEvents.Surface, s => { lock (surfaces) surfaces.Add(s); });
        await hub.StartAsync();

        await hub.InvokeAsync("ShiftBank", 8);
        await Until(async () => (await hub.InvokeAsync<StatusDto>("GetStatus")).BankOffset == 8);
        await hub.InvokeAsync("MoveFader", 0, 0.5);
        await Until(() => { lock (surfaces) return Task.FromResult(surfaces.Any(s => s.Strips.Count > 0 && Math.Abs(s.Strips[0].Fader - 0.5) < 0.001)); });
        await hub.DisposeAsync();
    }

    [Fact]
    public async Task Profiles_can_be_listed_validated_saved_and_activated()
    {
        var list = await _http.GetFromJsonAsync<List<ProfileSummaryDto>>("/api/profiles");
        Assert.Contains(list!, p => p.Id == "x32-default" && p.IsActive);

        var json = await _http.GetStringAsync("/api/profiles/x32-default");
        var copy = json.Replace("\"id\": \"x32-default\"", "\"id\": \"copy\"");
        var put = await _http.PutAsync("/api/profiles/copy", new StringContent(copy));
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var bad = await _http.PutAsync("/api/profiles/copy", new StringContent("{ \"id\": \"copy\", \"channels\": [] }"));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var result = await bad.Content.ReadFromJsonAsync<ValidationResultDto>();
        Assert.Contains(result!.Errors, e => e.Contains("channel"));

        Assert.Equal(HttpStatusCode.Accepted, (await _http.PostAsync("/api/profiles/copy/activate", null)).StatusCode);
        await Until(async () => (await _http.GetFromJsonAsync<StatusDto>("/api/status"))!.ProfileId == "copy");
        Assert.Equal(HttpStatusCode.Conflict, (await _http.DeleteAsync("/api/profiles/copy")).StatusCode);
    }

    [Fact]
    public async Task Health_endpoint()
    {
        var res = await _http.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}
