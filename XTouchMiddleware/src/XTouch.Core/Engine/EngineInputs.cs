using System.Threading.Channels;
using XTouch.Abstractions;
using XTouch.Abstractions.Targets;
using XTouch.Core.Profiles;

namespace XTouch.Core.Engine;

/// <summary>Everything the engine reacts to. All producers write to one channel; the engine is the single reader.</summary>
public abstract record EngineInput;
public sealed record FromSurface(SurfaceEvent Event) : EngineInput;
public sealed record FromTarget(string AdapterId, TargetEvent Event) : EngineInput;
public sealed record SurfaceConnection(bool Connected, string? PortName = null) : EngineInput;
public sealed record TargetConnection(string AdapterId, bool Connected) : EngineInput;

public abstract record EngineCommand : EngineInput;
public sealed record SetBankOffset(int Offset) : EngineCommand;
public sealed record ShiftBank(int Delta) : EngineCommand;
public sealed record SetLayer(string LayerId) : EngineCommand;
public sealed record ActivateProfile(CompiledProfile Profile) : EngineCommand;
public sealed record RunSelfTest : EngineCommand;
public sealed record SelfTestStep(int Step) : EngineCommand;

/// <summary>The engine's input queue.</summary>
public sealed class EngineChannel
{
    private readonly Channel<EngineInput> _channel = Channel.CreateBounded<EngineInput>(new BoundedChannelOptions(8192)
    {
        SingleReader = true,
        FullMode = BoundedChannelFullMode.DropOldest, // never block a MIDI callback; fader streams self-correct
    });

    public ChannelReader<EngineInput> Reader => _channel.Reader;

    public bool Post(EngineInput input) => _channel.Writer.TryWrite(input);

    public int Count => _channel.Reader.Count;
}
