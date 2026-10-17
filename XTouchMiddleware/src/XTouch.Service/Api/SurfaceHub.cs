using Microsoft.AspNetCore.SignalR;
using XTouch.Abstractions;
using XTouch.Contracts;
using XTouch.Core.Engine;
using XTouch.Service.Logging;
using XTouch.Surface.Transport;

namespace XTouch.Service.Api;

/// <summary>Real-time API for the UI. Mutations are posted to the engine channel, never applied directly.</summary>
public sealed class SurfaceHub(EngineChannel engine, HubStateBroadcaster state, UiLogSink logs, MidiTrace trace) : Hub
{
    public StatusDto GetStatus() => state.Status;
    public SurfaceDto GetSurface() => state.Surface;

    public void SetBankOffset(int offset) => engine.Post(new SetBankOffset(offset));
    public void ShiftBank(int delta) => engine.Post(new ShiftBank(delta));
    public void SetLayer(string layerId) => engine.Post(new SetLayer(layerId));
    public void RunSelfTest() => engine.Post(new RunSelfTest());

    // ----- Virtual surface: the UI acts as an on-screen X-Touch -----
    public void PressButton(int note, bool down) => engine.Post(new FromSurface(new ButtonChanged(note, down)));
    public void MoveFader(int strip, double value) => engine.Post(new FromSurface(new FaderMoved(strip, Math.Clamp(value, 0, 1))));
    public void TurnEncoder(int strip, int delta) => engine.Post(new FromSurface(new EncoderTurned(strip, Math.Clamp(delta, -63, 63))));

    public async Task<IReadOnlyList<LogEntryDto>> SubscribeLogs(bool on)
    {
        if (on) await Groups.AddToGroupAsync(Context.ConnectionId, HubStateBroadcaster.LogsGroup);
        else await Groups.RemoveFromGroupAsync(Context.ConnectionId, HubStateBroadcaster.LogsGroup);
        return on ? logs.Recent() : [];
    }

    /// <summary>Joins/leaves the trace group and switches MIDI tracing on or off globally.</summary>
    public async Task SetTrace(bool on)
    {
        if (on) await Groups.AddToGroupAsync(Context.ConnectionId, HubStateBroadcaster.TraceGroup);
        else await Groups.RemoveFromGroupAsync(Context.ConnectionId, HubStateBroadcaster.TraceGroup);
        trace.Enabled = on;
        state.RefreshStatus();
    }
}
