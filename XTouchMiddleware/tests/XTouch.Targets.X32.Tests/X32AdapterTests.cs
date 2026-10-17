using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using XTouch.Abstractions.Targets;

namespace XTouch.Targets.X32.Tests;

public sealed class X32AdapterTests : IAsyncLifetime
{
    private readonly X32Emulator _emulator = new(0) { AnimateMeters = true };
    private readonly X32Adapter _adapter = new(Options.Create(new X32Options { Host = "127.0.0.1" }), NullLogger<X32Adapter>.Instance);
    private readonly ConcurrentQueue<TargetEvent> _events = new();
    private readonly CancellationTokenSource _cts = new();
    private volatile bool _connected;
    private Task? _run;

    public Task InitializeAsync()
    {
        _emulator.Start();
        _adapter.Port = _emulator.Port;
        _run = _adapter.RunAsync(_events.Enqueue, c => _connected = c, _cts.Token);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _cts.Cancel();
        if (_run is not null) await _run;
        await _adapter.DisposeAsync();
        await _emulator.DisposeAsync();
    }

    private static async Task Until(Func<bool> condition, int timeoutMs = 5000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!condition())
        {
            if (sw.ElapsedMilliseconds > timeoutMs) throw new TimeoutException();
            await Task.Delay(20);
        }
    }

    [Fact]
    public async Task Connects_and_answers_queries()
    {
        await Until(() => _connected);
        _adapter.Send(new QueryParameter("ch/01/config/name"));
        await Until(() => _events.OfType<ParameterChanged>().Any(p => p.Address.Value == "ch/01/config/name"));
        var name = _events.OfType<ParameterChanged>().First(p => p.Address.Value == "ch/01/config/name");
        Assert.Equal("Kick", name.Value.Text);
    }

    [Fact]
    public async Task Sets_reach_the_console_with_the_right_types()
    {
        await Until(() => _connected);
        _adapter.Send(new SetParameter("ch/02/mix/fader", new ParameterValue(0.3)));
        _adapter.Send(new SetParameter("ch/02/mix/on", new ParameterValue(0)));
        await Until(() => _emulator.Get("ch/02/mix/on") is int i && i == 0);
        Assert.Equal(0.3f, _emulator.Get("ch/02/mix/fader"));
        Assert.Contains(_emulator.Received, r => r.Address == "/ch/02/mix/on" && r.Args[0] is int);
    }

    [Fact]
    public async Task Console_side_changes_are_pushed()
    {
        await Until(() => _connected);
        await Until(() => !_emulator.Received.IsEmpty && _emulator.Received.Any(r => r.Address == "/xremote"));
        _emulator.SetFromConsole("ch/05/mix/fader", 0.1f);
        await Until(() => _events.OfType<ParameterChanged>().Any(p => p.Address.Value == "ch/05/mix/fader"));
    }

    [Fact]
    public async Task Meters_arrive()
    {
        await Until(() => _events.OfType<MeterFrame>().Any());
        Assert.Equal(96, _events.OfType<MeterFrame>().First().Levels.Count);
    }
}
