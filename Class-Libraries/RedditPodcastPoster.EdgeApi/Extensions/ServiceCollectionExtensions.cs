using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration.Extensions;

namespace RedditPodcastPoster.EdgeApi.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEdgeApiClient(this IServiceCollection services)
    {
        services.AddScoped<IApiClient, ApiClient>()
            .AddHttpClient<IApiClient, ApiClient>()
            .ConfigurePrimaryHttpMessageHandler(() =>
            {
                return new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (m, c, ch, e) => true
                };
            });
        services.BindConfiguration<ApiOptions>("api");

        return services;
    }
}