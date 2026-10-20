using System.Text.Json;

namespace XTouch.UI.Services;

/// <summary>Per-user UI settings, stored in the user's application-data folder.</summary>
public sealed class UiSettings
{
    public string ServiceUrl { get; set; } = "http://127.0.0.1:5280";
    public string? Token { get; set; }

    /// <summary>Start XTouch.Service automatically when it is not running on this computer.</summary>
    public bool AutoStartService { get; set; } = true;

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XTouchMiddleware", "ui.json");

    public static UiSettings Load()
    {
        try
        {
            if (File.Exists(FilePath)) return JsonSerializer.Deserialize<UiSettings>(File.ReadAllText(FilePath)) ?? new();
        }
        catch (Exception)
        {
            // Corrupt settings: fall back to defaults.
        }
        return new UiSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
    }
}
