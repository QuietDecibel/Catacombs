using Microsoft.Extensions.Options;
using XTouch.Abstractions;
using XTouch.Core.Engine;
using XTouch.Core.Profiles;
using XTouch.Service.Api;
using XTouch.Service.Logging;
using XTouch.Service.Workers;
using XTouch.Surface;
using XTouch.Surface.Transport;
using XTouch.Targets.X32;

namespace XTouch.Service;

public static class ServiceRegistration
{
    public static IServiceCollection AddXTouchMiddleware(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<SurfaceOptions>(config.GetSection("Surface"));
        services.Configure<ProfileOptions>(config.GetSection("Profiles"));
        services.Configure<ApiOptions>(config.GetSection("Api"));

        // Logging to the UI
        services.AddSingleton<UiLogSink>();
        services.AddSingleton<ILoggerProvider, UiLoggerProvider>();

        // Surface
        services.AddSingleton<MidiTrace>();
        services.AddSingleton<IMidiTransport>(sp =>
        {
            var opt = sp.GetRequiredService<IOptions<SurfaceOptions>>().Value;
            IMidiTransport inner = opt.Transport == SurfaceTransportKind.Virtual
                ? new VirtualTransport()
                : ActivatorUtilities.CreateInstance<DryWetMidiTransport>(sp);
            return new TracingTransport(inner, sp.GetRequiredService<MidiTrace>());
        });
        services.AddSingleton<FeedbackRenderer>();
        services.AddSingleton<IFeedbackSink>(sp => sp.GetRequiredService<FeedbackRenderer>());

        // Targets
        services.AddX32Adapter(config.GetSection("Targets:X32"));
        services.AddSingleton<ITargetRegistry, TargetRegistry>();

        // Engine
        services.AddSingleton<EngineChannel>();
        services.AddSingleton<ProfileStore>();
        services.AddSingleton<HubStateBroadcaster>();
        services.AddSingleton<IStateBroadcaster>(sp => sp.GetRequiredService<HubStateBroadcaster>());
        services.AddSingleton(sp =>
        {
            var store = sp.GetRequiredService<ProfileStore>();
            var log = sp.GetRequiredService<ILogger<ControlEngine>>();
            CompiledProfile profile;
            try
            {
                profile = store.Load(store.ActiveId);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Active profile '{Id}' could not be loaded; using the built-in X32 profile", store.ActiveId);
                profile = ProfileCompiler.Compile(DefaultProfiles.X32());
            }
            return ActivatorUtilities.CreateInstance<ControlEngine>(sp, profile);
        });

        // Workers (start order matters: broadcaster before engine publishes)
        services.AddHostedService(sp => sp.GetRequiredService<HubStateBroadcaster>());
        services.AddHostedService<EngineWorker>();
        services.AddHostedService<SurfaceWorker>();
        services.AddSingleton<TargetsWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<TargetsWorker>());
        services.AddHostedService<ProfileWatcher>();

        services.AddSignalR();
        return services;
    }
}
