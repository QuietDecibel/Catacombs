using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XTouch.UI.Services;

namespace XTouch.UI.ViewModels;

public sealed partial class SettingsViewModel(IServiceClient client, UiSettings settings, Func<Task> reconnect) : ObservableObject
{
    [ObservableProperty] private string _serviceUrl = settings.ServiceUrl;
    [ObservableProperty] private string _token = settings.Token ?? "";
    [ObservableProperty] private string _message = "";

    public ObservableCollection<string> MidiInputs { get; } = [];
    public ObservableCollection<string> MidiOutputs { get; } = [];

    public string SettingsFile => UiSettings.FilePath;

    [RelayCommand]
    private async Task SaveAndReconnect()
    {
        if (!Uri.TryCreate(ServiceUrl, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            Message = "Service URL must be an http(s) address, e.g. http://127.0.0.1:5280";
            return;
        }
        settings.ServiceUrl = ServiceUrl.Trim();
        settings.Token = string.IsNullOrWhiteSpace(Token) ? null : Token.Trim();
        try { settings.Save(); Message = "Saved. Reconnecting…"; }
        catch (Exception ex) { Message = $"Could not save settings: {ex.Message}"; }
        await reconnect();
    }

    [RelayCommand]
    public async Task RefreshPorts()
    {
        if (!client.IsConnected) { Message = "Not connected to the service."; return; }
        try
        {
            var ports = await client.GetMidiPortsAsync();
            MidiInputs.Clear();
            MidiOutputs.Clear();
            foreach (var p in ports.Inputs) MidiInputs.Add(p);
            foreach (var p in ports.Outputs) MidiOutputs.Add(p);
            Message = ports.Inputs.Count == 0 ? "The service sees no MIDI input ports." : "";
        }
        catch (Exception ex)
        {
            Message = ex.Message;
        }
    }
}
