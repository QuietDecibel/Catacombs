using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XTouch.Contracts;
using XTouch.UI.Services;

namespace XTouch.UI.ViewModels;

public sealed partial class LayerButtonViewModel(string id, string label, Func<string, Task> select) : ObservableObject
{
    public string Id { get; } = id;
    public string Label { get; } = label;
    [ObservableProperty] private bool _isActive;
    [RelayCommand] private Task Select() => select(Id);
}

public sealed partial class FunctionKeyViewModel(int number, Func<int, Task> press) : ObservableObject
{
    public string Label { get; } = $"F{number}";
    public int Note { get; } = SurfaceNotes.F1 + number - 1;
    [ObservableProperty] private bool _isLit;
    [RelayCommand] private Task Press() => press(Note);
}

/// <summary>Live mirror of the X-Touch; every control also works as a virtual surface.</summary>
public sealed partial class SurfaceViewModel : ObservableObject
{
    private readonly IServiceClient _client;

    public ObservableCollection<StripViewModel> Strips { get; }
    public ObservableCollection<LayerButtonViewModel> Layers { get; } = [];
    public ObservableCollection<FunctionKeyViewModel> FunctionKeys { get; }

    [ObservableProperty] private double _master;
    [ObservableProperty] private bool _masterDragging;
    [ObservableProperty] private bool _masterTouched;
    [ObservableProperty] private string _assignment = "  ";
    [ObservableProperty] private string _timecode = "";
    [ObservableProperty] private string _bankText = "";
    [ObservableProperty] private bool _showEncoderValues;
    [ObservableProperty] private bool _canBankLeft;
    [ObservableProperty] private bool _canBankRight;

    public SurfaceViewModel(IServiceClient client)
    {
        _client = client;
        Strips = new(Enumerable.Range(0, 8).Select(i => new StripViewModel(i, client)));
        FunctionKeys = new(Enumerable.Range(1, 8).Select(n => new FunctionKeyViewModel(n, PressNote)));
    }

    partial void OnMasterChanged(double value)
    {
        if (MasterDragging) _ = _client.MoveFaderAsync(8, value);
    }

    public void Apply(SurfaceDto s)
    {
        for (int i = 0; i < Strips.Count && i < s.Strips.Count; i++) Strips[i].Apply(s.Strips[i]);
        if (!MasterDragging) Master = s.Master;
        MasterTouched = s.MasterTouched;
        var display = (s.Display ?? "").PadRight(12);
        Assignment = display[..2];
        Timecode = display[2..12];
        foreach (var f in FunctionKeys) f.IsLit = f.Note < s.Leds.Count && s.Leds[f.Note] != 0;
    }

    public void Apply(StatusDto status)
    {
        if (Layers.Select(l => l.Id).SequenceEqual(status.Layers.Select(l => l.Id)) == false)
        {
            Layers.Clear();
            foreach (var l in status.Layers) Layers.Add(new LayerButtonViewModel(l.Id, l.Label, id => _client.SetLayerAsync(id)));
        }
        foreach (var l in Layers) l.IsActive = l.Id == status.Layer;
        int first = status.BankOffset + 1, last = Math.Min(status.BankOffset + 8, status.ChannelCount);
        BankText = status.ChannelCount == 0 ? "" : $"Channels {first}–{last} of {status.ChannelCount}";
        ShowEncoderValues = status.ShowEncoderValues;
        CanBankLeft = status.BankOffset > 0;
        CanBankRight = status.BankOffset + 8 < status.ChannelCount;
    }

    private async Task PressNote(int note)
    {
        await _client.PressButtonAsync(note, true);
        await _client.PressButtonAsync(note, false);
    }

    [RelayCommand] private Task BankLeft() => _client.ShiftBankAsync(-8);
    [RelayCommand] private Task BankRight() => _client.ShiftBankAsync(8);
    [RelayCommand] private Task ChannelLeft() => _client.ShiftBankAsync(-1);
    [RelayCommand] private Task ChannelRight() => _client.ShiftBankAsync(1);
    [RelayCommand] private Task NameValue() => PressNote(SurfaceNotes.NameValue);
    [RelayCommand] private Task SelfTest() => _client.RunSelfTestAsync();
}
