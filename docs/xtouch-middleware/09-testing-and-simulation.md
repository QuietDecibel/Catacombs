# 9. Testing & Simulation

Goal: almost everything is testable **without** an X-Touch or a mixer.

## 9.1 Test pyramid

| Level | Project | What | Hardware? |
|-------|---------|------|-----------|
| Unit | `XTouch.Surface.Tests` | Decoder/encoder byte tables, renderer diffing, touch rules | No |
| Unit | `XTouch.Core.Tests` | Profile compile/validate, bank math, layer switching, `BuildStripView` | No |
| Unit | `XTouch.Targets.X32.Tests` | OSC codec round-trip, fader law, adapter vs `FakeX32` | No |
| Integration | `XTouch.IntegrationTests` | Full service (`WebApplicationFactory`) + `VirtualTransport` + `FakeX32` + SignalR client | No |
| Hardware | Manual checklist (§9.5) | Real X-Touch + real/fake mixer | Yes |

## 9.2 Virtual transport

```csharp
public sealed class VirtualTransport : IMidiTransport
{
    public List<byte[]> Sent { get; } = new();
    public string Name => "virtual";
    public bool IsConnected { get; private set; }
    public event Action<ReadOnlyMemory<byte>>? MessageReceived;
    public event Action<bool>? ConnectionChanged;

    public Task ConnectAsync(CancellationToken ct) { IsConnected = true; ConnectionChanged?.Invoke(true); return Task.CompletedTask; }
    public void Send(ReadOnlySpan<byte> m) { lock (Sent) Sent.Add(m.ToArray()); }

    // Test helpers: simulate the operator
    public void Inject(params byte[] m) => MessageReceived?.Invoke(m);
    public void TouchFader(int s, bool on) => Inject(0x90, (byte)(0x68 + s), (byte)(on ? 0x7F : 0));
    public void MoveFader(int s, double v) => Inject(McuEncoder.Fader(s, v));
    public void Unplug() { IsConnected = false; ConnectionChanged?.Invoke(false); }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

## 9.3 Key test cases

**Surface**
- Every row of the protocol tables in [chapter 3](03-xtouch-protocol-reference.md) decodes/encodes correctly (data-driven `[Theory]`).
- Encoder: `0x01`→+1, `0x3F`→+63, `0x41`→−1, `0x7F`→−63.
- Renderer sends nothing when desired == sent.
- Renderer **does not** send fader position while touched; sends exactly once on release.
- 1000 fader updates within one tick → one MIDI message.
- LCD change in one cell → one SysEx for that line, 56 chars + header.
- `Invalidate()` → next tick repaints everything.

**Core**
- Bank right at the last bank stays clamped; channel ◄/► shifts by 1.
- Target change for a channel not in the current bank produces **no** surface change.
- Target change for a visible channel updates the right strip.
- Layer switch repaints rings and LCD bottom line.
- Invalid profile on hot reload keeps the old profile active.
- Engine survives an exception thrown by an adapter's `Send`.

**Integration**
- Start service → `VirtualTransport.Connect` → surface receives full reset + repaint.
- Move virtual fader 3 → `FakeX32` receives `/ch/03/mix/fader` with the right float.
- `FakeX32` pushes a name change → LCD SysEx for strip 3 contains the new name.
- SignalR client receives `Surface` push within 100 ms of a change.
- Unplug/replug → repaint occurs again.

## 9.4 MIDI trace

Add a decorator transport that logs every message, enabled from the UI's
Diagnostics screen:

```csharp
public sealed class TracingTransport(IMidiTransport inner, ITraceSink sink) : IMidiTransport
{
    public bool Enabled { get; set; }
    public void Send(ReadOnlySpan<byte> m)
    {
        if (Enabled) sink.Write("OUT", m);
        inner.Send(m);
    }
    // MessageReceived: wrap inner's event and sink.Write("IN", m) when Enabled
    // ...other members delegate to inner
}
```

Format: `12:01:03.441 IN  E2 3A 5C  Fader 3 = 0.723`. This is the single most
useful tool for confirming firmware-specific behavior (scribble colors, Xctl).

## 9.5 Hardware acceptance checklist

Run on every release with a real X-Touch in MC/USB mode.

- [ ] Service starts before the X-Touch is plugged in; connects within 3 s of plug-in.
- [ ] Unplug during use → UI shows red; replug → full repaint, no restart.
- [ ] Self-test: every LED lights, all faders sweep 0→1→0, all rings sweep, LCD shows strip numbers in each color.
- [ ] Move 4 faders at once rapidly → mixer follows; no stutter or fighting.
- [ ] Hold a fader while changing the same channel on the mixer → fader does not move under the hand; on release it jumps to the mixer value or keeps hand value per profile setting.
- [ ] Bank ◄/► and Channel ◄/► repaint faders, names, colors, mutes in < 100 ms.
- [ ] Each layer button switches encoder function and rings.
- [ ] Meters move with audio and fall to zero when audio stops.
- [ ] Mixer powered off → target shows disconnected; surface keeps last state; mixer back → resync.
- [ ] Stop service → surface blanks.
- [ ] 8-hour soak: memory flat, no log errors.
