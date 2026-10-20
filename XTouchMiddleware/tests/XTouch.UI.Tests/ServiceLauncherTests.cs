using XTouch.UI.Services;

namespace XTouch.UI.Tests;

public class ServiceLauncherTests
{
    [Fact]
    public void Looks_next_to_the_ui_and_in_the_installed_service_folder()
    {
        var root = Path.Combine(Path.GetTempPath(), "xt");
        var candidates = ServiceLauncher.Candidates(Path.Combine(root, "ui")).ToList();
        Assert.Contains(candidates, c => c.StartsWith(Path.Combine(root, "service"), StringComparison.Ordinal));
        Assert.Contains(candidates, c => c.StartsWith(Path.Combine(root, "ui"), StringComparison.Ordinal));
    }

    [Fact]
    public async Task Does_nothing_for_remote_services_or_when_disabled()
    {
        Assert.Equal("", await ServiceLauncher.EnsureRunningAsync(new UiSettings { ServiceUrl = "http://192.168.1.20:5280" }));
        Assert.Equal("", await ServiceLauncher.EnsureRunningAsync(new UiSettings { ServiceUrl = "http://127.0.0.1:1", AutoStartService = false }));
    }

    [Fact]
    public async Task Reports_when_the_service_executable_is_missing()
    {
        var msg = await ServiceLauncher.EnsureRunningAsync(new UiSettings { ServiceUrl = "http://127.0.0.1:1" });
        Assert.Contains("not found", msg);
    }
}
