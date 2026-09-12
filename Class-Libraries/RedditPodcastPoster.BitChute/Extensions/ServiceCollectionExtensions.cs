// pragma: allowlist secret
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.BitChute.Extractors; // pragma: allowlist secret
using RedditPodcastPoster.BitChute.Matching; // pragma: allowlist secret
using RedditPodcastPoster.Models.Podcasts; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers; // pragma: allowlist secret

namespace RedditPodcastPoster.BitChute.Extensions; // pragma: allowlist secret

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBitChuteServices(this IServiceCollection services) // pragma: allowlist secret
    {
        services.AddHttpClient(nameof(BitChuteMetaDataExtractor), client => // pragma: allowlist secret
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json"));

        return services
            .AddScoped<IBitChuteMetaDataExtractor, BitChuteMetaDataExtractor>() // pragma: allowlist secret
            .AddScoped<INonPodcastServiceAdapter>(provider => // pragma: allowlist secret
                new CatalogKeyedNonPodcastServiceAdapter( // pragma: allowlist secret
                    NonPodcastService.BitChute, // pragma: allowlist secret
                    ServiceKeys.BitChute, // pragma: allowlist secret
                    BitChuteUrlMatcher.IsSubmitUrl, // pragma: allowlist secret
                    BitChuteUrlMatcher.IsSubmitUrl, // pragma: allowlist secret
                    provider.GetRequiredService<IBitChuteMetaDataExtractor>().GetMetaData)); // pragma: allowlist secret
    }
}
