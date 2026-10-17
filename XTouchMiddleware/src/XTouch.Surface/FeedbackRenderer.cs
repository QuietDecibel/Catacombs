using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XTouch.Abstractions;
using XTouch.Surface.Mcu;
using static XTouch.Abstractions.Mcu.McuConstants;

namespace XTouch.Surface;

/// <summary>
/// Sends the difference between the engine's desired surface state and what was last sent.
/// Rules: coalesce (only the latest state per tick), never drive a touched fader,
/// force-send a fader when it is released, and resend meters continuously.
/// </summary>
public sealed class FeedbackRenderer(IMidiTransport transport, IOptions<SurfaceOptions> options, ILogger<FeedbackRenderer> log)
    : IFeedbackSink
{
    private const double FaderTolerance = 1.0 / 1024; // X-Touch faders are ~10-bit

    private SurfaceSnapshot? _desired;
    private volatile bool _invalidated = true;

    // Last-sent state; null / NaN = unknown → must send.
    private readonly double[] _faders = new double[SurfaceState.FaderCount];
    private readonly bool[] _wasTouched = new bool[SurfaceState.FaderCount];
    private readonly LedState?[] _leds = new LedState?[SurfaceState.LedCount];
    private readonly RingState?[] _rings = new RingState?[Strips];
    private string? _lcdTop, _lcdBottom;
    private LcdColor[]? _colors;
    private readonly char?[] _digits = new char?[SevenSegDigits];
    private readonly bool[] _dots = new bool[SevenSegDigits];

    public void Publish(SurfaceSnapshot desired) => Volatile.Write(ref _desired, desired);

    /// <summary>Forget everything that was sent; the next tick repaints the whole surface.</summary>
    public void Invalidate() => _invalidated = true;

    public async Task RunAsync(CancellationToken ct)
    {
        var opt = options.Value;
        using var timer = new PeriodicTimer(opt.RenderInterval);
        int meterEvery = Math.Max(1, (int)Math.Round(opt.MeterInterval / opt.RenderInterval));
        long tick = 0;
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                if (!transport.IsConnected) continue;
                try
                {
                    Render();
                    if (++tick % meterEvery == 0) RenderMeters();
                }
                catch (Exception ex)
                {
                    log.LogWarning(ex, "Render tick failed");
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>Renders one tick. Public for tests.</summary>
    public void Render()
    {
        var d = Volatile.Read(ref _desired);
        if (d is null) return;
        if (_invalidated) ResetSent();

        for (int i = 0; i < SurfaceState.FaderCount; i++)
        {
            bool touched = d.FaderTouched[i];
            bool released = _wasTouched[i] && !touched;
            _wasTouched[i] = touched;
            if (touched) continue;                                  // hand on the fader: never drive the motor
            if (released || double.IsNaN(_faders[i]) || Math.Abs(_faders[i] - d.Faders[i]) >= FaderTolerance)
            {
                transport.Send(McuEncoder.Fader(i, d.Faders[i]));
                _faders[i] = d.Faders[i];
            }
        }

        for (int n = 0; n < SurfaceState.LedCount; n++)
        {
            if (n is >= FaderTouchBase and < FaderTouchBase + SurfaceState.FaderCount) continue; // touch notes are input only
            if (_leds[n] != d.Leds[n])
            {
                transport.Send(McuEncoder.Led(n, d.Leds[n]));
                _leds[n] = d.Leds[n];
            }
        }

        for (int s = 0; s < Strips; s++)
        {
            if (_rings[s] != d.Rings[s])
            {
                transport.Send(McuEncoder.Ring(s, d.Rings[s]));
                _rings[s] = d.Rings[s];
            }
        }

        // Whole 56-char line in one SysEx — fewer, larger messages are faster than one per cell.
        var top = string.Concat(d.LcdTop);
        if (top != _lcdTop) { transport.Send(McuEncoder.LcdText(0, top)); _lcdTop = top; }
        var bottom = string.Concat(d.LcdBottom);
        if (bottom != _lcdBottom) { transport.Send(McuEncoder.LcdText(LcdLineLength, bottom)); _lcdBottom = bottom; }

        if (_colors is null || !d.LcdColors.AsSpan().SequenceEqual(_colors))
        {
            transport.Send(McuEncoder.LcdColorsMessage(d.LcdColors));
            _colors = [.. d.LcdColors];
        }

        for (int i = 0; i < SevenSegDigits; i++)
        {
            char c = d.Display[i];
            bool dot = d.DisplayDots[i];
            if (_digits[i] != c || _dots[i] != dot)
            {
                transport.Send(McuEncoder.SevenSegment(i, c, dot));
                _digits[i] = c;
                _dots[i] = dot;
            }
        }
    }

    /// <summary>Sends every meter level. Public for tests.</summary>
    public void RenderMeters()
    {
        var d = Volatile.Read(ref _desired);
        if (d is null) return;
        for (int s = 0; s < Strips; s++) transport.Send(McuEncoder.Meter(s, d.Meters[s]));
    }

    private void ResetSent()
    {
        _invalidated = false;
        Array.Fill(_faders, double.NaN);
        Array.Clear(_wasTouched);
        Array.Clear(_leds);
        Array.Clear(_rings);
        _lcdTop = _lcdBottom = null;
        _colors = null;
        Array.Clear(_digits);
        Array.Clear(_dots);
    }
}
