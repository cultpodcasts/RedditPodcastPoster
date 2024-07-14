using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration.Extensions;

namespace RedditPodcastPoster.EdgeApi.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEdgeApiClient(
        this IServiceCollection services,
        bool bypassCertificateValidation)
    {
        services.BindConfiguration<ApiOptions>("api");

        services.AddScoped<IApiClient, ApiClient>();
        if (bypassCertificateValidation)
        {
            services.AddHttpClient<IApiClient, ApiClient>()
                .ConfigurePrimaryHttpMessageHandler(() =>
                {
                    return new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = (m, c, ch, e) => true
                    };
                });
        }
        else
        {
            services.AddHttpClient<IApiClient, ApiClient>();
        }

        return services;
    }
}