using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Search.Extensions;

namespace RedditPodcastPoster.EntitySearchIndexer.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEpisodeSearchIndexerService(this IServiceCollection services)
    {
        return services
            .AddSearch()
            .AddScoped<IEpisodeSearchIndexerService, EpisodeSearchIndexerService>();
    }
}