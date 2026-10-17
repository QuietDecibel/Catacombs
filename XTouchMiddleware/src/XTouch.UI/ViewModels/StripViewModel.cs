using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XTouch.Contracts;
using XTouch.UI.Services;

namespace XTouch.UI.ViewModels;

/// <summary>One channel strip of the on-screen X-Touch. Doubles as a virtual surface.</summary>
public sealed partial class StripViewModel(int index, IServiceClient client) : ObservableObject
{
    public int Index { get; } = index;
    public string Number => (Index + 1).ToString();

    [ObservableProperty] private string _channelKey = "";
    [ObservableProperty] private bool _hasChannel;
    [ObservableProperty] private string _top = "";
    [ObservableProperty] private string _bottom = "";
    [ObservableProperty] private string _color = "Off";
    [ObservableProperty] private double _fader;
    [ObservableProperty] private bool _isDragging;
    [ObservableProperty] private bool _touched;
    [ObservableProperty] private int _ringPosition;
    [ObservableProperty] private string _ringMode = "Dot";
    [ObservableProperty] private bool _ringCenter;
    [ObservableProperty] private bool _rec;
    [ObservableProperty] private bool _solo;
    [ObservableProperty] private bool _mute;
    [ObservableProperty] private bool _select;
    [ObservableProperty] private int _meter;

    // Only user drags are sent; updates from the service while dragging are ignored (same rule as the motor faders).
    partial void OnFaderChanged(double value)
    {
        if (IsDragging) _ = client.MoveFaderAsync(Index, value);
    }

    public void Apply(StripDto d)
    {
        if (!IsDragging) Fader = d.Fader;
        ChannelKey = d.ChannelKey;
        HasChannel = d.ChannelIndex is not null;
        Top = d.Top;
        Bottom = d.Bottom;
        Color = d.Color;
        Touched = d.Touched;
        RingPosition = d.RingPosition;
        RingMode = d.RingMode;
        RingCenter = d.RingCenter;
        Rec = d.Rec;
        Solo = d.Solo;
        Mute = d.Mute;
        Select = d.Select;
        Meter = d.Meter;
    }

    [RelayCommand] private Task Turn(int delta) => client.TurnEncoderAsync(Index, delta);
    [RelayCommand] private Task Push() => Press(SurfaceNotes.VPotPushBase + Index);
    [RelayCommand] private Task PressRec() => Press(SurfaceNotes.RecBase + Index);
    [RelayCommand] private Task PressSolo() => Press(SurfaceNotes.SoloBase + Index);
    [RelayCommand] private Task PressMute() => Press(SurfaceNotes.MuteBase + Index);
    [RelayCommand] private Task PressSelect() => Press(SurfaceNotes.SelectBase + Index);

    private async Task Press(int note)
    {
        await client.PressButtonAsync(note, true);
        await client.PressButtonAsync(note, false);
    }
}
