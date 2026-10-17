using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using XTouch.UI.Controls;
using XTouch.UI.Services;
using XTouch.UI.ViewModels;
using XTouch.UI.Views;

namespace XTouch.UI.Tests;

public class MainWindowTests
{
    private static (MainWindow Window, MainWindowViewModel Vm, FakeServiceClient Client) Open()
    {
        var client = new FakeServiceClient();
        var vm = new MainWindowViewModel(client, new UiSettings());
        var window = new MainWindow { DataContext = vm };
        window.Show();
        vm.ConnectAsync().Wait();
        client.PushStatus(FakeServiceClient.SampleStatus());
        client.PushSurface(FakeServiceClient.SampleSurface());
        Dispatcher.UIThread.RunJobs();
        return (window, vm, client);
    }

    private static string ScreenshotPath(string name)
    {
        var dir = Environment.GetEnvironmentVariable("XTOUCH_SCREENSHOT_DIR") ?? Path.Combine(AppContext.BaseDirectory, "screenshots");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, name);
    }

    private static void Capture(Window window, string name)
    {
#pragma warning disable CS0618 // simplest overload is fine for test screenshots
        window.CaptureRenderedFrame()?.Save(ScreenshotPath(name));
#pragma warning restore CS0618
    }

    [AvaloniaFact]
    public void Surface_tab_renders_strips_from_service_state()
    {
        var (window, vm, _) = Open();
        Assert.Equal(" Kick  ", vm.Surface.Strips[0].Top);
        Assert.Equal("Red", vm.Surface.Strips[0].Color);
        Assert.True(vm.Surface.Strips[1].Mute);
        Assert.True(vm.Surface.Strips[3].Solo);
        Assert.Equal("PA", vm.Surface.Assignment);
        Assert.True(vm.Surface.Layers.Single(l => l.Id == "pan").IsActive);
        Assert.True(vm.Surface.FunctionKeys[0].IsLit);
        Assert.True(vm.IsSurfaceConnected);
        Assert.Equal("Channels 1–8 of 56", vm.Surface.BankText);

        var faders = window.GetVisualDescendantsOfType<FaderControl>().ToList();
        Assert.Equal(9, faders.Count);

        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Capture(window, "surface.png");
    }

    [AvaloniaFact]
    public void Dragging_a_fader_sends_and_ignores_incoming_updates()
    {
        var (_, vm, client) = Open();
        var strip = vm.Surface.Strips[1];
        strip.IsDragging = true;
        strip.Fader = 0.9;
        client.PushSurface(FakeServiceClient.SampleSurface()); // would move it back to 0.47
        Assert.Equal(0.9, strip.Fader);
        Assert.Contains("Fader 1 0.90", client.Calls);
        strip.IsDragging = false;
        client.PushSurface(FakeServiceClient.SampleSurface());
        Assert.Equal(0.47, strip.Fader, 3);
    }

    [AvaloniaFact]
    public async Task Buttons_and_encoders_send_commands()
    {
        var (_, vm, client) = Open();
        await vm.Surface.Strips[2].PressMuteCommand.ExecuteAsync(null);
        await vm.Surface.Strips[0].TurnCommand.ExecuteAsync(-3);
        await vm.Surface.BankRightCommand.ExecuteAsync(null);
        await vm.Surface.Layers[1].SelectCommand.ExecuteAsync(null);
        Assert.Contains("Press 12 True", client.Calls);
        Assert.Contains("Press 12 False", client.Calls);
        Assert.Contains("Turn 0 -3", client.Calls);
        Assert.Contains("ShiftBank 8", client.Calls);
        Assert.Contains("SetLayer trim", client.Calls);
    }

    [AvaloniaFact]
    public async Task Profiles_tab_loads_and_shows_validation_errors()
    {
        var (window, vm, client) = Open();
        await vm.Profiles.RefreshCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("x32-default", vm.Profiles.Selected?.Id);
        Assert.Contains("\"id\": \"x32-default\"", vm.Profiles.EditorText);
        await vm.Profiles.ValidateCommand.ExecuteAsync(null);
        Assert.Single(vm.Profiles.Errors);

        vm.Profiles.NewId = "my-copy";
        vm.Profiles.EditorText = """{ "id": "x32-default", "name": "Base" }""";
        await vm.Profiles.DuplicateCommand.ExecuteAsync(null);
        Assert.Contains("Save my-copy", client.Calls);

        vm.SelectedTab = 1;
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Capture(window, "profiles.png");
    }

    [AvaloniaFact]
    public void Diagnostics_tab_shows_logs_and_trace()
    {
        var (window, vm, client) = Open();
        client.PushLogs(new(DateTimeOffset.Now, "Information", "X32Adapter", "X32 console responding at 127.0.0.1:10023"),
                        new(DateTimeOffset.Now, "Warning", "SurfaceWorker", "Surface disconnected"));
        vm.Diagnostics.HideMeters = true;
        client.PushTrace(new(DateTimeOffset.Now, "IN", "E2 3A 5C", "Fader 3 = 0.723"), new(DateTimeOffset.Now, "OUT", "D0 05", "Meter 1 = 5"));
        Assert.Equal(2, vm.Diagnostics.Logs.Count);
        Assert.Single(vm.Diagnostics.Trace); // meter filtered

        vm.Diagnostics.TraceEnabled = true;
        Assert.Contains("Trace True", client.Calls);

        vm.SelectedTab = 2;
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Capture(window, "diagnostics.png");
    }

    [Theory]
    [InlineData("Dot", 4, 4, true)]
    [InlineData("Dot", 4, 5, false)]
    [InlineData("Wrap", 4, 3, true)]
    [InlineData("BoostCut", 8, 7, true)]
    [InlineData("BoostCut", 8, 5, false)]
    [InlineData("BoostCut", 3, 4, true)]
    [InlineData("Spread", 2, 7, true)]
    [InlineData("Spread", 2, 8, false)]
    [InlineData("Wrap", 0, 1, false)]
    public void Ring_led_logic(string mode, int position, int led, bool lit) =>
        Assert.Equal(lit, RingControl.IsLit(led, mode, position));
}

internal static class VisualExtensions
{
    public static IEnumerable<T> GetVisualDescendantsOfType<T>(this Avalonia.Visual root) =>
        Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(root).OfType<T>();
}
