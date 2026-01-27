using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace PhotoBooth.Infrastructure.Network;

public static class NetworkSecurityServiceExtensions
{
    public static IServiceCollection AddNetworkSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<NetworkSecurityOptions>(configuration.GetSection("NetworkSecurity"));
        services.AddTransient<BlockingHttpHandler>();
        services.ConfigureAll<HttpClientFactoryOptions>(options =>
        {
            options.HttpMessageHandlerBuilderActions.Add(builder =>
            {
                var handler = builder.Services.GetRequiredService<BlockingHttpHandler>();
                builder.AdditionalHandlers.Add(handler);
            });
        });

        return services;
    }
}
