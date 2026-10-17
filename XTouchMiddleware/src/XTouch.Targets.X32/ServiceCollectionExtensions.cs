using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XTouch.Abstractions.Targets;

namespace XTouch.Targets.X32;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddX32Adapter(this IServiceCollection services, IConfiguration section)
    {
        services.Configure<X32Options>(section);
        services.AddSingleton<X32Adapter>();
        services.AddSingleton<ITargetAdapter>(sp => sp.GetRequiredService<X32Adapter>());
        return services;
    }
}
