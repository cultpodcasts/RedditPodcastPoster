using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Auth0.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.EdgeApi.Clients;
using RedditPodcastPoster.EdgeApi.Configuration;
using RedditPodcastPoster.EdgeApi.Extensions;
using RedditPodcastPoster.EdgeApi.Heroes;
using RedditPodcastPoster.PodcastServices.Abstractions.Heroes;

namespace RedditPodcastPoster.EdgeApi.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEdgeApiClient(
        this IServiceCollection services,
        bool bypassCertificateValidation)
    {
        services.AddAuth0Client();
        services.BindConfiguration<ApiOptions>("api");
        services.AddScoped<IApiClient, ApiClient>();
        services.AddScoped<IHeroEpisodePromoter, EdgeHeroEpisodePromoter>();
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
