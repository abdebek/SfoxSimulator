using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SfoxFeedLibrary.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSfoxSimulatorHostedService(this IServiceCollection services)
    {
        services.AddSingleton<ISfoxSimulatorService, SfoxSimulatorService>();
        services.AddSingleton<IHostedService>(provider => 
            (SfoxSimulatorService)provider.GetRequiredService<ISfoxSimulatorService>());
        return services;
    }
}
