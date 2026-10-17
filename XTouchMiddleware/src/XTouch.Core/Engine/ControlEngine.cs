using Microsoft.Extensions.Logging;
using XTouch.Abstractions;
using XTouch.Abstractions.Mcu;
using XTouch.Abstractions.Targets;
using XTouch.Core.Profiles;
using XTouch.Core.State;
using static XTouch.Abstractions.Mcu.McuConstants;

namespace XTouch.Core.Engine;

/// <summary>
/// The single owner of mixer state, surface state, bank and layer. Every input arrives through
/// <see cref="EngineChannel"/> and is handled on one task, so no locking is needed.
/// The engine never writes MIDI: it updates the desired <see cref="SurfaceState"/> and publishes
/// snapshots to the feedback renderer and the API layer.
/// </summary>
public sealed class ControlEngine
{
    private readonly EngineChannel _inputs;
    private readonly ITargetRegistry _targets;
    private readonly IFeedbackSink _feedback;
    private readonly IStateBroadcaster _broadcaster;
    private readonly ILogger<ControlEngine> _log;

    private readonly MixerState _mixer = new();
    private readonly SurfaceState _surface = new();
    private readonly Dictionary<string, ParameterValue> _changes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _targetConnected = new(StringComparer.OrdinalIgnoreCase);
    private CompiledProfile _profile;
    private int _bankOffset;
    private string _layer;
    private int? _selectedChannel;
    private bool _showEncoderValues;
    private bool _surfaceConnected;
    private string? _surfacePort;
    private bool _selfTestRunning;
    private EngineStatus? _status;

    public ControlEngine(EngineChannel inputs, ITargetRegistry targets, IFeedbackSink feedback,
        IStateBroadcaster broadcaster, CompiledProfile profile, ILogger<ControlEngine> log)
    {
        _inputs = inputs;
        _targets = targets;
        _feedback = feedback;
        _broadcaster = broadcaster;
        _log = log;
        _profile = profile;
        _layer = profile.DefaultLayer;
        foreach (var t in targets.All) _targetConnected[t.Id] = false;
        RepaintAll();
    }

    // ----- Read-only views (engine thread or tests only) -----
    public CompiledProfile Profile => _profile;
    public int BankOffset => _bankOffset;
    public string Layer => _layer;
    public MixerState Mixer => _mixer;
    public SurfaceState Surface => _surface;

    public async Task RunAsync(CancellationToken ct)
    {
        Publish();
        try
        {
            await foreach (var input in _inputs.Reader.ReadAllAsync(ct))
            {
                Handle(input);
                while (_inputs.Reader.TryRead(out var more)) Handle(more); // drain the burst, publish once
                Publish();
            }
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>Processes one input synchronously. Public for tests; production code goes through <see cref="RunAsync"/>.</summary>
    public void Handle(EngineInput input)
    {
        try
        {
            switch (input)
            {
                case FromSurface { Event: var e }: OnSurface(e); break;
                case FromTarget t: OnTarget(t); break;
                case SurfaceConnection c: OnSurfaceConnection(c); break;
                case TargetConnection t: OnTargetConnection(t); break;
                case EngineCommand c: OnCommand(c); break;
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Engine failed handling {Input}", input); // never kill the loop
        }
    }

    public void Publish()
    {
        var snapshot = _surface.Snapshot();
        _feedback.Publish(snapshot);
        IReadOnlyDictionary<string, ParameterValue>? changes = null;
        if (_changes.Count > 0)
        {
            changes = new Dictionary<string, ParameterValue>(_changes);
            _changes.Clear();
        }
        _broadcaster.Publish(snapshot, Status(), changes);
    }

    // =====================================================================
    // Surface input
    // =====================================================================

    private void OnSurface(SurfaceEvent e)
    {
        switch (e)
        {
            case FaderTouched t when t.Strip <= MasterFader:
                _surface.FaderTouched[t.Strip] = t.IsTouched;
                break;

            case FaderMoved f when f.Strip == MasterFader:
                _surface.Faders[MasterFader] = f.Value;
                if (_profile.Master is { } master) SendNormalized(master, f.Value);
                break;

            case FaderMoved f when f.Strip < Strips:
            {
                _surface.Faders[f.Strip] = f.Value; // desired == hand position, so release causes no jump
                if (ChannelAt(f.Strip)?.Fader is { } binding)
                {
                    SendNormalized(binding, f.Value);
                    UpdateStrip(f.Strip);
                }
                break;
            }

            case EncoderTurned enc when enc.Strip < Strips:
            {
                if (ChannelAt(enc.Strip) is not { } ch || !ch.Encoders.TryGetValue(_layer, out var b)) break;
                double current = Normalized(b.Address, b.Converter, b.Default);
                double next = Math.Clamp(current + enc.Delta * b.Step, 0, 1);
                if (b.Ring == RingMode.BoostCut && Math.Abs(next - b.Default) < b.Step / 2) next = b.Default; // snap to center
                SendNormalized(b.Address, b.Converter, next);
                UpdateStrip(enc.Strip);
                break;
            }

            case JogTurned j:
                MoveBank(j.Delta);
                break;

            case ButtonChanged { IsPressed: true } b:
                OnButtonPressed(b.Note);
                break;

            case ButtonChanged { IsPressed: false } b:
                if (_profile.Buttons.TryGetValue(b.Note, out var g) && g.Mode == ButtonMode.Momentary)
                {
                    SendNormalized(g, 0);
                    UpdateGlobals();
                }
                break;
        }
    }

    private void OnButtonPressed(int note)
    {
        switch (note)
        {
            case BankLeft: MoveBank(-Strips); return;
            case BankRight: MoveBank(+Strips); return;
            case ChannelLeft: MoveBank(-1); return;
            case ChannelRight: MoveBank(+1); return;
            case NameValue:
                _showEncoderValues = !_showEncoderValues;
                _status = null;
                RepaintAll();
                return;
        }

        if (note < AssignTrack)
        {
            int strip = note % Strips;
            var ch = ChannelAt(strip);
            if (ch is null) return;
            switch (note / Strips)
            {
                case 0: Toggle(ch.Rec); break;
                case 1: Toggle(ch.Solo); break;
                case 2: Toggle(ch.Mute); break;
                case 3:
                    _selectedChannel = _selectedChannel == ch.Index ? null : ch.Index;
                    _status = null;
                    RepaintAll();
                    return;
                case 4:
                    if (ch.Encoders.TryGetValue(_layer, out var enc)) SendNormalized(enc.Address, enc.Converter, enc.Default);
                    break;
            }
            UpdateStrip(strip);
            return;
        }

        if (_profile.LayerByNote(note) is { } layer)
        {
            SetActiveLayer(layer.Id);
            return;
        }

        if (_profile.Buttons.TryGetValue(note, out var binding))
        {
            switch (binding.Mode)
            {
                case ButtonMode.Toggle: Toggle(binding); break;
                case ButtonMode.Momentary: SendNormalized(binding, 1); break;
                case ButtonMode.Trigger: SendNormalized(binding, binding.TriggerValue); break;
            }
            UpdateGlobals();
        }
    }

    private void Toggle(Binding? b)
    {
        if (b is null) return;
        bool active = Normalized(b.Address, b.Converter, 0) >= 0.5;
        SendNormalized(b, active ? 0 : 1);
    }

    // =====================================================================
    // Target input
    // =====================================================================

    private void OnTarget(FromTarget t)
    {
        if (!string.Equals(t.AdapterId, _profile.Target, StringComparison.OrdinalIgnoreCase)) return;
        switch (t.Event)
        {
            case ParameterChanged p:
                if (!_mixer.Set(p.Address, p.Value)) return;
                _changes[p.Address.Value] = p.Value;
                OnParameterChanged(p.Address);
                break;
            case MeterFrame m:
                UpdateMeters(m);
                break;
        }
    }

    private void OnParameterChanged(ParameterAddress address)
    {
        if (_profile.ChannelsByAddress.TryGetValue(address.Value, out var channels))
        {
            foreach (int ch in channels)
            {
                int strip = ch - _bankOffset;
                if (strip is >= 0 and < Strips) UpdateStrip(strip);
            }
        }
        if (_profile.Master?.Address == address) UpdateMaster();
        if (_profile.Buttons.Values.Any(b => b.Address == address)) UpdateGlobals();
    }

    private void UpdateMeters(MeterFrame m)
    {
        for (int s = 0; s < Strips; s++)
        {
            int? idx = ChannelAt(s)?.Meter;
            _surface.Meters[s] = idx is { } i && i < m.Levels.Count ? LinearToMeter(m.Levels[i]) : 0;
        }
    }

    /// <summary>Linear level (1.0 = 0 dBFS) to an MCU meter step 0..12.</summary>
    public static int LinearToMeter(double linear)
    {
        if (linear <= 0) return 0;
        double db = 20 * Math.Log10(linear);
        return db switch
        {
            >= 0 => 12, >= -2 => 11, >= -4 => 10, >= -6 => 9, >= -9 => 8, >= -12 => 7,
            >= -15 => 6, >= -20 => 5, >= -25 => 4, >= -30 => 3, >= -40 => 2, >= -50 => 1, _ => 0,
        };
    }

    // =====================================================================
    // Connections and commands
    // =====================================================================

    private void OnSurfaceConnection(SurfaceConnection c)
    {
        _surfaceConnected = c.Connected;
        _surfacePort = c.PortName;
        _status = null;
        if (!c.Connected) Array.Clear(_surface.FaderTouched); // a lost touch-release must not block the motor forever
        RepaintAll();
    }

    private void OnTargetConnection(TargetConnection t)
    {
        _targetConnected[t.AdapterId] = t.Connected;
        _status = null;
        if (t.Connected && string.Equals(t.AdapterId, _profile.Target, StringComparison.OrdinalIgnoreCase)) QueryAll();
    }

    private void OnCommand(EngineCommand c)
    {
        switch (c)
        {
            case SetBankOffset b: MoveBank(b.Offset - _bankOffset); break;
            case ShiftBank b: MoveBank(b.Delta); break;
            case SetLayer l: SetActiveLayer(l.LayerId); break;
            case ActivateProfile p: LoadProfile(p.Profile); break;
            case RunSelfTest: StartSelfTest(); break;
            case SelfTestStep s: ApplySelfTestStep(s.Step); break;
        }
    }

    private void LoadProfile(CompiledProfile profile)
    {
        _profile = profile;
        _layer = profile.Layers.Any(l => l.Id == _layer) ? _layer : profile.DefaultLayer;
        _bankOffset = Math.Clamp(_bankOffset, 0, MaxBankOffset);
        _selectedChannel = null;
        _status = null;
        Array.Clear(_surface.Meters);
        _log.LogInformation("Profile '{Id}' active ({Channels} channels, {Layers} layers)", profile.Id, profile.ChannelCount, profile.Layers.Count);
        RepaintAll();
        QueryAll();
    }

    private void QueryAll()
    {
        if (_targets.Get(_profile.Target) is not { } adapter) return;
        foreach (var address in _profile.AllAddresses()) adapter.Send(new QueryParameter(address));
    }

    private void MoveBank(int delta)
    {
        int next = Math.Clamp(_bankOffset + delta, 0, MaxBankOffset);
        if (next == _bankOffset) return;
        _bankOffset = next;
        _status = null;
        Array.Clear(_surface.Meters);
        RepaintAll();
    }

    private int MaxBankOffset => Math.Max(0, _profile.ChannelCount - Strips);

    private void SetActiveLayer(string id)
    {
        if (_profile.Layers.All(l => l.Id != id) || id == _layer) return;
        _layer = id;
        _status = null;
        RepaintAll();
    }

    // =====================================================================
    // Self-test: lights everything, sweeps faders, then restores.
    // =====================================================================

    private void StartSelfTest()
    {
        if (_selfTestRunning) return;
        _selfTestRunning = true;
        _ = Task.Run(async () =>
        {
            for (int step = 0; step <= 4; step++)
            {
                _inputs.Post(new SelfTestStep(step));
                await Task.Delay(800);
            }
        });
    }

    private void ApplySelfTestStep(int step)
    {
        if (step >= 4)
        {
            _selfTestRunning = false;
            RepaintAll();
            return;
        }
        double fader = step switch { 0 => 1, 1 => 0.5, 2 => 0, _ => 0.75 };
        for (int i = 0; i < SurfaceState.FaderCount; i++) if (!_surface.FaderTouched[i]) _surface.Faders[i] = fader;
        for (int n = 0; n <= LastLedNote; n++) _surface.Leds[n] = step % 2 == 0 ? LedState.On : LedState.Off;
        for (int s = 0; s < Strips; s++)
        {
            _surface.Rings[s] = new RingState(RingMode.Wrap, step == 0 ? 11 : step * 3, step % 2 == 0);
            _surface.LcdColors[s] = (LcdColor)((s + step) % 7 + 1);
            _surface.LcdTop[s] = SurfaceText.Center($"STRIP{s + 1}");
            _surface.LcdBottom[s] = SurfaceText.Center($"TEST {step + 1}");
            _surface.Meters[s] = 12 - step * 3;
        }
        _surface.Display = $"T{step + 1}8888888888";
    }

    // =====================================================================
    // Feedback composition
    // =====================================================================

    private CompiledChannel? ChannelAt(int strip)
    {
        int idx = _bankOffset + strip;
        return strip is >= 0 and < Strips && idx < _profile.ChannelCount ? _profile.Channels[idx] : null;
    }

    private void RepaintAll()
    {
        if (_selfTestRunning) return;
        for (int s = 0; s < Strips; s++) UpdateStrip(s);
        UpdateMaster();
        UpdateGlobals();
    }

    private void UpdateStrip(int strip)
    {
        if (_selfTestRunning) return;
        var ch = ChannelAt(strip);
        bool touched = _surface.FaderTouched[strip];

        if (ch is null)
        {
            if (!touched) _surface.Faders[strip] = 0;
            _surface.Rings[strip] = RingState.Off;
            _surface.LcdTop[strip] = SurfaceText.Cell("");
            _surface.LcdBottom[strip] = SurfaceText.Cell("");
            _surface.LcdColors[strip] = LcdColor.Off;
            _surface.Leds[RecBase + strip] = _surface.Leds[SoloBase + strip] =
                _surface.Leds[MuteBase + strip] = _surface.Leds[SelectBase + strip] = LedState.Off;
            _surface.Meters[strip] = 0;
            return;
        }

        double fader = ch.Fader is { } fb ? Normalized(fb.Address, fb.Converter, 0) : 0;
        if (!touched) _surface.Faders[strip] = fader;

        string? name = ch.Name is { } nb ? _mixer.Get(nb.Address)?.Text : null;
        _surface.LcdTop[strip] = SurfaceText.Center(string.IsNullOrWhiteSpace(name) ? ch.Label : name);

        _surface.LcdColors[strip] = ch.Color is { } cb && _mixer.Get(cb.Address) is { } colorValue
            ? (LcdColor)((int)colorValue.Number & 0x07)
            : LcdColor.White;

        if (ch.Encoders.TryGetValue(_layer, out var enc))
        {
            double v = Normalized(enc.Address, enc.Converter, enc.Default);
            var ring = RingState.FromValue(v, enc.Ring);
            _surface.Rings[strip] = enc.Ring == RingMode.BoostCut && Math.Abs(v - 0.5) < 0.005 ? ring with { Center = true } : ring;
            _surface.LcdBottom[strip] = _showEncoderValues
                ? SurfaceText.Cell(enc.Converter.Format(v))
                : SurfaceText.Cell(ch.Fader?.Converter.Format(fader) ?? "");
        }
        else
        {
            _surface.Rings[strip] = RingState.Off;
            _surface.LcdBottom[strip] = SurfaceText.Cell(ch.Fader?.Converter.Format(fader) ?? "");
        }
        if (touched && ch.Fader is not null) _surface.LcdBottom[strip] = SurfaceText.Cell(ch.Fader.Converter.Format(_surface.Faders[strip]));

        _surface.Leds[RecBase + strip] = Led(ch.Rec);
        _surface.Leds[SoloBase + strip] = Led(ch.Solo);
        _surface.Leds[MuteBase + strip] = Led(ch.Mute);
        _surface.Leds[SelectBase + strip] = _selectedChannel == ch.Index ? LedState.On : LedState.Off;
    }

    private void UpdateMaster()
    {
        if (_selfTestRunning || _surface.FaderTouched[MasterFader]) return;
        _surface.Faders[MasterFader] = _profile.Master is { } m ? Normalized(m.Address, m.Converter, 0) : 0;
    }

    private void UpdateGlobals()
    {
        if (_selfTestRunning) return;
        foreach (var layer in _profile.Layers)
            if (layer.ButtonNote is { } note) _surface.Leds[note] = layer.Id == _layer ? LedState.On : LedState.Off;
        foreach (var (note, b) in _profile.Buttons) _surface.Leds[note] = Led(b);

        _surface.Leds[NameValue] = _showEncoderValues ? LedState.On : LedState.Off;
        _surface.Leds[BankLeft] = _surface.Leds[ChannelLeft] = _bankOffset > 0 ? LedState.On : LedState.Off;
        _surface.Leds[BankRight] = _surface.Leds[ChannelRight] = _bankOffset < MaxBankOffset ? LedState.On : LedState.Off;

        // Assignment display: first two letters of the layer; timecode: visible channel range.
        string layerLabel = _profile.Layers.FirstOrDefault(l => l.Id == _layer)?.Label ?? "";
        string assign = new string(layerLabel.Where(char.IsLetterOrDigit).Take(2).ToArray()).PadRight(2);
        int first = _bankOffset + 1, last = Math.Min(_bankOffset + Strips, _profile.ChannelCount);
        _surface.Display = assign + $" CH{first,3}-{last,2}".PadLeft(10)[..10];
    }

    private LedState Led(Binding? b) =>
        b is not null && Normalized(b.Address, b.Converter, 0) >= 0.5 ? LedState.On : LedState.Off;

    private double Normalized(ParameterAddress a, Conversion.IValueConverter c, double fallback) =>
        _mixer.Get(a) is { } v ? c.ToNormalized(v) : fallback;

    private void SendNormalized(Binding b, double normalized) => SendNormalized(b.Address, b.Converter, normalized);

    private void SendNormalized(ParameterAddress address, Conversion.IValueConverter converter, double normalized)
    {
        var value = converter.ToTarget(normalized);
        if (_mixer.Set(address, value)) _changes[address.Value] = value; // optimistic; the target echo confirms or corrects
        if (_targets.Get(_profile.Target) is { } adapter)
        {
            try { adapter.Send(new SetParameter(address, value)); }
            catch (Exception ex) { _log.LogWarning(ex, "Adapter {Id} failed to send {Address}", adapter.Id, address); }
        }
    }

    private EngineStatus Status()
    {
        if (_status is not null) return _status;
        var strips = new StripInfo[Strips];
        for (int s = 0; s < Strips; s++)
        {
            var ch = ChannelAt(s);
            strips[s] = new StripInfo(s, ch?.Index, ch?.Key ?? "", ch?.Label ?? "");
        }
        return _status = new EngineStatus(
            _surfaceConnected, _surfacePort,
            new Dictionary<string, bool>(_targetConnected),
            _profile.Id, _profile.Name, _bankOffset, _profile.ChannelCount, _layer,
            _profile.Layers.Select(l => (l.Id, l.Label)).ToList(), strips, _showEncoderValues);
    }
}
