using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using XTouch.UI.Services;
using XTouch.UI.ViewModels;
using XTouch.UI.Views;

namespace XTouch.UI.Tests;

/// <summary>
/// Smoke test against a running service. Skipped unless XTOUCH_SERVICE_URL is set, e.g.:
///   DOTNET_ENVIRONMENT=Demo dotnet run --project src/XTouch.Service
///   XTOUCH_SERVICE_URL=http://127.0.0.1:5280 dotnet test tests/XTouch.UI.Tests
/// </summary>
public class LiveServiceTests
{
    private static readonly string? Url = Environment.GetEnvironmentVariable("XTOUCH_SERVICE_URL");

    private static void Pump(Func<bool> until, int timeoutMs = 8000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!until() && sw.ElapsedMilliseconds < timeoutMs)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(20);
        }
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task Ui_drives_the_real_service()
    {
        Assert.SkipWhen(Url is null, "Set XTOUCH_SERVICE_URL to run against a live service.");
        UiThread.Post = a => Dispatcher.UIThread.Post(a);
        try
        {
            await using var client = new ServiceClient();
            var vm = new MainWindowViewModel(client, new UiSettings { ServiceUrl = Url! });
            var window = new MainWindow { DataContext = vm };
            window.Show();
            var connect = vm.ConnectAsync();
            Pump(() => connect.IsCompleted && vm.Surface.Strips[0].HasChannel);
            Assert.True(vm.IsServiceConnected);

            var strip = vm.Surface.Strips[0];
            strip.IsDragging = true;
            strip.Fader = 0.3;
            strip.IsDragging = false;
            Pump(() => strip.Bottom.Contains("-26"));
            Assert.Contains("-26.0", strip.Bottom); // 0.3 on the X32 law = -26 dB, round-tripped through the service

            await vm.Surface.BankRightCommand.ExecuteAsync(null);
            Pump(() => vm.Surface.BankText.StartsWith("Channels 9"));
            Assert.StartsWith("Channels 9", vm.Surface.BankText);
            await vm.Surface.BankLeftCommand.ExecuteAsync(null);
            Pump(() => vm.Surface.BankText.StartsWith("Channels 1"));

            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            var dir = Environment.GetEnvironmentVariable("XTOUCH_SCREENSHOT_DIR") ?? AppContext.BaseDirectory;
#pragma warning disable CS0618
            window.CaptureRenderedFrame()?.Save(Path.Combine(dir, "live-surface.png"));
#pragma warning restore CS0618
        }
        finally
        {
            UiThread.Post = a => a();
        }
    }
}
