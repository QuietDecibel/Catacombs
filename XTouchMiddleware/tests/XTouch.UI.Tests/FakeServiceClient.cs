using XTouch.Contracts;
using XTouch.UI.Services;

namespace XTouch.UI.Tests;

/// <summary>Records calls and lets tests push service events.</summary>
public sealed class FakeServiceClient : IServiceClient
{
    public List<string> Calls { get; } = [];
    public event Action<StatusDto>? StatusChanged;
    public event Action<SurfaceDto>? SurfaceChanged;
    public event Action<IReadOnlyList<LogEntryDto>>? LogsReceived;
    public event Action<IReadOnlyList<MidiTraceDto>>? TraceReceived;
    public event Action<bool>? ConnectionChanged;

    public bool IsConnected { get; private set; }
    public string ServiceUrl => "http://127.0.0.1:5280/";
    public List<ProfileSummaryDto> Profiles { get; } = [new("x32-default", "X32 – Inputs, Aux, Buses", "x32", DateTimeOffset.Now, true, true)];

    public Task ConnectAsync(UiSettings settings, CancellationToken ct = default)
    {
        IsConnected = true;
        ConnectionChanged?.Invoke(true);
        return Task.CompletedTask;
    }

    public void PushStatus(StatusDto s) => StatusChanged?.Invoke(s);
    public void PushSurface(SurfaceDto s) => SurfaceChanged?.Invoke(s);
    public void PushLogs(params LogEntryDto[] l) => LogsReceived?.Invoke(l);
    public void PushTrace(params MidiTraceDto[] t) => TraceReceived?.Invoke(t);

    private Task Record(string call) { Calls.Add(call); return Task.CompletedTask; }

    public Task ShiftBankAsync(int delta) => Record($"ShiftBank {delta}");
    public Task SetLayerAsync(string layerId) => Record($"SetLayer {layerId}");
    public Task PressButtonAsync(int note, bool down) => Record($"Press {note:X2} {down}");
    public Task MoveFaderAsync(int strip, double value) => Record($"Fader {strip} {value:0.00}");
    public Task TurnEncoderAsync(int strip, int delta) => Record($"Turn {strip} {delta}");
    public Task RunSelfTestAsync() => Record("SelfTest");
    public Task<IReadOnlyList<LogEntryDto>> SubscribeLogsAsync(bool on) => Task.FromResult<IReadOnlyList<LogEntryDto>>([]);
    public Task SetTraceAsync(bool on) => Record($"Trace {on}");
    public Task<IReadOnlyList<ProfileSummaryDto>> GetProfilesAsync() => Task.FromResult<IReadOnlyList<ProfileSummaryDto>>(Profiles);
    public Task<string?> GetProfileJsonAsync(string id) => Task.FromResult<string?>($$"""{ "id": "{{id}}", "name": "Test" }""");
    public Task<ValidationResultDto> ValidateProfileAsync(string json) => Task.FromResult(new ValidationResultDto(false, ["at least one channel is required"]));
    public Task<ValidationResultDto> SaveProfileAsync(string id, string json) { Calls.Add($"Save {id}"); return Task.FromResult(new ValidationResultDto(true, [])); }
    public Task<ValidationResultDto> ActivateProfileAsync(string id) => Task.FromResult(new ValidationResultDto(true, []));
    public Task<ValidationResultDto> DeleteProfileAsync(string id) => Task.FromResult(new ValidationResultDto(true, []));
    public Task<MidiPortsDto> GetMidiPortsAsync() => Task.FromResult(new MidiPortsDto(["X-TOUCH"], ["X-TOUCH"]));

    public static SurfaceDto SampleSurface()
    {
        string[] names = ["Kick", "Snare", "HiHat", "Tom 1", "Tom 2", "OH L", "OH R", "Bass"];
        string[] colors = ["Red", "Red", "Yellow", "Yellow", "Green", "Cyan", "Cyan", "Blue"];
        var strips = Enumerable.Range(0, 8).Select(i => new StripDto(
            i, i, $"ch/{i + 1:00}", 0.4 + i * 0.07, i == 2, $" {names[i],-5} ", "   0.0 ", colors[i],
            1 + i, i % 2 == 0 ? "BoostCut" : "Wrap", i == 0, false, i == 3, i == 1, i == 4, 2 + i)).ToList();
        var leds = new int[0x74];
        leds[0x2A] = 127;
        leds[0x36] = 127;
        return new SurfaceDto(strips, 0.75, false, "PA  CH  1- 8", leds);
    }

    public static StatusDto SampleStatus() => new(
        ApiInfo.Version, true, "Virtual X-Touch", "Virtual",
        [new TargetStatusDto("x32", "Behringer X32 @ 127.0.0.1:10023", true)],
        "x32-default", "X32 – Inputs, Aux, Buses", 0, 56, "pan",
        [new LayerDto("pan", "PAN"), new LayerDto("trim", "TRIM"), new LayerDto("send1", "SEND 1")], false, false);
}
