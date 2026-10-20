using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using XTouch.UI.Services;
using XTouch.UI.ViewModels;
using XTouch.UI.Views;

namespace XTouch.UI;

public sealed class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = UiSettings.Load();
            if (desktop.Args is { Length: > 0 } args && Array.IndexOf(args, "--service") is var i and >= 0 && i + 1 < args.Length)
                settings.ServiceUrl = args[i + 1];

            var client = new ServiceClient();
            var vm = new MainWindowViewModel(client, settings);
            desktop.MainWindow = new MainWindow { DataContext = vm };
            desktop.ShutdownRequested += (_, _) => client.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
            _ = Task.Run(async () =>
            {
                await ServiceLauncher.EnsureRunningAsync(settings);
                await vm.ConnectAsync(); // keeps retrying while the service starts up
            });
        }
        base.OnFrameworkInitializationCompleted();
    }
}
