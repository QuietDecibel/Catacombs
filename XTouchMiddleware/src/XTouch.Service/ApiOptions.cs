namespace XTouch.Service;

public sealed class ApiOptions
{
    /// <summary>Listen URL. Loopback by default; a LAN address requires <see cref="Token"/>.</summary>
    public string Url { get; set; } = "http://127.0.0.1:5280";

    /// <summary>Optional bearer token required for /api and /hub.</summary>
    public string? Token { get; set; }
}
