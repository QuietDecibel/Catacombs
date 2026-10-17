using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using XTouch.Abstractions;
using XTouch.Surface.Mcu;
using XTouch.Surface.Transport;

namespace XTouch.Surface.Tests;

public class FeedbackRendererTests
{
    private readonly VirtualTransport _transport = new();
    private readonly FeedbackRenderer _renderer;
    private readonly SurfaceState _state = new();

    public FeedbackRendererTests()
    {
        _transport.ConnectAsync(CancellationToken.None).Wait();
        _renderer = new FeedbackRenderer(_transport, Options.Create(new SurfaceOptions()), NullLogger<FeedbackRenderer>.Instance);
    }

    private List<byte[]> RenderNow()
    {
        _transport.ClearSent();
        _renderer.Publish(_state.Snapshot());
        _renderer.Render();
        return [.. _transport.Sent];
    }

    private static bool IsFader(byte[] m, int strip) => m[0] == (0xE0 | strip);

    [Fact]
    public void First_render_paints_everything()
    {
        var sent = RenderNow();
        Assert.Equal(9, sent.Count(m => (m[0] & 0xF0) == 0xE0));
        Assert.Equal(2, sent.Count(m => m[0] == 0xF0 && m[5] == 0x12));   // two LCD lines
        Assert.Single(sent, m => m[0] == 0xF0 && m[5] == 0x72);            // colors
        Assert.Equal(12, sent.Count(m => m[0] == 0xB0 && m[1] >= 0x40));   // 7-segment
    }

    [Fact]
    public void Nothing_is_sent_when_unchanged()
    {
        RenderNow();
        Assert.Empty(RenderNow());
    }

    [Fact]
    public void Only_changes_are_sent()
    {
        RenderNow();
        _state.Faders[2] = 0.5;
        _state.Leds[0x10] = LedState.On;
        var sent = RenderNow();
        Assert.Equal(2, sent.Count);
        Assert.Contains(sent, m => IsFader(m, 2));
        Assert.Contains(sent, m => m.SequenceEqual(McuEncoder.Led(0x10, LedState.On)));
    }

    [Fact]
    public void Touched_fader_is_never_driven_and_is_sent_once_on_release()
    {
        RenderNow();
        _state.FaderTouched[3] = true;
        _state.Faders[3] = 0.9;
        Assert.DoesNotContain(RenderNow(), m => IsFader(m, 3));

        _state.FaderTouched[3] = false;
        var sent = RenderNow();
        Assert.Single(sent, m => IsFader(m, 3));
        Assert.Empty(RenderNow());
    }

    [Fact]
    public void Release_resends_even_if_value_unchanged()
    {
        RenderNow();
        _state.FaderTouched[1] = true;
        RenderNow();
        _state.FaderTouched[1] = false;
        Assert.Single(RenderNow(), m => IsFader(m, 1));
    }

    [Fact]
    public void Many_updates_between_ticks_produce_one_message()
    {
        RenderNow();
        for (int i = 0; i < 1000; i++)
        {
            _state.Faders[0] = i / 1000.0;
            _renderer.Publish(_state.Snapshot());
        }
        _transport.ClearSent();
        _renderer.Render();
        Assert.Single(_transport.Sent);
    }

    [Fact]
    public void One_lcd_cell_change_sends_one_line()
    {
        RenderNow();
        _state.LcdTop[4] = "Vocals ";
        var sent = RenderNow();
        var lcd = Assert.Single(sent);
        Assert.Equal(0x12, lcd[5]);
        Assert.Equal(0, lcd[6]);
        Assert.Equal(7 + 56 + 1, lcd.Length);
    }

    [Fact]
    public void Invalidate_repaints_everything()
    {
        int full = RenderNow().Count;
        _renderer.Invalidate();
        Assert.Equal(full, RenderNow().Count);
    }

    [Fact]
    public void Meters_are_resent_every_time()
    {
        _state.Meters[0] = 7;
        _renderer.Publish(_state.Snapshot());
        _transport.ClearSent();
        _renderer.RenderMeters();
        _renderer.RenderMeters();
        Assert.Equal(16, _transport.Sent.Count);
        Assert.Contains(_transport.Sent, m => m.SequenceEqual(new byte[] { 0xD0, 0x07 }));
    }
}
