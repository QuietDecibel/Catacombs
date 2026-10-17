using Microsoft.Extensions.Logging.Abstractions;
using XTouch.Abstractions;
using XTouch.Abstractions.Targets;
using XTouch.Core.Engine;
using XTouch.Core.Profiles;
using XTouch.Core.Targets;

namespace XTouch.Core.Tests;

/// <summary>Engine wired to a loopback target ("x32" id so the default profile applies). Inputs are handled synchronously.</summary>
public sealed class TestRig
{
    public LoopbackTarget Target { get; } = new("x32");
    public EngineChannel Channel { get; } = new();
    public ControlEngine Engine { get; }
    public List<TargetEvent> Pending { get; } = [];

    public TestRig(ProfileDocument? profile = null)
    {
        Engine = new ControlEngine(Channel, new TargetRegistry([Target]), new NullSink(), new NullStateBroadcaster(),
            ProfileCompiler.Compile(profile ?? DefaultProfiles.X32()), NullLogger<ControlEngine>.Instance);
        // Capture the loopback echo instead of running the async loop.
        _ = Target.RunAsync(e => Pending.Add(e), _ => { }, CancellationToken.None);
    }

    public SurfaceState Surface => Engine.Surface;

    public void Input(SurfaceEvent e) => Engine.Handle(new FromSurface(e));

    public void Command(EngineCommand c) => Engine.Handle(c);

    /// <summary>Delivers target echoes to the engine.</summary>
    public void Pump()
    {
        var events = Pending.ToList();
        Pending.Clear();
        foreach (var e in events) Engine.Handle(new FromTarget("x32", e));
    }

    public void RemoteChange(string address, ParameterValue value)
    {
        Engine.Handle(new FromTarget("x32", new ParameterChanged(address, value)));
    }

    public IReadOnlyList<SetParameter> SetsSent() => Target.Received.OfType<SetParameter>().ToList();

    private sealed class NullSink : IFeedbackSink { public void Publish(SurfaceSnapshot desired) { } }
}
