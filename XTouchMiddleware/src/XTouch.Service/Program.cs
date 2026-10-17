using System.Net;
using Serilog;
using XTouch.Contracts;
using XTouch.Service;
using XTouch.Service.Api;

// Relative paths (profiles, logs) resolve next to the executable, also when started as an OS service.
Directory.SetCurrentDirectory(AppContext.BaseDirectory);

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory, // the same folder whether run from a shell or as an OS service
});

builder.Configuration
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables("XTOUCH_")
    .AddCommandLine(args);

builder.Logging.ClearProviders(); // Serilog writes console/file; other providers (UI log) are added below
builder.Services.AddSerilog((_, cfg) => cfg.ReadFrom.Configuration(builder.Configuration), writeToProviders: true);
builder.Services.AddWindowsService(o => o.ServiceName = "XTouch Middleware");
builder.Services.AddSystemd();
builder.Services.AddXTouchMiddleware(builder.Configuration);

var apiOptions = builder.Configuration.GetSection("Api").Get<ApiOptions>() ?? new ApiOptions();
var url = new Uri(apiOptions.Url);
if (!IsLoopback(url) && string.IsNullOrEmpty(apiOptions.Token))
    throw new InvalidOperationException($"Api:Url '{apiOptions.Url}' is reachable from the network; set Api:Token to protect it.");
builder.WebHost.UseUrls(apiOptions.Url);

var app = builder.Build();
app.UseApiToken();
app.MapHub<SurfaceHub>(ApiInfo.HubPath);
app.MapApiEndpoints();
app.Run();

static bool IsLoopback(Uri url) =>
    url.IsLoopback || (IPAddress.TryParse(url.Host, out var ip) && IPAddress.IsLoopback(ip));

public partial class Program;
