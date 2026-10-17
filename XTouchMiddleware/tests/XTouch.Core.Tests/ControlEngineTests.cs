using XTouch.Abstractions;
using XTouch.Abstractions.Mcu;
using XTouch.Abstractions.Targets;
using XTouch.Core.Engine;
using static XTouch.Abstractions.Mcu.McuConstants;

namespace XTouch.Core.Tests;

public class ControlEngineTests
{
    [Fact]
    public void Fader_move_sends_to_mapped_channel()
    {
        var rig = new TestRig();
        rig.Input(new FaderMoved(2, 0.6));
        var set = Assert.Single(rig.SetsSent());
        Assert.Equal("ch/03/mix/fader", set.Address.Value);
        Assert.Equal(0.6, set.Value.Number, 6);
    }

    [Fact]
    public void Master_fader_sends_to_main()
    {
        var rig = new TestRig();
        rig.Input(new FaderMoved(MasterFader, 0.2));
        Assert.Equal("main/st/mix/fader", Assert.Single(rig.SetsSent()).Address.Value);
    }

    [Fact]
    public void Remote_change_on_visible_channel_moves_fader_and_updates_lcd()
    {
        var rig = new TestRig();
        rig.RemoteChange("ch/04/mix/fader", new ParameterValue(0.5));
        rig.RemoteChange("ch/04/config/name", ParameterValue.OfText("Bass"));
        rig.RemoteChange("ch/04/config/color", new ParameterValue(4));
        Assert.Equal(0.5, rig.Surface.Faders[3]);
        Assert.Equal(" Bass  ", rig.Surface.LcdTop[3]);
        Assert.Equal(LcdColor.Blue, rig.Surface.LcdColors[3]);
        Assert.Equal(" -10.0 ", rig.Surface.LcdBottom[3]);
    }

    [Fact]
    public void Remote_change_on_hidden_channel_changes_nothing()
    {
        var rig = new TestRig();
        var before = rig.Surface.Snapshot();
        rig.RemoteChange("ch/20/mix/fader", new ParameterValue(0.1));
        Assert.Equal(before.Faders.ToArray(), rig.Surface.Snapshot().Faders.ToArray());
    }

    [Fact]
    public void Touched_fader_keeps_hand_position_against_echoes()
    {
        var rig = new TestRig();
        rig.Input(new FaderTouched(0, true));
        rig.Input(new FaderMoved(0, 0.8));
        rig.RemoteChange("ch/01/mix/fader", new ParameterValue(0.3)); // stale echo / remote change
        Assert.Equal(0.8, rig.Surface.Faders[0]);
        rig.Input(new FaderTouched(0, false));
        Assert.False(rig.Surface.FaderTouched[0]);
    }

    [Fact]
    public void Bank_right_shows_next_eight_channels_and_clamps()
    {
        var rig = new TestRig();
        rig.RemoteChange("ch/09/mix/fader", new ParameterValue(0.25));
        rig.Input(new ButtonChanged(BankRight, true));
        Assert.Equal(8, rig.Engine.BankOffset);
        Assert.Equal(0.25, rig.Surface.Faders[0]);

        for (int i = 0; i < 20; i++) rig.Input(new ButtonChanged(BankRight, true));
        Assert.Equal(56 - 8, rig.Engine.BankOffset);
        Assert.Equal(LedState.Off, rig.Surface.Leds[BankRight]);
        Assert.Equal(LedState.On, rig.Surface.Leds[BankLeft]);

        rig.Input(new ButtonChanged(ChannelLeft, true));
        Assert.Equal(47, rig.Engine.BankOffset);
    }

    [Fact]
    public void Mute_toggles_inverted_on_parameter()
    {
        var rig = new TestRig();
        rig.RemoteChange("ch/01/mix/on", new ParameterValue(1)); // channel on = not muted
        Assert.Equal(LedState.Off, rig.Surface.Leds[MuteBase]);

        rig.Input(new ButtonChanged(MuteBase, true));
        var set = Assert.Single(rig.SetsSent());
        Assert.Equal("ch/01/mix/on", set.Address.Value);
        Assert.Equal(0, set.Value.Number);
        Assert.Equal(LedState.On, rig.Surface.Leds[MuteBase]);

        rig.Input(new ButtonChanged(MuteBase, true));
        Assert.Equal(1, rig.SetsSent()[^1].Value.Number);
        Assert.Equal(LedState.Off, rig.Surface.Leds[MuteBase]);
    }

    [Fact]
    public void Solo_uses_channel_vars()
    {
        var rig = new TestRig();
        rig.Command(new SetBankOffset(32)); // aux inputs start at channel 33
        rig.Input(new ButtonChanged(SoloBase + 1, true));
        Assert.Equal("-stat/solosw/34", Assert.Single(rig.SetsSent()).Address.Value);
    }

    [Fact]
    public void Encoder_changes_pan_in_steps_and_updates_ring()
    {
        var rig = new TestRig();
        rig.Input(new EncoderTurned(0, 10));
        var set = Assert.Single(rig.SetsSent());
        Assert.Equal("ch/01/mix/pan", set.Address.Value);
        Assert.Equal(0.6, set.Value.Number, 6);
        Assert.Equal(RingMode.BoostCut, rig.Surface.Rings[0].Mode);
        Assert.Equal(7, rig.Surface.Rings[0].Position);
    }

    [Fact]
    public void Encoder_push_resets_to_default()
    {
        var rig = new TestRig();
        rig.Input(new EncoderTurned(0, 30));
        rig.Input(new ButtonChanged(VPotPushBase, true));
        Assert.Equal(0.5, rig.SetsSent()[^1].Value.Number);
        Assert.True(rig.Surface.Rings[0].Center);
    }

    [Fact]
    public void Layer_button_switches_encoder_target_and_lights_led()
    {
        var rig = new TestRig();
        rig.Input(new ButtonChanged(AssignSend, true));
        Assert.Equal("send1", rig.Engine.Layer);
        Assert.Equal(LedState.On, rig.Surface.Leds[AssignSend]);
        Assert.Equal(LedState.Off, rig.Surface.Leds[AssignPan]);
        rig.Input(new EncoderTurned(1, 5));
        Assert.Equal("ch/02/mix/01/level", rig.SetsSent()[^1].Address.Value);
    }

    [Fact]
    public void Name_value_shows_encoder_values_on_bottom_line()
    {
        var rig = new TestRig();
        rig.Input(new ButtonChanged(NameValue, true));
        Assert.Equal("  <C>  ", rig.Surface.LcdBottom[0]);
        Assert.Equal(LedState.On, rig.Surface.Leds[NameValue]);
    }

    [Fact]
    public void Global_button_toggles_mute_group()
    {
        var rig = new TestRig();
        rig.Input(new ButtonChanged(F1, true));
        var set = Assert.Single(rig.SetsSent());
        Assert.Equal("config/mute/1", set.Address.Value);
        Assert.Equal(1, set.Value.Number);
        Assert.Equal(LedState.On, rig.Surface.Leds[F1]);
    }

    [Fact]
    public void Display_shows_layer_and_channel_range()
    {
        var rig = new TestRig();
        Assert.Equal("PA  CH  1- 8", rig.Surface.Display.TrimEnd());
        rig.Command(new ShiftBank(3));
        Assert.Equal("PA  CH  4-11", rig.Surface.Display.TrimEnd());
    }

    [Fact]
    public void Strips_beyond_the_last_channel_are_blank()
    {
        var doc = Profiles.DefaultProfiles.X32();
        doc.Channels = doc.Channels.Take(3).ToList();
        var rig = new TestRig(doc);
        Assert.Equal(SurfaceText.Cell(""), rig.Surface.LcdTop[5]);
        Assert.Equal(LcdColor.Off, rig.Surface.LcdColors[5]);
        rig.Input(new FaderMoved(5, 0.7));
        Assert.Empty(rig.SetsSent());
    }

    [Fact]
    public void Meters_follow_the_bank()
    {
        var rig = new TestRig();
        var levels = new float[96];
        levels[0] = 1.0f;   // ch 1: 0 dBFS
        levels[8] = 0.1f;   // ch 9: -20 dBFS
        rig.Engine.Handle(new FromTarget("x32", new MeterFrame(levels)));
        Assert.Equal(12, rig.Surface.Meters[0]);
        rig.Command(new ShiftBank(8));
        rig.Engine.Handle(new FromTarget("x32", new MeterFrame(levels)));
        Assert.Equal(5, rig.Surface.Meters[0]);
    }

    [Fact]
    public void Events_from_other_targets_are_ignored()
    {
        var rig = new TestRig();
        rig.Engine.Handle(new FromTarget("daw", new ParameterChanged("ch/01/mix/fader", new ParameterValue(0.1))));
        Assert.NotEqual(0.1, rig.Surface.Faders[0]);
    }

    [Fact]
    public void Activating_a_profile_repaints_and_queries_the_target()
    {
        var rig = new TestRig();
        var doc = Profiles.DefaultProfiles.X32();
        doc.Id = "small";
        doc.Channels = doc.Channels.Skip(40).ToList(); // buses only
        rig.Command(new ActivateProfile(Profiles.ProfileCompiler.Compile(doc)));
        Assert.Equal("small", rig.Engine.Profile.Id);
        Assert.Contains(rig.Target.Received.OfType<QueryParameter>(), q => q.Address.Value == "bus/01/mix/fader");
        Assert.Equal(" Bus 1 ", rig.Surface.LcdTop[0]);
    }

    [Fact]
    public void Surface_disconnect_clears_stuck_touches()
    {
        var rig = new TestRig();
        rig.Input(new FaderTouched(4, true));
        rig.Engine.Handle(new SurfaceConnection(false));
        Assert.False(rig.Surface.FaderTouched[4]);
    }

    [Theory]
    [InlineData(1.0, 12)]
    [InlineData(0.5, 8)] // -6 dB
    [InlineData(0.01, 2)]
    [InlineData(0.0, 0)]
    public void Linear_to_meter(double linear, int step) => Assert.Equal(step, ControlEngine.LinearToMeter(linear));

    [Fact]
    public void Echo_after_send_does_not_change_state()
    {
        var rig = new TestRig();
        rig.Input(new FaderMoved(0, 0.42));
        rig.Pump();
        Assert.Equal(0.42, rig.Surface.Faders[0], 6);
    }
}
