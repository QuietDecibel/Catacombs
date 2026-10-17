using Avalonia;
using Avalonia.Headless;
using XTouch.UI.ViewModels;

[assembly: AvaloniaTestApplication(typeof(XTouch.UI.Tests.TestApp))]

namespace XTouch.UI.Tests;

public static class TestApp
{
    public static AppBuilder BuildAvaloniaApp()
    {
        UiThread.Post = a => a(); // tests already run on the UI thread
        return AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }
}
