using CommunityToolkit.Mvvm.ComponentModel;
using XTouch.Contracts;
using XTouch.UI.Services;

namespace XTouch.UI.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IServiceClient _client;
    private readonly UiSettings _settings;

    public SurfaceViewModel Surface { get; }
    public ProfilesViewModel Profiles { get; }
    public DiagnosticsViewModel Diagnostics { get; }
    public SettingsViewModel Settings { get; }

    [ObservableProperty] private bool _isServiceConnected;
    [ObservableProperty] private bool _isSurfaceConnected;
    [ObservableProperty] private string _surfaceText = "Surface: —";
    [ObservableProperty] private bool _isTargetConnected;
    [ObservableProperty] private string _targetText = "Target: —";
    [ObservableProperty] private string _profileText = "";
    [ObservableProperty] private string _serviceText = "Connecting to service…";
    [ObservableProperty] private int _selectedTab;

    public MainWindowViewModel(IServiceClient client, UiSettings settings)
    {
        _client = client;
        _settings = settings;
        Surface = new SurfaceViewModel(client);
        Profiles = new ProfilesViewModel(client);
        Diagnostics = new DiagnosticsViewModel(client);
        Settings = new SettingsViewModel(client, settings, ConnectAsync);

        client.SurfaceChanged += s => UiThread.Post(() => Surface.Apply(s));
        client.StatusChanged += s => UiThread.Post(() => ApplyStatus(s));
        client.LogsReceived += l => UiThread.Post(() => Diagnostics.AddLogs(l));
        client.TraceReceived += t => UiThread.Post(() => Diagnostics.AddTrace(t));
        client.ConnectionChanged += c => UiThread.Post(() => OnConnectionChanged(c));
    }

    public Task ConnectAsync() => _client.ConnectAsync(_settings);

    private void OnConnectionChanged(bool connected)
    {
        IsServiceConnected = connected;
        ServiceText = connected ? $"Service: {_client.ServiceUrl}" : $"Service offline — retrying {_settings.ServiceUrl}";
        if (!connected) return;
        _ = Diagnostics.OnConnectedAsync();
        _ = Profiles.Refresh();
        _ = Settings.RefreshPorts();
    }

    private void ApplyStatus(StatusDto s)
    {
        if (s.ApiVersion != ApiInfo.Version)
            ServiceText = $"Service API v{s.ApiVersion} does not match this UI (v{ApiInfo.Version}) — update both.";
        IsSurfaceConnected = s.SurfaceConnected;
        SurfaceText = s.SurfaceConnected ? $"Surface: {s.SurfacePort}" : $"Surface: not connected ({s.SurfaceTransport})";
        IsTargetConnected = s.Targets.Count > 0 && s.Targets.All(t => t.Connected);
        TargetText = s.Targets.Count == 0 ? "Target: none" : string.Join("  ", s.Targets.Select(t => $"{t.DisplayName}: {(t.Connected ? "online" : "offline")}"));
        ProfileText = $"Profile: {s.ProfileName}";
        Surface.Apply(s);
        if (Profiles.Profiles.Count > 0 && Profiles.Profiles.FirstOrDefault(p => p.IsActive)?.Id != s.ProfileId) _ = Profiles.Refresh();
    }
}
