using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XTouch.Contracts;
using XTouch.UI.Services;

namespace XTouch.UI.ViewModels;

/// <summary>Live service log and MIDI trace.</summary>
public sealed partial class DiagnosticsViewModel(IServiceClient client) : ObservableObject
{
    private const int MaxLogs = 1000, MaxTrace = 2000;

    public ObservableCollection<LogEntryDto> Logs { get; } = [];
    public ObservableCollection<MidiTraceDto> Trace { get; } = [];

    [ObservableProperty] private bool _traceEnabled;
    [ObservableProperty] private bool _hideMeters = true;

    partial void OnTraceEnabledChanged(bool value) => _ = client.SetTraceAsync(value);

    public void AddLogs(IEnumerable<LogEntryDto> entries) => AddCapped(Logs, entries, MaxLogs);

    public void AddTrace(IEnumerable<MidiTraceDto> entries) =>
        AddCapped(Trace, HideMeters ? entries.Where(e => !e.Description.StartsWith("Meter", StringComparison.Ordinal)) : entries, MaxTrace);

    /// <summary>Called after (re)connecting: fetch recent log entries and restore the trace switch.</summary>
    public async Task OnConnectedAsync()
    {
        var recent = await client.SubscribeLogsAsync(true);
        UiThread.Post(() =>
        {
            Logs.Clear();
            AddLogs(recent);
        });
        if (TraceEnabled) await client.SetTraceAsync(true);
    }

    private static void AddCapped<T>(ObservableCollection<T> list, IEnumerable<T> items, int max)
    {
        foreach (var item in items) list.Add(item);
        while (list.Count > max) list.RemoveAt(0);
    }

    [RelayCommand] private void ClearLogs() => Logs.Clear();
    [RelayCommand] private void ClearTrace() => Trace.Clear();
}
