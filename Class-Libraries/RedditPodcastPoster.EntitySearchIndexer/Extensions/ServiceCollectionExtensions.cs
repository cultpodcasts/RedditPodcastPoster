using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Models;
using RedditPodcastPoster.EntitySearchIndexer.Services;
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
