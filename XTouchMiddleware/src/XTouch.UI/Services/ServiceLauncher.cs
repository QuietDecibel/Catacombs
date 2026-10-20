using System.Diagnostics;
using System.Net;

namespace XTouch.UI.Services;

/// <summary>
/// Starts XTouch.Service when the UI is opened and the service is not running on this computer,
/// so a single desktop shortcut is enough. The service keeps running after the UI closes.
/// </summary>
public static class ServiceLauncher
{
    private static readonly string ExeName = OperatingSystem.IsWindows() ? "XTouch.Service.exe" : "XTouch.Service";

    /// <summary>Where to look for the service executable, relative to the UI.</summary>
    public static IEnumerable<string> Candidates(string uiDir) =>
    [
        Path.Combine(uiDir, ExeName),
        Path.GetFullPath(Path.Combine(uiDir, "..", "service", ExeName)),  // installed layout: <dir>/ui + <dir>/service
        Path.Combine(uiDir, "service", ExeName),
    ];

    public static async Task<string> EnsureRunningAsync(UiSettings settings)
    {
        if (!settings.AutoStartService) return "";
        if (!Uri.TryCreate(settings.ServiceUrl, UriKind.Absolute, out var url) || !IsLocal(url)) return "";
        if (await IsRunningAsync(url)) return "";

        var exe = Candidates(AppContext.BaseDirectory).FirstOrDefault(File.Exists);
        if (exe is null) return "Service not running and XTouch.Service was not found next to the UI.";
        try
        {
            Process.Start(new ProcessStartInfo(exe)
            {
                WorkingDirectory = Path.GetDirectoryName(exe)!,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            return $"Started {exe}";
        }
        catch (Exception ex)
        {
            return $"Could not start the service: {ex.Message}";
        }
    }

    private static bool IsLocal(Uri url) => url.IsLoopback || (IPAddress.TryParse(url.Host, out var ip) && IPAddress.IsLoopback(ip));

    private static async Task<bool> IsRunningAsync(Uri baseUrl)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(1.5) };
            using var res = await http.GetAsync(new Uri(baseUrl, "/health"));
            return true; // any HTTP answer means something is listening there
        }
        catch (Exception)
        {
            return false;
        }
    }
}
